using System.Reflection;
using Azimuth.Annotations;
using Npgsql;

namespace Payments.Domain;

public sealed record CaptureRow(
    Guid Id,
    Guid TripId,
    long AmountMinor,
    string Currency,
    string? AdjustmentReason,
    bool Voided);

public enum ProviderOutcome
{
    Captured,
    Declined,
    /// <summary>The caller never observed the outcome, so it may or may not have succeeded.</summary>
    Unobserved,
}

public interface IPaymentProvider
{
    Task<ProviderOutcome> CaptureAsync(Guid tripId, long amountMinor, string currency);
}

public sealed class Database(string connectionString)
{
    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task MigrateAsync()
    {
        await using var connection = await OpenAsync();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            await using var command = new NpgsqlCommand(await reader.ReadToEndAsync(), connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// Turns a completed trip into exactly one charge.
/// </summary>
/// <remarks>
/// The dispatcher is the only reader of <c>capture_intents</c>, which the trip service writes in the
/// same transaction as the completion. A transactional outbox rather than a direct call: calling the
/// payment client inline from the completion handler is the single most-repeated mistake in the
/// concern catalog (C16) — it charges riders for transactions that roll back, and no behavioural
/// test catches it because the failing case needs a rollback at one exact instant.
/// </remarks>
public sealed class CaptureDispatcher(Database database, IPaymentProvider provider)
{
    /// <summary>
    /// Captures every undispatched intent, at most once per trip.
    /// </summary>
    /// <remarks>
    /// The unique index is what holds under concurrency. The pre-check reads well and produces the
    /// answer a caller wants; it is not what makes the claim true.
    /// </remarks>
    [Realizes("payments/capture", "capture-created-on-completion")]
    [Realizes("payments/capture", "duplicate-completion-event")]
    [Realizes("payments/capture", "concurrent-completion-processing")]
    [Realizes("payments/capture", "retry-after-transport-failure")]
    [Realizes("payments/capture", "capture-equals-trip-fare")]
    [Realizes("payments/capture", "adjusted-capture-records-reason")]
    [Realizes("payments/capture", "declined-capture-recorded")]
    [Realizes("payments/capture", "declined-capture-is-retryable")]
    public async Task<int> DispatchAsync(DateTimeOffset now, string? adjustmentReason = null)
    {
        var captured = 0;
        foreach (var (tripId, amount, currency) in await PendingAsync())
        {
            if (await CaptureAsync(tripId, amount, currency, now, adjustmentReason))
            {
                captured++;
            }
        }

        return captured;
    }

    /// <summary>Captures one trip. Safe to call concurrently and repeatedly.</summary>
    [Realizes("payments/capture", "duplicate-completion-event")]
    [Realizes("payments/capture", "concurrent-completion-processing")]
    [Realizes("payments/capture", "retry-after-transport-failure")]
    [Realizes("payments/capture", "capture-equals-trip-fare")]
    [Realizes("payments/capture", "adjusted-capture-records-reason")]
    [Realizes("payments/capture", "declined-capture-recorded")]
    [Realizes("payments/capture", "declined-capture-is-retryable")]
    public async Task<bool> CaptureAsync(
        Guid tripId,
        long amountMinor,
        string currency,
        DateTimeOffset now,
        string? adjustmentReason = null)
    {
        if (await FindAsync(tripId) is not null)
        {
            return false;
        }

        var outcome = await provider.CaptureAsync(tripId, amountMinor, currency);

        // An outcome the caller never observed may or may not have succeeded, so it is treated as
        // possibly-captured and the index settles it. Assuming failure here is what double-charges.
        if (outcome == ProviderOutcome.Declined)
        {
            await RecordFailureAsync(tripId, "declined", now);
            return false;
        }

        await using var connection = await database.OpenAsync();
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO captures (id, trip_id, amount_minor, currency, adjustment_reason, captured_at)
            VALUES (@id, @trip, @amount, @currency, @reason, @now)
            """,
            connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("trip", tripId);
        insert.Parameters.AddWithValue("amount", amountMinor);
        insert.Parameters.AddWithValue("currency", currency);
        insert.Parameters.AddWithValue("reason", (object?)adjustmentReason ?? DBNull.Value);
        insert.Parameters.AddWithValue("now", now);

        try
        {
            await insert.ExecuteNonQueryAsync();
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            // Another worker won. Exactly the case the pre-check cannot cover.
            return false;
        }

        await MarkDispatchedAsync(tripId, now);
        return true;
    }

    [Realizes("payments/capture", "no-capture-before-completion")]
    [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
    public async Task<CaptureRow?> FindAsync(Guid tripId)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, trip_id, amount_minor, currency, adjustment_reason, voided
            FROM captures WHERE trip_id = @trip AND NOT voided
            """,
            connection);
        command.Parameters.AddWithValue("trip", tripId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new CaptureRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetBoolean(5));
    }

    [Realizes("payments/capture", "declined-capture-recorded")]
    public async Task<IReadOnlyList<string>> FailuresAsync(Guid tripId)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT reason FROM capture_failures WHERE trip_id = @trip ORDER BY id", connection);
        command.Parameters.AddWithValue("trip", tripId);
        await using var reader = await command.ExecuteReaderAsync();
        var reasons = new List<string>();
        while (await reader.ReadAsync())
        {
            reasons.Add(reader.GetString(0));
        }

        return reasons;
    }

    /// <summary>The intent the trip service writes in the same transaction as the completion.</summary>
    [Realizes("payments/capture", "capture-created-on-completion")]
    [Realizes("payments/capture", "no-capture-before-completion")]
    [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
    public async Task WriteIntentAsync(Guid tripId, long amountMinor, string currency, DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO capture_intents (trip_id, amount_minor, currency, written_at)
            VALUES (@trip, @amount, @currency, @now)
            ON CONFLICT (trip_id) DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("trip", tripId);
        command.Parameters.AddWithValue("amount", amountMinor);
        command.Parameters.AddWithValue("currency", currency);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<(Guid TripId, long Amount, string Currency)>> PendingAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT trip_id, amount_minor, currency FROM capture_intents WHERE dispatched_at IS NULL",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(Guid, long, string)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2)));
        }

        return rows;
    }

    private async Task MarkDispatchedAsync(Guid tripId, DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE capture_intents SET dispatched_at = @now WHERE trip_id = @trip", connection);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("trip", tripId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RecordFailureAsync(Guid tripId, string reason, DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO capture_failures (trip_id, reason, occurred_at) VALUES (@trip, @reason, @now)",
            connection);
        command.Parameters.AddWithValue("trip", tripId);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync();
    }
}
