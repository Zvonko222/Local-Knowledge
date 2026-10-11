using System.Collections.ObjectModel;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>问答消息：气泡文本 + （助手消息）引用的知识库来源。</summary>
public sealed class ChatMessageViewModel : ViewModelBase
{
    private string _text;
    private IReadOnlyList<ChatSourceViewModel> _sources;

    public ChatMessageViewModel(string role, string text)
    {
        Role = role;
        _text = text;
        _sources = Array.Empty<ChatSourceViewModel>();
        TimeText = DateTimeOffset.Now.ToString("HH:mm");
    }

    public string Role { get; }
    public bool IsUser => Role == "user";
    public string TimeText { get; }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public IReadOnlyList<ChatSourceViewModel> Sources
    {
        get => _sources;
        set => SetField(ref _sources, value);
    }
}

/// <summary>回答引用的一条知识库来源。</summary>
public sealed class ChatSourceViewModel
{
    public ChatSourceViewModel(string index, string title, string id)
    {
        Index = index;
        Title = title;
        Id = id;
        Label = $"[{index}] {title}";
    }

    /// <summary>回答中标注的序号，如 "1"。</summary>
    public string Index { get; }
    public string Title { get; }
    public string Id { get; }

    /// <summary>来源徽章显示文本，如 “[1] 架构笔记”。</summary>
    public string Label { get; }
}

/// <summary>
/// 问答页面：先用本地 API 全文检索知识库资料，再把资料作为上下文交给
/// 本地大模型（OpenAI 兼容接口）生成回答。数据全程不出本机。
/// </summary>
public sealed class ChatViewModel : PageViewModelBase
{
    public const string SystemPrompt =
        "你是本地知识库助手。请只依据用户提供的资料回答问题；" +
        "资料不足以回答时明确说明，不要编造。用中文回答，" +
        "并在引用某条资料的位置标注其序号，如 [1]、[2]。";

    /// <summary>喂给模型的每条资料的最大长度（字符），控制上下文规模。</summary>
    private const int SourceSnippetLength = 800;

    /// <summary>每次问答最多引用的资料条数。</summary>
    private const int MaxSources = 6;

    private readonly IKnowledgeApiClient _api;
    private readonly ILlmClient _llm;

    private string _question = string.Empty;
    private string _statusText = string.Empty;

    public ChatViewModel(IKnowledgeApiClient api, ILlmClient llm)
    {
        _api = api;
        _llm = llm;

        SendCommand = new AsyncRelayCommand(SendAsync, onException: ex =>
            AppendAssistant($"操作失败：{ex.Message}"));
        ClearCommand = new RelayCommand(ClearConversation);
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand ClearCommand { get; }

    public bool HasConversation => Messages.Count > 0;

    public string Question
    {
        get => _question;
        set => SetField(ref _question, value);
    }

    /// <summary>页面内提示（模型未配置、发送失败等原因）。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public override void OnNavigatedTo()
    {
        // 每次进入页面时提示当前模型配置状态，避免用户问了才发现没配模型
        StatusText = _llm.Model.Length == 0
            ? "尚未配置本地模型：请到“设置”页填写模型名称并测试连接。"
            : $"本地模型：{_llm.Model}";
    }

    public async Task SendAsync()
    {
        var question = Question.Trim();
        if (question.Length == 0)
        {
            StatusText = "请输入问题。";
            return;
        }

        if (_llm.Model.Length == 0)
        {
            AppendAssistant(
                "尚未配置模型，暂时无法生成回答。\n\n" +
                "请到“设置”页填写 OpenAI 兼容地址、模型名称与 API 密钥（云端 API 必填，" +
                "本地服务如 Ollama 可留空密钥），然后点“测试连接”。");
            return;
        }

        AppendUser(question);
        Question = string.Empty;
        StatusText = string.Empty;

        var assistant = AppendAssistant("正在检索知识库…");

        var results = await _api.SearchAsync(question);
        if (results is null)
        {
            assistant.Text = _api.LastError ?? "无法连接本地 API，请确认 API 服务已启动。";
            return;
        }
        if (results.Results.Count == 0)
        {
            assistant.Text = $"知识库中没有找到与「{question}」相关的资料。\n" +
                             "可以先在“资料库”导入相关文档，再试一次。";
            return;
        }

        var hits = results.Results.Take(MaxSources).ToList();
        assistant.Text = $"命中 {hits.Count} 条资料，正在生成回答…";
        var (context, sources) = BuildContext(hits);

        try
        {
            var answer = await _llm.CompleteAsync(SystemPrompt, context + "\n\n问题：" + question);
            assistant.Text = answer.Trim().Length == 0 ? "（模型返回了空回答）" : answer.Trim();
            assistant.Sources = sources;
        }
        catch (Exception ex)
        {
            assistant.Text = "调用本地模型失败：" + ex.Message;
        }
    }

    public void ClearConversation()
    {
        Messages.Clear();
        OnPropertyChanged(nameof(HasConversation));
        OnNavigatedTo();
    }

    private (string Context, IReadOnlyList<ChatSourceViewModel> Sources) BuildContext(List<Document> documents)
    {
        var contextBuilder = new System.Text.StringBuilder();
        var sources = new List<ChatSourceViewModel>();
        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            contextBuilder.AppendLine($"[{i + 1}] {document.Title}");
            contextBuilder.AppendLine(Snippet(document.Content));
            contextBuilder.AppendLine();
            sources.Add(new ChatSourceViewModel((i + 1).ToString(), document.Title, document.Id));
        }
        return (contextBuilder.ToString(), sources);
    }

    private static string Snippet(string text) =>
        string.IsNullOrEmpty(text) || text.Length <= SourceSnippetLength
            ? text
            : text[..SourceSnippetLength] + "…";

    private ChatMessageViewModel AppendUser(string text)
    {
        var message = new ChatMessageViewModel("user", text);
        Messages.Add(message);
        OnPropertyChanged(nameof(HasConversation));
        return message;
    }

    private ChatMessageViewModel AppendAssistant(string text)
    {
        var message = new ChatMessageViewModel("assistant", text);
        Messages.Add(message);
        OnPropertyChanged(nameof(HasConversation));
        return message;
    }
}
