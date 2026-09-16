using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using KukaManager.Data;
using KukaManager.Design;
using KukaManager.Services;
using KukaManager.Utils;
using KukaManager.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KukaManager;

public static class Program
{
    private static Mutex? _mutex;
    private static readonly nint DpiAwarenessContextPerMonitorV2 = new(-4);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    [STAThread]
    public static void Main()
    {
        // The manifest handles DPI on supported Windows versions. This early call is an additional
        // safeguard for unpacked/self-contained launches before any WPF visual is created.
        try { _ = SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorV2); } catch { }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppPaths.EnsureDirectories();
        _mutex = new Mutex(true, "Local\\KukaManager-Desktop-SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("酷咔管理系统已经在运行。", "酷咔管理系统", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var log = new LogService();
        try
        {
            using var host = CreateHost(log);
            host.Start();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Theme.Apply(app);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Error(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)), "UnhandledException");
            app.DispatcherUnhandledException += (_, e) => { log.Error(e.Exception, "DispatcherUnhandledException"); MessageBox.Show(e.Exception.Message, "酷咔发生错误", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };

            var store = host.Services.GetRequiredService<KukaStore>();
            store.IntegrityCheck();
            if (!store.AccountInitialized())
            {
                var setup = new SetupWindow(store);
                if (setup.ShowDialog() != true) return;
            }
            var login = new LoginWindow(store);
            if (login.ShowDialog() != true) return;

            try { store.Backup(); } catch (Exception ex) { log.Error(ex, "startup backup"); }
            try { store.ProcessDue(); } catch (Exception ex) { log.Error(ex, "startup process due"); }

            var main = host.Services.GetRequiredService<MainWindow>();
            app.MainWindow = main;
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
            app.Run();
            host.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log.Error(ex, "startup");
            MessageBox.Show(ex.ToString(), "酷咔启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    private static IHost CreateHost(LogService log)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(log);
        builder.Services.AddDbContextFactory<KukaDbContext>(options => options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
        builder.Services.AddSingleton(_ => new KukaStore(AppPaths.DatabasePath));
        builder.Services.AddSingleton<FinanceExcelService>();
        builder.Services.AddSingleton<ExchangeExcelService>();
        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }
}
