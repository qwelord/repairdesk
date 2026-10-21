using System.Data;
using MySqlConnector;
using RepairDesk.Core;

namespace RepairDesk.Data;

public sealed partial class Repository(DatabaseSettings settings)
{
    public DatabaseSettings Settings { get; } = settings;
    private Staff? session;
    public Staff Actor => session ?? throw new BusinessException("Требуется вход в систему.");

    public async Task TestConnectionAsync()
    {
        await using var connection = new MySqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand("SELECT version FROM schema_info LIMIT 1", connection);
        var version = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (version != 2)
            throw new BusinessException("Версия базы не поддерживается этой сборкой приложения.");
    }

    public async Task<Staff> LoginAsync(string login, string password)
    {
        var rows = await QueryAsync("SELECT id,name,login,role,password_hash FROM staff WHERE login=@login AND active=1", reader =>
            (User: new Staff(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)), Hash: reader.GetString(4)), ("login", login));
        if (rows.Count != 1 || !Passwords.Verify(password, rows[0].Hash))
            throw new BusinessException("Неверный логин или пароль.");
        session = rows[0].User;
        return session;
    }

    public void Logout() => session = null;

    public Task AddStaffAsync(string name, string login, string password, string role) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanManage)
            throw new BusinessException("Недостаточно прав.");
        OrderRules.Required(name, "Укажите имя сотрудника.", 120);
        OrderRules.Required(login, "Укажите логин.", 80);
        if (password.Length is < 10 or > 128)
            throw new BusinessException("Пароль: от 10 до 128 символов.");
        if (!OrderRules.Roles.Contains(role))
            throw new BusinessException("Выберите роль.");
        await using var command = Command(connection, transaction, "INSERT INTO staff(name,login,password_hash,role) VALUES(@name,@login,@hash,@role)", ("name", name.Trim()), ("login", login.Trim()), ("hash", Passwords.Hash(password)), ("role", role));
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (MySqlException error) when (error.Number == 1062) { throw new BusinessException("Логин уже занят."); }
        return 0;
    });

    public async Task BackupAsync(string path, string schemaPath)
    {
        if (!Actor.CanManage)
            throw new BusinessException("Резервное копирование доступно администратору.");
        await using var connection = new MySqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var output = new System.Text.StringBuilder(await File.ReadAllTextAsync(schemaPath));
        output.AppendLine("\nDELETE FROM schema_info;");
        foreach (var table in new[] { "schema_info", "staff", "clients", "parts", "services", "games", "orders", "order_lines", "payments", "order_events", "stock_moves", "benchmarks", "builds", "build_parts", "estimates", "estimate_lines", "diagnostics" })
        {
            await using var command = Command(connection, transaction, $"SELECT * FROM `{table}`");
            await using var reader = await command.ExecuteReaderAsync();
            var names = Enumerable.Range(0, reader.FieldCount).Select(index => "`" + reader.GetName(index) + "`").ToArray();
            while (await reader.ReadAsync())
            {
                var values = Enumerable.Range(0, reader.FieldCount).Select(index =>
                {
                    if (reader.IsDBNull(index))
                        return "NULL";
                    var value = reader.GetValue(index);
                    if (value is DateTime date)
                        return "'" + date.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "'";
                    if (value is bool boolean)
                        return boolean ? "1" : "0";
                    if (value is string text)
                        return text.Length == 0 ? "''" : "CONVERT(0x" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(text)) + " USING utf8mb4)";
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";
                });
                output.AppendLine($"INSERT INTO `{table}` ({string.Join(',', names)}) VALUES ({string.Join(',', values)});");
            }
        }
        await transaction.CommitAsync();
        await File.WriteAllTextAsync(path, output.ToString());
    }

    public Task<int> CreateBuildOrderAsync(int clientId, BuildSelection build, string name) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanReceive)
            throw new BusinessException("Оформление заказа доступно приемщику и администратору.");
        var check = Compatibility.Check(build);
        if (!check.IsComplete || check.HasErrors)
            throw new BusinessException("Завершите сборку и устраните несовместимости.");
        if (clientId <= 0)
            throw new BusinessException("Выберите клиента.");
        OrderRules.Required(name, "Укажите название сборки.", 150);
        await using var create = Command(connection, transaction, "INSERT INTO orders(client_id,device_type,device,complaint,device_condition,due_at) VALUES(@client,'ПК',@name,'Сборка или модернизация по согласованной конфигурации.','Новые комплектующие со склада.',@due)", ("client", clientId), ("name", name), ("due", DateTime.Today.AddDays(3)));
        await create.ExecuteNonQueryAsync();
        var id = (int)create.LastInsertedId;
        foreach (var part in build.Parts.OrderBy(part => part.Id))
        {
            await MoveStockAsync(connection, transaction, part.Id, -1, $"Комплектование RD-{id:00000}", id);
            await using var line = Command(connection, transaction, "INSERT INTO order_lines(order_id,kind,part_id,name,quantity,unit_price) SELECT @order,'Запчасть',id,name,1,price FROM parts WHERE id=@part", ("order", id), ("part", part.Id));
            await line.ExecuteNonQueryAsync();
        }
        await using var labor = Command(connection, transaction, "INSERT INTO order_lines(order_id,kind,name,quantity,unit_price) SELECT @order,'Работа',name,1,price FROM services WHERE name='Сборка и проверка ПК'", ("order", id));
        await labor.ExecuteNonQueryAsync();
        await EventAsync(connection, transaction, id, "Заказ создан из конфигуратора. Комплектующие выделены со склада; добавлена работа по сборке.");
        return id;
    });
    public Task<List<Staff>> StaffAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT id,name,login,role FROM staff WHERE active=1 ORDER BY name", reader => new Staff(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
    public Task<List<Client>> ClientsAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT id,name,phone,email,notes FROM clients ORDER BY name", ReadClient);
    }
    private static Client ReadClient(MySqlDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));

    public async Task<int> SaveClientAsync(int? id, string name, string phone, string email, string notes)
    {
        if (!Actor.CanReceive)
            throw new BusinessException("Управление клиентами доступно приемщику и администратору.");
        OrderRules.ValidateClient(name, phone, email);
        if (notes.Length > 2000)
            throw new BusinessException("Примечание: не более 2000 символов.");
        return await TransactionAsync(async (connection, transaction) =>
        {
            var sql = id == null ? "INSERT INTO clients(name,phone,email,notes) VALUES(@name,@phone,@email,@notes)" : "UPDATE clients SET name=@name,phone=@phone,email=@email,notes=@notes WHERE id=@id";
            await using var command = Command(connection, transaction, sql, ("id", id), ("name", name.Trim()), ("phone", phone.Trim()), ("email", email.Trim()), ("notes", notes.Trim()));
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new BusinessException("Клиент не найден.");
            return id ?? (int)command.LastInsertedId;
        });
    }

    private const string OrderSelect = "SELECT o.id,o.client_id,c.name,c.phone,o.device_type,o.device,o.serial,o.complaint,o.device_condition,o.diagnosis,o.status,o.technician_id,COALESCE(s.name,'Не назначен'),o.created_at,o.due_at,COALESCE((SELECT SUM(l.quantity*l.unit_price) FROM order_lines l WHERE l.order_id=o.id),0),o.paid,o.version FROM orders o JOIN clients c ON c.id=o.client_id LEFT JOIN staff s ON s.id=o.technician_id";
    private static RepairOrder ReadOrder(MySqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetInt32(11), reader.GetString(12), reader.GetDateTime(13), reader.GetDateTime(14), reader.GetDecimal(15), reader.GetDecimal(16), reader.GetInt32(17));
    public Task<List<RepairOrder>> OrdersAsync() => QueryAsync(OrderSelect + (Actor.Role == "Мастер" ? " WHERE o.technician_id=@actor" : "") + " ORDER BY o.id DESC", ReadOrder, ("actor", Actor.Id));
    public Task<List<OrderLine>> LinesAsync(int orderId)
    {
        _ = Actor;
        return QueryAsync("SELECT id,kind,part_id,name,quantity,unit_price FROM order_lines WHERE order_id=@id ORDER BY id", reader => new OrderLine(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4), reader.GetDecimal(5)), ("id", orderId));
    }
    public Task<List<OrderEvent>> EventsAsync(int orderId)
    {
        _ = Actor;
        return QueryAsync("SELECT e.created_at,s.name,e.message FROM order_events e JOIN staff s ON s.id=e.staff_id WHERE order_id=@id ORDER BY e.id DESC", reader => new OrderEvent(reader.GetDateTime(0), reader.GetString(1), reader.GetString(2)), ("id", orderId));
    }
    public Task<List<ServiceItem>> ServicesAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT id,name,price FROM services ORDER BY name", reader => new ServiceItem(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2)));
    }

    public async Task<int> CreateOrderAsync(NewOrder order)
    {
        if (!Actor.CanReceive)
            throw new BusinessException("Прием техники доступен приемщику и администратору.");
        OrderRules.Validate(order);
        return await TransactionAsync(async (connection, transaction) =>
        {
            await using var command = Command(connection, transaction, "INSERT INTO orders(client_id,device_type,device,serial,complaint,device_condition,due_at,technician_id) VALUES(@client,@type,@device,@serial,@complaint,@condition,@due,@tech)", ("client", order.ClientId), ("type", order.DeviceType), ("device", order.Device.Trim()), ("serial", order.Serial.Trim()), ("complaint", order.Complaint.Trim()), ("condition", order.Condition.Trim()), ("due", order.DueAt.Date), ("tech", order.TechnicianId));
            await command.ExecuteNonQueryAsync();
            var id = (int)command.LastInsertedId;
            await EventAsync(connection, transaction, id, "Техника принята. Создан заказ.");
            return id;
        });
    }

    public Task UpdateOrderAsync(RepairOrder snapshot, string diagnosis, DateTime due, int? technicianId, string nextStatus) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (diagnosis.Length > 4000)
            throw new BusinessException("Диагностика: не более 4000 символов.");
        if (!Actor.CanReceive && (order.TechnicianId != technicianId || due.Date != order.DueAt.Date))
            throw new BusinessException("Назначение мастера и срок меняет приемщик.");
        if (due.Date < order.CreatedAt.Date)
            throw new BusinessException("Срок не может быть раньше даты приема.");
        if (nextStatus != order.Status)
            OrderRules.CheckTransition(Actor, order, nextStatus, diagnosis);
        if (nextStatus == "Отменен")
        {
            await using var parts = Command(connection, transaction, "SELECT part_id,quantity FROM order_lines WHERE order_id=@id AND part_id IS NOT NULL ORDER BY part_id", ("id", order.Id));
            var returned = new List<(int PartId, int Quantity)>();
            await using (var reader = await parts.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    returned.Add((reader.GetInt32(0), reader.GetInt32(1)));
            foreach (var item in returned)
                await MoveStockAsync(connection, transaction, item.PartId, item.Quantity, $"Возврат: {order.Number}", order.Id);
        }
        await using var update = Command(connection, transaction, "UPDATE orders SET diagnosis=@diagnosis,due_at=@due,technician_id=@tech,status=@status,version=version+1 WHERE id=@id", ("diagnosis", diagnosis.Trim()), ("due", due.Date), ("tech", technicianId), ("status", nextStatus), ("id", order.Id));
        await update.ExecuteNonQueryAsync();
        await EventAsync(connection, transaction, order.Id, nextStatus == order.Status ? "Обновлены диагностика и параметры заказа." : $"Статус: {order.Status} → {nextStatus}.");
        return 0;
    });

    public Task AddLineAsync(RepairOrder snapshot, int? partId, ServiceItem? service, int quantity) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (order.Status == "Готов")
            throw new BusinessException("Для изменения состава верните заказ в ремонт.");
        if (quantity is < 1 or > 100)
            throw new BusinessException("Количество: от 1 до 100.");
        string name;
        decimal price;
        string kind;
        if (partId is { } selectedPart)
        {
            await using (var partCommand = Command(connection, transaction, "SELECT name,price FROM parts WHERE id=@id FOR UPDATE", ("id", selectedPart)))
            await using (var reader = await partCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new BusinessException("Деталь не найдена.");
                name = reader.GetString(0);
                price = reader.GetDecimal(1);
            }
            kind = "Запчасть";
            await MoveStockAsync(connection, transaction, selectedPart, -quantity, $"Установка: {order.Number}", order.Id);
        }
        else if (service != null)
        {
            await using var serviceCommand = Command(connection, transaction, "SELECT name,price FROM services WHERE id=@id", ("id", service.Id));
            await using var reader = await serviceCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new BusinessException("Услуга не найдена.");
            name = reader.GetString(0);
            price = reader.GetDecimal(1);
            kind = "Работа";
        }
        else
            throw new BusinessException("Выберите работу или запчасть.");
        await using var command = Command(connection, transaction, "INSERT INTO order_lines(order_id,kind,part_id,name,quantity,unit_price) VALUES(@order,@kind,@part,@name,@qty,@price)", ("order", order.Id), ("kind", kind), ("part", partId), ("name", name), ("qty", quantity), ("price", price));
        await command.ExecuteNonQueryAsync();
        await TouchAsync(connection, transaction, order.Id);
        await EventAsync(connection, transaction, order.Id, $"Добавлено: {name}, {quantity} шт., {Money.Format(price * quantity)}.");
        return 0;
    });

    public Task RemoveLineAsync(RepairOrder snapshot, OrderLine line) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (order.Status == "Готов")
            throw new BusinessException("Для изменения состава верните заказ в ремонт.");
        if (order.Paid > order.Total - line.Total)
            throw new BusinessException("Сначала верните избыточную оплату.");
        await using var command = Command(connection, transaction, "DELETE FROM order_lines WHERE id=@id AND order_id=@order", ("id", line.Id), ("order", order.Id));
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new BusinessException("Строка уже изменена. Обновите заказ.");
        if (line.PartId is { } partId)
            await MoveStockAsync(connection, transaction, partId, line.Quantity, $"Возврат: {order.Number}", order.Id);
        await TouchAsync(connection, transaction, order.Id);
        await EventAsync(connection, transaction, order.Id, $"Удалено: {line.Name}.");
        return 0;
    });

    public Task PayAsync(RepairOrder snapshot, decimal amount) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanReceive)
            throw new BusinessException("Оплату оформляет приемщик или администратор.");
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (amount == 0 || decimal.Round(amount, 2) != amount || order.Paid + amount < 0 || order.Paid + amount > order.Total)
            throw new BusinessException("Проверьте сумму. Оплата не должна превышать стоимость, а возврат — внесенную сумму.");
        await using var payment = Command(connection, transaction, "INSERT INTO payments(order_id,staff_id,amount) VALUES(@id,@staff,@amount)", ("id", order.Id), ("staff", Actor.Id), ("amount", amount));
        await payment.ExecuteNonQueryAsync();
        await using var update = Command(connection, transaction, "UPDATE orders SET paid=paid+@amount,version=version+1 WHERE id=@id", ("amount", amount), ("id", order.Id));
        await update.ExecuteNonQueryAsync();
        await EventAsync(connection, transaction, order.Id, $"{(amount > 0 ? "Оплата" : "Возврат оплаты")}: {Money.Format(Math.Abs(amount))}.");
        return 0;
    });

    public Task<List<Part>> PartsAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT id,category,brand,name,price,stock,min_stock,socket,memory_type,wattage,capacity,length_mm,form_factor,supported_forms,max_gpu_mm,max_memory_gb,cpu_support,power_connectors,source_url,notes FROM parts ORDER BY category,name", reader => new Part(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7), reader.GetString(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11), reader.GetString(12), reader.GetString(13), reader.GetInt32(14), reader.GetInt32(15), reader.GetString(16), reader.GetString(17), reader.GetString(18), reader.GetString(19)));
    }
    public Task<List<StockMove>> StockMovesAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT m.created_at,p.name,m.quantity,m.reason,s.name FROM stock_moves m JOIN parts p ON p.id=m.part_id JOIN staff s ON s.id=m.staff_id ORDER BY m.id DESC LIMIT 300", reader => new StockMove(reader.GetDateTime(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4)));
    }

    public Task AdjustStockAsync(int partId, int quantity, string reason) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanReceive)
            throw new BusinessException("Изменять склад может приемщик или администратор.");
        OrderRules.Required(reason, "Укажите причину движения.", 250);
        if (quantity == 0 || Math.Abs((long)quantity) > 10000)
            throw new BusinessException("Количество: от −10000 до 10000, кроме нуля.");
        await MoveStockAsync(connection, transaction, partId, quantity, reason.Trim(), null);
        return 0;
    });

    public Task<int> SavePartAsync(Part part, bool create) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanManage)
            throw new BusinessException("Каталог редактирует администратор.");
        OrderRules.Required(part.Name, "Укажите название.", 160);
        OrderRules.Required(part.Brand, "Укажите производителя.", 60);
        if (part.Socket.Length > 30 || part.MemoryType.Length > 30 || part.FormFactor.Length > 30 || part.SupportedForms.Length > 100 || part.PowerConnectors.Length > 100 || part.CpuSupport.Length > 4000 || part.Notes.Length > 4000)
            throw new BusinessException("Превышена длина технического поля. Сокет, память и формат — до 30 символов, разъемы и форматы корпуса — до 100.");
        if (!new[] { "Процессор", "Материнская плата", "Видеокарта", "Память", "Накопитель", "Блок питания", "Корпус" }.Contains(part.Category))
            throw new BusinessException("Выберите категорию.");
        if (part.Price < 0 || part.Price > 10000000 || decimal.Round(part.Price, 2) != part.Price || part.MinStock is < 0 or > 10000)
            throw new BusinessException("Проверьте цену и минимальный остаток.");
        if (new[] { part.Wattage, part.Capacity, part.LengthMm, part.MaxGpuMm, part.MaxMemoryGb }.Any(value => value is < 0 or > 100000))
            throw new BusinessException("Технические параметры должны быть от 0 до 100000.");
        if (part.SourceUrl.Length > 600 || part.SourceUrl.Length > 0 && (!Uri.TryCreate(part.SourceUrl, UriKind.Absolute, out var source) || source.Scheme != "https"))
            throw new BusinessException("Источник должен быть HTTPS-ссылкой.");
        if (part.Category is "Процессор" or "Материнская плата" && string.IsNullOrWhiteSpace(part.Socket))
            throw new BusinessException("Укажите сокет.");
        if (part.Category is "Материнская плата" or "Память" && part.MemoryType is not ("DDR4" or "DDR5"))
            throw new BusinessException("Укажите тип памяти DDR4 или DDR5.");
        if (part.Category is "Процессор" or "Видеокарта" or "Блок питания" && part.Wattage <= 0)
            throw new BusinessException("Укажите положительную мощность.");
        if (part.Category == "Материнская плата" && (part.MaxMemoryGb <= 0 || string.IsNullOrWhiteSpace(part.FormFactor)))
            throw new BusinessException("Укажите форм-фактор платы и максимальный объем памяти.");
        if (part.Category == "Видеокарта" && (part.LengthMm <= 0 || string.IsNullOrWhiteSpace(part.PowerConnectors)))
            throw new BusinessException("Укажите длину и разъемы питания GPU.");
        if (part.Category is "Память" or "Накопитель" && part.Capacity <= 0)
            throw new BusinessException("Укажите положительный объем.");
        if (part.Category == "Корпус" && (part.MaxGpuMm <= 0 || string.IsNullOrWhiteSpace(part.SupportedForms)))
            throw new BusinessException("Укажите форматы платы и максимальную длину GPU.");
        var assignments = "category=@category,brand=@brand,name=@name,price=@price,min_stock=@min,socket=@socket,memory_type=@memory,wattage=@watts,capacity=@capacity,length_mm=@length,form_factor=@form,supported_forms=@forms,max_gpu_mm=@gpu,max_memory_gb=@ram,cpu_support=@cpu,power_connectors=@power,source_url=@source,notes=@notes";
        var sql = create ? "INSERT INTO parts SET " + assignments : "UPDATE parts SET " + assignments + " WHERE id=@id";
        await using var command = Command(connection, transaction, sql, ("id", part.Id), ("category", part.Category), ("brand", part.Brand.Trim()), ("name", part.Name.Trim()), ("price", part.Price), ("min", part.MinStock), ("socket", part.Socket), ("memory", part.MemoryType), ("watts", part.Wattage), ("capacity", part.Capacity), ("length", part.LengthMm), ("form", part.FormFactor), ("forms", part.SupportedForms), ("gpu", part.MaxGpuMm), ("ram", part.MaxMemoryGb), ("cpu", part.CpuSupport), ("power", part.PowerConnectors), ("source", part.SourceUrl), ("notes", part.Notes));
        try
        {
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new BusinessException("Комплектующая не найдена.");
        }
        catch (MySqlException error) when (error.Number == 1062) { throw new BusinessException("Комплектующая с таким названием уже есть."); }
        return create ? (int)command.LastInsertedId : part.Id;
    });

    public Task UpdatePartAsync(int partId, decimal price, int minStock) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanManage)
            throw new BusinessException("Цены и пороги меняет администратор.");
        if (price < 0 || price > 10000000 || decimal.Round(price, 2) != price || minStock is < 0 or > 10000)
            throw new BusinessException("Проверьте цену и минимальный остаток.");
        await using var command = Command(connection, transaction, "UPDATE parts SET price=@price,min_stock=@min WHERE id=@id", ("price", price), ("min", minStock), ("id", partId));
        await command.ExecuteNonQueryAsync();
        return 0;
    });

    public Task<List<Game>> GamesAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT id,name FROM games ORDER BY id", reader => new Game(reader.GetInt32(0), reader.GetString(1)));
    }
    public Task<List<Benchmark>> BenchmarksAsync(int gpuId, int width)
    {
        _ = Actor;
        return QueryAsync("SELECT b.id,b.game_id,g.name,b.gpu_id,p.name,b.test_cpu,b.width,b.height,b.preset,b.ray_tracing,b.upscaling,b.average_fps,b.low_fps,b.source_title,b.source_url,b.test_date,b.notes FROM benchmarks b JOIN games g ON g.id=b.game_id JOIN parts p ON p.id=b.gpu_id WHERE gpu_id=@gpu AND width=@width ORDER BY g.id", reader => new Benchmark(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetDecimal(11), reader.IsDBNull(12) ? null : reader.GetDecimal(12), reader.GetString(13), reader.GetString(14), reader.GetDateTime(15), reader.GetString(16)), ("gpu", gpuId), ("width", width));
    }

    public Task SaveBuildAsync(string name, int? clientId, BuildSelection build) => TransactionAsync(async (connection, transaction) =>
    {
        var result = Compatibility.Check(build);
        if (!result.IsComplete || result.HasErrors)
            throw new BusinessException("Завершите подбор и устраните несовместимости.");
        OrderRules.Required(name, "Укажите название сборки.", 150);
        await using var command = Command(connection, transaction, "INSERT INTO builds(name,client_id,staff_id,total) VALUES(@name,@client,@staff,@total)", ("name", name.Trim()), ("client", clientId), ("staff", Actor.Id), ("total", build.Price));
        await command.ExecuteNonQueryAsync();
        var buildId = command.LastInsertedId;
        foreach (var part in build.Parts)
        {
            await using var item = Command(connection, transaction, "INSERT INTO build_parts(build_id,part_id,price) VALUES(@build,@part,@price)", ("build", buildId), ("part", part.Id), ("price", part.Price));
            await item.ExecuteNonQueryAsync();
        }
        return 0;
    });
    public Task<List<SavedBuild>> BuildsAsync()
    {
        _ = Actor;
        return QueryAsync("SELECT b.id,b.name,COALESCE(c.name,'Без клиента'),COALESCE(GROUP_CONCAT(p.name ORDER BY p.category SEPARATOR ', '),''),b.total,b.created_at FROM builds b LEFT JOIN clients c ON c.id=b.client_id LEFT JOIN build_parts bp ON bp.build_id=b.id LEFT JOIN parts p ON p.id=bp.part_id GROUP BY b.id,b.name,c.name,b.total,b.created_at ORDER BY b.id DESC", reader => new SavedBuild(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDateTime(5)));
    }

    public Task<List<int>> BuildPartIdsAsync(int buildId)
    {
        _ = Actor;
        return QueryAsync("SELECT part_id FROM build_parts WHERE build_id=@id", reader => reader.GetInt32(0), ("id", buildId));
    }

    public Task ChangePasswordAsync(string oldPassword, string newPassword) => TransactionAsync(async (connection, transaction) =>
    {
        if (newPassword.Length is < 10 or > 128)
            throw new BusinessException("Новый пароль: от 10 до 128 символов.");
        await using var read = Command(connection, transaction, "SELECT password_hash FROM staff WHERE id=@id FOR UPDATE", ("id", Actor.Id));
        if (!Passwords.Verify(oldPassword, Convert.ToString(await read.ExecuteScalarAsync()) ?? ""))
            throw new BusinessException("Текущий пароль неверен.");
        await using var update = Command(connection, transaction, "UPDATE staff SET password_hash=@hash WHERE id=@id", ("id", Actor.Id), ("hash", Passwords.Hash(newPassword)));
        await update.ExecuteNonQueryAsync();
        return 0;
    });

    private async Task<RepairOrder> LockedOrderAsync(MySqlConnection connection, MySqlTransaction transaction, RepairOrder snapshot)
    {
        await using (var command = Command(connection, transaction, "SELECT version FROM orders WHERE id=@id FOR UPDATE", ("id", snapshot.Id)))
        {
            var version = await command.ExecuteScalarAsync();
            if (version == null || Convert.ToInt32(version) != snapshot.Version)
                throw new BusinessException("Заказ изменен другим сотрудником. Обновите его и повторите действие.");
        }
        await using var read = Command(connection, transaction, OrderSelect + " WHERE o.id=@id", ("id", snapshot.Id));
        await using var reader = await read.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new BusinessException("Заказ не найден.");
        return ReadOrder(reader);
    }

    private async Task MoveStockAsync(MySqlConnection connection, MySqlTransaction transaction, int partId, int quantity, string reason, int? orderId)
    {
        await using var update = Command(connection, transaction, "UPDATE parts SET stock=stock+@qty WHERE id=@part AND stock+@qty>=0", ("qty", quantity), ("part", partId));
        if (await update.ExecuteNonQueryAsync() != 1)
            throw new BusinessException("Недостаточно деталей на складе. Обновите остатки.");
        await using var move = Command(connection, transaction, "INSERT INTO stock_moves(part_id,staff_id,order_id,quantity,reason) VALUES(@part,@staff,@order,@qty,@reason)", ("part", partId), ("staff", Actor.Id), ("order", orderId), ("qty", quantity), ("reason", reason));
        await move.ExecuteNonQueryAsync();
    }
    private async Task EventAsync(MySqlConnection connection, MySqlTransaction transaction, int orderId, string message)
    {
        await using var command = Command(connection, transaction, "INSERT INTO order_events(order_id,staff_id,message) VALUES(@order,@staff,@message)", ("order", orderId), ("staff", Actor.Id), ("message", message));
        await command.ExecuteNonQueryAsync();
    }
    private static async Task TouchAsync(MySqlConnection connection, MySqlTransaction transaction, int orderId)
    {
        await using var command = Command(connection, transaction, "UPDATE orders SET version=version+1 WHERE id=@id", ("id", orderId));
        await command.ExecuteNonQueryAsync();
    }
    private async Task<T> TransactionAsync<T>(Func<MySqlConnection, MySqlTransaction, Task<T>> action)
    {
        _ = Actor;
        await using var connection = new MySqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var result = await action(connection, transaction);
        await transaction.CommitAsync();
        return result;
    }
    private async Task<List<T>> QueryAsync<T>(string sql, Func<MySqlDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new MySqlConnection(Settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = Command(connection, null, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(map(reader));
        return rows;
    }
    private static MySqlCommand Command(MySqlConnection connection, MySqlTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new MySqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue("@" + parameter.Name, parameter.Value ?? DBNull.Value);
        return command;
    }
}
