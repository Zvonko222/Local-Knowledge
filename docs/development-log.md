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
