using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>详情面板中的“相关文档”条目（基于后端向量索引的语义相似度）。</summary>
public sealed class RelatedDocumentItem : ViewModelBase
{
    private string _scoreText;

    public RelatedDocumentItem(string title, string documentId, double similarity)
    {
        Title = title;
        DocumentId = documentId;
        _scoreText = $"相似度 {similarity:P0}";
    }

    public string Title { get; }
    public string DocumentId { get; }

    public string ScoreText
    {
        get => _scoreText;
        set => SetField(ref _scoreText, value);
    }

    public string Label => $"{Title}（{ScoreText}）";
}

/// <summary>标签分类芯片：Tag 为 null 表示“全部”，空字符串表示“未分类”。</summary>
public sealed class TagFilterItem : ViewModelBase
{
    private int _count;

    public TagFilterItem(string? tag, int count, int totalDocs)
    {
        Tag = tag;
        _count = count;
        TotalDocs = totalDocs;
    }

    /// <summary>null = 全部；"" = 未分类；其他 = 标签名。</summary>
    public string? Tag { get; }

    /// <summary>“全部”芯片显示的是文档总数，其余为该分类的数量。</summary>
    public int TotalDocs { get; }

    public int Count
    {
        get => _count;
        set => SetField(ref _count, value);
    }

    public bool IsAll => Tag is null;

    /// <summary>本地化覆盖标签（由 VM 在语言切换/重建时填入）；空则用内置中文。</summary>
    public string? LabelOverride { get; set; }

    public string Label =>
        Tag is null
            ? LabelOverride ?? $"全部 ({TotalDocs})"
        : Tag!.Length == 0
            ? LabelOverride ?? $"未分类 ({Count})"
        : $"{Tag} ({Count})";
}

