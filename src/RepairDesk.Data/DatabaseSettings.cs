using System.Text.Json;
using MySqlConnector;

namespace RepairDesk.Data;

public sealed record DatabaseSettings
{
    public string Server { get; init; } = "127.0.0.1";
    public uint Port { get; init; } = 3306;
    public string Database { get; init; } = "repairdesk";
    public string User { get; init; } = "repairdesk";
    public string Password { get; init; } = "";

    public string ConnectionString => new MySqlConnectionStringBuilder
    {
        Server = Server,
        Port = Port,
        Database = Database,
        UserID = User,
        Password = Password,
        ConnectionTimeout = 5,
        DefaultCommandTimeout = 15,
        CharacterSet = "utf8mb4",
        SslMode = MySqlSslMode.Preferred
    }.ConnectionString;

    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepairDesk", "connection.local.json");
    public static DatabaseSettings Load()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "connection.local.json");
        var path = File.Exists(portable) ? portable : SettingsPath;
        return File.Exists(path) ? JsonSerializer.Deserialize<DatabaseSettings>(File.ReadAllText(path)) ?? new() : new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
