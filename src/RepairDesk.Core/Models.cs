using System.Globalization;

namespace RepairDesk.Core;

public record Staff(int Id, string Name, string Login, string Role)
{
    public bool CanManage => Role == "Администратор";
    public bool CanReceive => Role is "Администратор" or "Приемщик";
}

public record Client(int Id, string Name, string Phone, string Email, string Notes)
{
    public override string ToString() => $"{Name} · {Phone}";
}

public record RepairOrder(int Id, int ClientId, string ClientName, string Phone, string DeviceType,
    string Device, string Serial, string Complaint, string Condition, string Diagnosis, string Status,
    int? TechnicianId, string Technician, DateTime CreatedAt, DateTime DueAt, decimal Total,
    decimal Paid, int Version)
{
    public string Number => $"RD-{Id:00000}";
    public string TotalText => Money.Format(Total);
    public decimal AmountDue => Status == "Отменен" ? 0 : Total - Paid;
    public string DueText => DueAt.ToString("dd MMM", CultureInfo.GetCultureInfo("ru-RU"));
    public bool IsOverdue => DueAt.Date < DateTime.Today && Status is not ("Выдан" or "Отменен");
    public override string ToString() => Number;
}

public record NewOrder(int ClientId, string DeviceType, string Device, string Serial, string Complaint,
    string Condition, DateTime DueAt, int? TechnicianId);
public record OrderLine(int Id, string Kind, int? PartId, string Name, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
    public string TotalText => Money.Format(Total);
}
public record OrderEvent(DateTime CreatedAt, string Author, string Message)
{
    public string TimeText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
}
public record StockMove(DateTime CreatedAt, string Part, int Quantity, string Reason, string Author);
public record ServiceItem(int Id, string Name, decimal Price)
{
    public override string ToString() => $"{Name} — {Money.Format(Price)}";
}

public record Part(int Id, string Category, string Brand, string Name, decimal Price, int Stock,
    int MinStock, string Socket, string MemoryType, int Wattage, int Capacity, int LengthMm,
    string FormFactor, string SupportedForms, int MaxGpuMm, int MaxMemoryGb, string CpuSupport,
    string PowerConnectors, string SourceUrl, string Notes)
{
    public string PriceText => Money.Format(Price);
    public string StockText => Stock == 0 ? "Нет в наличии" : $"{Stock} шт.";
    public override string ToString() => $"{Name} — {PriceText} ({StockText})";
}

public record BuildSelection(Part? Cpu, Part? Board, Part? Gpu, Part? Ram, Part? Psu, Part? Case, Part? Storage)
{
    public IReadOnlyList<Part> Parts => new[] { Cpu, Board, Gpu, Ram, Psu, Case, Storage }.OfType<Part>().ToArray();
    public decimal Price => Parts.Sum(part => part.Price);
}
public enum FindingLevel
{
    Pass, Warning, Error
}
public record Finding(FindingLevel Level, string Title, string Detail);
public record CompatibilityResult(IReadOnlyList<Finding> Findings, int RecommendedWatts)
{
    public bool HasErrors => Findings.Any(finding => finding.Level == FindingLevel.Error);
    public bool IsComplete
    {
        get; init;
    }
}
public record Game(int Id, string Name);
public record Benchmark(int Id, int GameId, string Game, int GpuId, string Gpu, string TestCpu,
    int Width, int Height, string Preset, string RayTracing, string Upscaling, decimal AverageFps,
    decimal? LowFps, string SourceTitle, string SourceUrl, DateTime TestDate, string Notes)
{
    public string Resolution => $"{Width} × {Height}";
    public string FpsText => AverageFps.ToString("0.#", CultureInfo.InvariantCulture);
}
public record SavedBuild(int Id, string Name, string Client, string Parts, decimal Price, DateTime CreatedAt);

public static class Money
{
    public static string Format(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";
}

public sealed class BusinessException(string message) : Exception(message);

public static class OrderRules
{
    public static readonly string[] Statuses = ["Принят", "Диагностика", "Согласование", "Ожидание деталей", "В ремонте", "Готов", "Выдан", "Отменен"];
    public static readonly string[] Roles = ["Администратор", "Приемщик", "Мастер"];

    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["Принят"] = ["Диагностика", "Отменен"],
        ["Диагностика"] = ["Согласование", "Отменен"],
        ["Согласование"] = ["Ожидание деталей", "В ремонте", "Отменен"],
        ["Ожидание деталей"] = ["В ремонте", "Отменен"],
        ["В ремонте"] = ["Готов"],
        ["Готов"] = ["В ремонте", "Выдан"],
        ["Выдан"] = [],
        ["Отменен"] = []
    };

    public static IReadOnlyList<string> NextStatuses(string status) => Transitions.GetValueOrDefault(status) ?? [];
    public static void CheckTransition(Staff actor, RepairOrder order, string next, string diagnosis)
    {
        CheckOrderAccess(actor, order);
        if (!NextStatuses(order.Status).Contains(next))
            throw new BusinessException("Такой переход статуса недоступен.");
        if (next is "Выдан" or "Отменен" && !actor.CanReceive)
            throw new BusinessException("Выдачу и отмену оформляет приемщик или администратор.");
        if (next is "Согласование" or "Готов" && string.IsNullOrWhiteSpace(diagnosis))
            throw new BusinessException("Сначала укажите результат диагностики.");
        if (next == "Выдан" && order.Paid < order.Total)
            throw new BusinessException("Перед выдачей необходимо полностью оплатить заказ.");
        if (next == "Отменен" && order.Paid > 0)
            throw new BusinessException("Сначала оформите возврат оплаты.");
    }

    public static void CheckOrderAccess(Staff actor, RepairOrder order)
    {
        if (actor.Role == "Мастер" && order.TechnicianId != actor.Id)
            throw new BusinessException("Мастер может изменять только назначенные ему заказы.");
        if (order.Status is "Выдан" or "Отменен")
            throw new BusinessException("Заказ закрыт. Изменения недоступны.");
    }

    public static void Validate(NewOrder order)
    {
        if (order.ClientId <= 0)
            throw new BusinessException("Выберите клиента.");
        Required(order.Device, "Укажите модель устройства.", 160);
        Required(order.Complaint, "Опишите неисправность.", 2000);
        Required(order.Condition, "Укажите состояние и комплектность.", 2000);
        if (order.DeviceType is not ("ПК" or "Ноутбук"))
            throw new BusinessException("Выберите тип устройства.");
        if (order.DueAt.Date < DateTime.Today)
            throw new BusinessException("Срок не может быть раньше сегодняшнего дня.");
        if (order.Serial.Length > 120)
            throw new BusinessException("Серийный номер: не более 120 символов.");
    }

    public static void ValidateClient(string name, string phone, string email)
    {
        Required(name, "Укажите имя клиента.", 120);
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
            throw new BusinessException("Телефон должен содержать от 10 до 15 цифр.");
        if (email.Length > 180 || email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new BusinessException("Проверьте электронную почту.");
    }

    public static void Required(string value, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessException(message);
        if (value.Length > maxLength)
            throw new BusinessException($"Допустимо не более {maxLength} символов.");
    }
}
