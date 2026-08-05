using System.Reflection;
using Npgsql;

namespace Trip.Storage;

/// <summary>
/// Connections and migrations. Migrations are plain SQL because the storage constraints they create
/// are named in <c>design/</c> as the mechanism behind two critical requirements — a migration that
/// a reader cannot check against that claim would defeat the point.
/// </summary>
public sealed class Database(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellation = default)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellation);
        return connection;
    }

    public async Task MigrateAsync(CancellationToken cancellation = default)
    {
        await using var connection = await OpenAsync(cancellation);
        foreach (var script in Scripts())
        {
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync(cancellation);
        }
    }

    private static IEnumerable<string> Scripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return reader.ReadToEnd();
        }
    }
}
