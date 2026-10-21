using MySqlConnector;
using RepairDesk.Core;

namespace RepairDesk.Data;

public sealed partial class Repository
{
    public async Task<Staff> ResumeSessionAsync(int id)
    {
        var users = await QueryAsync("SELECT id,name,login,role FROM staff WHERE id=@id AND active=1", reader => new Staff(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)), ("id", id));
        session = users.SingleOrDefault() ?? throw new BusinessException("Учётная запись недоступна. Войдите снова.");
        return session;
    }

    public async Task<RepairOrder> OrderAsync(int id)
    {
        var orders = await QueryAsync(OrderSelect + " WHERE o.id=@id", ReadOrder, ("id", id));
        var order = orders.SingleOrDefault() ?? throw new BusinessException("Заказ не найден.");
        if (Actor.Role == "Мастер" && order.TechnicianId != Actor.Id)
            throw new BusinessException("Этот заказ назначен другому мастеру.");
        return order;
    }

    public Task DeleteClientAsync(int id) => DeleteEntityAsync("clients", id, false);
    public Task DeletePartAsync(int id) => DeleteEntityAsync("parts", id, true);
    public Task DeleteServiceAsync(int id) => DeleteEntityAsync("services", id, true);
    public Task DeleteBuildAsync(int id) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanReceive) throw new BusinessException("Недостаточно прав.");
        await using var lines = Command(connection, transaction, "DELETE FROM build_parts WHERE build_id=@id", ("id", id));
        await lines.ExecuteNonQueryAsync();
        await using var command = Command(connection, transaction, "DELETE FROM builds WHERE id=@id", ("id", id));
        if (await command.ExecuteNonQueryAsync() != 1) throw new BusinessException("Сборка не найдена.");
        return 0;
    });

    private Task DeleteEntityAsync(string table, int id, bool adminOnly) => TransactionAsync(async (connection, transaction) =>
    {
        if (adminOnly ? !Actor.CanManage : !Actor.CanReceive) throw new BusinessException("Недостаточно прав.");
        if (table == "parts")
        {
            await using var stock = Command(connection, transaction, "SELECT stock FROM parts WHERE id=@id FOR UPDATE", ("id", id));
            if (Convert.ToInt32(await stock.ExecuteScalarAsync()) != 0) throw new BusinessException("Перед удалением остаток должен быть нулевым.");
        }
        await using var command = Command(connection, transaction, $"DELETE FROM {table} WHERE id=@id", ("id", id));
        try
        {
            if (await command.ExecuteNonQueryAsync() != 1) throw new BusinessException("Запись не найдена.");
        }
        catch (MySqlException error) when (error.Number == 1451) { throw new BusinessException("Запись используется в истории. Удаление запрещено, чтобы сохранить документы."); }
        return 0;
    });

    public Task<int> SaveServiceAsync(int? id, string name, decimal price) => TransactionAsync(async (connection, transaction) =>
    {
        if (!Actor.CanManage) throw new BusinessException("Услуги редактирует администратор.");
        OrderRules.Required(name, "Укажите название услуги.", 160);
        if (price < 0 || price > 10000000 || decimal.Round(price, 2) != price) throw new BusinessException("Проверьте стоимость.");
        await using var command = Command(connection, transaction, id == null ? "INSERT INTO services(name,price) VALUES(@name,@price)" : "UPDATE services SET name=@name,price=@price WHERE id=@id", ("id", id), ("name", name.Trim()), ("price", price));
        if (await command.ExecuteNonQueryAsync() != 1) throw new BusinessException("Услуга не найдена.");
        return id ?? (int)command.LastInsertedId;
    });

    public async Task<List<Estimate>> EstimatesAsync(int orderId)
    {
        await OrderAsync(orderId);
        return await QueryAsync("SELECT e.id,e.order_id,e.status,e.note,e.approved_by,e.created_at,COALESCE(SUM(l.quantity*l.unit_price),0) FROM estimates e LEFT JOIN estimate_lines l ON l.estimate_id=e.id WHERE e.order_id=@id GROUP BY e.id,e.order_id,e.status,e.note,e.approved_by,e.created_at ORDER BY e.id DESC", reader => new Estimate(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDateTime(5), reader.GetDecimal(6)), ("id", orderId));
    }

    public async Task<List<EstimateLine>> EstimateLinesAsync(int orderId, int estimateId)
    {
        await OrderAsync(orderId);
        return await QueryAsync("SELECT l.id,l.part_id,l.name,l.quantity,l.unit_price FROM estimate_lines l JOIN estimates e ON e.id=l.estimate_id WHERE e.id=@estimate AND e.order_id=@order ORDER BY l.id", reader => new EstimateLine(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetDecimal(4)), ("estimate", estimateId), ("order", orderId));
    }

    public Task<int> CreateEstimateAsync(RepairOrder snapshot, string note, IReadOnlyList<EstimateInput> items) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (!Actor.CanReceive) throw new BusinessException("Смету оформляет приемщик или администратор.");
        if (note.Length > 2000 || items.Count is < 1 or > 50) throw new BusinessException("Смета: от 1 до 50 позиций; примечание до 2000 символов.");
        await using var create = Command(connection, transaction, "INSERT INTO estimates(order_id,note) VALUES(@order,@note)", ("order", order.Id), ("note", note.Trim()));
        await create.ExecuteNonQueryAsync();
        var id = (int)create.LastInsertedId;
        foreach (var item in items.OrderBy(item => item.PartId))
        {
            if (item.Quantity is < 1 or > 100 || item.PartId.HasValue == item.ServiceId.HasValue) throw new BusinessException("Выберите деталь или услугу и количество от 1 до 100.");
            var sql = item.PartId.HasValue ? "SELECT name,price FROM parts WHERE id=@id FOR UPDATE" : "SELECT name,price FROM services WHERE id=@id FOR UPDATE";
            string name; decimal price;
            await using (var read = Command(connection, transaction, sql, ("id", item.PartId ?? item.ServiceId)))
            await using (var reader = await read.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new BusinessException("Позиция каталога не найдена.");
                name = reader.GetString(0); price = reader.GetDecimal(1);
            }
            await using var line = Command(connection, transaction, "INSERT INTO estimate_lines(estimate_id,part_id,name,quantity,unit_price) VALUES(@id,@part,@name,@qty,@price)", ("id", id), ("part", item.PartId), ("name", name), ("qty", item.Quantity), ("price", price));
            await line.ExecuteNonQueryAsync();
        }
        await TouchAsync(connection, transaction, order.Id);
        await EventAsync(connection, transaction, order.Id, $"Составлена смета №{id}. Цены зафиксированы, детали пока не списаны.");
        return id;
    });

    public Task DecideEstimateAsync(RepairOrder snapshot, int estimateId, string decision, string approvedBy) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (!Actor.CanReceive) throw new BusinessException("Согласование оформляет приемщик или администратор.");
        if (decision is not ("Согласована" or "Отклонена" or "Применена")) throw new BusinessException("Неверное действие со сметой.");
        await using var read = Command(connection, transaction, "SELECT status FROM estimates WHERE id=@id AND order_id=@order FOR UPDATE", ("id", estimateId), ("order", order.Id));
        var status = Convert.ToString(await read.ExecuteScalarAsync());
        if (decision == "Применена" ? status != "Согласована" : status != "Черновик") throw new BusinessException("Статус сметы уже изменён. Обновите заказ.");
        if (decision != "Применена") OrderRules.Required(approvedBy, "Укажите, кто подтвердил решение клиента.", 120);
        if (decision == "Применена")
        {
            if (order.Status == "Готов") throw new BusinessException("Сначала верните заказ в ремонт.");
            var lines = new List<EstimateLine>();
            await using (var command = Command(connection, transaction, "SELECT id,part_id,name,quantity,unit_price FROM estimate_lines WHERE estimate_id=@id ORDER BY part_id,id", ("id", estimateId)))
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) lines.Add(new(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetDecimal(4)));
            foreach (var line in lines)
            {
                if (line.PartId is { } partId) await MoveStockAsync(connection, transaction, partId, -line.Quantity, $"Смета №{estimateId}: {order.Number}", order.Id);
                await using var insert = Command(connection, transaction, "INSERT INTO order_lines(order_id,kind,part_id,name,quantity,unit_price) VALUES(@order,@kind,@part,@name,@qty,@price)", ("order", order.Id), ("kind", line.PartId.HasValue ? "Запчасть" : "Работа"), ("part", line.PartId), ("name", line.Name), ("qty", line.Quantity), ("price", line.UnitPrice));
                await insert.ExecuteNonQueryAsync();
            }
        }
        await using var update = Command(connection, transaction, "UPDATE estimates SET status=@status,approved_by=IF(@status='Применена',approved_by,@name) WHERE id=@id", ("status", decision), ("name", approvedBy.Trim()), ("id", estimateId));
        await update.ExecuteNonQueryAsync();
        await TouchAsync(connection, transaction, order.Id);
        await EventAsync(connection, transaction, order.Id, $"Смета №{estimateId}: {decision.ToLowerInvariant()}.");
        return 0;
    });

    public async Task<List<Diagnostic>> DiagnosticsAsync(int orderId)
    {
        await OrderAsync(orderId);
        return await QueryAsync("SELECT d.test_key,d.result,d.note,s.name,d.updated_at FROM diagnostics d JOIN staff s ON s.id=d.staff_id WHERE order_id=@id", reader => new Diagnostic(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)), ("id", orderId));
    }

    public Task SaveDiagnosticAsync(RepairOrder snapshot, string key, string result, string note) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        if (!DiagnosticTests.Items.ContainsKey(key) || !new[] { "Не проверено", "Норма", "Проблема", "Не применимо" }.Contains(result) || note.Length > 1000) throw new BusinessException("Проверьте результат диагностики и примечание.");
        await using var command = Command(connection, transaction, "INSERT INTO diagnostics(order_id,test_key,result,note,staff_id) VALUES(@order,@key,@result,@note,@staff) ON DUPLICATE KEY UPDATE result=@result,note=@note,staff_id=@staff", ("order", order.Id), ("key", key), ("result", result), ("note", note.Trim()), ("staff", Actor.Id));
        await command.ExecuteNonQueryAsync();
        await TouchAsync(connection, transaction, order.Id);
        await EventAsync(connection, transaction, order.Id, $"{DiagnosticTests.Items[key]}: {result}.");
        return 0;
    });

    public Task AddNoteAsync(RepairOrder snapshot, string message) => TransactionAsync(async (connection, transaction) =>
    {
        var order = await LockedOrderAsync(connection, transaction, snapshot);
        OrderRules.CheckOrderAccess(Actor, order);
        OrderRules.Required(message, "Введите заметку.", 500);
        await EventAsync(connection, transaction, order.Id, message.Trim());
        await TouchAsync(connection, transaction, order.Id);
        return 0;
    });
}
