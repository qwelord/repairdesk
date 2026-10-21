using RepairDesk.Core;
using RepairDesk.Data;
using MySqlConnector;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
void Reject(Action action, string name) { try { action(); } catch (BusinessException) { Check(true, name); return; } throw new Exception("FAIL: " + name); }
async Task RejectAsync(Func<Task> action, string name) { try { await action(); } catch (BusinessException) { Check(true, name); return; } throw new Exception("FAIL: " + name); }

var hash = Passwords.Hash("correct-password-2026");
Check(Passwords.Verify("correct-password-2026", hash), "Password hash verifies");
Check(!Passwords.Verify("wrong", hash), "Wrong password rejected");
Check(!Passwords.Verify("x", "broken"), "Malformed hash rejected");
Check(Passwords.Hash("same") != Passwords.Hash("same"), "Unique random password salts");
Reject(() => OrderRules.ValidateClient("A", "12", ""), "Short phone rejected");
Reject(() => OrderRules.ValidateClient("A", "79000000000", "bad-email"), "Invalid email rejected");
OrderRules.ValidateClient("Анна", "+7 (900) 000-00-00", "anna@example.test");
Check(true, "Valid client accepted");
Reject(() => OrderRules.Validate(new(1, "ПК", "Model", "", "Fault", "Case", DateTime.Today.AddDays(-1), null)), "Past deadline rejected");
Reject(() => OrderRules.Validate(new(0, "ПК", "Model", "", "Fault", "Case", DateTime.Today, null)), "Missing client rejected");
var staff = new Staff(1, "Admin", "admin", "Администратор");
var master = new Staff(3, "Master", "master", "Мастер");
var order = new RepairOrder(1, 1, "Client", "Phone", "ПК", "Model", "", "Fault", "Case", "Diagnosis", "Готов", 3, "Master", DateTime.Now, DateTime.Today, 100, 0, 1);
Reject(() => OrderRules.CheckTransition(staff, order, "Выдан", "Diagnosis"), "Unpaid issue rejected");
OrderRules.CheckTransition(staff, order with { Paid = 100 }, "Выдан", "Diagnosis");
Check(true, "Paid order can be issued");
Reject(() => OrderRules.CheckTransition(master, order with { Paid = 100 }, "Выдан", "Diagnosis"), "Master cannot issue device");
Reject(() => OrderRules.CheckTransition(staff, order with { Status = "Принят" }, "Готов", "Diagnosis"), "Invalid status jump rejected");
Reject(() => OrderRules.CheckOrderAccess(master, order with { TechnicianId = 9 }), "Other technician order protected");
Reject(() => OrderRules.CheckTransition(staff, order with { Status = "Диагностика" }, "Согласование", ""), "Diagnosis required");
Part Make(int id, string category) => new(id, category, "Brand", category, 100, 5, 1, "AM4", "DDR4", 100, 32, 200, "Micro-ATX", "Micro-ATX|ATX", 300, 128, "Процессор", "1×8-pin", "https://example.test", "");
var cpu = Make(1, "Процессор");
var board = Make(2, "Материнская плата");
var gpu = Make(3, "Видеокарта");
var ram = Make(4, "Память");
var psu = Make(5, "Блок питания") with { Wattage = 650 };
var chassis = Make(6, "Корпус");
var storage = Make(7, "Накопитель");
var build = new BuildSelection(cpu, board, gpu, ram, psu, chassis, storage);
Check(!Compatibility.Check(build).HasErrors, "Compatible assembly passes");
Check(Compatibility.Check(build with { Board = board with { Socket = "AM5" } }).HasErrors, "Wrong CPU socket rejected");
Check(Compatibility.Check(build with { Ram = ram with { MemoryType = "DDR5" } }).HasErrors, "Wrong RAM generation rejected");
Check(Compatibility.Check(build with { Ram = ram with { Capacity = 256 } }).HasErrors, "RAM capacity limit enforced");
Check(Compatibility.Check(build with { Gpu = gpu with { LengthMm = 500 } }).HasErrors, "GPU length checked");
Check(Compatibility.Check(build with { Psu = psu with { Wattage = 100 } }).HasErrors, "PSU headroom checked");
Check(Compatibility.Check(build with { Psu = psu with { PowerConnectors = "12VHPWR" } }).HasErrors, "GPU power connector checked");
Check(Compatibility.Check(build with { Case = chassis with { SupportedForms = "Mini-ITX" } }).HasErrors, "Case form factor checked");
Check(!Compatibility.Check(build with { Cpu = null }).IsComplete, "Incomplete build identified");
Check(Compatibility.Check(build with { Gpu = gpu with { Stock = 0 } }).Findings.Any(finding => finding.Title == "Нет на складе"), "Stock warning shown");

