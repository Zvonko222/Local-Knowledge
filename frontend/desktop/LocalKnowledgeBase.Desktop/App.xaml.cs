using System.Windows;
using LocalKnowledgeBase.Desktop.Services;
using LocalKnowledgeBase.Desktop.ViewModels;
using LocalKnowledgeBase.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LocalKnowledgeBase.Desktop;

/// <summary>
/// 应用组合根：所有服务与 ViewModel 在这里注册，
/// MainWindow 及其 ShellViewModel 由容器创建。
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // 启动后异步探测一次 API 连接，结果写入状态栏
        _ = _services.GetRequiredService<ShellViewModel>().InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 基础服务
        // StatusReporter 按具体类型注册：ShellViewModel 用它做绑定源，页面用 IStatusReporter 上报
        services.AddSingleton<StatusReporter>();
        services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<StatusReporter>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IKnowledgeApiClient>(_ =>
            new KnowledgeBaseApiClient(ClientSettingsService.Load().ApiBaseUrl));

        // 页面 ViewModel（Shell 每次启动创建一套，页面切换不重建）
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ShellViewModel>();

        // 视图
        services.AddSingleton<MainWindow>();
    }
}
