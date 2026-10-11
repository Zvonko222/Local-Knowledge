# 开发记录

本文件记录 Local Knowledge Base 项目的所有开发活动，按时间顺序组织。每个条目包含日期、完成工作、决策说明和验证结果。

> 早期条目保留了当时的工程路径。当前目录结构以根目录 README 和
> `docs/architecture.md` 为准。

---

## 2026-10-10：项目初始化与基础骨架

### 背景

项目由用户在 Visual Studio 中初始化，包含两个独立的解决方案：

- `LocalKnowledgeBase.slnx` — WPF 桌面客户端解决方案
- `LocalKnowledgeBase.slnx` — ASP.NET Core Web API 解决方案

`src/LocalKnowledgeBase.Core` 类库存在但尚未集成到任一解决方案。

### 工作

1. **桌面端切换为 WPF**
   - 原骨架基于 Avalonia，根据用户偏好改用现有 WPF 工程。
   - 确认 WPF 解决方案使用传统 `LocalKnowledgeBase.Desktop.sln` 格式，目标为 `.NET 10.0`，workloads 为 `WindowsDesktop`。

2. **移除 Avalonia 依赖**
   - 原启动配置指向 Avalonia 类库项目，导致 Visual Studio 选中错误的启动项目。
   - 启动项目已更正为 `LocalKnowledgeBase.Desktop`（WPF 桌面应用）。

3. **实现 WebAPI 状态接口**
   - 新增 `KnowledgeBaseController.cs`，包含接口：
     - `GET /api/knowledge-base/status`
   - 接口返回 `KnowledgeBaseStatus` 记录，包含 `Name`、`Stage`、`Mode` 字段。

4. **更新 WPF MainWindow UI**
   - 左侧暗色导航栏：资料库、项目记忆、搜索、设置。
   - 右侧主内容区：欢迎语、当前阶段提示、本地数据目录选择占位。

### 验证

- WPF 编译：通过，0 警告，0 错误
- WebAPI 编译：通过，0 警告，0 错误
- WPF 进程启动：通过
- WebAPI 状态接口 (`GET /api/knowledge-base/status`)：通过，返回 200 OK 及有效 JSON

---

## 2026-10-10：Visual Studio 启动项目修复

### 问题

- 原 Avalonia 解决方案启动时，Visual Studio 选中类库项目而非应用程序入口，导致启动失败（无窗口弹出）。

### 解决

- WPF 解决方案 (`LocalKnowledgeBase.Desktop.sln`) 现正确将 `LocalKnowledgeBase.Desktop` 设为启动项目。

### 验证

- 按 `F5` 成功启动主窗口。

---

## 2026-10-10：文档编写

### 工作

- 编写完整 README.md，包含架构图、项目结构、构建运行指南、API 参考、配置说明、数据存储计划、开发路线图等。
- 编写双语开发日志。
- 创建 .gitignore 和 TODO.md。

---

## 2026-10-10：Phase 2 - 数据管理实现

### 背景

需要实现本地数据存储、文档导入、搜索和项目记忆功能。

### 决策

- 由于环境限制（nuget.org 被禁用，本地 NuGet 源不可用），选择使用 **JSON 文件存储** 而非 SQLite。
  - 优点：零外部依赖，.NET 10 内置 `System.Text.Json` 支持
  - 数据存储为 `documents.json`、`memory.json` 和 `raw_files/` 目录
  - 搜索通过 `StringComparison.OrdinalIgnoreCase` 实现
  - 符合用户偏好：使用标准格式、易迁移、无复杂依赖

### 工作

1. **KnowledgeBase.Core 重构**
   - 删除 `Class1.cs` 和 `SQLite Service`
   - 新增 `Models/Document.cs`：定义 `Document`、`MemoryEntry` 实体
   - 新增 `Services/KnowledgeBaseService.cs`：JSON 文件存储服务，包含文档 CRUD、记忆 CRUD、搜索、文本分块、文件内容提取
   - 新增 `Services/ConfigService.cs`：数据目录配置的持久化（保存在 `%APPDATA%/LocalKnowledgeBase/data_directory.txt`）

2. **WebAPI 重构**
   - 更新 `Program.cs`：注册 `KnowledgeBaseService` 为 Singleton
   - 重写 `KnowledgeBaseController.cs`：完整的 API 端点
     - `GET /api/knowledge-base/status` — 服务状态
     - `GET/POST /api/knowledge-base/data-directory` — 数据目录管理
     - `GET /documents/{id}`, `POST /documents`, `DELETE /documents/{id}` — 文档 CRUD
     - `GET /search?q=...` — 搜索文档
     - `GET /memory`, `GET /memory/{id}`, `POST /memory`, `DELETE /memory/{id}` — 记忆 CRUD
     - `GET /memory/search?q=...` — 搜索记忆
   - LocalKnowledgeBase.Api.csproj 添加 `KnowledgeBase.Core` 项目引用

