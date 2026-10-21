using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepairDesk.Api;
using RepairDesk.Core;
using RepairDesk.Data;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var sample = new Benchmark(1, 1, "Test", 7, "GPU", "CPU", 1920, 1080, "Ultra", "Off", "Off", 100, null, "Source", "https://example.test", DateTime.Today, "");
var game = new Game(1, "Test");
Check(BuildEngine.Predict(game, 7, 1920, [sample]).Fps == 100, "Exact published FPS preserved");
var inferred = BuildEngine.Predict(game, 7, 2560, [sample, sample with { Id = 2, Width = 3840, Height = 2160, AverageFps = 25 }]);
Check(inferred.Fps is > 25 and < 100 && inferred.Lower < inferred.Upper && inferred.Kind != "Замер из источника", "Resolution interpolation labelled and bounded");
Check(BuildEngine.Predict(new(999, "Unknown"), 7, 1920, [sample]).Fps == null, "Unknown game never gets fabricated FPS");
Check(BuildEngine.Predict(game, null, 1920, [sample]).Fps == null, "Missing GPU never gets FPS");
var mismatched = BuildEngine.Predict(game, 7, 2560, [sample, sample with { Width = 3840, Preset = "Low" }]);
Check(mismatched.Fps == null, "Different presets never interpolated together");
var path = Environment.GetEnvironmentVariable("REPAIRDESK_TEST_CONNECTION");
if (path == null)
{
    Console.WriteLine($"{passed} model checks passed. Set REPAIRDESK_TEST_CONNECTION for HTTP integration tests.");
    return;
}
var settings = JsonSerializer.Deserialize<DatabaseSettings>(await File.ReadAllTextAsync(path)) ?? throw new Exception("Invalid test settings");
if (args.Contains("--initialize")) await Provisioning.InitializeAsync(settings, Path.Combine(AppContext.BaseDirectory, "database"));
await using var app = ApiHost.Create(settings, Path.Combine(AppContext.BaseDirectory, "wwwroot"));
await app.StartAsync();
using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
http.DefaultRequestHeaders.Add("X-RepairDesk-Client", "studio");
async Task<HttpResponseMessage> Send(string method, string route, object? body = null) => await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api" + route) { Content = body == null ? null : JsonContent.Create(body) });
async Task<JsonElement> Json(string route)
{
    using var response = await Send("GET", route);
    response.EnsureSuccessStatusCode();
    return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
}
async Task Success(string method, string route, object? body, string name)
{
    using var response = await Send(method, route, body);
    if (!response.IsSuccessStatusCode)
        throw new Exception(name + ": " + await response.Content.ReadAsStringAsync());
    Check(true, name);
}
async Task Status(string method, string route, object? body, HttpStatusCode expected, string name)
{
    using var response = await Send(method, route, body);
    Check(response.StatusCode == expected, name + " (" + (int)response.StatusCode + ")");
}
await Status("GET", "/clients", null, HttpStatusCode.Unauthorized, "Anonymous CRUD rejected");
using (var page = await http.GetAsync("/")) Check(page.IsSuccessStatusCode && page.Headers.Contains("Content-Security-Policy"), "Frontend served with CSP");
await Status("POST", "/auth/login", new { login = "admin", password = "wrong" }, HttpStatusCode.Unauthorized, "Invalid password rejected");
await Success("POST", "/auth/login", new { login = "admin", password = "RepairDesk2026!" }, "Admin cookie login");
Check((await Json("/auth/me")).GetProperty("login").GetString() == "admin", "Session identity returned");
http.DefaultRequestHeaders.Remove("X-RepairDesk-Client");
await Status("POST", "/clients", new { name = "Blocked" }, HttpStatusCode.Forbidden, "CSRF custom header required");
http.DefaultRequestHeaders.Add("X-RepairDesk-Client", "studio");
http.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
await Status("POST", "/clients", new { name = "Blocked" }, HttpStatusCode.Forbidden, "Foreign origin rejected");
http.DefaultRequestHeaders.Remove("Origin");
await Status("POST", "/clients", new { name = (string?)null, phone = "79001112233", email = "", notes = "" }, HttpStatusCode.BadRequest, "Null required field rejected");
await Status("POST", "/clients", new { name = "Test", phone = "letters79001112233", email = "", notes = "" }, HttpStatusCode.Conflict, "Alphabetic phone rejected");
using var created = await Send("POST", "/clients", new { name = "API O'Connor <script>", phone = "79001112233", email = "qa@example.test", notes = "Integration" });
Check(created.StatusCode == HttpStatusCode.Created && created.Headers.Location != null, "Client create returns 201 and Location");
var clientId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
await Success("PUT", "/clients/" + clientId, new { name = "API O'Connor", phone = "79001112233", email = "", notes = "Edited" }, "Client update");
Check((await Json("/clients/" + clientId)).GetProperty("notes").GetString() == "Edited", "Client read persisted update");
using var createdOrder = await Send("POST", "/orders", new NewOrder(clientId, "ПК", "API workstation", "API-01", "Fault", "Case", DateTime.Today.AddDays(3), 3));
createdOrder.EnsureSuccessStatusCode();
var orderId = (await createdOrder.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
string OrderRoute() => "/orders/" + orderId;
async Task<int> Version() => (await Json(OrderRoute())).GetProperty("order").GetProperty("version").GetInt32();
await Status("DELETE", "/clients/" + clientId, null, HttpStatusCode.Conflict, "Referenced client protected");
var version = await Version();
await Success("POST", OrderRoute() + "/notes", new { version, message = "Note through REST" }, "Order note persisted");
await Status("POST", OrderRoute() + "/notes", new { version, message = "Stale" }, HttpStatusCode.Conflict, "Optimistic concurrency rejected stale update");
await Success("PUT", OrderRoute() + "/diagnostics/storage", new { version = await Version(), result = "Проблема", note = "SMART: reallocated sectors" }, "Diagnostic checklist persisted");
var detail = await Json(OrderRoute());
Check(detail.GetProperty("diagnostics")[0].GetProperty("result").GetString() == "Проблема", "Diagnostic result read through API");
var partsBefore = await Json("/parts");
var stockBefore = partsBefore.EnumerateArray().Single(part => part.GetProperty("id").GetInt32() == 12).GetProperty("stock").GetInt32();
using var estimateResponse = await Send("POST", OrderRoute() + "/estimates", new { version = await Version(), note = "Price snapshot", items = new[] { new EstimateInput(12, null, 1), new EstimateInput(null, 1, 1) } });
estimateResponse.EnsureSuccessStatusCode();
var estimateId = (await estimateResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
var estimateRoute = OrderRoute() + "/estimates/" + estimateId;
Check((await Json("/parts")).EnumerateArray().Single(part => part.GetProperty("id").GetInt32() == 12).GetProperty("stock").GetInt32() == stockBefore, "Draft estimate never reserves stock");
await Status("PUT", estimateRoute, new { version = await Version(), status = "Применена", approvedBy = "" }, HttpStatusCode.Conflict, "Unapproved estimate cannot be applied");
await Success("PUT", estimateRoute, new { version = await Version(), status = "Согласована", approvedBy = "QA client" }, "Estimate client decision recorded");
await Success("PUT", estimateRoute, new { version = await Version(), status = "Применена", approvedBy = "" }, "Approved estimate applied atomically");
await Status("PUT", estimateRoute, new { version = await Version(), status = "Применена", approvedBy = "" }, HttpStatusCode.Conflict, "Estimate cannot be applied twice");
detail = await Json(OrderRoute());
Check(detail.GetProperty("lines").GetArrayLength() == 2, "Estimate creates exactly two order lines");
Check((await Json("/parts")).EnumerateArray().Single(part => part.GetProperty("id").GetInt32() == 12).GetProperty("stock").GetInt32() == stockBefore - 1, "Applied estimate decrements stock once");
await Status("POST", OrderRoute() + "/payments", new { version = await Version(), amount = 9999999 }, HttpStatusCode.Conflict, "Overpayment rejected");
using var evaluation = await Send("POST", "/builds/evaluate", new BuildRequest([1, 4, 7, 10, 14, 16, 12]));
evaluation.EnsureSuccessStatusCode();
var buildResult = await evaluation.Content.ReadFromJsonAsync<JsonElement>();
Check(!buildResult.GetProperty("compatibility").GetProperty("hasErrors").GetBoolean(), "Compatible build evaluated through REST");
Check(buildResult.GetProperty("performance").EnumerateArray().Any(item => item.GetProperty("kind").GetString() == "Замер из источника"), "FPS provenance returned through REST");
await Status("POST", "/builds/evaluate", new BuildRequest([1, 2]), HttpStatusCode.Conflict, "Duplicate component category rejected");
await Status("POST", "/builds/evaluate", new BuildRequest([7], Width: 2000), HttpStatusCode.Conflict, "Unsupported resolution rejected");
await Success("POST", "/builds/recommend", new BudgetRequest(150000), "Budget recommender executes");
using (var backup = await Send("GET", "/exports/backup.sql")) Check(backup.IsSuccessStatusCode && (await backup.Content.ReadAsStringAsync()).Contains("CREATE TABLE diagnostics"), "Backup contains Studio tables");
await Success("DELETE", OrderRoute() + "?version=" + await Version(), null, "DELETE order cancels without destroying history");
Check((await Json("/parts")).EnumerateArray().Single(part => part.GetProperty("id").GetInt32() == 12).GetProperty("stock").GetInt32() == stockBefore, "Cancellation returns estimate stock");
await Success("POST", "/auth/logout", null, "Logout clears session");
await Status("GET", "/orders", null, HttpStatusCode.Unauthorized, "Logged out session cannot access data");
await Success("POST", "/auth/login", new { login = "master", password = "RepairDesk2026!" }, "Master login");
await Status("POST", "/clients", new { name = "Denied", phone = "79001112233", email = "", notes = "" }, HttpStatusCode.Conflict, "Master cannot create clients");
await Status("GET", "/exports/backup.sql", null, HttpStatusCode.Conflict, "Master cannot download database backup");
Check((await Json("/orders")).EnumerateArray().All(order => order.GetProperty("technicianId").GetInt32() == 3), "Master sees only assigned orders");
await app.StopAsync();
Console.WriteLine($"{passed} checks passed.");
