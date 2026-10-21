using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using MySqlConnector;
using RepairDesk.Core;
using RepairDesk.Data;

namespace RepairDesk.Desktop;

internal sealed class PortableServer
{
    private sealed record State(string RootPassword, string AppPassword, uint Port);
    private readonly string serverFolder;
    private readonly string stateFolder;
    private readonly string baseDirectory;
    public PortableServer(string? baseDirectory = null, string? stateFolder = null)
    {
        this.baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        serverFolder = Path.Combine(this.baseDirectory, "server");
        this.stateFolder = stateFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepairDesk", "portable");
    }
    private DatabaseSettings? rootSettings;
    private Process? server;
    public static bool Available => File.Exists(Path.Combine(AppContext.BaseDirectory, "server", "bin", "mariadbd.exe"));

    public async Task<DatabaseSettings> StartAsync()
    {
        Directory.CreateDirectory(stateFolder);
        var statePath = Path.Combine(stateFolder, "server.local.json");
        State state;
        if (File.Exists(statePath))
            state = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(statePath)) ?? throw new BusinessException("Поврежден файл настроек переносимой базы.");
        else
        {
            uint port = 33366;
            for (; port < 33386; port++)
            {
                var listener = new TcpListener(IPAddress.Loopback, (int)port);
                try
                {
                    listener.Start();
                    listener.Stop();
                    break;
                }
                catch (SocketException) { listener.Stop(); }
            }
            if (port == 33386)
                throw new BusinessException("Не найден свободный локальный порт для базы данных.");
            state = new(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), port);
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));
        }
        var data = Path.Combine(stateFolder, "data");
        rootSettings = new()
        {
            Port = state.Port,
            User = "root",
            Password = state.RootPassword
        };
        if (!Directory.Exists(Path.Combine(data, "mysql")))
        {
            var initialize = new ProcessStartInfo(Path.Combine(serverFolder, "bin", "mariadb-install-db.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            initialize.ArgumentList.Add("--datadir=" + data);
            initialize.ArgumentList.Add("--password=" + state.RootPassword);
            initialize.ArgumentList.Add("--port=" + state.Port);
            using var process = Process.Start(initialize) ?? throw new BusinessException("Не удалось запустить подготовку MariaDB.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                Diagnostics.Write(new IOException(output + error));
                throw new BusinessException("MariaDB не удалось подготовить файлы данных. Проверьте доступ к папке профиля и наличие библиотек Visual C++ из комплекта.");
            }
        }
        if (!await IsOurServerAsync(data))
        {
            var info = new ProcessStartInfo(Path.Combine(serverFolder, "bin", "mariadbd.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in new[] { "--no-defaults", "--basedir=" + serverFolder, "--datadir=" + data, "--port=" + state.Port, "--bind-address=127.0.0.1", "--skip-name-resolve", "--innodb-buffer-pool-size=64M", "--max-connections=25", "--log-error=" + Path.Combine(stateFolder, "server.log") })
                info.ArgumentList.Add(argument);
            server = Process.Start(info) ?? throw new BusinessException("Не удалось запустить MariaDB.");
            var ready = false;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (server.HasExited)
                    break;
                await Task.Delay(250);
                if (await IsOurServerAsync(data))
                {
                    ready = true;
                    break;
                }
            }
            if (!ready)
                throw new BusinessException("MariaDB не запустилась. Подробности: %LOCALAPPDATA%\\RepairDesk\\portable\\server.log. Возможно, порт занят или запуск программ запрещен политикой учебного ПК.");
        }
        var builder = new MySqlConnectionStringBuilder(rootSettings.ConnectionString) { Database = "", AllowUserVariables = true };
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var exists = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='repairdesk' AND table_name='schema_info'", connection);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0)
                await Provisioning.InitializeAsync(rootSettings, Path.Combine(baseDirectory, "database"));
            await using var account = new MySqlCommand("CREATE USER IF NOT EXISTS 'repairdesk_app'@'127.0.0.1' IDENTIFIED BY @password; GRANT SELECT,INSERT,UPDATE,DELETE ON repairdesk.* TO 'repairdesk_app'@'127.0.0.1';", connection);
            account.Parameters.AddWithValue("password", state.AppPassword);
            await account.ExecuteNonQueryAsync();
        }
        return new()
        {
            Port = state.Port,
            User = "repairdesk_app",
            Password = state.AppPassword
        };
    }

    private async Task<bool> IsOurServerAsync(string data)
    {
        if (rootSettings == null)
            return false;
        var builder = new MySqlConnectionStringBuilder(rootSettings.ConnectionString) { Database = "", ConnectionTimeout = 1 };
        try
        {
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand("SELECT @@datadir", connection);
            var actual = Convert.ToString(await command.ExecuteScalarAsync());
            if (!string.Equals(Path.GetFullPath(actual ?? "").TrimEnd('\\', '/'), Path.GetFullPath(data).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("Локальный порт занят другой базой. Ее данные не изменялись.");
            return true;
        }
        catch (MySqlException) { return false; }
    }

    public void Stop()
    {
        if (rootSettings == null)
            return;
        try
        {
            var builder = new MySqlConnectionStringBuilder(rootSettings.ConnectionString) { Database = "", ConnectionTimeout = 2 };
            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = new MySqlCommand("SHUTDOWN", connection) { CommandTimeout = 3 };
            command.ExecuteNonQuery();
            server?.WaitForExit(5000);
        }
        catch (MySqlException) { }
        finally { server?.Dispose(); }
    }
}