3. **WPF 客户端重构**
   - 新增 `Themes/ModernDesign.xaml`：现代设计系统（颜色、按钮样式、文本样式、卡片样式、DataGrid 样式）
   - 更新 `App.xaml`：引用 ModernDesign 资源字典
   - 新增 `Models/KnowledgeBaseModels.cs`：客户端数据模型
   - 新增 `Services/KnowledgeBaseApiClient.cs`：HTTP API 客户端
   - 新增 `ViewModels/ViewModelBase.cs`：基础 INotifyPropertyChanged 实现
   - 新增 `ViewModels/RelayCommand.cs`：ICommand 实现
   - 新增 `ViewModels/MainViewModel.cs`：主视图模型（导航、API 调用、文档/记忆数据管理）
   - 新增 `Views/KnowledgeBasePage.xaml/.cs`：资料库页面（文档列表、导入按钮、文档详情双击查看）
   - 新增 `Views/MemoryPage.xaml/.cs`：项目记忆页面（记忆表单、记忆列表）
   - 新增 `Views/SearchPage.xaml/.cs`：搜索页面（搜索框、结果列表）
   - 更新 `Views/SettingsPage.xaml/.cs`：设置页面（数据目录选择、API 连接测试）
   - 重写 `MainWindow.xaml`：现代侧栏导航 + 内容区域 + 状态栏

4. **移除模板文件**
   - 删除 `WeatherForecastController.cs` 和 `WeatherForecast.cs`

### WPF 遇到的问题

- **命名空间冲突**：`UseWindowsForms` 导致 `UserControl`、`Application`、`WindowStartupState`、`OpenFileDialog` 出现二义性
  - 解决：在 XAML/code-behind 中使用完整限定名 `System.Windows.Controls.UserControl`

### 验证

- WebAPI 编译：通过，0 警告，0 错误
- WPF 编译：通过，0 警告，0 错误
- WPF 可用新样式主题
- 导航按钮正常切换页面

---

## 2026-10-10：UI 现代化改造

### 工作

