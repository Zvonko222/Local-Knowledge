using System.Windows;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>界面文字本地化：双语表（zh/en），写入应用资源供 XAML DynamicResource 使用。</summary>
public interface ILocalization
{
    /// <summary>当前语言："zh" 或 "en"。</summary>
    string Culture { get; }

    /// <summary>语言切换后触发（VM 可刷新自身文案）。</summary>
    event EventHandler? Changed;

    /// <summary>按键取文案；未知键返回键名。</summary>
    string Get(string key);

    /// <summary>切换语言并立即把全部文案写入应用资源。</summary>
    void SetCulture(string culture);
}

/// <summary>默认实现：双语表内置，语言选择持久化由调用方（设置页）负责。</summary>
public sealed class Localization : ILocalization
{
    // 键 → (中文, English)。新增界面文案时在此登记。
    private static readonly Dictionary<string, (string Zh, string En)> Table = new()
    {
        ["Str_Nav_Library"] = ("资料库", "Library"),
        ["Str_Nav_Memory"] = ("项目记忆", "Memory"),
        ["Str_Nav_Chat"] = ("问答", "Ask"),
        ["Str_Nav_Search"] = ("搜索", "Search"),
        ["Str_Nav_Settings"] = ("设置", "Settings"),
        ["Str_Sidebar_Workspace"] = ("工作区", "Workspace"),
        ["Str_Sidebar_LocalFirst"] = ("本地优先", "Local-first"),
        ["Str_Status_Ready"] = ("服务就绪", "Ready"),
        ["Str_Status_ApiConnected"] = ("API 已连接", "API connected"),
        ["Str_Status_ApiDisconnected"] = ("API 未连接", "API disconnected"),
        ["Str_Status_ApiNotConnectedMsg"] = ("API 未连接，请在“设置”页检查服务地址，或先启动 API 服务。",
            "API not connected. Check the address on the Settings page or start the API service."),

        ["Str_Docs_Title"] = ("资料库", "Library"),
        ["Str_Docs_Subtitle"] = ("本地文档与提取内容 · 支持 txt / md / pdf / docx · 可直接把文件拖进窗口",
            "Local documents & extracted text · txt / md / pdf / docx · drag files into the window"),
        ["Str_Btn_Import"] = ("导入文档", "Import"),
        ["Str_Btn_Refresh"] = ("刷新", "Refresh"),
        ["Str_Btn_ExportAll"] = ("导出全部", "Export all"),
        ["Str_List_Docs"] = ("文档列表", "Documents"),
        ["Str_Btn_Clear"] = ("清空", "Clear"),
        ["Str_Col_Title"] = ("标题", "Title"),
        ["Str_Col_Tags"] = ("标签", "Tags"),
        ["Str_Col_Type"] = ("类型", "Type"),
        ["Str_Col_Size"] = ("大小", "Size"),
        ["Str_Col_Created"] = ("创建时间", "Created"),
        ["Str_Col_Updated"] = ("更新时间", "Updated"),
        ["Str_Detail_Title"] = ("文档详情", "Document details"),
        ["Str_Badge_Editable"] = ("可编辑", "Editable"),
        ["Str_Field_Title"] = ("标题", "Title"),
        ["Str_Field_Tags"] = ("标签", "Tags"),
        ["Str_Field_Content"] = ("内容", "Content"),
        ["Str_Btn_SaveChanges"] = ("保存更改", "Save changes"),
        ["Str_Btn_Export"] = ("导出", "Export"),
        ["Str_Btn_FileLocation"] = ("文件位置", "File location"),
        ["Str_Btn_Delete"] = ("删除", "Delete"),
        ["Str_Related_Title"] = ("相关文档", "Related documents"),
        ["Chip_All"] = ("全部", "All"),
        ["Chip_Untagged"] = ("未分类", "Untagged"),

        ["Str_Mem_Title"] = ("项目记忆", "Memory"),
        ["Str_Mem_Subtitle"] = ("决策、偏好与长期上下文", "Decisions, preferences & long-term context"),
        ["Str_Mem_Form"] = ("新建或编辑记忆", "New or edit memory"),
        ["Str_Mem_FormHint"] = ("标题、标签与正文", "Title, tags & content"),
        ["Str_Badge_LongTerm"] = ("长期上下文", "Long-term"),
        ["Str_Btn_Save"] = ("保存", "Save"),
        ["Str_Mem_List"] = ("记忆列表", "Memory entries"),

        ["Str_Chat_Title"] = ("问答", "Ask"),
        ["Str_Chat_Subtitle"] = ("先检索知识库资料，再由本地大模型生成回答 · 数据全程不出本机",
            "Retrieve from the knowledge base, then answer with your own model · data stays local"),
        ["Str_Chat_Empty"] = ("向知识库提问", "Ask your knowledge base"),
        ["Str_Chat_EmptyHint"] = ("例如：知识库里有哪些关于架构的约定？", "e.g. What conventions are documented?"),
        ["Str_Btn_Send"] = ("发送", "Send"),

        ["Str_Search_Title"] = ("搜索", "Search"),
        ["Str_Search_Subtitle"] = ("文档标题、正文和标签", "Document titles, content & tags"),
        ["Str_Btn_Search"] = ("搜索", "Search"),
        ["Str_Search_Header"] = ("搜索结果", "Search results"),
        ["Str_Search_NoResults"] = ("暂无搜索结果", "No results"),
        ["Str_Search_Hint"] = ("正文命中结果", "Content matches"),

        ["Str_Set_Title"] = ("设置", "Settings"),
        ["Str_Set_Subtitle"] = ("存储位置与服务连接", "Storage & connections"),
        ["Str_DataDir"] = ("数据目录", "Data directory"),
        ["Str_DataDir_Desc"] = ("知识库、原始文件与索引数据都会保存在这里。",
            "Documents, raw files and index data are stored here."),
        ["Str_Badge_Local"] = ("本地存储", "Local storage"),
        ["Str_Btn_PickDir"] = ("选择目录", "Choose folder"),
        ["Str_Btn_OpenDir"] = ("打开目录", "Open folder"),
        ["Str_Btn_Backup"] = ("备份", "Backup"),
        ["Str_Btn_Apply"] = ("应用", "Apply"),
        ["Str_Api_Title"] = ("API 服务", "API service"),
        ["Str_Api_Desc"] = ("桌面客户端通过此地址访问本地 Web API。",
            "The desktop client reaches the local Web API through this address."),
        ["Str_Btn_Test"] = ("测试连接", "Test connection"),
        ["Str_Profile_Title"] = ("检索预算（token 消耗）", "Retrieval budget (token usage)"),
        ["Str_Profile_Desc"] = ("选择档位后，搜索、问答与 AI 检索自动按该档限制单次上下文规模。",
            "Pick a preset to cap how much context each retrieval injects."),
        ["Str_Profile_Status_Format"] = ("当前档位：{0}｜单次检索 ≤ {1} 字符 / {2} 条（token 比重约 {3}×）",
            "Preset: {0} | up to {1} chars / {2} results per retrieval (about {3}× tokens)"),
        ["Str_Profile_Applied_Format"] = ("已切换到 {0}：单次检索 ≤ {1} 字符 / {2} 条（token 比重约 {3}×），对所有检索入口生效。",
            "Switched to {0}: up to {1} chars / {2} results per retrieval (about {3}× tokens), applies everywhere."),
        ["Str_Lang_Title"] = ("语言 / Language", "Language / Language"),
        ["Str_Lang_Desc"] = ("界面文字切换即时生效（部分状态提示暂为中文）。",
            "UI text switches immediately (some status messages are Chinese-only for now)."),
        ["Str_Lang_Zh"] = ("中文（默认）", "中文（默认）"),
        ["Str_Lang_En"] = ("English", "English"),
        ["Str_Lang_Applied"] = ("界面语言已切换。", "Language switched."),
        ["Str_Theme_Title"] = ("背景颜色", "Background color"),
        ["Str_Theme_Desc"] = ("更换界面底色与侧栏配色，重启应用后生效。", 
            "Change the page background and sidebar palette. Takes effect after restart."),
        ["Str_Theme_Default"] = ("默认（米白 + 墨绿侧栏）", "Default (cream + dark green)"),
        ["Str_Theme_Cool"] = ("冷灰蓝", "Cool gray-blue"),
        ["Str_Theme_Warm"] = ("暖米色", "Warm beige"),
        ["Str_Theme_Saved_Format"] = ("已保存“{0}”，重启应用后生效。", "Saved '{0}'. Takes effect after restart."),
        ["Str_About_Name"] = ("Local Knowledge Base", "Local Knowledge Base"),
        ["Str_About_Desc"] = ("本地优先的知识库与项目记忆工具", "Local-first knowledge base & project memory"),
    };

    private string _culture = "zh";

    public string Culture { get; private set; } = "zh";

    public event EventHandler? Changed;

    public string Get(string key) =>
        Table.TryGetValue(key, out var pair)
            ? (Culture == "en" ? pair.En : pair.Zh)
            : key;

    public void SetCulture(string culture)
    {
        culture = culture == "en" ? "en" : "zh";

        // 无论语言是否变化都写入资源：应用启动时必须填充（否则 StaticResource 解析即崩）
        if (Application.Current is not null)
            foreach (var key in Table.Keys)
                Application.Current.Resources[key] =
                    Table.TryGetValue(key, out var pair) ? (culture == "en" ? pair.En : pair.Zh) : key;

        if (Culture == culture)
            return;

        Culture = culture;
        _culture = culture;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
