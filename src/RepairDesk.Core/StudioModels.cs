namespace RepairDesk.Core;

public record Estimate(int Id, int OrderId, string Status, string Note, string ApprovedBy, DateTime CreatedAt, decimal Total);
public record EstimateLine(int Id, int? PartId, string Name, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}
public record EstimateInput(int? PartId, int? ServiceId, int Quantity);
public record Diagnostic(string TestKey, string Result, string Note, string Author, DateTime UpdatedAt);
public static class DiagnosticTests
{
    public static readonly IReadOnlyDictionary<string, string> Items = new Dictionary<string, string>
    {
        ["visual"] = "Осмотр корпуса и разъёмов",
        ["boot"] = "Загрузка и стабильность BIOS",
        ["memory"] = "Тест оперативной памяти",
        ["storage"] = "SMART и состояние накопителей",
        ["cpu"] = "Нагрузка и температура CPU",
        ["gpu"] = "Нагрузка и температура GPU",
        ["power"] = "Питание и зарядка",
        ["network"] = "Сеть и периферия",
        ["final"] = "Контроль после ремонта"
    };
}
