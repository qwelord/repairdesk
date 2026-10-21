using System.Windows;
using System.Windows.Controls;
using RepairDesk.Core;

namespace RepairDesk.Desktop;

public partial class MainWindow
{
    private async Task ShowStockAsync()
    {
        PageSubtitle.Text = "Каталог комплектующих и движение остатков";
        var root = new DockPanel();
        var search = Ui.Input();
        search.Width = 280;
        search.ToolTip = "Название, производитель или категория";
        search.Tag = "Поиск комплектующей";
        var category = Ui.Combo(new[] { "Все категории" }.Concat(parts.Select(part => part.Category).Distinct()), "Все категории");
        category.Width = 200;
        var table = Ui.Table(("Комплектующая", "Name", 3), ("Категория", "Category", 1.4), ("Цена", "PriceText", 1), ("Остаток", "StockText", 1.1), ("Минимум", "MinStock", 0.7));
        void Filter() => table.ItemsSource = parts.Where(part => (part.Name + part.Brand + part.Category).Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) && ((string?)category.SelectedItem == "Все категории" || part.Category == (string?)category.SelectedItem)).ToList();
        search.TextChanged += (_, _) => Filter();
        category.SelectionChanged += (_, _) => Filter();
        Filter();
        Part Selected() => table.SelectedItem as Part ?? throw new BusinessException("Выберите комплектующую.");
        var move = Ui.AsyncButton("Приход / списание", async () =>
        {
            var part = Selected();
            var dialog = new FormWindow(this, "Движение запчасти", 560, 480);
            var quantity = Ui.Input("1");
            var reason = Ui.Input();
            dialog.Fields.Children.Add(Ui.Text(part.Name, 19, true));
            dialog.Fields.Children.Add(Ui.Muted($"Сейчас на складе: {part.Stock} шт."));
            dialog.Fields.Children.Add(Ui.Field("Количество (отрицательное для списания)", quantity));
            dialog.Fields.Children.Add(Ui.Field("Основание", reason));
            dialog.Submit("Записать", () => { if (!int.TryParse(quantity.Text, out var count)) throw new BusinessException("Введите целое число."); return repository.AdjustStockAsync(part.Id, count, reason.Text); });
            if (dialog.ShowDialog() == true)
            {
                parts = await repository.PartsAsync();
                await ShowStockAsync();
            }
        }, true);
        move.IsEnabled = repository.Actor.CanReceive;
        var price = Ui.AsyncButton("Цена и минимум", async () =>
        {
            var part = Selected();
            var dialog = new FormWindow(this, "Параметры склада", 540, 420);
            var amount = Ui.Input(part.Price.ToString());
            var minimum = Ui.Input(part.MinStock.ToString());
            dialog.Fields.Children.Add(Ui.Text(part.Name, 17, true));
            dialog.Fields.Children.Add(Ui.Field("Цена, ₽", amount));
            dialog.Fields.Children.Add(Ui.Field("Минимальный остаток", minimum));
            dialog.Submit("Сохранить", () => { if (!int.TryParse(minimum.Text, out var count)) throw new BusinessException("Введите целый минимум."); return repository.UpdatePartAsync(part.Id, Amount(amount), count); });
            if (dialog.ShowDialog() == true)
            {
                parts = await repository.PartsAsync();
                await ShowStockAsync();
            }
        });
        price.IsEnabled = repository.Actor.CanManage;
        var catalogEdit = Ui.AsyncButton("Карточка", async () => await EditPartAsync(Selected()));
        catalogEdit.IsEnabled = repository.Actor.CanManage;
        var catalogAdd = Ui.AsyncButton("Новая деталь", async () => await EditPartAsync(null));
        catalogAdd.IsEnabled = repository.Actor.CanManage;
        var toolbar = Ui.Row(search, category, move);
        toolbar.Margin = new Thickness(0, 0, 0, 8);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        var tools = Ui.Row(catalogAdd, catalogEdit, price);
        tools.Margin = new Thickness(0, 0, 0, 16);
        DockPanel.SetDock(tools, Dock.Top);
        root.Children.Add(tools);
        var tabs = new TabControl();
        var catalog = new TabItem { Header = "Каталог", Content = table };
        tabs.Items.Add(catalog);
        var movements = Ui.Table(("Дата", "CreatedAt", 1.3), ("Комплектующая", "Part", 2.5), ("Кол-во", "Quantity", 0.7), ("Основание", "Reason", 2), ("Сотрудник", "Author", 1.5));
        movements.ItemsSource = await repository.StockMovesAsync();
        tabs.Items.Add(new TabItem { Header = "История движений", Content = movements });
        var details = Ui.Text("Выберите комплектующую, чтобы увидеть характеристики и примечания.");
        details.Margin = new Thickness(0, 12, 0, 0);
        DockPanel.SetDock(details, Dock.Bottom);
        root.Children.Add(details);
        table.SelectionChanged += (_, _) => { if (table.SelectedItem is Part part) details.Text = $"{part.Name}\n{part.Notes}\n{(part.Stock <= part.MinStock ? "Остаток достиг минимального уровня. " : "")}Источник характеристик: {part.SourceUrl}"; };
        root.Children.Add(tabs);
        PageContent.Content = root;
    }

    private async Task EditPartAsync(Part? part)
    {
        var dialog = new FormWindow(this, part == null ? "Новая комплектующая" : "Карточка комплектующей", 640, 820);
        var categories = new[] { "Процессор", "Материнская плата", "Видеокарта", "Память", "Накопитель", "Блок питания", "Корпус" };
        var category = Ui.Combo(categories, part?.Category ?? "Процессор");
        dialog.Fields.Children.Add(Ui.Field("Категория", category));
        var fields = new Dictionary<string, TextBox>();
        void Field(string label, string value)
        {
            var input = Ui.Input(value);
            fields[label] = input;
            dialog.Fields.Children.Add(Ui.Field(label, input));
        }
        Field("Название", part?.Name ?? "");
        Field("Производитель", part?.Brand ?? "");
        Field("Цена, ₽", part?.Price.ToString() ?? "0");
        Field("Минимальный остаток", part?.MinStock.ToString() ?? "1");
        Field("Сокет (например AM4)", part?.Socket ?? "");
        Field("Память (DDR4 или DDR5)", part?.MemoryType ?? "");
        Field("Мощность, Вт", part?.Wattage.ToString() ?? "0");
        Field("Объем, ГБ", part?.Capacity.ToString() ?? "0");
        Field("Длина GPU, мм", part?.LengthMm.ToString() ?? "0");
        Field("Форм-фактор платы", part?.FormFactor ?? "");
        Field("Форматы корпуса через |", part?.SupportedForms ?? "");
        Field("Лимит длины GPU корпуса, мм", part?.MaxGpuMm.ToString() ?? "0");
        Field("Максимум RAM платы, ГБ", part?.MaxMemoryGb.ToString() ?? "0");
        Field("Поддерживаемые CPU, точные названия через |", part?.CpuSupport ?? "");
        Field("Разъемы PCIe (например 1×8-pin)", part?.PowerConnectors ?? "");
        Field("HTTPS-ссылка производителя", part?.SourceUrl ?? "");
        Field("Примечание", part?.Notes ?? "");
        dialog.Fields.Children.Add(Ui.Muted("Параметры, не относящиеся к категории, оставьте пустыми или нулевыми. Начальный остаток новой детали — 0; поступление оформляется отдельно."));
        string Value(string key) => fields[key].Text.Trim();
        int Number(string key) => int.TryParse(Value(key), out var value) ? value : throw new BusinessException("Ожидается целое число: " + key);
        dialog.Submit("Сохранить", () => repository.SavePartAsync(new(part?.Id ?? 0, (string)category.SelectedItem, Value("Производитель"), Value("Название"), Amount(fields["Цена, ₽"]), part?.Stock ?? 0, Number("Минимальный остаток"), Value("Сокет (например AM4)"), Value("Память (DDR4 или DDR5)"), Number("Мощность, Вт"), Number("Объем, ГБ"), Number("Длина GPU, мм"), Value("Форм-фактор платы"), Value("Форматы корпуса через |"), Number("Лимит длины GPU корпуса, мм"), Number("Максимум RAM платы, ГБ"), Value("Поддерживаемые CPU, точные названия через |"), Value("Разъемы PCIe (например 1×8-pin)"), Value("HTTPS-ссылка производителя"), Value("Примечание")), part == null));
        if (dialog.ShowDialog() == true)
        {
            parts = await repository.PartsAsync();
            await ShowStockAsync();
        }
    }

    private async Task ShowBuildsAsync()
    {
        PageSubtitle.Text = "Согласованные конфигурации и сохраненные предложения";
        var builds = await repository.BuildsAsync();
        var panel = new DockPanel();
        var table = Ui.Table(("Название", "Name", 1.3), ("Клиент", "Client", 1.2), ("Комплектующие", "Parts", 3.5), ("Цена, ₽", "Price", 1));
        table.ItemsSource = builds;
        var open = Ui.AsyncButton("Открыть в конфигураторе", async () =>
        {
            if (table.SelectedItem is not SavedBuild saved)
                throw new BusinessException("Выберите сохраненную сборку.");
            var ids = await repository.BuildPartIdsAsync(saved.Id);
            buildIds.Clear();
            foreach (var part in parts.Where(part => ids.Contains(part.Id)))
                buildIds[part.Category] = part.Id;
            page = "Конфигуратор ПК";
            await LoadPageAsync();
        }, true);
        open.Margin = new Thickness(0, 0, 0, 14);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(open, Dock.Top);
        panel.Children.Add(open);
        var note = Ui.Muted("Сохранение предложения не резервирует склад. Резервирование выполняется при создании заказа из конфигуратора.");
        note.Margin = new Thickness(0, 0, 0, 18);
        DockPanel.SetDock(note, Dock.Top);
        panel.Children.Add(note);
        if (builds.Count == 0)
        {
            panel.Children.Add(Ui.Surface(Ui.Stack(Ui.Text("Сохраненных сборок пока нет", 22, true), Ui.Muted("Откройте конфигуратор, подберите комплектующие и сохраните предложение клиенту."))));
        }
        else
            panel.Children.Add(table);
        PageContent.Content = panel;
    }
}
