using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RepairDesk.Core;
using RepairDesk.Data;

namespace RepairDesk.Desktop;

public partial class MainWindow : Window
{
    private Repository repository = new(new DatabaseSettings());
    private string page = "Заказы";
    private readonly List<Button> navigationButtons = [];
    private bool busy;
    private bool dark;
    private List<Part> parts = [];
    private List<Client> clients = [];
    private List<Staff> staff = [];

    public MainWindow() : this(null) { }

    internal MainWindow(Repository? initializedRepository)
    {
        InitializeComponent();
        foreach (var name in new[] { "Заказы", "Клиенты", "Запчасти и склад", "Конфигуратор ПК", "Сохраненные сборки", "Настройки" })
        {
            var button = Ui.AsyncButton(name, async () => { if (busy) return; page = name; await LoadPageAsync(); });
            button.SetResourceReference(StyleProperty, "NavButton");
            navigationButtons.Add(button);
            Navigation.Children.Add(button);
        }
        Loaded += async (_, _) =>
        {
            if (initializedRepository == null)
                await BeginSessionAsync();
            else
            {
                repository = initializedRepository;
                StaffLabel.Text = repository.Actor.Name;
                RoleLabel.Text = repository.Actor.Role;
                await LoadPageAsync();
            }
        };
    }

