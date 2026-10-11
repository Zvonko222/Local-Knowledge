using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 资料库页面：文档列表、本地筛选、导入、编辑与删除。
/// 所有业务动作都走 <see cref="IKnowledgeApiClient"/>，UI 细节（弹窗）走 <see cref="IDialogService"/>。
/// </summary>
public sealed class DocumentsViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly IDialogService _dialogs;
    private readonly IStatusReporter _status;

    private List<Document> _allDocuments = new();
    private string _filterText = string.Empty;
    private Document? _selectedDocument;
    private string _detailTitle = string.Empty;
    private string _detailTags = string.Empty;
    private string _detailContent = string.Empty;
    private string _detailMeta = string.Empty;
    private string _statusText = string.Empty;

    public DocumentsViewModel(IKnowledgeApiClient api, IDialogService dialogs, IStatusReporter status)
    {
        _api = api;
        _dialogs = dialogs;
        _status = status;

        ImportCommand = new AsyncRelayCommand(ImportFromDialogAsync, onException: ShowFault);
        LoadCommand = new AsyncRelayCommand(LoadAsync, onException: ShowFault);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasSelection, onException: ShowFault);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => HasSelection, onException: ShowFault);
        ClearFilterCommand = new RelayCommand(() => FilterText = string.Empty);
    }

    /// <summary>筛选后展示在表格中的文档。</summary>
    public ObservableCollection<Document> Documents { get; } = new();

    public ICommand ImportCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearFilterCommand { get; }

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
        ApplyFilter();
    }

    /// <summary>弹出文件选择框并把选中的文件导入（命令入口）。</summary>
    private async Task ImportFromDialogAsync()
    {
        var directory = await _api.GetDataDirectoryAsync();
        var initialDirectory = directory?.Configured == true ? directory.Path : null;

        var path = _dialogs.PickOpenFile(
            "选择要导入的文档", "文本文件|*.txt;*.md|所有文件|*.*", initialDirectory);
        if (path is null)
            return;

        await ImportFileAsync(path);
    }

    /// <summary>导入指定路径的文件（公开方法，便于测试与复用）。</summary>
    public async Task ImportFileAsync(string filePath)
    {
        StatusText = "正在导入…";
        var fileName = Path.GetFileName(filePath);
        var content = await File.ReadAllBytesAsync(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);

        var created = await _api.AddDocumentAsync(title, fileName, content);
        if (created is null)
        {
            StatusText = _api.LastError ?? "导入失败，请检查 API 服务是否运行。";
            return;
        }

        await LoadAsync();
        StatusText = $"导入成功：{fileName}";
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

    /// <summary>把 <see cref="FilterText"/> 应用到全量数据上，结果写入 <see cref="Documents"/>。</summary>
    private void ApplyFilter()
    {
        var query = _filterText.Trim();
        var filtered = query.Length == 0
            ? _allDocuments
            : _allDocuments.Where(document =>
                document.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (document.Tags?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (document.FileType?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                document.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Documents.Clear();
        foreach (var document in filtered)
            Documents.Add(document);

        StatusText = filtered.Count == _allDocuments.Count
            ? $"共 {_allDocuments.Count} 个文档"
            : $"显示 {filtered.Count} / {_allDocuments.Count}";

        SelectedDocument = null;
    }

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
