using System.Windows;
using System.Windows.Controls;
using RepairDesk.Core;

namespace RepairDesk.Desktop;

public partial class MainWindow
{
    private List<RepairOrder> orders = [];
    private DataGrid? ordersTable;
    private Border? orderDetail;

    private async Task ShowOrdersAsync(int? selectedId = null)
    {
        PageSubtitle.Text = "Прием техники, диагностика и ремонт";
        orders = await repository.OrdersAsync();
        var search = Ui.Input();
        search.Width = 260;
        search.ToolTip = "Заказ, модель, имя или телефон";
        search.Tag = "Поиск по заказам";
        var filter = Ui.Combo(new[] { "Активные", "Все заказы" }.Concat(OrderRules.Statuses), "Активные");
        filter.Width = 170;
        ordersTable = Ui.Table(("Заказ", "Number", 1), ("Устройство", "Device", 2.1), ("Статус", "Status", 1.4), ("Срок", "DueText", 0.8));
        ordersTable.SelectionChanged += async (_, _) =>
        {
            try
            {
                if (ordersTable.SelectedItem is RepairOrder order)
                    await RenderOrderAsync(order);
                else if (orderDetail != null)
                    orderDetail.Child = Ui.Stack(Ui.Text("Ничего не найдено", 22, true), Ui.Muted("Измените запрос или выберите другой статус."));
            }
            catch (Exception error) { Ui.ShowError(error); }
        };
        void Filter()
        {
            var text = search.Text.Trim();
            var status = (string?)filter.SelectedItem ?? "Активные";
            var filtered = orders.Where(order => (order.Number + order.Device + order.ClientName + order.Phone).Contains(text, StringComparison.OrdinalIgnoreCase));
            if (status == "Активные")
                filtered = filtered.Where(order => order.Status is not ("Выдан" or "Отменен"));
            else if (status != "Все заказы")
                filtered = filtered.Where(order => order.Status == status);
            var previous = (ordersTable.SelectedItem as RepairOrder)?.Id;
            var visible = filtered.ToList();
            ordersTable.ItemsSource = visible;
            ordersTable.SelectedItem = visible.FirstOrDefault(item => item.Id == previous) ?? visible.FirstOrDefault();
        }
        search.TextChanged += (_, _) => Filter();
        filter.SelectionChanged += (_, _) => Filter();
        Filter();
        var add = Ui.AsyncButton("Принять технику", NewOrderDialogAsync, true);
        add.IsEnabled = repository.Actor.CanReceive;
        var export = Ui.Button("Экспорт", () => Exports.SaveOrders(this, orders));
        var toolbar = Ui.Row(search, filter, add, export);
        toolbar.Margin = new Thickness(0, 0, 0, 18);
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        var split = new Grid();
        split.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1.1, GridUnitType.Star)
        });
        split.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(18)
        });
        split.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        split.Children.Add(ordersTable);
        orderDetail = Ui.Surface(Ui.Stack(Ui.Text("Выберите заказ", 22, true), Ui.Muted("Здесь появятся устройство, работы, запчасти и история ремонта.")), new Thickness(22));
        Grid.SetColumn(orderDetail, 2);
        split.Children.Add(orderDetail);
        root.Children.Add(split);
        PageContent.Content = root;
        ordersTable.SelectedItem = orders.FirstOrDefault(order => order.Id == selectedId) ?? (ordersTable.ItemsSource as IEnumerable<RepairOrder>)?.FirstOrDefault();
    }

    private async Task RenderOrderAsync(RepairOrder order)
    {
        var lines = await repository.LinesAsync(order.Id);
        var events = await repository.EventsAsync(order.Id);
        if (orderDetail == null || ordersTable?.SelectedItem is not RepairOrder selected || selected.Id != order.Id || selected.Version != order.Version)
            return;
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(order.Number, 24, true));
        panel.Children.Add(Ui.Muted($"{order.Status} · Принят {order.CreatedAt:dd.MM.yyyy}"));
        panel.Children.Add(Ui.Text(order.Device, 20, true));
        panel.Children.Add(Ui.Muted($"{order.DeviceType} · S/N: {(order.Serial.Length == 0 ? "не указан" : order.Serial)}"));
        panel.Children.Add(Ui.Text(order.ClientName, 15, true));
        panel.Children.Add(Ui.Muted(order.Phone));
        panel.Children.Add(Ui.Text("Неисправность", 14, true));
        panel.Children.Add(Ui.Text(order.Complaint));
        panel.Children.Add(Ui.Text("Состояние и комплектность", 14, true));
        panel.Children.Add(Ui.Muted(order.Condition));
        panel.Children.Add(Ui.Text("Диагностика", 14, true));
        panel.Children.Add(Ui.Text(order.Diagnosis.Length == 0 ? "Еще не проведена" : order.Diagnosis));
        panel.Children.Add(Ui.Muted($"Мастер: {order.Technician}\nСрок: {order.DueAt:dd.MM.yyyy}{(order.IsOverdue ? " — просрочен" : "")}"));
        var active = order.Status is not ("Выдан" or "Отменен");
        var edit = Ui.AsyncButton("Изменить заказ", () => EditOrderAsync(order), true);
        edit.IsEnabled = active;
        var print = Ui.Button("Квитанция", () => Exports.PrintOrder(this, order, lines));
        panel.Children.Add(Ui.Row(edit, print));
        panel.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 16) });
        panel.Children.Add(Ui.Text("Работы и запчасти", 18, true));
        if (lines.Count == 0)
            panel.Children.Add(Ui.Muted("Работы и детали еще не добавлены."));
        foreach (var line in lines)
        {
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
            var remove = Ui.AsyncButton("Убрать", async () => { if (!Ui.Confirm($"Убрать «{line.Name}»? Деталь вернется на склад.")) return; await repository.RemoveLineAsync(order, line); await ReloadOrderAsync(order.Id); });
            remove.IsEnabled = active && order.Status != "Готов";
            remove.Padding = new Thickness(7, 3, 7, 3);
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(Ui.Stack(Ui.Text(line.Name, 13, true), Ui.Muted($"{line.Quantity} × {Money.Format(line.UnitPrice)} = {line.TotalText}")));
            panel.Children.Add(row);
        }
        var addLine = Ui.AsyncButton("Добавить работу / деталь", () => AddLineDialogAsync(order));
        addLine.IsEnabled = active && order.Status != "Готов";
        panel.Children.Add(addLine);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 16, 0, 12) });
        panel.Children.Add(Ui.Text($"Итого {order.TotalText}", 24, true));
        panel.Children.Add(Ui.Muted($"Оплачено {Money.Format(order.Paid)} · Осталось {Money.Format(order.AmountDue)}"));
        var payment = Ui.AsyncButton("Оплата / возврат", () => PaymentDialogAsync(order));
        payment.IsEnabled = active && repository.Actor.CanReceive;
        panel.Children.Add(payment);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 16) });
        panel.Children.Add(Ui.Text("История заказа", 18, true));
        foreach (var entry in events)
            panel.Children.Add(Ui.Stack(Ui.Muted($"{entry.TimeText} · {entry.Author}", 12), Ui.Text(entry.Message, 13)));
        orderDetail.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task ReloadOrderAsync(int id)
    {
        await ShowOrdersAsync(id);
        parts = await repository.PartsAsync();
    }

    private Task NewOrderDialogAsync()
    {
        var dialog = new FormWindow(this, "Прием техники", 640, 800);
        var client = Ui.Combo(clients, display: "Name");
        var addClient = Ui.AsyncButton("Новый клиент", async () => { var id = await EditClientAsync(null); client.ItemsSource = clients; client.SelectedItem = clients.FirstOrDefault(item => item.Id == id); });
        dialog.Fields.Children.Add(Ui.Field("Клиент", client));
        dialog.Fields.Children.Add(addClient);
        var type = Ui.Combo(new[] { "ПК", "Ноутбук" }, "Ноутбук");
        var device = Ui.Input();
        var serial = Ui.Input();
        var complaint = Ui.Input("", true);
        var condition = Ui.Input("", true);
        var due = new DatePicker { SelectedDate = DateTime.Today.AddDays(3) };
        var tech = Ui.Combo(staff.Where(item => item.Role is "Мастер" or "Администратор"), display: "Name");
        dialog.Fields.Children.Add(Ui.Field("Тип устройства", type));
        dialog.Fields.Children.Add(Ui.Field("Модель устройства", device));
        dialog.Fields.Children.Add(Ui.Field("Серийный номер (если есть)", serial));
        dialog.Fields.Children.Add(Ui.Field("Неисправность со слов клиента", complaint));
        dialog.Fields.Children.Add(Ui.Field("Состояние, повреждения и комплектность", condition));
        dialog.Fields.Children.Add(Ui.Field("Плановый срок", due));
        dialog.Fields.Children.Add(Ui.Field("Мастер (можно назначить позже)", tech));
        int? created = null;
        dialog.Submit("Создать заказ", async () => { created = await repository.CreateOrderAsync(new((client.SelectedItem as Client)?.Id ?? 0, (string)type.SelectedItem, device.Text, serial.Text, complaint.Text, condition.Text, due.SelectedDate ?? DateTime.MinValue, (tech.SelectedItem as Staff)?.Id)); });
        dialog.ShowDialog();
        return created is { } id ? ReloadOrderAsync(id) : Task.CompletedTask;
    }

    private Task EditOrderAsync(RepairOrder order)
    {
        var dialog = new FormWindow(this, $"Заказ {order.Number}", 600, 720);
        var diagnosis = Ui.Input(order.Diagnosis, true);
        diagnosis.MinHeight = 160;
        var next = Ui.Combo(new[] { order.Status }.Concat(OrderRules.NextStatuses(order.Status)), order.Status);
        var due = new DatePicker { SelectedDate = order.DueAt, IsEnabled = repository.Actor.CanReceive };
        var tech = Ui.Combo(staff.Where(item => item.Role is "Мастер" or "Администратор"), staff.FirstOrDefault(item => item.Id == order.TechnicianId), "Name");
        tech.IsEnabled = repository.Actor.CanReceive;
        dialog.Fields.Children.Add(Ui.Field("Результат диагностики и выполненные действия", diagnosis));
        dialog.Fields.Children.Add(Ui.Field("Статус", next));
        dialog.Fields.Children.Add(Ui.Field("Мастер", tech));
        dialog.Fields.Children.Add(Ui.Field("Плановый срок", due));
        dialog.Fields.Children.Add(Ui.Muted("При отмене установленные детали вернутся на склад. Выдача доступна после полной оплаты."));
        dialog.Submit("Сохранить", () => repository.UpdateOrderAsync(order, diagnosis.Text, due.SelectedDate ?? order.DueAt, (tech.SelectedItem as Staff)?.Id, (string)next.SelectedItem));
        return dialog.ShowDialog() == true ? ReloadOrderAsync(order.Id) : Task.CompletedTask;
    }

    private async Task AddLineDialogAsync(RepairOrder order)
    {
        parts = await repository.PartsAsync();
        var services = await repository.ServicesAsync();
        var dialog = new FormWindow(this, "Работа или запчасть", 650, 500);
        var kind = Ui.Combo(new[] { "Работа", "Запчасть" }, "Работа");
        var item = new ComboBox { ItemsSource = services };
        var quantity = Ui.Input("1");
        kind.SelectionChanged += (_, _) => { item.SelectedItem = null; item.ItemsSource = (string)kind.SelectedItem == "Работа" ? services : parts.Where(part => part.Stock > 0).ToList(); };
        dialog.Fields.Children.Add(Ui.Field("Тип позиции", kind));
        dialog.Fields.Children.Add(Ui.Field("Позиция", item));
        dialog.Fields.Children.Add(Ui.Field("Количество", quantity));
        dialog.Fields.Children.Add(Ui.Muted("Запчасть списывается со склада при добавлении в заказ. Цена фиксируется в заказе."));
        dialog.Submit("Добавить", async () => { if (!int.TryParse(quantity.Text, out var count)) throw new BusinessException("Введите целое количество."); await repository.AddLineAsync(order, (item.SelectedItem as Part)?.Id, item.SelectedItem as ServiceItem, count); });
        if (dialog.ShowDialog() == true)
            await ReloadOrderAsync(order.Id);
    }

    private Task PaymentDialogAsync(RepairOrder order)
    {
        var dialog = new FormWindow(this, "Оплата заказа", 500, 420);
        var amount = Ui.Input((order.Total - order.Paid).ToString(System.Globalization.CultureInfo.GetCultureInfo("ru-RU")));
        dialog.Fields.Children.Add(Ui.Text($"Стоимость {order.TotalText}", 20, true));
        dialog.Fields.Children.Add(Ui.Muted($"Внесено {Money.Format(order.Paid)}"));
        dialog.Fields.Children.Add(Ui.Field("Сумма, ₽ (для возврата — со знаком минус)", amount));
        dialog.Submit("Записать оплату", () => repository.PayAsync(order, Amount(amount)));
        return dialog.ShowDialog() == true ? ReloadOrderAsync(order.Id) : Task.CompletedTask;
    }
}