if (args.Contains("--integration"))
{
    var password = Environment.GetEnvironmentVariable("REPAIRDESK_TEST_PASSWORD") ?? throw new Exception("Set REPAIRDESK_TEST_PASSWORD");
    var settings = new DatabaseSettings { Port = uint.Parse(Environment.GetEnvironmentVariable("REPAIRDESK_TEST_PORT") ?? "33367"), User = "root", Password = password };
    if (args.Contains("--initialize"))
        await Provisioning.InitializeAsync(settings, Path.Combine(Directory.GetCurrentDirectory(), "database"));
    var repo = new Repository(settings);
    await repo.TestConnectionAsync();
    await repo.LoginAsync("admin", "RepairDesk2026!");
    await RejectAsync(() => repo.LoginAsync("admin", "wrong"), "DB authentication rejects wrong password");
    var clients = await repo.ClientsAsync();
    var parts = await repo.PartsAsync();
    var clientId = await repo.SaveClientAsync(null, "Проверка O'Connor", "79001112233", "qa@example.test", "Интеграционная проверка параметризованных запросов");
    Check((await repo.ClientsAsync()).Any(client => client.Id == clientId && client.Name.Contains("O'Connor")), "SQL parameters preserve apostrophe");
    var id = await repo.CreateOrderAsync(new(clientId, "ПК", "QA workstation", "QA-001", "Fault", "Case", DateTime.Today.AddDays(2), 3));
    async Task<RepairOrder> Current() => (await repo.OrdersAsync()).Single(item => item.Id == id);
    var snapshot = await Current();
    var originalStock = parts.Single(item => item.Id == 12).Stock;
    await repo.AddLineAsync(snapshot, 12, null, 1);
    Check((await repo.PartsAsync()).Single(item => item.Id == 12).Stock == originalStock - 1, "Part allocation decreases stock");
    await RejectAsync(() => repo.AddLineAsync(snapshot, 12, null, 1), "Stale order edit rejected");
    snapshot = await Current();
    var beforeLines = (await repo.LinesAsync(id)).Count;
    await RejectAsync(() => repo.AddLineAsync(snapshot, 12, null, 100), "Insufficient stock rejected");
    Check((await repo.LinesAsync(id)).Count == beforeLines, "Failed allocation rolled back");
    Check((await repo.PartsAsync()).Single(item => item.Id == 12).Stock == originalStock - 1, "Failed allocation preserves stock");
    var line = (await repo.LinesAsync(id)).Single();
    await repo.RemoveLineAsync(snapshot, line);
    Check((await repo.PartsAsync()).Single(item => item.Id == 12).Stock == originalStock, "Removing line returns stock");
    snapshot = await Current();
    await repo.AddLineAsync(snapshot, null, (await repo.ServicesAsync()).First(), 1);
    snapshot = await Current();
    await RejectAsync(() => repo.PayAsync(snapshot, snapshot.Total + 1), "Overpayment rejected");
    await repo.PayAsync(snapshot, snapshot.Total);
    snapshot = await Current();
    Check(snapshot.Paid == snapshot.Total, "Payment stored");
    await repo.PayAsync(snapshot, -snapshot.Paid);
    snapshot = await Current();
    Check(snapshot.Paid == 0, "Refund stored");
    foreach (var status in new[] { "Диагностика", "Согласование", "В ремонте", "Готов" })
    {
        snapshot = await Current();
        await repo.UpdateOrderAsync(snapshot, "QA diagnosis", snapshot.DueAt, 3, status);
    }
    snapshot = await Current();
    await repo.PayAsync(snapshot, snapshot.Total);
    snapshot = await Current();
    await repo.UpdateOrderAsync(snapshot, snapshot.Diagnosis, snapshot.DueAt, 3, "Выдан");
    Check((await Current()).Status == "Выдан", "Full repair lifecycle completes");
    await RejectAsync(() => repo.AddLineAsync(Current().GetAwaiter().GetResult(), 12, null, 1), "Closed order immutable");
    var masterRepo = new Repository(settings);
    await masterRepo.LoginAsync("master", "RepairDesk2026!");
    Check((await masterRepo.OrdersAsync()).All(item => item.TechnicianId == 3), "Master sees only assigned orders");
    await RejectAsync(() => masterRepo.AdjustStockAsync(12, 1, "Forbidden"), "Master cannot change warehouse");
    var validBuild = new BuildSelection(parts.Single(item => item.Id == 2), parts.Single(item => item.Id == 5), parts.Single(item => item.Id == 7), parts.Single(item => item.Id == 11), parts.Single(item => item.Id == 14), parts.Single(item => item.Id == 16), parts.Single(item => item.Id == 12));
    var stockBefore = await repo.PartsAsync();
    var buildOrderId = await repo.CreateBuildOrderAsync(clientId, validBuild, "QA assembly");
    Check((await repo.LinesAsync(buildOrderId)).Count == 8, "Assembly order includes seven parts and labor");
    await repo.SaveBuildAsync("QA saved build", clientId, validBuild);
    Check((await repo.BuildsAsync()).Any(item => item.Name == "QA saved build"), "Build proposal saved");
    var cancel = (await repo.OrdersAsync()).Single(item => item.Id == buildOrderId);
    await repo.UpdateOrderAsync(cancel, "", cancel.DueAt, null, "Отменен");
    var stockAfter = await repo.PartsAsync();
    Check(validBuild.Parts.All(part => stockBefore.Single(item => item.Id == part.Id).Stock == stockAfter.Single(item => item.Id == part.Id).Stock), "Cancellation returns all assembly parts atomically");
    await RejectAsync(() => repo.CreateBuildOrderAsync(clientId, validBuild with { Gpu = parts.Single(item => item.Id == 9) }, "Unavailable build"), "Assembly with unavailable part rejected");
    Check((await repo.PartsAsync()).Single(item => item.Id == 2).Stock == stockAfter.Single(item => item.Id == 2).Stock, "Failed multi-part assembly rolls back earlier allocations");
    Check((await repo.EventsAsync(id)).Count >= 8, "Order audit trail retained");
    Check((await repo.BenchmarksAsync(7, 1920)).Count >= 5, "Verified benchmark data available offline");
    await RejectAsync(() => Provisioning.InitializeAsync(settings, Path.Combine(Directory.GetCurrentDirectory(), "database")), "Initialization protects existing database");
    var concurrentId = await repo.CreateOrderAsync(new(clientId, "ПК", "Concurrent QA", "", "Fault", "Case", DateTime.Today.AddDays(1), 3));
    var concurrentSnapshot = (await repo.OrdersAsync()).Single(item => item.Id == concurrentId);
    var beforeConcurrent = (await repo.PartsAsync()).Single(item => item.Id == 12).Stock;
    async Task<bool> Allocate()
    {
        try
        {
            await repo.AddLineAsync(concurrentSnapshot, 12, null, 1);
            return true;
        }
        catch (BusinessException) { return false; }
    }
    var concurrentResults = await Task.WhenAll(Allocate(), Allocate());
    Check(concurrentResults.Count(success => success) == 1, "Concurrent edits allow exactly one writer");
    Check((await repo.PartsAsync()).Single(item => item.Id == 12).Stock == beforeConcurrent - 1, "Concurrent edits do not double-debit inventory");
    var concurrentLatest = (await repo.OrdersAsync()).Single(item => item.Id == concurrentId);
    await repo.UpdateOrderAsync(concurrentLatest, "", concurrentLatest.DueAt, 3, "Отменен");
    await RejectAsync(() => repo.SavePartAsync(parts[0] with { Id = 0, Name = "Invalid board", Category = "Материнская плата", Socket = "" }, true), "Incomplete technical specification rejected");
    await RejectAsync(() => masterRepo.CreateOrderAsync(new(clientId, "ПК", "Forbidden", "", "Fault", "Case", DateTime.Today, null)), "Master cannot receive equipment");
    var output = Environment.GetEnvironmentVariable("REPAIRDESK_TEST_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "RepairDesk-QA");
    Directory.CreateDirectory(output);
    await repo.BackupAsync(Path.Combine(output, "backup.sql"), Path.Combine(Directory.GetCurrentDirectory(), "database", "schema.sql"));
    Check(File.ReadAllText(Path.Combine(output, "backup.sql")).Contains("INSERT INTO `orders`"), "Consistent SQL backup generated");
    await using var connection = new MySqlConnection(settings.ConnectionString);
    await connection.OpenAsync();
    var backup = await File.ReadAllTextAsync(Path.Combine(output, "backup.sql"));
    backup = backup.Replace("CREATE DATABASE IF NOT EXISTS repairdesk ", "CREATE DATABASE IF NOT EXISTS repairdesk_qa_restore ").Replace("USE repairdesk;", "USE repairdesk_qa_restore;");
    await using (var restore = new MySqlCommand(backup, connection) { CommandTimeout = 60 })
        await restore.ExecuteNonQueryAsync();
    await using (var verify = new MySqlCommand("SELECT COUNT(*) FROM repairdesk_qa_restore.orders", connection))
        Check(Convert.ToInt32(await verify.ExecuteScalarAsync()) == (await repo.OrdersAsync()).Count, "Backup restores into independent database");
}
Console.WriteLine($"RESULT: {passed} passed; 0 failed");
