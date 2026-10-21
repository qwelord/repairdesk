using MySqlConnector;
using RepairDesk.Core;

namespace RepairDesk.Data;

public static class Provisioning
{
    public static async Task EnsureCatalogAsync(DatabaseSettings settings, string sqlDirectory)
    {
        await using var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        var sql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "catalog.sql"));
        await using var command = new MySqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    public static async Task InitializeAsync(DatabaseSettings settings, string sqlDirectory)
    {
        if (settings.Database != "repairdesk")
            throw new BusinessException("Скрипты создают базу repairdesk. Укажите это имя в настройках.");
        var scripts = new List<string>();
        foreach (var filename in new[] { "schema.sql", "demo.sql", "benchmarks.sql", "catalog.sql" })
        {
            var path = Path.Combine(sqlDirectory, filename);
            if (!File.Exists(path))
                throw new BusinessException($"Не найден файл {filename}. Используйте полную папку приложения.");
            scripts.Add(await File.ReadAllTextAsync(path));
        }
        var builder = new MySqlConnectionStringBuilder(settings.ConnectionString) { Database = "", AllowUserVariables = true };
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using (var exists = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='repairdesk'", connection))
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) > 0)
                throw new BusinessException("База repairdesk уже содержит таблицы. Инициализация отменена, данные сохранены. Используйте проверку подключения.");
        foreach (var sql in scripts)
        {
            await using var command = new MySqlCommand(sql, connection) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync();
        }
    }
}
