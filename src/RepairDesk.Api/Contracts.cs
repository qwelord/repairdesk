using RepairDesk.Core;

namespace RepairDesk.Api;

public record LoginRequest(string Login, string Password);
public record ClientRequest(string Name, string Phone, string Email, string Notes);
public record ServiceRequest(string Name, decimal Price);
public record OrderUpdate(int Version, string Diagnosis, DateTime DueAt, int? TechnicianId, string Status);
public record LineRequest(int Version, int? PartId, int? ServiceId, int Quantity);
public record PaymentRequest(int Version, decimal Amount);
public record NoteRequest(int Version, string Message);
public record StockRequest(int Quantity, string Reason);
public record EstimateRequest(int Version, string Note, List<EstimateInput> Items);
public record DecisionRequest(int Version, string Status, string ApprovedBy);
public record DiagnosticRequest(int Version, string Result, string Note);
public record PasswordRequest(string OldPassword, string NewPassword);
public record StaffRequest(string Name, string Login, string Password, string Role);
public record BuildRequest(List<int> PartIds, string Name = "Новая конфигурация", int? ClientId = null, int Width = 1920);
public record CompareRequest(List<int> Current, List<int> Proposed, int Width);
public record BudgetRequest(decimal Budget, int Width = 1920, bool InStockOnly = true);
