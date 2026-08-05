using Azimuth.Annotations;
using Pricing;
using Npgsql;
using Trip.Domain;

namespace Trip.Storage;

public sealed record QuoteRow(
    Guid Id,
    string Pickup,
    string Dropoff,
    Money Total,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    Guid? ConsumedByTrip);

public sealed record TripRow(
    Guid Id,
    string RiderId,
    string? AssignedDriverId,
    TripState State,
    Money Fare,
    Guid QuoteId);

public sealed record DriverRow(string Id, string Display, string Vehicle, string? Position);

public sealed class QuoteStore(Database database)
{
    /// <summary>A quote is never reissued under the same identifier; there is no update path.</summary>
    [Realizes("pricing/quote", "quote-returned")]
    [Realizes("pricing/quote", "breakdown-accompanies-quote")]
    public async Task<QuoteRow> IssueAsync(
        string pickup,
        string dropoff,
        Money total,
        TimeSpan validFor,
        DateTimeOffset now)
    {
        var row = new QuoteRow(Guid.NewGuid(), pickup, dropoff, total, now, now + validFor, null);
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO quotes (id, pickup, dropoff, total_minor, currency, issued_at, expires_at)
            VALUES (@id, @pickup, @dropoff, @total, @currency, @issued, @expires)
            """,
            connection);
        command.Parameters.AddWithValue("id", row.Id);
        command.Parameters.AddWithValue("pickup", pickup);
        command.Parameters.AddWithValue("dropoff", dropoff);
        command.Parameters.AddWithValue("total", total.MinorUnits);
        command.Parameters.AddWithValue("currency", total.Currency);
        command.Parameters.AddWithValue("issued", now);
        command.Parameters.AddWithValue("expires", row.ExpiresAt);
        await command.ExecuteNonQueryAsync();
        return row;
    }

    [Realizes("pricing/quote", "quote-valid-before-expiry")]
    [Realizes("pricing/quote", "quote-invalid-after-expiry")]
    public async Task<QuoteRow?> FindAsync(Guid id)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, pickup, dropoff, total_minor, currency, issued_at, expires_at, consumed_by_trip
            FROM quotes WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new QuoteRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Money.Of(reader.GetInt64(3), reader.GetString(4)),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7));
    }
}

public sealed class TripStore(Database database)
{
    /// <summary>
    /// Admits a request and creates the trip, in one transaction.
    /// </summary>
    /// <remarks>
    /// The sole constructor of a trip. Quote validation and both uniqueness rules are settled here
    /// against real storage: the checks read well and produce the error the rider sees, but the two
    /// partial unique indexes are what hold when two requests arrive together.
    /// </remarks>
    [Realizes("trip/request", "request-admitted-with-valid-quote")]
    [Realizes("trip/request", "request-rejected-with-expired-quote")]
    [Realizes("trip/request", "request-rejected-with-unknown-quote")]
    [Realizes("trip/request", "quote-consumed-once")]
    [Realizes("trip/request", "trip-created-in-requested-state")]
    [Realizes("trip/request", "second-request-rejected-while-active")]
    [Realizes("trip/request", "request-admitted-after-terminal")]
    public async Task<AdmissionResult> AdmitAsync(string riderId, Guid quoteId, DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var lookup = new NpgsqlCommand(
            "SELECT total_minor, currency, expires_at, consumed_by_trip FROM quotes WHERE id = @id",
            connection,
            transaction))
        {
            lookup.Parameters.AddWithValue("id", quoteId);
            await using var reader = await lookup.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return AdmissionResult.Rejected("unknown-quote");
            }

            var fare = Money.Of(reader.GetInt64(0), reader.GetString(1));
            var expires = reader.GetFieldValue<DateTimeOffset>(2);
            var consumed = !reader.IsDBNull(3);
            await reader.CloseAsync();

            if (expires <= now)
            {
                return AdmissionResult.Rejected("expired-quote");
            }

            if (consumed)
            {
                return AdmissionResult.Rejected("quote-already-consumed");
            }

            var tripId = Guid.NewGuid();
            try
            {
                await using (var insert = new NpgsqlCommand(
                    """
                    INSERT INTO trips (id, rider_id, state, fare_minor, currency, quote_id, created_at)
                    VALUES (@id, @rider, 'requested', @fare, @currency, @quote, @now)
                    """,
                    connection,
                    transaction))
                {
                    insert.Parameters.AddWithValue("id", tripId);
                    insert.Parameters.AddWithValue("rider", riderId);
                    insert.Parameters.AddWithValue("fare", fare.MinorUnits);
                    insert.Parameters.AddWithValue("currency", fare.Currency);
                    insert.Parameters.AddWithValue("quote", quoteId);
                    insert.Parameters.AddWithValue("now", now);
                    await insert.ExecuteNonQueryAsync();
                }

                await using (var consume = new NpgsqlCommand(
                    "UPDATE quotes SET consumed_by_trip = @trip WHERE id = @id AND consumed_by_trip IS NULL",
                    connection,
                    transaction))
                {
                    consume.Parameters.AddWithValue("trip", tripId);
                    consume.Parameters.AddWithValue("id", quoteId);
                    if (await consume.ExecuteNonQueryAsync() != 1)
                    {
                        await transaction.RollbackAsync();
                        return AdmissionResult.Rejected("quote-already-consumed");
                    }
                }

                await transaction.CommitAsync();
                return AdmissionResult.Admitted(tripId, fare);
            }
            catch (PostgresException e) when (e.SqlState == "23505")
            {
                await transaction.RollbackAsync();
                return AdmissionResult.Rejected(
                    e.ConstraintName == "ux_trip_rider_active" ? "rider-has-active-trip" : "quote-already-consumed");
            }
        }
    }

    public async Task<TripRow?> FindAsync(Guid id)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, rider_id, assigned_driver_id, state, fare_minor, currency, quote_id FROM trips WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TripRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            TripStateMachine.Parse(reader.GetString(3)),
            Money.Of(reader.GetInt64(4), reader.GetString(5)),
            reader.GetGuid(6));
    }

    /// <summary>
    /// Applies an event through the state machine, with a conditional write on the current state.
    /// </summary>
    /// <remarks>
    /// Two mechanisms for one rule. The machine alone does not survive concurrency: two in-flight
    /// handlers can both read <c>in-progress</c>, both pass, and the later write wins. The
    /// conditional update is what makes a replayed transition inert rather than merely unlikely.
    /// </remarks>
    [Realizes("trip/lifecycle", "assigned-to-in-progress")]
    [Realizes("trip/lifecycle", "in-progress-to-completed")]
    [Realizes("trip/lifecycle", "unpermitted-transition-rejected")]
    [Realizes("trip/lifecycle", "no-transition-out-of-terminal")]
    [Realizes("trip/lifecycle", "replayed-transition-is-inert")]
    [Realizes("trip/lifecycle", "transition-records-actor-and-instant")]
    [Realizes("trip/lifecycle", "history-is-append-only")]
    [Realizes("trip/lifecycle", "rider-cancels-before-start")]
    [Realizes("trip/lifecycle", "driver-cancels-after-assignment")]
    [Realizes("trip/lifecycle", "cancellation-after-completion-rejected")]
    public async Task<TransitionResult> ApplyAsync(
        Guid tripId,
        TripEvent @event,
        string actor,
        DateTimeOffset now,
        string? assignDriverId = null)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        TripState from;
        await using (var read = new NpgsqlCommand(
            "SELECT state FROM trips WHERE id = @id FOR UPDATE", connection, transaction))
        {
            read.Parameters.AddWithValue("id", tripId);
            var state = await read.ExecuteScalarAsync();
            if (state is null)
            {
                return TransitionResult.Rejected("unknown-trip");
            }

            from = TripStateMachine.Parse((string)state);
        }

        var next = TripStateMachine.Next(from, @event);
        if (next is null)
        {
            await transaction.RollbackAsync();
            return TransitionResult.Rejected("transition-not-permitted");
        }

        var to = next.Value.To;
        await using (var write = new NpgsqlCommand(
            """
            UPDATE trips
            SET state = @to,
                assigned_driver_id = COALESCE(@driver, assigned_driver_id)
            WHERE id = @id AND state = @from
            """,
            connection,
            transaction))
        {
            write.Parameters.AddWithValue("to", TripStateMachine.Name(to));
            write.Parameters.AddWithValue("from", TripStateMachine.Name(from));
            write.Parameters.AddWithValue("id", tripId);
            write.Parameters.AddWithValue("driver", (object?)assignDriverId ?? DBNull.Value);
            if (await write.ExecuteNonQueryAsync() != 1)
            {
                await transaction.RollbackAsync();
                return TransitionResult.Rejected("state-moved-under-us");
            }
        }

        await using (var history = new NpgsqlCommand(
            """
            INSERT INTO trip_transitions (trip_id, from_state, to_state, actor, occurred_at)
            VALUES (@id, @from, @to, @actor, @now)
            """,
            connection,
            transaction))
        {
            history.Parameters.AddWithValue("id", tripId);
            history.Parameters.AddWithValue("from", TripStateMachine.Name(from));
            history.Parameters.AddWithValue("to", TripStateMachine.Name(to));
            history.Parameters.AddWithValue("actor", actor);
            history.Parameters.AddWithValue("now", now);
            await history.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return TransitionResult.Applied(from, to);
    }

    public async Task<IReadOnlyList<(string From, string To, string Actor)>> HistoryAsync(Guid tripId)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT from_state, to_state, actor FROM trip_transitions WHERE trip_id = @id ORDER BY id",
            connection);
        command.Parameters.AddWithValue("id", tripId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string, string, string)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    public async Task<DriverRow?> DriverAsync(string id)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id, display, vehicle, position FROM drivers WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DriverRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }
}

public readonly record struct AdmissionResult(bool Ok, Guid TripId, Money Fare, string? Reason)
{
    public static AdmissionResult Admitted(Guid tripId, Money fare) => new(true, tripId, fare, null);

    public static AdmissionResult Rejected(string reason) => new(false, Guid.Empty, default, reason);
}

public readonly record struct TransitionResult(bool Ok, TripState From, TripState To, string? Reason)
{
    public static TransitionResult Applied(TripState from, TripState to) => new(true, from, to, null);

    public static TransitionResult Rejected(string reason) => new(false, default, default, reason);
}
