using Azimuth.Annotations;
using Pricing;
using Npgsql;

namespace Trip.Storage;

public sealed record OfferRow(Guid TripId, string DriverId, string State);

public sealed class OfferStore(Database database)
{
    /// <summary>Offers a requested trip to the available drivers near its pickup, and to no others.</summary>
    [Realizes("trip/dispatch", "offer-sent-to-available-nearby-driver")]
    [Realizes("trip/dispatch", "unavailable-driver-not-offered")]
    [Realizes("trip/dispatch", "no-available-drivers")]
    public async Task<int> FanOutAsync(Guid tripId, string near, DateTimeOffset now, TimeSpan validFor)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO offers (trip_id, driver_id, state, offered_at, expires_at)
            SELECT @trip, id, 'offered', @now, @expires
            FROM drivers
            WHERE available AND near = @near
            ON CONFLICT DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("trip", tripId);
        command.Parameters.AddWithValue("near", near);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("expires", now + validFor);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Settles acceptance by compare-and-set on the trip's assignment.
    /// </summary>
    /// <remarks>
    /// Not a check-then-write in the handler: two accepts arriving together both read null, and only
    /// the update matters. The losing driver's answer comes from the affected-row count rather than
    /// from re-reading, so there is no window in which a loser is told it won.
    /// <para>
    /// A distributed lock over the trip was rejected — it moves the correctness argument into the
    /// lock service's availability, and a lock that fails open under partition produces exactly the
    /// double assignment it was bought to prevent.
    /// </para>
    /// </remarks>
    [Realizes("trip/dispatch", "first-acceptance-assigns")]
    [Realizes("trip/dispatch", "concurrent-acceptances-yield-one-assignment")]
    [Realizes("trip/dispatch", "late-acceptance-rejected")]
    [Realizes("trip/dispatch", "other-offers-withdrawn")]
    public async Task<bool> AcceptAsync(Guid tripId, string driverId, DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var claim = new NpgsqlCommand(
            """
            UPDATE trips
            SET assigned_driver_id = @driver, state = 'assigned'
            WHERE id = @trip AND assigned_driver_id IS NULL AND state = 'requested'
            """,
            connection,
            transaction))
        {
            claim.Parameters.AddWithValue("driver", driverId);
            claim.Parameters.AddWithValue("trip", tripId);
            if (await claim.ExecuteNonQueryAsync() != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        await using (var history = new NpgsqlCommand(
            """
            INSERT INTO trip_transitions (trip_id, from_state, to_state, actor, occurred_at)
            VALUES (@trip, 'requested', 'assigned', @driver, @now)
            """,
            connection,
            transaction))
        {
            history.Parameters.AddWithValue("trip", tripId);
            history.Parameters.AddWithValue("driver", driverId);
            history.Parameters.AddWithValue("now", now);
            await history.ExecuteNonQueryAsync();
        }

        await using (var withdraw = new NpgsqlCommand(
            """
            UPDATE offers SET state = 'withdrawn'
            WHERE trip_id = @trip AND driver_id <> @driver AND state = 'offered'
            """,
            connection,
            transaction))
        {
            withdraw.Parameters.AddWithValue("trip", tripId);
            withdraw.Parameters.AddWithValue("driver", driverId);
            await withdraw.ExecuteNonQueryAsync();
        }

        await using (var accepted = new NpgsqlCommand(
            "UPDATE offers SET state = 'accepted' WHERE trip_id = @trip AND driver_id = @driver",
            connection,
            transaction))
        {
            accepted.Parameters.AddWithValue("trip", tripId);
            accepted.Parameters.AddWithValue("driver", driverId);
            await accepted.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return true;
    }

    /// <summary>An offer past its expiry is withdrawn and no longer shown.</summary>
    [Realizes("trip/dispatch", "expired-offer-withdrawn")]
    public async Task<int> WithdrawExpiredAsync(DateTimeOffset now)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE offers SET state = 'withdrawn' WHERE state = 'offered' AND expires_at <= @now",
            connection);
        command.Parameters.AddWithValue("now", now);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<OfferRow>> ForTripAsync(Guid tripId)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT trip_id, driver_id, state FROM offers WHERE trip_id = @trip ORDER BY driver_id",
            connection);
        command.Parameters.AddWithValue("trip", tripId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<OfferRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new OfferRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }
}