- 添加 `Themes/ModernDesign.xaml` 设计系统
- 色彩方案：深蓝侧栏 (#1A1A2E)、深蓝主题色 (#1E88E5)、白色卡片
- 按钮样式：PrimaryButton（实心）、SecondaryButton（边框）、NavLinkButton（导航链接）
- 卡片样式：CardBorder（白色背景、圆角 10px）
- DataGrid 样式：ModernDataGrid（无表格线、悬停高亮、柔和表头）
- TextBox 样式：ModernTextBox（聚焦蓝边框）
- 状态栏：显示当前状态和 API 连接状态

### 文件变更

- `Themes/ModernDesign.xaml` (新增)
- `App.xaml` (更新：资源字典引用)
- `App.xaml.cs` (更新：简化，移除 OnStartup 重写)
- `MainWindow.xaml` (重写：现代布局)
- `MainWindow.xaml.cs` (更新：去除冗余 ViewModel 属性)
- `Views/*.xaml` + `.xaml.cs` (4 个页面统一现代化)
- `ViewModels/MainViewModel.cs` (更新：修复命名空间冲突)

---

## 2026-10-10：Phase 3 增量 - 完整编辑闭环

### 工作

- 后端新增文档和记忆更新接口：
  - `PUT /api/knowledge-base/documents/{id}`
  - `PUT /api/knowledge-base/memory/{id}`
- 文档导入改为安全存储文件名，保留原始文件名，并修正删除原始文件时路径不一致的问题。
- 数据目录修改后通过 `KnowledgeBaseService.Reconfigure` 立即生效，不再要求重启 API 服务。
- 新增 `LOCAL_KNOWLEDGE_BASE_DATA_DIR` 环境变量，用于测试和自动化部署时覆盖数据目录。
- WPF 资料库页面支持单击查看、编辑标题/标签/内容、保存和删除。
- WPF 项目记忆页面支持选中编辑、更新和删除。
- 修复 `ModernTextBox` 模板缺少 `PART_ContentHost` 的问题，输入框现在可以正常编辑。
- WPF API 地址可在设置页应用并保存到 `%APPDATA%\LocalKnowledgeBase\client_settings.json`。
- 搜索页支持回车搜索，并显示文档标签。
- 新增 5 个 `KnowledgeBase.Core` 单元测试，覆盖文档/记忆 CRUD、搜索上限、目录重配置和文本分块。

### 验证

- WPF 编译：通过，0 警告，0 错误
- Web API 编译：通过，0 警告，0 错误
- 单元测试：5/5 通过
- 真实 API 闭环：文档新增、更新、搜索、删除，记忆新增、更新、删除全部通过
- WPF UI 自动化：记忆新增/更新通过，文档选中/编辑/保存通过

---

## 2026-10-10：桌面端视觉重构

### 工作

- 重构 `ModernDesign.xaml` 设计系统，改用暖灰背景、墨绿侧栏、青绿色操作色和琥珀色强调色。
- 增加统一的图标字体、按钮、卡片、输入框、表格、状态栏和危险操作样式。
- 重做主窗口侧栏，加入品牌标识、导航图标、选中状态和底部 API 状态卡片。
- 重做资料库、项目记忆、搜索和设置页面的页面层级、间距、表单布局与列表样式。
- 增加响应式内容宽度转换器，使内容在大窗口保持 960 宽度，小窗口按可用空间收窄。
- 修正数据目录接口，使其返回当前服务实际使用的目录。

### 验证

- WPF 编译：通过，0 警告，0 错误
- 后端编译：通过，0 警告，0 错误
- 数据层测试：5/5 通过
- 四页离屏截图检查：布局无重叠、无裁切，导航选中状态和数据表格正常

---

## 2026-10-10：工程目录标准化与应用功能补全

### 工程结构

- 将三个生产项目统一迁移到 `src/`：
  - `LocalKnowledgeBase.Core`
  - `LocalKnowledgeBase.Api`
  - `LocalKnowledgeBase.Desktop`
- 将测试项目迁移到 `tests/LocalKnowledgeBase.Core.Tests`。
- 新增根解决方案 `LocalKnowledgeBase.slnx`，Visual Studio 只需打开一个解决方案。
- 项目名、程序集名、XML 命名空间和 C# 命名空间已统一为 `LocalKnowledgeBase.*`。
- 新增 `Directory.Build.props` 统一编译属性。
- 新增 `Directory.Packages.props` 统一 NuGet 包版本。
- 新增 `global.json` 固定 .NET 10 SDK 的向前滚动策略。
- 新增 `.editorconfig`，统一缩进、换行、编码和 XML/XAML 格式。
- 新增 `scripts/` 下的构建、测试、运行 API 和运行桌面端脚本。
- 重写 README，并新增 `docs/architecture.md`。
- 更新 Dockerfile 和 HTTP 请求文件，使其适配新的 `src/` 布局。

### 应用功能

- 资料库页面新增本地筛选，可按标题、标签、文件类型或正文过滤当前文档列表。
- 筛选在客户端执行，不会为每次输入额外请求 API。
- 清空筛选后恢复完整文档列表，并同步更新显示计数。

### 验证

- 根解决方案 restore：通过
- 根解决方案 Debug build：通过，0 警告，0 错误
- 根解决方案测试：5/5 通过
- `scripts/build.ps1`：通过
- `scripts/test.ps1`：通过
- 新目录下 API 启动与状态接口：通过
- 新目录下 WPF 桌面端启动：通过

---

## 2026-10-10：后端/前端顶层拆分

### 调整

- 将目录拆为明确的顶层 `backend/` 与 `frontend/`：
  - API：`backend/LocalKnowledgeBase.Api`
  - 共享核心：`backend/LocalKnowledgeBase.Core`
  - 后端测试：`backend/tests/LocalKnowledgeBase.Core.Tests`
  - 桌面端：`frontend/desktop/LocalKnowledgeBase.Desktop`
  - 移动端预留：`frontend/mobile`
- 更新根解决方案、项目引用、Dockerfile、脚本和文档路径。
- 移动端目录保留设计说明，后续可在不改动桌面端和后端目录的情况下加入项目。

---

## 2026-10-11：桌面端 MVVM 重构

### 背景

桌面端四个页面功能可用，但结构上存在几个问题：`MainViewModel` 一个类承担导航、
状态、文档、记忆、搜索全部逻辑；页面通过
`(MainViewModel)((MainWindow)Window.GetWindow(this)!).DataContext` 反向抓取主
ViewModel；`MainWindow.xaml` 声明了 `Window.DataContext` 而 code-behind 又 new 了
一个 `MainViewModel` 覆盖（启动时状态加载执行两次）；为使用 `FolderBrowserDialog`
引入了 `UseWindowsForms`，造成一系列命名空间冲突的变通。

### 工作

1. **按页面拆分 ViewModel**
   - 删除 `MainViewModel`，新增 `ShellViewModel`（仅导航与状态栏）与
     `DocumentsViewModel`、`MemoryViewModel`、`SearchViewModel`、`SettingsViewModel`。
   - 新增 `PageViewModelBase.OnNavigatedTo()`，页面进入时自行刷新数据。
   - 需要测试的业务动作暴露为公开 `Task` 方法，`AsyncRelayCommand` 只做薄封装。

2. **引入依赖注入**
   - `Directory.Packages.props` 新增 `Microsoft.Extensions.DependencyInjection 10.0.12`。
   - `App.xaml.cs` 成为组合根：服务、页面 VM、`MainWindow` 全部由容器创建；
     移除 `StartupUri`，XAML 中不再声明 `DataContext`（避免 VM 双重创建）。

3. **服务层抽象**
   - 新增 `IKnowledgeApiClient` 接口；HTTP 实现统一"失败不抛异常、写入 `LastError`"约定。
   - 新增 `IDialogService`（WPF `OpenFileDialog` / `OpenFolderDialog` / `MessageBox`），
     移除 `UseWindowsForms` 与相关命名冲突变通。
   - 新增 `IStatusReporter` / `StatusReporter`：页面通过它向主窗口状态栏汇报，
     不再直接引用 Shell。

4. **视图层全部改为绑定驱动**
   - 侧栏导航由手写四个 `ToggleButton` 改为 `ListBox`（新增 `NavListBox` /
     `NavListBoxItem` 样式），`SelectedItem` 与 `ShellViewModel` 双向绑定。
   - 页面 code-behind 只剩 `InitializeComponent()`；搜索框回车改用 `KeyBinding`。
   - 新增 `FileSizeConverter`，大小列由原始字节改为可读格式。
   - API 指示灯（绿/红）随连接状态变化。

5. **新增桌面测试项目**
   - `frontend/desktop/LocalKnowledgeBase.Desktop.Tests`（xUnit），通过内存版
     `FakeKnowledgeApiClient` 与 `FakeDialogService` 驱动 ViewModel，覆盖筛选、
     保存校验、导入、删除确认、编辑状态与搜索，共 11 个用例。
   - 已加入根解决方案与中央包管理。

6. **文档**
   - 新增 `docs/desktop-frontend.md`（中文，面向下一位开发者的前端指南：
     结构、启动流程、导航机制、新增页面步骤、约定、已知事项）。
   - README 的 Repository Layout 补充 Desktop.Tests 并链接上述文档。

### 过程中发现并修复的问题

- `ShellViewModel` 切换页面时未触发 `OnPropertyChanged(nameof(CurrentPage))`，
  导致侧栏选中但内容区不切换（UIA 冒烟测试发现）。
- 导入/保存/删除成功后的提示被 `LoadAsync` 的计数文案覆盖，调整为先刷新后提示
  （单元测试发现）。
- DI 未按具体类型注册 `StatusReporter`，`ShellViewModel` 激活失败导致启动崩溃
  （运行冒烟测试发现）。

### 验证

- 根解决方案 Debug 构建：通过，0 警告 0 错误
- 单元测试：后端 5/5、桌面 11/11 全部通过
- 真机冒烟：API（https profile）+ 桌面端同时启动，四个页面逐一截图检查；
  状态栏显示"服务就绪 / API 已连接"，资料库页正确显示通过 API 导入的测试文档
  （大小、时间、类型列渲染正确），测试完成后数据已删除
- `front-end/`（早期遗留的空目录）已删除

---

## 2026-10-11：MCP 服务器接入

### 背景

用户希望本地 AI（ZCode、Claude Desktop 等）能直接调取知识库。业界通行做法是把
能力封装成 MCP（Model Context Protocol）服务器。项目路线图中的
"MCP Server for Agent connectivity" 在本阶段落地。

### 决策

- **传输方式选 stdio**：本地 AI 客户端的主流接入方式，客户端把 MCP 服务器作为
  子进程拉起，无需额外开放端口。
- **MCP 经由 HTTP 调 API，而不是直连 JSON 文件**：`KnowledgeBaseService`
  采用进程内缓存 + 全量回写的存储模型，若 MCP 直连文件会形成第二个写入方，
  API 的内存副本过期后会覆盖 MCP 写入的数据。保持 API 为唯一写入方。
- **官方 C# SDK**：`ModelContextProtocol 2.2.0`（stable），中央包管理固定版本。

### 工作

1. 新增 `backend/LocalKnowledgeBase.Mcp`（net10.0 控制台，引用 Core 复用领域模型）。
2. 暴露 8 个 `kb_` 前缀工具：kb_status、kb_search_documents、kb_list_documents、
   kb_get_document、kb_search_memory、kb_list_memory、kb_get_memory、kb_save_memory。
   工具描述使用中文；搜索结果正文截断为摘要以节省模型上下文。
   AI 不开放删除能力，删除一律走桌面端。
3. API 地址通过环境变量 `LOCAL_KNOWLEDGE_BASE_API_URL` 配置，默认
   `http://localhost:5062`（与 run-api.ps1 默认 profile 一致）。
4. 日志经 M.E.Logging 输出到 stderr（`LogToStandardErrorThreshold = Trace`），
   stdout 保持纯协议通道。
5. 新增 `scripts/run-mcp.ps1`（调试运行）与 `scripts/publish-mcp.ps1`
   （发布到 `backend/LocalKnowledgeBase.Mcp/publish/`，.gitignore 已覆盖）。
6. README、TODO、docs/architecture.md 全部改为中文；
   新增中文使用教程 `docs/tutorial.md`（含 ZCode / Claude Desktop 配置示例）。

### 过程中发现的问题

- MCP SDK 对无默认值的参数（即使可空引用类型）按必填处理，
  `kb_save_memory` 的 `id`/`tags` 补上 `= null` 默认值并调整参数顺序。
- Windows PowerShell 5.1 读取无 BOM 的 UTF-8 脚本会按 ANSI 解析，
  中文脚本报"字符串缺少终止符"，run-mcp.ps1 / publish-mcp.ps1 已加 UTF-8 BOM。

### 验证

- 构建：0 警告 0 错误
- stdio 端到端冒烟：initialize 握手、tools/list（8 个工具齐全）、
  kb_list_documents / kb_search_documents（真实文档命中）、
  kb_save_memory 新建、kb_search_memory 命中、错误路径（错误 id 提示正常），
  日志确认全部走 stderr；测试数据已清理。
- `dotnet test`：后端 5/5、桌面 11/11 通过。

---

## 2026-10-11：导入格式扩展与易用性完善

### 工作

1. **文档提取格式扩展（Core）**
   - 新增 `TextExtractor`：pdf 由 PdfPig 0.1.16 逐页提取（纯 .NET 本地解析，无网络调用、无原生依赖），
     docx 解压读取 `word/document.xml` 按 w:p 段落拼接；txt / md 及其余仍按文本读取。
   - `KnowledgeBaseService.ExtractTextFromFile` 改为委托 TextExtractor，对外签名不变。

2. **桌面端易用性**
   - 导入对话框支持一次多选（`IDialogService.PickOpenFiles`），完成后显示"成功 N / M"汇总。
   - 资料库页支持从资源管理器拖拽文件导入（`Page_DragOver` / `Page_Drop`）。
   - 设置页新增"打开目录"按钮：资源管理器打开当前数据目录，便于查看与手动备份
     （`IDialogService.OpenInExplorer`，无目录时按钮禁用）。

3. **MCP 新增 kb_add_document**
   - AI 可把整理的结论/笔记作为新文档存入知识库（内容 UTF-8 编码后走 API 导入），
     保存后立即可被 kb_search_documents 检索；扩展名缺省补 .md。
   - MCP 工具增至 9 个；已重新发布 publish 目录。

### 验证

- 单元测试：后端 9/9（新增 TextExtractor 4 例，PDF/docx 样例程序化构造）、桌面 12/12（新增批量导入 1 例）
- 真实 PDF 链路：321KB 的实际 PDF 经 API 导入，提取出 1.8 万字符正文，检索命中，测试数据已删除
- 发布版 MCP exe 冒烟：tools/list 显示 9 个工具，kb_add_document 存入中文笔记后立即检索命中
- 全解决方案构建：0 警告 0 错误

---

## 2026-10-11：AI 问答（本地 RAG 闭环）

### 背景

知识库的"R"（检索）已有，这一阶段补上"A"（生成）：在桌面端新增"问答"页，
先全文检索知识库资料，再交给**本地大模型**生成带引用的回答，形成完整 RAG 链路。

### 决策

- **模型接入选 OpenAI 兼容接口**：Ollama、LM Studio、vLLM、llama.cpp server 全都提供
  `/v1/chat/completions`，一套实现通吃；只填主机名时自动补 `/v1`。
- **ViewModel 不读配置文件**：`ILlmClient` 在启动时按 `client_settings.json` 配好，
  设置页修改后调用 `Configure` 即时生效；ChatViewModel 只看 `_llm.Model` 是否为空，
  保证可测试。
- **防编造**：系统提示词要求模型只依据提供的资料回答并标注 [1] [2] 引用；检索无命中时
  不调用模型，直接提示"未找到相关资料"。
- **修复保存覆盖问题**：设置页此前保存 API 地址会新建配置对象，把将来的模型配置一起
  抹掉；改为"读取-修改-写回"整个 `ClientSettings`。

### 工作

1. 新增 `ILlmClient` / `OpenAiCompatibleLlmClient`（支持注入 HttpMessageHandler 供测试）。
2. 新增 `ChatViewModel` + `ChatPage`：消息气泡（用户右/助手左）、来源徽章、空状态引导、
   Enter 发送、新消息自动滚动；导航增至五项。
3. 设置页新增"本地模型"配置区（地址、模型名、应用、测试连接）。
4. 全局 `DispatcherUnhandledException`：异常写入
   `%LOCALAPPDATA%\LocalKnowledgeBase\crash.log`，弹窗只弹一次。
5. 项目记忆页新增本地筛选；文档详情新增"导出 Markdown"。
6. 测试增至 33 个：问答流程（未配模型/命中/无命中/空问题/模型失败）、
   LLM 客户端（地址规范化、请求体、解析、离线探测）、记忆筛选、Markdown 导出。

### 过程中发现并修复的问题

- `ChatPage` 写了 `SelectionMode="Disabled"`——WPF 该枚举没有此值，模板加载即抛
  XamlParseException（原先表现为应用静默消失，加全局异常日志后定位）。
- 在 `CollectionChanged` 内同步 `ScrollIntoView` 重入 ItemsControl 容器生成，触发
  "ItemsControl is inconsistent with its items source"；滚动改为 `Dispatcher.BeginInvoke` 延迟。
- WPF 按钮内容为面板时 UIA Name 为空，发送按钮补 `AutomationProperties.Name`（兼顾无障碍）。
- 崩溃处理器里的模态 MessageBox 会泵消息导致布局异常递归弹窗、栈溢出；加"只弹一次"标记。

### 验证

- 全解决方案构建：0 警告 0 错误；测试 33/33 通过
- 真机冒烟：python 起一个 OpenAI 兼容桩模型服务（11434 端口），UIA 实操
  "导航到问答页 → 输入问题 → 发送"，截图确认：命中资料后模型回答正常渲染、
  引用徽章 [1] 显示、未命中路径提示正确、全程无崩溃；测试数据与桩服务已清理
- Windows PowerShell 5.1 的脚本 BOM 问题在本阶段再次出现，测试脚本已统一加 BOM

---

## 2026-10-11：检索相关度打分与知识库导出

### 工作

1. **检索算法升级（Core）**
   - `SearchDocuments` / `SearchMemoryEntries` 由"整句子串匹配"升级为：
     按空白与中英文标点分词 → 逐词打分（标题命中 ×5、标签 ×3、正文 ×1）→
     按得分排序（同分按更新时间）。整句提问、粘贴文本（短语与原文逐字一致）都能命中。
   - 新增 `TokenizeQuery` / `ScoreText` 供测试与复用；API 契约不变（调用方自动受益）。

2. **知识库一键导出（桌面端）**
   - 资料库工具栏新增"导出全部"：选择目录后每篇文档生成一个 .md 文件
     （重名自动加序号），并生成 `_索引.md` 汇总；空知识库时提示且不弹目录框。

### 验证

- 单元测试 43/43（Core 17 + Desktop 26）：新增分词、打分权重、整句粘贴场景、
  多词求和排序、记忆检索、导出合集（含索引与空库路径）
- 真机 API 冒烟：三篇不同相关度的文档，"docker 部署"按相关度返回两篇、
  标题命中的排最前、逐字短语命中、无关词返回空，测试数据已清理
- 过程中确认的流程注意点：`dotnet test` 不会重建 API 项目，
  验证 API 行为前需 `dotnet build LocalKnowledgeBase.slnx`；
  API 进程存活时全解决方案构建会因 DLL 被锁失败，先停 API 再构建

---

## 2026-10-11：AI 专用 HTTP 接口（api/ai）

### 背景

MCP 已覆盖支持 MCP 的 AI 客户端；但大量 AI 工具（ZCode 的 HTTP 工具调用、Dify、n8n、
OpenWebUI、自研 Agent）只走 HTTP。本阶段在 API 上新增 AI 专用接口层，
把"检索 → 打包上下文 → 调本地模型"的能力以标准 HTTP JSON 形式开放。

### 工作

1. **Core**：新增 `AiContextBuilder`（纯函数）——把检索结果打包成带 [n] 编号引用的
   上下文文本 + 来源列表（含文档 Id），系统提示词常量随附；与桌面端问答格式一致。
2. **API**：新增 `AiController`（路由 `api/ai`）：
   - `GET /api/ai/status` — 模型配置状态与数据目录（接入前探测）
   - `POST /api/ai/context` — {query, max, snippetLength} → 打包上下文（不需要模型）
   - `POST /api/ai/answer` — {query, max} → 检索 + 本地模型生成带引用回答；
     模型未配置返回 503（附配置指引），调用失败返回 502
   - 模型经 `LOCAL_KNOWLEDGE_BASE_LLM_URL` / `LOCAL_KNOWLEDGE_BASE_LLM_MODEL`
     环境变量配置（与 LOCAL_KNOWLEDGE_BASE_DATA_DIR 同模式），未配置不影响其余接口
3. **API**：新增 `LocalLlmClient`（服务端版 OpenAI 兼容客户端，只填主机名自动补 /v1）。
4. **MCP**：新增第 10 个工具 `kb_get_context`（打包上下文，含来源文档编号），
   publish 目录已重新发布。

### 验证

- 单元测试 48/48（Core 22 + Desktop 26）：新增 AiContextBuilder 5 例
  （编号与来源映射、截断、空结果、过小 snippet 钳制、系统提示词约束）
- 真机冒烟（python 桩模型 11434 + 环境变量启动 API）：
  - status 正确报告 llmConfigured/model/数据目录
  - context 命中时返回编号上下文与来源；无命中返回 0
  - answer 命中时返回模型回答 + 来源（含文档 Id）；无命中时返回提示文案；空查询 400
  - MCP tools/list 10 个工具，kb_get_context 命中路径返回可用上下文
- 测试数据全部清理；用户数据目录（demo/RAG）未被触碰

---

## 2026-10-11：ZCode MCP 接入落地与日常启动脚本

- 已把 `local-knowledge-base` MCP 服务器写入 ZCode 用户级配置
  （`%USERPROFILE%\.zcode\cli\config.json` 的 `mcp.servers`，本机原先无此文件），
  ZCode 新会话起即可通过 kb_ 工具读写知识库。
- 新增 `scripts/start-api.ps1`：后台最小化启动 API（MCP 与桌面端都依赖它常驻），
  已验证 http:5062 →（HTTPS 重定向自动跟随）→ https:7047 全链路可用。
- MCP 发布目录 exe 为最新（含 kb_get_context，共 10 个工具）。

---

## 2026-10-11：知识库使用技能（SKILL.md）

- 新增 `.zcode/skills/local-knowledge-base/SKILL.md`（仓库源文件），
  并安装到用户级 `%USERPROFILE%\.zcode\skills\local-knowledge-base\`：
  定义 AI 使用知识库的规范——工具速查（10 个 kb_* 工具按场景选用）、
  保存记忆的时机与格式（查重后更新、报告编号）、检索与引用规范（[n] 标注、
  无命中如实说明、不编造）、API 未运行时自动执行 start-api.ps1 并重试、
  无删除工具的边界说明。
- 与已写入的用户级 MCP 配置配套：MCP 提供"能调用"，SKILL.md 提供"懂规矩"。

---

## 2026-10-11：双击启动器与一次桌面快捷方式事故（已恢复）

### 新增

- 仓库根目录新增双击启动器（内容纯 ASCII，免疫系统代码页差异）：
  - `启动知识库服务.bat`：检查 7047 端口，未运行则后台最小化启动 API，已运行则提示退出；
  - `启动桌面客户端.bat`：优先启动已编译 exe，缺失时回退 dotnet run。
- 两个分支均已实测（运行中提示 / 冷启动 15 秒内 API 上线）。

### 事故与恢复（重要）

用 WScript.Shell 批量建桌面快捷方式时发生误删：清理乱码 lnk 的过滤条件写成了
"删除所有非 ASCII 文件名的 .lnk"，把用户桌面 15 个原有快捷方式一并删除。

- 已恢复 8 个：从开始菜单（用户/公共）找到同名副本复制回桌面（原中文名）。
- 已重建 7 个：定位到各目标程序后用 8.3 短路径 + 英文文件名重建
  （AdvancedSettings / geek / LauncherGame / LiveCaptionsTranslator / Photoshop /
  QuestTool / JianYing——其中剪映因目标路径含中文且 8.3 短名仍含中文，
  其启动器未完成，目标 exe 路径已写入桌面《快捷方式状态清单.txt》供手动重建）。
- 完整清单在桌面 `快捷方式状态清单.txt`。

### 根因记录（本机编码环境）

本机"非 Unicode 程序代码页"不是中文(936)：
- 所有经 ANSI/控制台代码页转换的中文都变 `?`（curl 表单体、GBK ps1 脚本、
  WScript 中文路径静默失败且不留文件）；
- 可靠路径：PowerShell 用 **UTF-8 BOM 或 -EncodedCommand(UTF-16LE)**，
  .NET 文件 API / Python os 用 Unicode API 不经转换；
- 教训：批量删除必须精确匹配目标文件名，禁止按"非 ASCII"这类宽泛条件过滤。

---

## 2026-10-11：SKILL.md 正规化重写与云端 API 模型支持

### 背景

用户明确：AI 均通过云端 API 接入，不要求本地部署模型；SKILL.md 需按正式规范重写。
原桌面端模型客户端不支持鉴权，导致 GLM/DeepSeek 等云端 OpenAI 兼容接口无法使用。

### 工作

1. **SKILL.md 全面重写**（仓库源文件 + 用户级副本同步更新）：
   - 结构正规化：概述（数据/通道/模型要求）→ 工具清单（10 个工具按场景选用）→
     操作规范（检索与回答、保存记忆、沉淀文档、故障处理，MUST/应当/禁止分级）→
     数据与安全边界。
   - 明确"模型要求：无"，知识库与所用模型（云端 API 或本地部署）完全解耦。
2. **API 密钥支持**：
   - 桌面端 `ILlmClient.Configure(baseUrl, model, apiKey)`，密钥以
     `Authorization: Bearer` 头发送；`client_settings.json` 新增 `LlmApiKey`；
     设置页 LLM 卡片增加密钥输入。
   - 后端 `/api/ai/answer` 同步支持 `LOCAL_KNOWLEDGE_BASE_LLM_KEY` 环境变量。
3. 文档去除"必须本地部署模型"的表述：教程第 5 节改为云端 API 优先
   （GLM/DeepSeek/OpenAI 配置表），Ollama 降级为可选示例之一。

### 验证

- 测试 49/49（新增：带密钥请求携带 Bearer 头、云端路径不追加 /v1、
  未配密钥不带鉴权头；随文案更新修正 1 处断言）
- 全解决方案构建 0 警告 0 错误

---

## 2026-10-11：检索算法升级、标签分类可视化与 AI 检索摘要优化

### 1. 检索算法升级（Core，搜索/问答/MCP/HTTP 全入口生效）

- 关键词打分升级：标题命中 ×8、标签 ×4、正文按**出现次数** ×2（单词最多计 5 次）；
  关键词不少于 2 个且**全部命中**时额外 +8 覆盖加成。
- 新增 `BestSnippet`：摘要窗口以关键词命中位置为中心（而非从头截断）。
- 单元测试更新并新增 5 例（词频上限、覆盖加成、摘要定位、无命中回退）。

### 2. AI 调用的检索优化

- `AiContextBuilder` 上下文摘要改为查询相关窗口（模型拿到的片段直接对准问题）；
  来源列表（/api/ai/context 与 MCP kb_get_context）新增每条"摘要"字段。
- MCP `kb_search_documents` 的内容摘要同步改为查询相关。
- 后端 `POST /documents` 导入接口支持 `tags` 字段（AI 存文档可带分类），
  MCP `kb_add_document` 增加 tags 参数。

### 3. 标签分类可视化（桌面端资料库）

- 文档列表上方新增**标签分类芯片**：全部 / 各标签 / 未分类（自动统计数量），
  点选即过滤，可与关键词筛选叠加；标签编辑后芯片自动重建并保持选中。
- 样式：TagChipListBox / TagChipItem（ModernDesign.xaml）。
- 后端导入接口 tags 支持后，AI 通过 kb_add_document 存的文档自动进入分类体系。

### 4. 真机验证与 bug 修复

- 修复：**首屏数据不加载**——ShellViewModel 此前注册为 transient，App 启动时
  `GetRequiredService<ShellViewModel>()` 创建了第二个实例，首屏 OnNavigatedTo
  加载到了无人显示的实例上；改为 singleton 并在 InitializeAsync 末尾显式触发
  首屏加载。该 bug 同时解释了历次冒烟中"启动后文档列表为空"的现象。
- 真机 UIA 验证：芯片计数正确（全部 4 / 架构 2 / 部署 1 / 未分类 2），
  点选"架构"后列表精确过滤为 2 篇；测试文档已清理。

### 验证

- 测试 69/69（Core 38 + Desktop 31）
- 全解决方案构建 0 警告 0 错误

---

## 2026-10-11：会话存本工作流与 token 预算控制

### 背景

用户核心诉求：**每次对话都产生一个存本**（知识放知识库），另一个 AI 询问相关问题时
调用知识库回答——即**以硬盘存储为载体的超长上下文**；同时要有索引/预算等机制
**减少检索消耗的 token 量**。

### 工作

1. **AiContextBuilder 支持总字符预算**：`Build(..., maxTotalChars)` 按相关度从高到低
   装入条目，装不下即停（第一条始终保留）；AiContext 新增 IncludedCount / Truncated。
2. **接口暴露预算**：`/api/ai/context` 请求新增 `maxTotalChars`（默认 4000），
   响应新增 `includedCount` / `truncated`；`/api/ai/answer` 内部预算固定 4000。
3. **MCP `kb_get_context` 新增 `max_chars` 参数**，返回体增加“已装入条数 / 因预算舍弃”。
4. **SKILL.md 新增 3.5 会话存本工作流**（触发条件、提炼规则、查重、
   `title=会话存本：<主题>` / `tags=会话存本+主题词` 的格式约定、保存后报告）与
   **3.6 Token 消耗控制**（必用 kb_get_context + 按任务规模设 max_chars、
   摘要优先于全文、禁止“保险式”塞全文）。
5. 教程新增 5.4 会话存本用法与省 token 机制表；README 同步。

### 验证

- 单元测试 72/72：新增预算用例（预算内截断并标记、宽预算全保留、
  首条超预算仍保留）
- MCP 工具签名变更后全解决方案构建 0 警告 0 错误

---

## 2026-10-11：相关文档（跨文档语义关联）与一键备份

### 工作

1. **Core**：`VectorIndexService.FindMostSimilar(documentId, topK)`——按余弦相似度
   找最相关文档，自动排除自身与维度不一致（换模型遗留）的条目。
2. **API**：`GET /api/ai/related/{id}`——embedding 未配置返回 503、文档不存在 404、
   未建向量返回空列表。
3. **MCP**：第 11 个工具 `kb_related_documents`（阅读/总结一篇文档时拉取相关资料）。
4. **桌面端**：资料库详情面板新增“相关文档”按钮组（相似度百分比），点击在列表中
   定位该文档；设置页新增“备份”按钮——数据目录一键压缩为 ZIP。
5. 新增 `ParameterRelayCommand`（带 CommandParameter 的同步命令）。

### 排障记录

- publish 目录的 exe 被此前 stdio 冒烟残留的 MCP 进程锁定，导致 `dotnet publish`
  **静默失败**（脚本无条件打印成功横幅）——发布目录停留在旧版本，工具清单差 1 个。
  已：杀残留进程 + publish 脚本增加 `$LASTEXITCODE` 检查。
- 桌面端“首屏文档列表为空”根因同日修复（ShellViewModel transient 双实例）。

### 验证

- 测试 75/75（Core 43 + Desktop 32）：新增 FindMostSimilar 排序/排除自身与
  维度不一致条目、备份数据目录 ZIP 生成
- 真机 e2e：桩 embedding 下导入自动索引 → `/ai/related/{id}` 返回同语义簇文档
  （相似度 100%），正交文档被过滤；MCP tools/list 11 个工具含 kb_related_documents
- 测试数据已清理；用户文档未受影响

---

## 2026-10-11：检索结果注入瘦身（去冗余摘要）

- `kb_get_context` / `/api/ai/context` 的来源列表此前每条携带 200 字摘要，
  与上下文正文中同一文档的片段完全重复——单次检索多耗约 1000 字符。
  已移除（来源仅保留序号/标题/文档编号），上下文正文信息不变。
- SKILL.md 3.6 新增第 5 条：同一会话同一主题只检索一次，杜绝结果反复驻留上下文。

---

## 2026-10-11：检索预算三档预设（界面与 AI 均可切换）

### 功能

- **三档预设**（Core `RetrievalProfileService`，持久化于
  `%APPDATA%\LocalKnowledgeBase\retrieval_profile.json`）：
  | 档位 | 最大条数 | 单条摘要 | 总字符预算 | token 比重 |
  | light 轻量 | 3 | 400 | 1500 | 约 0.4× |
  | standard 标准 | 6 | 800 | 4000 | 1×（默认） |
  | deep 深度 | 10 | 1200 | 8000 | 约 2× |
- **生效范围**：`GET /search`、`/api/ai/context`、`/api/ai/answer`、
  MCP `kb_search_documents` / `kb_get_context`——请求未显式指定参数时套用档位；
  显式传参仍可临时覆盖。
- **两种切换方式**：桌面端"设置 → 检索预算（token 消耗）"卡片（下拉三档 + 应用）；
  MCP 第 12 个工具 `kb_set_profile`（AI 运行时切换）。
- **API**：`GET/PUT /api/ai/profile` 读写档位（含各档 token 比重，供界面渲染）。
- 测试 78/78（新增档位持久化、回落解析、档位切换经 API）。

### 排障与教训

- 档位测试通过修改进程 APPDATA 环境变量注入临时目录——xunit 并行集合下会污染
  其他测试；`RetrievalProfileService` 已支持显式目录注入，测试不再改全局环境。
- 本轮档位切换测试曾把用户真实的档位文件留在 light，已清除（默认回 standard）。
- 测试操纵进程级全局状态（环境变量）必须避免；改为被测对象支持显式依赖注入。
