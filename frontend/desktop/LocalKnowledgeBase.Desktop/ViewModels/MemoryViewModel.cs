using System.Collections.ObjectModel;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 项目记忆页面：新建/编辑/删除长期记忆条目。
/// 顶部表单既用于新建（<see cref="IsEditing"/> 为 false）也用于编辑选中的条目。
/// </summary>
public sealed class MemoryViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly IDialogService _dialogs;
    private readonly IStatusReporter _status;

    private string _title = string.Empty;
    private string _tags = string.Empty;
    private string _content = string.Empty;
    private MemoryEntry? _selectedEntry;
    private string? _editingId;
    private string _statusText = string.Empty;

    public MemoryViewModel(IKnowledgeApiClient api, IDialogService dialogs, IStatusReporter status)
    {
        _api = api;
        _dialogs = dialogs;
        _status = status;

        LoadCommand = new AsyncRelayCommand(LoadAsync, onException: ShowFault);
        SaveCommand = new AsyncRelayCommand(SaveAsync, onException: ShowFault);
        ClearCommand = new RelayCommand(ClearForm);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => IsEditing, onException: ShowFault);
    }

    public ObservableCollection<MemoryEntry> Entries { get; } = new();

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>是否处于“编辑已有条目”状态（区别于新建）。</summary>
    public bool IsEditing => _editingId is not null;

    public string SaveButtonText => IsEditing ? "更新" : "保存";

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetField(ref _tags, value);
    }

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    /// <summary>列表选中的条目；选中后表单进入编辑状态。</summary>
    public MemoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetField(ref _selectedEntry, value))
                return;

            if (value is { } entry)
            {
                _editingId = entry.Id;
                Title = entry.Title;
                Tags = entry.Tags ?? string.Empty;
                Content = entry.Content;
                StatusText = $"正在编辑：{entry.Title}";
            }
            NotifyEditingChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public override async void OnNavigatedTo() => await LoadAsync();

    public async Task LoadAsync()
    {
        var entries = await _api.GetMemoryEntriesAsync();
        if (entries is null)
        {
            StatusText = _api.LastError ?? "加载失败，请检查 API 服务是否运行。";
            return;
        }

        Entries.Clear();
        foreach (var entry in entries)
            Entries.Add(entry);
        StatusText = $"共 {entries.Count} 条记忆";
    }

    public async Task SaveAsync()
    {
        var title = Title.Trim();
        var content = Content.Trim();
        var tags = NullIfEmpty(Tags);

        if (title.Length == 0 || content.Length == 0)
        {
            StatusText = "请填写标题和内容。";
            return;
        }

        StatusText = "正在保存…";
        var saved = _editingId is null
            ? await _api.AddMemoryEntryAsync(title, content, tags)
            : await _api.UpdateMemoryEntryAsync(_editingId, title, content, tags);

        if (saved is null)
        {
            StatusText = _api.LastError ?? "保存失败，请检查 API 服务是否运行。";
            return;
        }

        StatusText = $"保存成功：{title}";
        ClearForm();
        await LoadAsync();
    }

    public async Task DeleteAsync()
    {
        if (_editingId is not { } id)
            return;

        if (!_dialogs.Confirm("删除记忆", $"确定删除记忆“{Title}”吗？此操作无法撤销。"))
            return;

        StatusText = "正在删除…";
        if (!await _api.DeleteMemoryEntryAsync(id))
        {
            StatusText = _api.LastError ?? "删除失败，请检查 API 服务是否运行。";
            return;
        }

        StatusText = "记忆已删除。";
        ClearForm();
        await LoadAsync();
    }

    private void ClearForm()
    {
        _editingId = null;
        _selectedEntry = null;
        Title = string.Empty;
        Tags = string.Empty;
        Content = string.Empty;
        StatusText = string.Empty;
        NotifyEditingChanged();
    }

    private void NotifyEditingChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ShowFault(Exception ex) => StatusText = $"操作失败：{ex.Message}";
}
