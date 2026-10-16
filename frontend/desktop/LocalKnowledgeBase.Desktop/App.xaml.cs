using System.Windows;
using LocalKnowledgeBase.Desktop.Services;
using LocalKnowledgeBase.Desktop.ViewModels;
using LocalKnowledgeBase.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Localization = LocalKnowledgeBase.Desktop.Services.Localization;

namespace LocalKnowledgeBase.Desktop;

/// <summary>
/// 应用组合根：所有服务与 ViewModel 在这里注册，
/// MainWindow 及其 ShellViewModel 由容器创建。
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private bool _crashDialogShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 未处理异常：写日志 + 弹窗提示（只弹一次，避免布局期异常递归弹窗），避免应用静默消失
        DispatcherUnhandledException += (_, args) =>
        {
            var disposition = CrashLogger.Log(args.Exception);
            if (disposition == CrashLogDisposition.Repeated)
            {
                args.Handled = true;
                Shutdown(1);
                return;
            }

            if (!_crashDialogShown)
            {
                _crashDialogShown = true;
                MessageBox.Show(
                    "发生未处理的异常，详细信息已写入 %LOCALAPPDATA%\\LocalKnowledgeBase\\crash.log\n\n" +
                    args.Exception.Message,
                    "Local Knowledge Base",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            args.Handled = true;
        };

        var clientSettings = ClientSettingsService.Load();

        // 调色板最先合并：样式解析时从调色板取色（背景主题在设置页选择，重启后生效）
        var paletteSource = clientSettings.Background switch
        {
            "cool" => "Themes/Palette.Cool.xaml",
            "warm" => "Themes/Palette.Warm.xaml",
            _ => "Themes/Palette.Default.xaml"
        };
        Resources.MergedDictionaries.Insert(0,
            new ResourceDictionary { Source = new Uri(paletteSource, UriKind.Relative) });

        // 界面语言：把全部文案写入应用资源（XAML 用 DynamicResource），供设置页运行时切换
        var localization = new Localization();
        localization.SetCulture(clientSettings.Language);

        var services = new ServiceCollection();
        services.AddSingleton<ILocalization>(localization);
        ConfigureServices(services, clientSettings, localization);
        _services = services.BuildServiceProvider();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // 启动后异步探测一次 API 连接，结果写入状态栏，并触发首屏数据加载
        _ = _services.GetRequiredService<ShellViewModel>().InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, ClientSettings clientSettings,
        Localization localization)
    {
        // 基础服务
        // StatusReporter 按具体类型注册：ShellViewModel 用它做绑定源，页面用 IStatusReporter 上报
        services.AddSingleton<StatusReporter>();
        services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<StatusReporter>());
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<IKnowledgeApiClient>(_ =>
            new KnowledgeBaseApiClient(clientSettings.ApiBaseUrl));
        // 本地大模型（AI 问答用）：地址、模型与密钥来自 client_settings.json，设置页可随时修改
        services.AddSingleton<ILlmClient>(_ =>
            new OpenAiCompatibleLlmClient(clientSettings.LlmBaseUrl, clientSettings.LlmModel, clientSettings.LlmApiKey));

        services.AddSingleton(localization);

        // 页面 ViewModel（Shell 为单例：MainWindow 注入的实例与 App 启动初始化的是同一个；
        // 页面 VM 随 Shell 只创建一套，页面切换不重建）
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();

        // 视图
        services.AddSingleton<MainWindow>();
    }
}
