using System.IO;
using System.Windows;
using System.Windows.Threading;
using RepairDesk.Data;
using RepairDesk.Api;
using Microsoft.AspNetCore.Builder;

namespace RepairDesk.Desktop;

public partial class App : Application
{
    private Mutex? instance;
    private PortableServer? portableServer;
    private WebApplication? api;
    internal static DatabaseSettings? PortableSettings
    {
        get; private set;
    }
    protected override async void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);
        DispatcherUnhandledException += OnUnhandled;
        instance = new Mutex(true, "Local\\RepairDesk.Studio", out var first);
        if (!first)
        {
            MessageBox.Show("RepairDesk уже запущен. Откройте его окно на панели задач.", "RepairDesk");
            Shutdown();
            return;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (PortableServer.Available)
        {
            var loading = new Window { Title = "RepairDesk", Width = 500, Height = 210, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = new System.Windows.Controls.TextBlock { Text = "Подготовка локальной базы данных…\nПервый запуск может занять немного больше времени.", Margin = new Thickness(30), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } };
            loading.Show();
            try
            {
                portableServer = new PortableServer(stateFolder: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepairDesk", "studio-v2"));
                PortableSettings = await portableServer.StartAsync();
            }
            catch (Exception error) { Diagnostics.Write(error); MessageBox.Show(error is RepairDesk.Core.BusinessException ? error.Message : "Не удалось подготовить локальную базу. Подробности сохранены в журнале RepairDesk.", "RepairDesk", MessageBoxButton.OK, MessageBoxImage.Error); loading.Close(); Shutdown(1); return; }
            loading.Close();
        }
        try
        {
            var settings = PortableSettings ?? DatabaseSettings.Load();
            await new Repository(settings).TestConnectionAsync();
            api = ApiHost.Create(settings, Path.Combine(AppContext.BaseDirectory, "wwwroot"));
            await api.StartAsync();
        }
        catch (Exception error)
        {
            Diagnostics.Write(error);
            MessageBox.Show("Не удалось запустить локальный API. Используйте полный переносной комплект или проверьте connection.local.json. Подробности в журнале RepairDesk.", "RepairDesk Studio");
            Shutdown(1);
            return;
        }
        var window = new MainWindow(new Uri(api.Urls.Single()));
        window.Closed += OnWindowClosed;
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs args)
    {
        portableServer?.Stop();
        instance?.Dispose();
        base.OnExit(args);
    }

    private async void OnWindowClosed(object? sender, EventArgs args)
    {
        try
        {
            if (api != null)
            {
                await api.StopAsync();
                await api.DisposeAsync();
                api = null;
            }
            await Task.Run(() => portableServer?.Stop());
            portableServer = null;
        }
        catch (Exception error) { Diagnostics.Write(error); }
        finally { Shutdown(); }
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Diagnostics.Write(args.Exception);
        MessageBox.Show("Не удалось выполнить действие. Подробности записаны в журнал. Повторите после обновления данных.", "RepairDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true;
    }
}

public static class Diagnostics
{
    public static void Write(Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepairDesk", "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, DateTime.Today.ToString("yyyy-MM-dd") + ".log"), $"{DateTime.Now:O} {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
