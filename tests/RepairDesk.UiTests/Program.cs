using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RepairDesk.Data;
using RepairDesk.Desktop;

namespace RepairDesk.UiTests;

public static class Program
{
    private static int exitCode;
    [STAThread]
    public static int Main()
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/RepairDesk;component/Theme.xaml") });
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            try
            {
                await VerifyAsync();
            }
            catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
        return exitCode;
    }

    private static async Task VerifyAsync()
    {
        var folder = Environment.GetEnvironmentVariable("REPAIRDESK_TEST_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "RepairDesk-QA");
        Directory.CreateDirectory(folder);
        if (Environment.GetEnvironmentVariable("REPAIRDESK_PORTABLE_ROOT") is { } portableRoot)
        {
            var portable = new PortableServer(portableRoot, Path.Combine(folder, "portable-fresh-" + Guid.NewGuid().ToString("N")));
            try
            {
                var portableSettings = await portable.StartAsync();
                var portableRepo = new Repository(portableSettings);
                await portableRepo.TestConnectionAsync();
                await portableRepo.LoginAsync("admin", "RepairDesk2026!");
                if ((await portableRepo.OrdersAsync()).Count != 5)
                    throw new Exception("Portable fresh database not initialized");
                Console.WriteLine("PASS: Portable MariaDB initializes without Windows service");
                portable.Stop();
                portableSettings = await portable.StartAsync();
                await new Repository(portableSettings).TestConnectionAsync();
                Console.WriteLine("PASS: Portable database survives restart");
            }
            finally { portable.Stop(); }
        }
        var repo = new Repository(new()
        {
            Port = uint.Parse(Environment.GetEnvironmentVariable("REPAIRDESK_TEST_PORT") ?? "33367"),
            User = "root",
            Password = Environment.GetEnvironmentVariable("REPAIRDESK_TEST_PASSWORD") ?? throw new Exception("Set test DB password")
        });
        await repo.LoginAsync("admin", "RepairDesk2026!");
        var window = new MainWindow(repo);
        window.Show();
        await Task.Delay(700);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        async Task Page(string name, string file)
        {
            typeof(MainWindow).GetField("page", flags)!.SetValue(window, name);
            await (Task)typeof(MainWindow).GetMethod("LoadPageAsync", flags)!.Invoke(window, null)!;
            await Task.Delay(200);
            window.UpdateLayout();
            var notice = (Border)window.FindName("Notice");
            if (notice.Visibility == Visibility.Visible)
                throw new Exception("Page failed: " + name + " " + ((TextBlock)window.FindName("NoticeText")).Text);
            Capture(window, Path.Combine(folder, file + ".png"));
            Console.WriteLine("PASS: Render " + name);
        }
        await Page("Заказы", "orders");
        void Modal(string filename, Action open)
        {
            var captured = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(item => (item is FormWindow || item.Title.StartsWith("Квитанция")) && item.IsVisible);
                if (dialog == null)
                    return;
                timer.Stop();
                dialog.UpdateLayout();
                Capture(dialog, Path.Combine(folder, filename + ".png"));
                captured = true;
                dialog.Close();
            };
            timer.Start();
            open();
            timer.Stop();
            if (!captured)
                throw new Exception("Dialog not captured: " + filename);
            Console.WriteLine("PASS: Dialog " + filename);
        }
        Modal("new-order", () => typeof(MainWindow).GetMethod("NewOrderDialogAsync", flags)!.Invoke(window, null));
        Modal("new-client", () => typeof(MainWindow).GetMethod("EditClientAsync", flags)!.Invoke(window, new object?[] { null }));
        var editable = (await repo.OrdersAsync()).First(item => item.Status == "В ремонте");
        Modal("edit-order", () => typeof(MainWindow).GetMethod("EditOrderAsync", flags)!.Invoke(window, new object[] { editable }));
        Modal("payment", () => typeof(MainWindow).GetMethod("PaymentDialogAsync", flags)!.Invoke(window, new object[] { editable }));
        Modal("part", () => typeof(MainWindow).GetMethod("EditPartAsync", flags)!.Invoke(window, new object?[] { null }));
        var printable = (await repo.OrdersAsync()).First();
        var lines = await repo.LinesAsync(printable.Id);
        Modal("receipt", () => Exports.PrintOrder(window, printable, lines));
        var search = Descendants<TextBox>((ContentControl)window.FindName("PageContent")).Single(input => Equals(input.Tag, "Поиск по заказам"));
        search.Text = "no-matching-order-qa";
        await Task.Delay(150);
        if (!Descendants<TextBlock>(window).Any(block => block.Text == "Ничего не найдено"))
            throw new Exception("Empty search leaves stale order details");
        search.Text = "";
        await Task.Delay(200);
        Console.WriteLine("PASS: Search clears stale order details");
        await Page("Клиенты", "clients");
        await Page("Запчасти и склад", "stock");
        await Page("Конфигуратор ПК", "configurator-empty");
        var content = (ContentControl)window.FindName("PageContent");
        var preset = Descendants<Button>(content).Single(button => Equals(button.Content, "Пример: Ryzen 5 / RTX 4060"));
        preset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(600);
        window.UpdateLayout();
        Capture(window, Path.Combine(folder, "configurator.png"));
        var tabs = Descendants<TabControl>(content).Single();
        tabs.SelectedIndex = 1;
        await Task.Delay(100);
        window.UpdateLayout();
        Capture(window, Path.Combine(folder, "fps.png"));
        if (!Descendants<TextBlock>(content).Any(block => block.Text.Contains("117.3 FPS")))
            throw new Exception("FPS results not rendered");
        Console.WriteLine("PASS: Published benchmark visible");
        await Page("Сохраненные сборки", "builds");
        await Page("Настройки", "settings");
        await Page("Заказы", "orders-before-theme");
        ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(400);
        window.UpdateLayout();
        Capture(window, Path.Combine(folder, "orders-dark.png"));
        Console.WriteLine("PASS: Dark theme render");
        window.Width = 1100;
        window.Height = 720;
        await Task.Delay(100);
        window.UpdateLayout();
        Capture(window, Path.Combine(folder, "orders-minimum.png"));
        Exports.WriteXlsx(Path.Combine(folder, "orders.xlsx"), new List<string[]> { new[] { "Заказ", "Клиент", "Стоимость" }, new[] { "RD-00001", "=HYPERLINK(\"https://example.test\")", "1800.50" } }, new HashSet<int> { 2 });
        using (var archive = System.IO.Compression.ZipFile.OpenRead(Path.Combine(folder, "orders.xlsx")))
        using (var input = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open())
        {
            var document = System.Xml.Linq.XDocument.Load(input);
            System.Xml.Linq.XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            if (document.Descendants(sheet + "f").Any())
                throw new Exception("User text exported as formula");
            if (!document.Descendants(sheet + "c").Any(cell => (string?)cell.Attribute("t") == "n" && cell.Element(sheet + "v")?.Value == "1800.50"))
                throw new Exception("Amount not exported as number");
        }
        Console.WriteLine("PASS: Excel export generated");
        window.Close();
        Console.WriteLine("RESULT: UI checks passed");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);
            if (child is T match)
                yield return match;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }
    private static void Capture(FrameworkElement element, string path)
    {
        var background = Ui.Brush("CanvasBrush");
        if (element is Window window && window.Content is FrameworkElement content)
        {
            background = window.Background;
            element = content;
        }
        var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(background, null, bounds);
            drawing.DrawRectangle(new VisualBrush(element), null, bounds);
        }
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }

}
