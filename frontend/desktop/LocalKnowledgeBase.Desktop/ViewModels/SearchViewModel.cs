using System.Collections.ObjectModel;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>搜索页面：调用后端全文检索并展示命中的文档。</summary>
public sealed class SearchViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly IStatusReporter _status;

    private string _query = string.Empty;
    private bool _hasSearched;
    private string _statusText = string.Empty;

    public SearchViewModel(IKnowledgeApiClient api, IStatusReporter status)
    {
        _api = api;
        _status = status;
        SearchCommand = new AsyncRelayCommand(SearchAsync, onException: ShowFault);
    }

    public ICommand SearchCommand { get; }

    /// <summary>搜索关键词。</summary>
    public string Query
    {
        get => _query;
        set => SetField(ref _query, value);
    }

    /// <summary>本次会话是否已执行过搜索（用于控制“暂无结果”空状态）。</summary>
    public bool HasSearched
    {
        get => _hasSearched;
        private set
        {
            if (SetField(ref _hasSearched, value))
                OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool HasResults => Results.Count > 0;

    /// <summary>是否显示“暂无搜索结果”空状态。</summary>
    public bool ShowEmptyState => HasSearched && !HasResults;

    public string ResultCountText => $"搜索结果 ({Results.Count})";

    public ObservableCollection<Document> Results { get; } = new();

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>执行搜索（公开方法，便于测试与复用）。</summary>
    public async Task SearchAsync()
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            StatusText = "请输入搜索关键词。";
            return;
        }

        StatusText = "搜索中…";
        var results = await _api.SearchAsync(query);
        if (results is null)
        {
            StatusText = _api.LastError ?? "搜索失败，请检查 API 服务是否运行。";
            return;
        }

        Results.Clear();
        foreach (var document in results.Results)
            Results.Add(document);

        HasSearched = true;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultCountText));
        StatusText = Results.Count > 0 ? $"找到 {Results.Count} 个结果" : string.Empty;
    }

    private void ShowFault(Exception ex) => StatusText = $"搜索失败：{ex.Message}";
}
