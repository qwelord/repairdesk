using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RepairDesk.Core;

namespace RepairDesk.Desktop;

public static class Ui
{
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
    public static TextBlock Text(string text, double size = 14, bool bold = false) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8) };
    public static TextBlock Muted(string text, double size = 13)
    {
        var control = Text(text, size);
        control.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        return control;
    }
    public static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children)
            panel.Children.Add(child);
        return panel;
    }
    public static StackPanel Row(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is FrameworkElement element)
                element.Margin = new Thickness(0, 0, 8, 0);
            panel.Children.Add(child);
        }
        return panel;
    }
    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text };
        if (primary)
            button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        button.Click += (_, _) => action();
        return button;
    }
    public static Button AsyncButton(string text, Func<Task> action, bool primary = false)
    {
        var button = new Button { Content = text };
        if (primary)
            button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (Exception error) { ShowError(error); } finally { button.IsEnabled = true; } };
        return button;
    }
    public static TextBox Input(string value = "", bool multiline = false) => new() { Text = value, MinHeight = multiline ? 90 : 38, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden, MaxLength = 4000 };
    public static ComboBox Combo<T>(IEnumerable<T> values, T? selected = default, string? display = null)
    {
        var combo = new ComboBox { ItemsSource = values.ToList() };
        if (display != null)
            combo.DisplayMemberPath = display;
        if (selected != null)
            combo.SelectedItem = selected;
        return combo;
    }
    public static StackPanel Field(string label, FrameworkElement control)
    {
        control.Margin = new Thickness(0, 0, 0, 16);
        return Stack(Muted(label), control);
    }
    public static Border Surface(UIElement child, Thickness? padding = null) => new() { Background = Brush("SurfaceBrush"), BorderBrush = Brush("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = padding ?? new Thickness(20), Child = child };
    public static DataGrid Table(params (string Header, string Path, double Width)[] columns)
    {
        var grid = new DataGrid();
        foreach (var column in columns)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
            textStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(column.Path)));
            var minimum = column.Path switch
            {
                "Number" => 105,
                "Device" => 200,
                "Status" => 140,
                "DueText" => 85,
                "Name" => 180,
                _ => 85
            };
            grid.Columns.Add(new DataGridTextColumn { Header = column.Header, Binding = new Binding(column.Path), ElementStyle = textStyle, Width = new DataGridLength(column.Width, DataGridLengthUnitType.Star), MinWidth = minimum });
        }
        return grid;
    }
    public static void ShowError(Exception error)
    {
        if (error is BusinessException)
            MessageBox.Show(error.Message, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Information);
        else
        {
            Diagnostics.Write(error);
            var message = error is MySqlConnector.MySqlException ? "Нет доступа к базе или данные не удалось сохранить. Проверьте запуск MySQL и настройки подключения. Действие не подтверждено." : "Не удалось выполнить действие. Обновите данные и повторите. Подробности сохранены в журнале.";
            MessageBox.Show(message, "RepairDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    public static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    public static bool Confirm(string text) => MessageBox.Show(text, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}

public sealed class FormWindow : Window
{
    public StackPanel Fields { get; } = new();
    public TextBlock Error { get; } = new() { Foreground = new SolidColorBrush(Color.FromRgb(173, 56, 35)), Margin = new Thickness(0, 12, 0, 12) };
    public StackPanel Actions { get; } = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    public FormWindow(Window owner, string title, double width = 570, double height = 650)
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Owner = owner;
        Title = title;
        Width = width;
        Height = height;
        MinWidth = 420;
        MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(26) };
        var heading = Ui.Text(title, 24, true);
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);
        var footer = Ui.Stack(Error, Actions);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = Fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 15, 0, 0) });
        Content = root;
        Actions.Children.Add(Ui.Button("Отмена", () => Close()));
    }
    public void Submit(string title, Func<Task> action, bool close = true)
    {
        var button = Ui.AsyncButton(title, async () =>
        {
            Error.Text = "";
            try
            {
                await action();
                if (close)
                {
                    DialogResult = true;
                }
            }
            catch (Exception error) { if (error is BusinessException) Error.Text = error.Message; else { Diagnostics.Write(error); Error.Text = "Не удалось выполнить действие. Проверьте подключение и введенные данные."; } }
        }, true);
        button.Margin = new Thickness(8, 0, 0, 0);
        Actions.Children.Add(button);
        button.IsDefault = true;
    }
}
