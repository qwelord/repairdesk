using System.Text.Json;
using RepairDesk.Api;
using RepairDesk.Data;

var settingsPath = Environment.GetEnvironmentVariable("REPAIRDESK_CONNECTION") ?? Path.Combine(AppContext.BaseDirectory, "connection.local.json");
if (!File.Exists(settingsPath)) throw new InvalidOperationException("Укажите REPAIRDESK_CONNECTION: путь к connection.local.json с параметрами MySQL.");
var settings = JsonSerializer.Deserialize<DatabaseSettings>(await File.ReadAllTextAsync(settingsPath)) ?? throw new InvalidOperationException("Проверьте файл подключения.");
await new Repository(settings).TestConnectionAsync();
await Provisioning.EnsureCatalogAsync(settings, Path.Combine(AppContext.BaseDirectory, "database"));
var app = ApiHost.Create(settings, Path.Combine(AppContext.BaseDirectory, "wwwroot"), Environment.GetEnvironmentVariable("REPAIRDESK_URL") ?? "http://127.0.0.1:5180");
await app.RunAsync();