/// <summary>
/// 资料库页面：文档列表、本地筛选、导入、编辑与删除。
/// 所有业务动作都走 <see cref="IKnowledgeApiClient"/>，UI 细节（弹窗）走 <see cref="IDialogService"/>。
/// </summary>
public sealed class DocumentsViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly IDialogService _dialogs;
    private readonly IStatusReporter _status;
    private readonly ILocalization _localization;

    private List<Document> _allDocuments = new();
    private string _filterText = string.Empty;
    private TagFilterItem? _selectedTagFilter;
    private Document? _selectedDocument;
    private string _detailTitle = string.Empty;
    private string _detailTags = string.Empty;
    private string _detailContent = string.Empty;
    private string _detailMeta = string.Empty;
    private string _statusText = string.Empty;

    public DocumentsViewModel(IKnowledgeApiClient api, IDialogService dialogs, IStatusReporter status,
        ILocalization localization)
    {
        _api = api;
        _dialogs = dialogs;
        _status = status;
        _localization = localization;

        ImportCommand = new AsyncRelayCommand(ImportFromDialogAsync, onException: ShowFault);
        LoadCommand = new AsyncRelayCommand(LoadAsync, onException: ShowFault);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasSelection, onException: ShowFault);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => HasSelection, onException: ShowFault);
        ClearFilterCommand = new RelayCommand(ClearAllFilters);
        ExportCommand = new RelayCommand(ExportSelected, () => HasSelection);
        ExportAllCommand = new AsyncRelayCommand(ExportAllAsync, onException: ShowFault);
        SelectRelatedCommand = new ParameterRelayCommand(o => SelectRelatedById(o as string));
        OpenFileLocationCommand = new AsyncRelayCommand(OpenFileLocationAsync, () => HasSelection, onException: ShowFault);
    }

    private int _relatedRequestId;

    /// <summary>加载选中文档的相关文档；过期响应（用户已切换选区）直接丢弃。</summary>
    private async Task LoadRelatedAsync(Document document)
    {
        var requestId = ++_relatedRequestId;
        RelatedDocuments.Clear();
        OnPropertyChanged(nameof(HasRelatedDocuments));

        var related = await _api.GetRelatedAsync(document.Id, 5);
        if (requestId != _relatedRequestId)
            return;

        if (related is not null)
        {
            foreach (var item in related)
                RelatedDocuments.Add(new RelatedDocumentItem(item.Title, item.DocumentId, item.Similarity));
            OnPropertyChanged(nameof(HasRelatedDocuments));
        }
    }

    /// <summary>在资源管理器中定位选中文档的原始文件（无原始文件时提示）。</summary>
    public async Task OpenFileLocationAsync()
    {
        if (SelectedDocument is not { } document)
            return;

        StatusText = "正在定位文件位置…";
        var path = await _api.GetDocumentFilePathAsync(document.Id);
        if (string.IsNullOrEmpty(path))
        {
            StatusText = "未找到该文档的原始文件。";
            return;
        }

        StatusText = $"文件位置：{path}";
        _dialogs.RevealFile(path);
    }

    /// <summary>点击相关文档：在列表中定位并选中（被过滤隐藏时仅提示）。</summary>
    private void SelectRelatedById(string? documentId)
    {
        if (documentId is null)
            return;

        var match = Documents.FirstOrDefault(d => d.Id == documentId);
        if (match is not null)
            SelectedDocument = match;
        else
            StatusText = "相关文档不在当前列表中，请清空筛选后再点击。";
    }

    /// <summary>筛选后展示在表格中的文档。</summary>
    public ObservableCollection<Document> Documents { get; } = new();

    /// <summary>当前选中文档的“相关文档”（语义相似度降序）。</summary>
    public ObservableCollection<RelatedDocumentItem> RelatedDocuments { get; } = new();

    public bool HasRelatedDocuments => RelatedDocuments.Count > 0;

    /// <summary>标签分类芯片（全部 / 各标签 / 未分类），由当前全量文档自动统计。</summary>
    public ObservableCollection<TagFilterItem> TagFilters { get; } = new();

    public bool HasTagFilters => TagFilters.Count > 0;

    /// <summary>当前选中的分类芯片；变更立即过滤列表。</summary>
    public TagFilterItem? SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (SetField(ref _selectedTagFilter, value))
                ApplyFilter();
        }
    }

    public ICommand ImportCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ExportAllCommand { get; }
    public ICommand SelectRelatedCommand { get; }
    public ICommand OpenFileLocationCommand { get; }

    /// <summary>本地筛选关键词（标题、标签、类型、正文），输入即过滤，不请求 API。</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
                ApplyFilter();
        }
    }

    /// <summary>表格当前选中的文档；选中后下方详情面板可编辑。</summary>
    public Document? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetField(ref _selectedDocument, value))
            {
                DetailTitle = value?.Title ?? string.Empty;
                DetailTags = value?.Tags ?? string.Empty;
                DetailContent = value?.Content ?? string.Empty;
                DetailMeta = DescribeMeta(value);
                OnPropertyChanged(nameof(HasSelection));

                RelatedDocuments.Clear();
                OnPropertyChanged(nameof(HasRelatedDocuments));
                if (value is not null)
                    _ = LoadRelatedAsync(value);
            }
        }
    }

    public bool HasSelection => SelectedDocument is not null;

    // 详情面板的编辑缓冲区：保存前不会直接影响表格行，避免边改边变。
    public string DetailTitle
    {
        get => _detailTitle;
        set => SetField(ref _detailTitle, value);
    }

    public string DetailTags
    {
        get => _detailTags;
        set => SetField(ref _detailTags, value);
    }

    public string DetailContent
    {
        get => _detailContent;
        set => SetField(ref _detailContent, value);
    }

    public string DetailMeta
    {
        get => _detailMeta;
        private set => SetField(ref _detailMeta, value);
    }

    /// <summary>页面内操作反馈（区别于主窗口状态栏）。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public override async void OnNavigatedTo() => await LoadAsync();

    public async Task LoadAsync()
    {
        StatusText = "正在加载文档…";
        var documents = await _api.GetDocumentsAsync();
        if (documents is null)
        {
            StatusText = _api.LastError ?? "加载失败，请检查 API 服务是否运行。";
            return;
        }

        _allDocuments = documents;
        RebuildTagFilters(); // 内部会触发 ApplyFilter
    }

    /// <summary>弹出文件选择框（可多选）并导入全部选中文件（命令入口）。</summary>
    private async Task ImportFromDialogAsync()
    {
        var directory = await _api.GetDataDirectoryAsync();
        var initialDirectory = directory?.Configured == true ? directory.Path : null;

        var paths = _dialogs.PickOpenFiles(
            "选择要导入的文档（可按住 Ctrl 多选）",
            "支持的文件|*.txt;*.md;*.pdf;*.docx|文本文件|*.txt;*.md|PDF 文档|*.pdf|Word 文档|*.docx|所有文件|*.*",
            initialDirectory);
        if (paths.Count == 0)
            return;

        await ImportFilesAsync(paths);
    }

    /// <summary>批量导入指定路径的文件（公开方法，供对话框、拖拽与测试复用）。</summary>
    public async Task ImportFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return;

        var succeeded = 0;
        var lastFailedFile = string.Empty;
        foreach (var path in filePaths)
        {
            StatusText = $"正在导入（{succeeded + 1}/{filePaths.Count}）…";
            if (await ImportSingleFileAsync(path))
                succeeded++;
            else
                lastFailedFile = Path.GetFileName(path);
        }

        await LoadAsync();

        StatusText = filePaths.Count == 1
            ? succeeded == 1
                ? $"导入成功：{Path.GetFileName(filePaths[0])}"
                : _api.LastError ?? $"导入失败：{lastFailedFile}"
            : $"导入完成：成功 {succeeded} / {filePaths.Count}"
              + (succeeded < filePaths.Count ? $"，失败：{lastFailedFile}" : string.Empty);
    }

    private async Task<bool> ImportSingleFileAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var content = await File.ReadAllBytesAsync(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);

        return await _api.AddDocumentAsync(title, fileName, content) is not null;
    }

    public async Task SaveAsync()
    {
        if (SelectedDocument is not { } document)
            return;

        var title = DetailTitle.Trim();
        if (title.Length == 0)
        {
            StatusText = "文档标题不能为空。";
            return;
        }

        StatusText = "正在保存…";
        var updated = await _api.UpdateDocumentAsync(document.Id, title, DetailContent, NullIfEmpty(DetailTags));
        if (updated is null)
        {
            StatusText = _api.LastError ?? "保存失败，请检查 API 服务是否运行。";
            return;
        }

        await LoadAsync();
        StatusText = $"文档已更新：{updated.Title}";
    }

    public async Task DeleteAsync()
    {
        if (SelectedDocument is not { } document)
            return;

        if (!_dialogs.Confirm("删除文档", $"确定删除文档“{document.Title}”吗？此操作无法撤销。"))
            return;

        StatusText = "正在删除…";
        if (!await _api.DeleteDocumentAsync(document.Id))
        {
            StatusText = _api.LastError ?? "删除失败，请检查 API 服务是否运行。";
            return;
        }

        await LoadAsync();
        StatusText = "文档已删除。";
    }

    /// <summary>清空关键词与分类筛选。</summary>
    private void ClearAllFilters()
    {
        FilterText = string.Empty;
        if (TagFilters.Count > 0)
            SelectedTagFilter = TagFilters[0]; // 全部
    }

    /// <summary>把选中文档导出为 Markdown 文件（选保存位置 → 写文件）。</summary>
    public void ExportSelected()
    {
        if (SelectedDocument is not { } document)
            return;

        var path = _dialogs.PickSaveFile(
            "导出文档为 Markdown",
            "Markdown 文件|*.md|所有文件|*.*",
            SanitizeFileName(document.Title) + ".md");
        if (path is null)
            return;

        File.WriteAllText(path, $"# {document.Title}\n\n{document.Content}", new UTF8Encoding(false));
        StatusText = $"已导出：{Path.GetFileName(path)}";
    }

    /// <summary>
    /// 把整个知识库导出为 Markdown 合集：每篇文档一个 .md 文件，
    /// 另加一个 _索引.md 汇总列表。用于备份或迁移到其他工具。
    /// </summary>
    public async Task ExportAllAsync()
    {
        var documents = await _api.GetDocumentsAsync();
        if (documents is null)
        {
            StatusText = _api.LastError ?? "加载失败，请检查 API 服务是否运行。";
            return;
        }
        if (documents.Count == 0)
        {
            StatusText = "知识库为空，没有可导出的文档。";
            return;
        }

        var folder = _dialogs.PickFolder("选择导出位置（会为每篇文档生成一个 .md 文件）");
        if (folder is null)
            return;

        Directory.CreateDirectory(folder);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            var baseName = SanitizeFileName(document.Title);
            var fileName = baseName + ".md";
            for (var suffix = 2; !usedNames.Add(fileName); suffix++)
                fileName = $"{baseName} ({suffix}).md";

            File.WriteAllText(
                Path.Combine(folder, fileName),
                $"# {document.Title}\n\n{document.Content}",
                new UTF8Encoding(false));
        }

        var index = new StringBuilder("# 知识库导出索引\n\n");
        index.AppendLine($"导出时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        index.AppendLine($"文档数量：{documents.Count}");
        index.AppendLine();
        foreach (var document in documents)
            index.AppendLine($"- [{document.Title}]({SanitizeFileName(document.Title)}.md)");

        File.WriteAllText(Path.Combine(folder, "_索引.md"), index.ToString(), new UTF8Encoding(false));
        StatusText = $"已导出 {documents.Count} 篇文档到：{folder}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name.Length == 0 ? "document" : name;
    }

    /// <summary>把分类芯片与关键词筛选依次应用，结果写入 <see cref="Documents"/>。</summary>
    private void ApplyFilter()
    {
        IEnumerable<Document> filtered = _allDocuments;

        // 第一层：标签分类（芯片）
        var selectedTag = SelectedTagFilter;
        if (selectedTag is not null && !selectedTag.IsAll)
        {
            filtered = selectedTag.Tag!.Length == 0
                ? filtered.Where(document => string.IsNullOrWhiteSpace(document.Tags))
                : filtered.Where(document =>
                    ParseTags(document.Tags).Contains(selectedTag.Tag!, StringComparer.OrdinalIgnoreCase));
        }

        // 第二层：关键词（标题、标签、类型、正文）
        var query = _filterText.Trim();
        if (query.Length > 0)
        {
            filtered = filtered.Where(document =>
                document.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (document.Tags?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (document.FileType?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                document.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();

        Documents.Clear();
        foreach (var document in list)
            Documents.Add(document);

        StatusText = list.Count == _allDocuments.Count
            ? $"共 {_allDocuments.Count} 个文档"
            : $"显示 {list.Count} / {_allDocuments.Count}";

        SelectedDocument = null;
    }

    /// <summary>按当前全量文档重建标签芯片；原选中分类若仍存在则保持。</summary>
    private void RebuildTagFilters()
    {
        var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var untagged = 0;
        foreach (var document in _allDocuments)
        {
            var tags = ParseTags(document.Tags);
            if (tags.Count == 0)
                untagged++;
            else
                foreach (var tag in tags)
                    counts[tag] = counts.GetValueOrDefault(tag) + 1;
        }

        TagFilters.Clear();
        TagFilters.Add(new TagFilterItem(null, _allDocuments.Count, _allDocuments.Count)
        {
            LabelOverride = $"{_localization.Get("Chip_All")} ({_allDocuments.Count})"
        });
        foreach (var pair in counts)
            TagFilters.Add(new TagFilterItem(pair.Key, pair.Value, _allDocuments.Count));
        if (untagged > 0)
            TagFilters.Add(new TagFilterItem(string.Empty, untagged, _allDocuments.Count)
            {
                LabelOverride = $"{_localization.Get("Chip_Untagged")} ({untagged})"
            });

        OnPropertyChanged(nameof(HasTagFilters));

        // 保持原选中分类；分类已不存在（如标签被改）时回落到“全部”
        var previousTag = SelectedTagFilter?.Tag;
        SelectedTagFilter = TagFilters.FirstOrDefault(f => f.Tag == previousTag) ?? TagFilters[0];
    }

    /// <summary>把逗号分隔的标签串解析为去重后的标签列表。</summary>
    private static List<string> ParseTags(string? tags) =>
        (tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string DescribeMeta(Document? document)
    {
        if (document is null)
            return string.Empty;

        return $"文件: {document.OriginalFileName ?? "未知"} | 类型: {document.FileType ?? "未知"} | " +
               $"大小: {document.FileSize} 字节 | 更新: {document.UpdatedAt:g}";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ShowFault(Exception ex) => StatusText = $"操作失败：{ex.Message}";
}
