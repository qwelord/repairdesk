using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RepairDesk.Core;

namespace RepairDesk.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<string, int> buildIds = [];

    private void ShowConfigurator()
    {
        PageSubtitle.Text = "Совместимость, стоимость и ориентиры производительности";
        var root = new Grid();
        root.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        root.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(22)
        });
        root.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        var selectionPanel = new StackPanel();
        selectionPanel.Children.Add(Ui.Text("Комплектующие", 21, true));
        selectionPanel.Children.Add(Ui.Muted("Полная сборка настольного ПК. Компоненты ноутбуков требуют отдельной проверки по модели устройства."));
        var selects = new Dictionary<string, ComboBox>();
        foreach (var category in new[] { "Процессор", "Материнская плата", "Видеокарта", "Память", "Накопитель", "Блок питания", "Корпус" })
        {
            var combo = Ui.Combo(parts.Where(part => part.Category == category), display: "Name");
            selects[category] = combo;
            if (buildIds.TryGetValue(category, out var id))
                combo.SelectedItem = parts.FirstOrDefault(part => part.Id == id);
            selectionPanel.Children.Add(Ui.Field(category, combo));
        }
        BuildSelection Current() => new(selects["Процессор"].SelectedItem as Part, selects["Материнская плата"].SelectedItem as Part, selects["Видеокарта"].SelectedItem as Part, selects["Память"].SelectedItem as Part, selects["Блок питания"].SelectedItem as Part, selects["Корпус"].SelectedItem as Part, selects["Накопитель"].SelectedItem as Part);
        var cost = Ui.Text("", 26, true);
        selectionPanel.Children.Add(cost);
        var preset = Ui.Button("Пример: Ryzen 5 / RTX 4060", () =>
        {
            foreach (var pair in new Dictionary<string, int> { { "Процессор", 2 }, { "Материнская плата", 5 }, { "Видеокарта", 7 }, { "Память", 11 }, { "Накопитель", 12 }, { "Блок питания", 14 }, { "Корпус", 16 } })
                selects[pair.Key].SelectedItem = parts.First(part => part.Id == pair.Value);
        });
        selectionPanel.Children.Add(preset);
        selectionPanel.Children.Add(Ui.AsyncButton("Сохранить предложение", async () =>
        {
            var build = Current();
            var check = Compatibility.Check(build);
            if (!check.IsComplete || check.HasErrors)
                throw new BusinessException("Выберите все компоненты и устраните несовместимости.");
            var dialog = new FormWindow(this, "Сохранение сборки", 560, 420);
            var name = Ui.Input("Игровой ПК");
            var client = Ui.Combo(clients, display: "Name");
            dialog.Fields.Children.Add(Ui.Field("Название", name));
            dialog.Fields.Children.Add(Ui.Field("Клиент (необязательно)", client));
            dialog.Submit("Сохранить", () => repository.SaveBuildAsync(name.Text, (client.SelectedItem as Client)?.Id, build));
            dialog.ShowDialog();
            await Task.CompletedTask;
        }));
        var order = Ui.AsyncButton("Оформить сборку в заказ", async () =>
        {
            var build = Current();
            var check = Compatibility.Check(build);
            if (!check.IsComplete || check.HasErrors)
                throw new BusinessException("Выберите все компоненты и устраните несовместимости.");
            var dialog = new FormWindow(this, "Заказ на сборку", 560, 470);
            var name = Ui.Input("Сборка игрового ПК");
            var client = Ui.Combo(clients, display: "Name");
            dialog.Fields.Children.Add(Ui.Field("Клиент", client));
            dialog.Fields.Children.Add(Ui.Field("Название устройства", name));
            dialog.Fields.Children.Add(Ui.Muted("Все детали выделятся со склада одной операцией. К стоимости добавится услуга «Сборка и проверка ПК»."));
            int? id = null;
            dialog.Submit("Создать заказ", async () => id = await repository.CreateBuildOrderAsync((client.SelectedItem as Client)?.Id ?? 0, build, name.Text));
            if (dialog.ShowDialog() == true && id != null)
            {
                page = "Заказы";
                await LoadPageAsync();
            }
        }, true);
        order.IsEnabled = repository.Actor.CanReceive;
        selectionPanel.Children.Add(order);
        var left = new ScrollViewer { Content = Ui.Surface(selectionPanel), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(left);
        var right = new DockPanel();
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        var resolution = Ui.Combo(new[] { "1920 × 1080", "2560 × 1440", "3840 × 2160" }, "1920 × 1080");
        var resultPanel = new StackPanel();
        var fpsPanel = new StackPanel();
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Совместимость", Content = new ScrollViewer { Content = resultPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        var fpsRoot = new DockPanel();
        var resolutionField = Ui.Field("Разрешение теста", resolution);
        DockPanel.SetDock(resolutionField, Dock.Top);
        fpsRoot.Children.Add(resolutionField);
        fpsRoot.Children.Add(new ScrollViewer { Content = fpsPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        tabs.Items.Add(new TabItem { Header = "Производительность", Content = fpsRoot });
        right.Children.Add(tabs);
        var generation = 0;
        async Task UpdateAsync()
        {
            var current = ++generation;
            var build = Current();
            cost.Text = Money.Format(build.Price);
            buildIds.Clear();
            foreach (var pair in selects)
                if (pair.Value.SelectedItem is Part part)
                    buildIds[pair.Key] = part.Id;
            resultPanel.Children.Clear();
            var result = Compatibility.Check(build);
            resultPanel.Children.Add(Ui.Text(result.HasErrors ? "Найдены несовместимости" : result.IsComplete ? "Основные параметры совместимы" : "Выберите комплектующие", 22, true));
            foreach (var finding in result.Findings)
            {
                var block = Ui.Stack(Ui.Text(finding.Title, 15, true), Ui.Muted(finding.Detail));
                var border = new Border { BorderThickness = new Thickness(3, 0, 0, 0), BorderBrush = new SolidColorBrush(finding.Level == FindingLevel.Error ? Color.FromRgb(181, 56, 41) : finding.Level == FindingLevel.Pass ? Color.FromRgb(57, 118, 89) : Color.FromRgb(178, 128, 45)), Padding = new Thickness(13, 4, 8, 4), Margin = new Thickness(0, 10, 0, 8), Child = block };
                resultPanel.Children.Add(border);
            }
            resultPanel.Children.Add(Ui.Text("Характеристики выбранных деталей", 17, true));
            foreach (var part in build.Parts)
            {
                resultPanel.Children.Add(Ui.Text($"{part.Name} · {part.PriceText} · {part.StockText}", 13, true));
                resultPanel.Children.Add(Ui.Muted(part.Notes));
                resultPanel.Children.Add(Ui.Button("Спецификация производителя", () => Ui.OpenUrl(part.SourceUrl)));
            }
            fpsPanel.Children.Clear();
            if (build.Gpu == null)
            {
                fpsPanel.Children.Add(Ui.Muted("Выберите видеокарту для поиска результатов тестов."));
                return;
            }
            fpsPanel.Children.Add(Ui.Muted("Загрузка локальных результатов…"));
            var width = resolution.SelectedIndex switch
            {
                1 => 2560,
                2 => 3840,
                _ => 1920
            };
            var benchmarks = await repository.BenchmarksAsync(build.Gpu.Id, width);
            var games = await repository.GamesAsync();
            if (current != generation)
                return;
            fpsPanel.Children.Clear();
            fpsPanel.Children.Add(Ui.Text("Ориентир по видеокарте", 21, true));
            fpsPanel.Children.Add(Ui.Muted("Это опубликованные измерения на других тестовых стендах, а не замер выбранного ПК. CPU, память, версия игры и драйверы влияют на результат. Автоматическая поправка на CPU не применяется."));
            if (result.HasErrors)
                fpsPanel.Children.Add(Ui.Text("Сборка несовместима. Результаты ниже не подтверждают ее работоспособность.", 14, true));
            foreach (var game in games)
            {
                var benchmark = benchmarks.FirstOrDefault(item => item.GameId == game.Id);
                var panel = Ui.Stack(Ui.Text(game.Name, 16, true));
                if (benchmark == null)
                    panel.Children.Add(Ui.Muted("Нет проверенного результата для этой видеокарты и разрешения."));
                else
                {
                    panel.Children.Add(Ui.Text($"{benchmark.FpsText} FPS", 27, true));
                    if (benchmark.LowFps is { } low)
                        panel.Children.Add(Ui.Muted($"1% low: {low:0.#} FPS"));
                    panel.Children.Add(new ProgressBar { Value = Math.Min(240, (double)benchmark.AverageFps), Maximum = 240, Height = 4, Foreground = Ui.Brush("AccentBrush"), Margin = new Thickness(0, 0, 0, 10) });
                    panel.Children.Add(Ui.Muted($"{benchmark.Resolution} · {benchmark.Preset}\nRT: {benchmark.RayTracing} · Масштабирование: {benchmark.Upscaling}\nСтенд: {benchmark.TestCpu}\n{benchmark.Notes}"));
                    panel.Children.Add(Ui.Button("Открыть источник теста", () => Ui.OpenUrl(benchmark.SourceUrl)));
                }
                panel.Margin = new Thickness(0, 14, 0, 8);
                fpsPanel.Children.Add(panel);
                fpsPanel.Children.Add(new Separator());
            }
        }
        foreach (var combo in selects.Values)
            combo.SelectionChanged += async (_, _) => { try { await UpdateAsync(); } catch (Exception error) { Ui.ShowError(error); } };
        resolution.SelectionChanged += async (_, _) => { try { await UpdateAsync(); } catch (Exception error) { Ui.ShowError(error); } };
        PageContent.Content = root;
        _ = UpdateAsync();
    }
}
