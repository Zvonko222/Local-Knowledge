# 桌面前端（WPF）开发指南

本文面向要继续开发 `LocalKnowledgeBase.Desktop` 的开发者，说明前端的结构、
约定和常见改动入口。整体架构与仓库边界见 [architecture.md](architecture.md)。

## 技术栈

- WPF（`net10.0-windows`），`UseWPF`，**不依赖 WinForms**
- 手写 MVVM（`ViewModelBase` + `RelayCommand` / `AsyncRelayCommand`），不使用 MVVM 框架
- `Microsoft.Extensions.DependencyInjection` 作为组合根（与后端 ASP.NET Core 一致）
- xUnit 单元测试（只测 ViewModel，不测 UI）

## 目录结构

```text
frontend/desktop/
├── LocalKnowledgeBase.Desktop/            # WPF 应用
│   ├── App.xaml / App.xaml.cs             # 组合根：DI 注册 + VM→View 模板
│   ├── MainWindow.xaml / .cs              # 外壳：侧栏导航 + 内容区 + 状态栏
│   ├── Models/ApiModels.cs                # 与后端 JSON 契约对应的 DTO
│   ├── Services/
│   │   ├── IKnowledgeApiClient.cs         # API 客户端抽象（ViewModel 只依赖它）
│   │   ├── KnowledgeBaseApiClient.cs      # HTTP 实现
│   │   ├── ILlmClient / OpenAiCompatibleLlmClient # 本地大模型客户端（问答用，OpenAI 兼容接口）
│   │   ├── IDialogService / DialogService # 文件/目录选择、确认框（可测试的 UI 封装）
│   │   ├── IStatusReporter / StatusReporter # 状态栏消息与 API 连接指示
│   │   └── ClientSettingsService.cs       # 本机 client_settings.json 读写
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs               # INotifyPropertyChanged 基类
│   │   ├── RelayCommand / AsyncRelayCommand
│   │   ├── PageViewModelBase.cs           # 页面 VM 基类（OnNavigatedTo）
│   │   ├── ShellViewModel.cs              # 导航 + 状态栏
│   │   └── Documents / Memory / Chat / Search / SettingsViewModel.cs
│   ├── Views/                             # 四个页面，code-behind 只有 InitializeComponent
│   ├── Converters/                        # FileSizeConverter、ResponsivePageWidthConverter
│   └── Themes/ModernDesign.xaml           # 颜色、字体、控件样式（设计系统）
└── LocalKnowledgeBase.Desktop.Tests/      # ViewModel 单元测试（内存假 API）
```

## 启动流程

1. `App.xaml` **没有** `StartupUri`。
2. `App.xaml.cs` 构建 `ServiceCollection` 并注册全部服务和 ViewModel。
3. 容器创建 `MainWindow`，其构造函数接收注入的 `ShellViewModel` 作为 `DataContext`。
4. 窗口 `Show()` 后调用 `ShellViewModel.InitializeAsync()`，探测一次 API 连接并写入状态栏。

**不要**在 XAML 里声明 `<Window.DataContext>` —— 那会造成 ViewModel 双重创建
（历史上的真实 bug：启动时状态被加载两次）。

## 导航机制

- `ShellViewModel.NavItems` 定义侧栏项（图标、标题、页面 VM），顺序即显示顺序。
- 侧栏是 `ListBox`（`ModernDesign.xaml` 中的 `NavListBox` / `NavListBoxItem` 样式），
  `SelectedItem` 与 `ShellViewModel.SelectedNavItem` 双向绑定。
- 选中项变化时，`CurrentPage` 换成对应页面 VM 并触发 `OnNavigatedTo()`（页面在此刷新数据）。
- 内容区 `ContentControl Content="{Binding CurrentPage}"`，
  由 `App.xaml` 中的 `DataTemplate`（VM 类型 → View）自动实例化视图。

### 新增一个页面的步骤

1. 在 `ViewModels/` 新建 `XxxViewModel : PageViewModelBase`，数据刷新逻辑写在 `OnNavigatedTo()`。
2. 在 `Views/` 新建 `XxxPage.xaml`（DataContext 就是 VM，不要在 code-behind 写业务）。
3. 在 `App.xaml` 加一条 `<DataTemplate DataType="{x:Type vm:XxxViewModel}"><views:XxxPage/></DataTemplate>`。
4. 在 `ShellViewModel` 构造函数的 `NavItems` 里加一项（图标用 Segoe Fluent Icons 字形）。
5. `App.xaml.cs` 注册 `services.AddTransient<XxxViewModel>()`。

## 服务层约定

- **API 错误处理**：`IKnowledgeApiClient` 的方法失败时不抛异常，返回 `null` / `false`，
  并把可读错误写入 `LastError`。ViewModel 展示 `_api.LastError ?? "默认提示"`。
- **LLM 调用**：`ILlmClient.CompleteAsync` 失败时抛 `InvalidOperationException`
  （带可读中文信息），由 ChatViewModel 捕获后写入气泡文本。问答前用
  `_llm.Model` 判断是否已配置，避免 ViewModel 直接读配置文件（保证可测试）。
- **对话框**：ViewModel 一律通过 `IDialogService`，不要直接 `new OpenFileDialog()`
  或 `MessageBox.Show()` —— 否则无法单测。
- **状态栏**：页面 VM 通过注入的 `IStatusReporter` 上报消息与 API 状态，
  不直接引用 `ShellViewModel`。
- **客户端配置**：API 地址与本地模型配置保存在
  `%APPDATA%\LocalKnowledgeBase\client_settings.json`，由 `ClientSettingsService`
  读写。**保存任何一项都要“读取-修改-写回”整个对象**，否则会覆盖其他配置项。
- **全局异常**：`App.xaml.cs` 挂接了 `DispatcherUnhandledException`，异常写入
  `%LOCALAPPDATA%\LocalKnowledgeBase\crash.log` 并弹窗（只弹一次，防止布局期
  异常递归弹窗导致栈溢出）。排查“应用闪退”先看这个日志。

## 绑定与命令约定

- 输入框即时过滤用 `UpdateSourceTrigger=PropertyChanged`。
- 异步操作用 `AsyncRelayCommand`：执行期间自动禁用按钮（`IsRunning`），
  异常走 `onException` 回调，不允许 `async void` 散落在页面里。
- 需要被测试覆盖的业务动作，写成 ViewModel 上的公开 `Task` 方法，
  命令只做薄封装（参考 `DocumentsViewModel.ImportFileAsync`）。

## 单元测试

```powershell
dotnet test LocalKnowledgeBase.slnx
```

`LocalKnowledgeBase.Desktop.Tests` 通过 `Fakes/FakeKnowledgeApiClient`（内存数据）和
`FakeDialogService`（不弹窗）驱动 ViewModel，覆盖筛选、校验、保存、搜索等逻辑。
新增页面行为时请补对应测试。

## 已知事项

- 后端 `GET /status` 返回的 `KnowledgeBaseStatus.Mode` 字段实际装的是数据目录
  （历史命名，见 `KnowledgeBaseController.cs`），客户端 DTO 保留原名以匹配 JSON。
- API 客户端放行了本地开发证书（`DangerousAcceptAnyServerCertificateValidator`），
  仅限本地开发使用；规范做法是执行 `dotnet dev-certs https --trust`。
- 后端搜索接口支持 `max` 参数限制条数，客户端当前未传（使用后端默认值）。