    private async Task BeginSessionAsync()
    {
        try
        {
            repository = new Repository(App.PortableSettings ?? DatabaseSettings.Load());
        }
        catch (Exception error) { Diagnostics.Write(error); repository = new Repository(new DatabaseSettings()); }
        Navigation.IsEnabled = false;
        LogoutButton.IsEnabled = false;
        if (App.PortableSettings == null && !System.IO.File.Exists(DatabaseSettings.SettingsPath) && !System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "connection.local.json")))
            ShowConnectionDialog();
        var signedIn = false;
        while (!signedIn)
        {
            var dialog = new FormWindow(this, "Вход в RepairDesk", 480, 540);
            dialog.Fields.Children.Add(Ui.Muted("Учетная запись сотрудника сервисного центра", 14));
            var login = Ui.Input();
            login.MaxLength = 80;
            var password = new PasswordBox();
            dialog.Fields.Children.Add(Ui.Field("Логин", login));
            dialog.Fields.Children.Add(Ui.Field("Пароль", password));
            if (App.PortableSettings == null)
                dialog.Fields.Children.Add(Ui.Button("Настроить подключение", () => ShowConnectionDialog()));
            else
                dialog.Fields.Children.Add(Ui.Muted("Локальная база подключена. Учебные логины: admin, reception, master. Начальный пароль: RepairDesk2026!"));
            dialog.Submit("Войти", async () => { await repository.TestConnectionAsync(); await repository.LoginAsync(login.Text.Trim(), password.Password); signedIn = true; });
            dialog.Loaded += (_, _) => login.Focus();
            if (dialog.ShowDialog() != true)
            {
                Close();
                return;
            }
        }
        StaffLabel.Text = repository.Actor.Name;
        RoleLabel.Text = repository.Actor.Role;
        Navigation.IsEnabled = true;
        LogoutButton.IsEnabled = true;
        page = "Заказы";
        await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        if (busy)
            return;
        busy = true;
        Navigation.IsEnabled = false;
        Notice.Visibility = Visibility.Collapsed;
        try
        {
            parts = await repository.PartsAsync();
            clients = await repository.ClientsAsync();
            staff = await repository.StaffAsync();
            PageTitle.Text = page;
            foreach (var button in navigationButtons)
                button.Background = (string)button.Content == page ? new SolidColorBrush(Color.FromRgb(66, 77, 81)) : Brushes.Transparent;
            switch (page)
            {
                case "Заказы":
                    await ShowOrdersAsync();
                    break;
                case "Клиенты":
                    ShowClients();
                    break;
                case "Запчасти и склад":
                    await ShowStockAsync();
                    break;
                case "Конфигуратор ПК":
                    ShowConfigurator();
                    break;
                case "Сохраненные сборки":
                    await ShowBuildsAsync();
                    break;
                case "Настройки":
                    ShowSettings();
                    break;
            }
            StatusLabel.Text = $"{repository.Settings.Server}:{repository.Settings.Port} / {repository.Settings.Database}     Обновлено {DateTime.Now:HH:mm}";
        }
        catch (Exception error) { Diagnostics.Write(error); NoticeText.Text = error is BusinessException ? error.Message : "Не удалось загрузить данные. Проверьте MySQL и подключение в настройках, затем нажмите «Обновить»."; Notice.Visibility = Visibility.Visible; }
        finally { busy = false; Navigation.IsEnabled = true; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs args) => await LoadPageAsync();
    private async void Logout_Click(object sender, RoutedEventArgs args)
    {
        if (busy)
            return;
        repository.Logout();
        PageContent.Content = null;
        await BeginSessionAsync();
    }

    private void ShowConnectionDialog()
    {
        var settings = repository.Settings;
        var dialog = new FormWindow(this, "Подключение к базе", 570, 760);
        dialog.Fields.Children.Add(Ui.Muted("MySQL 8.0+ или MariaDB 10.6+. Сервер запускается отдельно; Docker не используется."));
        var host = Ui.Input(settings.Server);
        var port = Ui.Input(settings.Port.ToString());
        var database = Ui.Input(settings.Database);
        var user = Ui.Input(settings.User);
        var password = new PasswordBox { Password = settings.Password };
        dialog.Fields.Children.Add(Ui.Field("Адрес сервера", host));
        dialog.Fields.Children.Add(Ui.Field("Порт", port));
        dialog.Fields.Children.Add(Ui.Field("База данных", database));
        dialog.Fields.Children.Add(Ui.Field("Пользователь MySQL", user));
        dialog.Fields.Children.Add(Ui.Field("Пароль MySQL", password));
        DatabaseSettings Read()
        {
            if (!uint.TryParse(port.Text, out var number) || number is < 1 or > 65535)
                throw new BusinessException("Порт должен быть от 1 до 65535.");
            if (string.IsNullOrWhiteSpace(host.Text) || string.IsNullOrWhiteSpace(user.Text))
                throw new BusinessException("Укажите сервер и пользователя.");
            return new()
            {
                Server = host.Text.Trim(),
                Port = number,
                Database = database.Text.Trim(),
                User = user.Text.Trim(),
                Password = password.Password
            };
        }
        dialog.Fields.Children.Add(Ui.AsyncButton("Создать новую учебную базу", async () =>
        {
            if (!Ui.Confirm("Создать новую базу repairdesk с демонстрационными данными? Если таблицы уже существуют, операция будет отклонена."))
                return;
            try
            {
                await Provisioning.InitializeAsync(Read(), System.IO.Path.Combine(AppContext.BaseDirectory, "database"));
                dialog.Error.Text = "База создана. Нажмите «Проверить и сохранить».";
            }
            catch (Exception error) { Diagnostics.Write(error); dialog.Error.Text = error is BusinessException ? error.Message : "Инициализация не выполнена. Проверьте права пользователя и журнал. При частичной ошибке используйте новую пустую базу."; }
        }));
        dialog.Fields.Children.Add(Ui.Muted("Учетные записи учебной базы: admin, reception, master. Начальный пароль: RepairDesk2026! Смените его после входа."));
        dialog.Submit("Проверить и сохранить", async () => { var next = Read(); var candidate = new Repository(next); await candidate.TestConnectionAsync(); next.Save(); if (Navigation.IsEnabled) throw new BusinessException("Настройки сохранены. Перезапустите приложение для нового подключения."); repository = candidate; });
        dialog.ShowDialog();
    }

    private void Theme_Click(object sender, RoutedEventArgs args)
    {
        dark = !dark;
        var values = dark ? new[] { "#303A3E", "#242D31", "#EDF0F1", "#B1BCBF", "#48565C", "#B84E26" } : new[] { "#F2F3F3", "#FFFFFF", "#20272A", "#687277", "#DDE2E3", "#B84E26" };
        var keys = new[] { "CanvasBrush", "SurfaceBrush", "TextBrush", "MutedBrush", "LineBrush", "AccentBrush" };
        for (var index = 0; index < keys.Length; index++)
            Application.Current.Resources[keys[index]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[index]));
        ThemeButton.Content = dark ? "Светлая тема" : "Темная тема";
        _ = LoadPageAsync();
    }

    private static decimal Amount(TextBox input)
    {
        if (!decimal.TryParse(input.Text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            throw new BusinessException("Введите корректную сумму.");
        return amount;
    }

    private void ShowClients()
    {
        PageSubtitle.Text = "Контакты, устройства и история обращений";
        var search = Ui.Input();
        search.Width = 300;
        search.ToolTip = "Поиск по имени или телефону";
        search.Tag = "Имя или телефон";
        var table = Ui.Table(("Клиент", "Name", 2), ("Телефон", "Phone", 1.4), ("Электронная почта", "Email", 1.6), ("Примечание", "Notes", 2));
        table.ItemsSource = clients;
        search.TextChanged += (_, _) => table.ItemsSource = clients.Where(client => (client.Name + client.Phone + client.Email).Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var add = Ui.AsyncButton("Добавить клиента", async () => { await EditClientAsync(null); }, true);
        add.IsEnabled = repository.Actor.CanReceive;
        var edit = Ui.AsyncButton("Изменить", async () => { if (table.SelectedItem is Client client) await EditClientAsync(client); else throw new BusinessException("Выберите клиента."); });
        edit.IsEnabled = repository.Actor.CanReceive;
        var history = Ui.AsyncButton("Обращения клиента", async () =>
        {
            if (table.SelectedItem is not Client client)
                throw new BusinessException("Выберите клиента.");
            var rows = (await repository.OrdersAsync()).Where(order => order.ClientId == client.Id).ToList();
            var dialog = new FormWindow(this, client.Name, 850, 580);
            var historyTable = Ui.Table(("Заказ", "Number", 1), ("Устройство", "Device", 2), ("Статус", "Status", 1.3), ("Стоимость", "TotalText", 1));
            historyTable.Height = 320;
            historyTable.ItemsSource = rows;
            dialog.Fields.Children.Add(historyTable);
            dialog.ShowDialog();
        });
        var root = new DockPanel();
        var toolbar = Ui.Row(search, add, edit, history);
        toolbar.Margin = new Thickness(0, 0, 0, 18);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(table);
        PageContent.Content = root;
    }

    private async Task<int?> EditClientAsync(Client? client)
    {
        if (!repository.Actor.CanReceive)
            throw new BusinessException("Недостаточно прав.");
        var dialog = new FormWindow(this, client == null ? "Новый клиент" : "Редактирование клиента");
        var name = Ui.Input(client?.Name ?? "");
        var phone = Ui.Input(client?.Phone ?? "");
        var email = Ui.Input(client?.Email ?? "");
        var notes = Ui.Input(client?.Notes ?? "", true);
        dialog.Fields.Children.Add(Ui.Field("Имя и фамилия", name));
        dialog.Fields.Children.Add(Ui.Field("Телефон", phone));
        dialog.Fields.Children.Add(Ui.Field("Электронная почта", email));
        dialog.Fields.Children.Add(Ui.Field("Примечание", notes));
        int? result = null;
        dialog.Submit("Сохранить", async () => result = await repository.SaveClientAsync(client?.Id, name.Text, phone.Text, email.Text, notes.Text));
        dialog.ShowDialog();
        if (result != null)
        {
            clients = await repository.ClientsAsync();
            if (page == "Клиенты")
                ShowClients();
        }
        return result;
    }

    private void ShowSettings()
    {
        PageSubtitle.Text = "Подключение, безопасность и обслуживание";
        var panel = Ui.Stack(Ui.Text("Учетная запись", 20, true), Ui.Muted($"{repository.Actor.Name} · {repository.Actor.Role}"));
        panel.Children.Add(Ui.Button("Сменить пароль", () =>
        {
            var dialog = new FormWindow(this, "Смена пароля", 500, 480);
            var current = new PasswordBox();
            var next = new PasswordBox();
            var repeat = new PasswordBox();
            dialog.Fields.Children.Add(Ui.Field("Текущий пароль", current));
            dialog.Fields.Children.Add(Ui.Field("Новый пароль, не менее 10 символов", next));
            dialog.Fields.Children.Add(Ui.Field("Повтор нового пароля", repeat));
            dialog.Submit("Сохранить", async () => { if (next.Password != repeat.Password) throw new BusinessException("Пароли не совпадают."); await repository.ChangePasswordAsync(current.Password, next.Password); });
            dialog.ShowDialog();
        }));
        panel.Children.Add(Ui.Text("База данных", 20, true));
        panel.Children.Add(Ui.Muted($"{repository.Settings.Server}:{repository.Settings.Port} / {repository.Settings.Database}"));
        panel.Children.Add(Ui.Muted("Для смены подключения выйдите из учетной записи и нажмите «Настроить подключение» в окне входа."));
        if (repository.Actor.CanManage)
        {
            panel.Children.Add(Ui.AsyncButton("Сохранить резервную копию SQL", async () =>
            {
                var picker = new Microsoft.Win32.SaveFileDialog { Filter = "SQL backup|*.sql", FileName = $"repairdesk-{DateTime.Now:yyyyMMdd-HHmm}.sql" };
                if (picker.ShowDialog(this) != true)
                    return;
                await repository.BackupAsync(picker.FileName, System.IO.Path.Combine(AppContext.BaseDirectory, "database", "schema.sql"));
                MessageBox.Show("Резервная копия сохранена. Она содержит данные клиентов и хеши паролей; храните ее вне публичного репозитория.", "RepairDesk");
            }));
            panel.Children.Add(Ui.Button("Добавить сотрудника", ShowNewStaff));
        }
        panel.Children.Add(Ui.Text("О приложении", 20, true));
        panel.Children.Add(Ui.Muted("RepairDesk 1.0 · Windows · C# / WPF · MySQL / MariaDB\nЛокальный каталог комплектующих. Цены и остатки демонстрационные.\nИзменения заказов и движения запчастей сохраняются в истории.\nЖурнал ошибок: %LOCALAPPDATA%\\RepairDesk\\logs"));
        PageContent.Content = new ScrollViewer { Content = Ui.Surface(panel), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void ShowNewStaff()
    {
        var dialog = new FormWindow(this, "Новый сотрудник", 530, 580);
        var name = Ui.Input();
        var login = Ui.Input();
        var password = new PasswordBox();
        var role = Ui.Combo(OrderRules.Roles, "Мастер");
        dialog.Fields.Children.Add(Ui.Field("Имя сотрудника", name));
        dialog.Fields.Children.Add(Ui.Field("Логин", login));
        dialog.Fields.Children.Add(Ui.Field("Пароль, не менее 10 символов", password));
        dialog.Fields.Children.Add(Ui.Field("Роль", role));
        dialog.Submit("Создать", () => repository.AddStaffAsync(name.Text, login.Text, password.Password, (string)role.SelectedItem));
        dialog.ShowDialog();
    }
}
