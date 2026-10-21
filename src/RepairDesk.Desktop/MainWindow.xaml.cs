using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace RepairDesk.Desktop;

public partial class MainWindow : Window
{
    private readonly Uri address;
    private WebView2? browser;
    public MainWindow(Uri address)
    {
        this.address = address;
        InitializeComponent();
        Loaded += OpenBrowser;
        Closed += (_, _) => browser?.Dispose();
    }

    private async void OpenBrowser(object sender, RoutedEventArgs args)
    {
        try
        {
            var fixedRuntime = Path.Combine(AppContext.BaseDirectory, "webview2");
            var environment = await CoreWebView2Environment.CreateAsync(Directory.Exists(fixedRuntime) ? fixedRuntime : null, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepairDesk", "studio", "browser"));
            browser = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(243, 244, 240) };
            BrowserHost.Children.Add(browser);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.Settings.IsWebMessageEnabled = false;
            browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            browser.CoreWebView2.NewWindowRequested += (_, request) => { request.Handled = true; OpenExternal(request.Uri); };
            browser.CoreWebView2.NavigationStarting += (_, request) =>
            {
                if (Uri.TryCreate(request.Uri, UriKind.Absolute, out var target) && target.GetLeftPart(UriPartial.Authority) != address.GetLeftPart(UriPartial.Authority))
                {
                    request.Cancel = true;
                    OpenExternal(request.Uri);
                }
            };
            browser.Source = address;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            BrowserHost.Children.Add(new System.Windows.Controls.TextBlock { Margin = new Thickness(36), TextWrapping = TextWrapping.Wrap, Text = "Не найден WebView2. Используйте комплект с папкой webview2 или браузер.\n\nПриложение открыто в браузере. Не закрывайте это окно до завершения работы." });
            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception error) { Diagnostics.Write(error); MessageBox.Show("Не удалось открыть интерфейс. Подробности записаны в журнал RepairDesk.", "RepairDesk Studio"); }
    }

    private static void OpenExternal(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var target) && target.Scheme == "https")
            Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
    }
}
