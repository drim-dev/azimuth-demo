using System.Net;
using Azimuth.Annotations;
using Common.Identity;
using Common.Messaging;
using Common.Referrals;
using Common.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Trips.Domain;
using Trips.Features.Referrals;
using Trips.Features.Trips;
using Trips.Tests.Fixtures;
using Xunit;

namespace Trips.Tests.Features.Referrals;

[Collection(ReferralTestsCollection.Name)]
public sealed class ReferralRewardsTests(ReferralTestFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Reset(Cancellation.Token());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_summary_requests_return_one_stable_code()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();

        var summaries = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.ReferralSummary(rider)));

        summaries.Select(x => x.ReferralCode).Distinct().Should().ContainSingle();
        (await fixture.Database.Count<ReferralAccount>(
            x => x.RiderId == rider,
            Cancellation.Token())).Should().Be(1);
    }

    [Fact]
    [Covers("referrals/rewards", "known-code-is-attributed", Scope.Component,
        Quantification.Example)]
    public async Task A_known_code_on_the_first_trip_creates_one_pending_attribution()
    {
        var client = fixture.HttpClient.CreateClient();
        var referrer = Api.Rider();
        var referred = Api.Rider();
        var code = (await client.ReferralSummary(referrer)).ReferralCode;

        var response = await client.RequestRide(referred, await client.QuoteId(), code);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attribution = await fixture.Database.SingleOrDefault<ReferralAttribution>(
            x => x.ReferredRiderId == referred,
            Cancellation.Token());
        attribution.Should().NotBeNull();
        attribution!.ReferrerRiderId.Should().Be(referrer);
        (await client.ReferralSummary(referred)).AttributionStatus.Should().Be("pending");
    }

    [Fact]
    [Covers("referrals/rewards", "unknown-code-is-rejected", Scope.Component,
        Quantification.Example)]
    [Covers("referrals/rewards", "self-referral-is-rejected", Scope.Component,
        Quantification.Example)]
    public async Task Unknown_and_self_codes_create_neither_trip_nor_attribution()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();
        var ownCode = (await client.ReferralSummary(rider)).ReferralCode;

        await (await client.RequestRide(rider, await client.QuoteId(), "unknown-code"))
            .ShouldBeBusinessRuleError("trip:request:create:unknown_referral_code");
        await (await client.RequestRide(rider, await client.QuoteId(), ownCode))
            .ShouldBeBusinessRuleError("trip:request:create:self_referral");

        (await fixture.Database.Count<Trip>(x => x.RiderId == rider, Cancellation.Token()))
            .Should().Be(0);
        (await fixture.Database.Count<ReferralAttribution>(
            x => x.ReferredRiderId == rider,
            Cancellation.Token())).Should().Be(0);
    }

    [Fact]
    [Covers("referrals/rewards", "attribution-cannot-be-replaced", Scope.Component,
        Quantification.Universal, Oracle.Relational)]
    public async Task Eligibility_closes_by_attribution_unattributed_admission_or_contention()
    {
        var client = fixture.HttpClient.CreateClient();
        var firstCode = (await client.ReferralSummary(Api.Rider())).ReferralCode;
        var secondCode = (await client.ReferralSummary(Api.Rider())).ReferralCode;

        var attributedRider = Api.Rider();
        var first = await (await client.RequestRide(
            attributedRider,
            await client.QuoteId(),
            firstCode)).Read<RequestRide.Response>();
        await client.Move(first.TripId, "cancel", attributedRider);
        await (await client.RequestRide(attributedRider, await client.QuoteId(), secondCode))
            .ShouldBeBusinessRuleError("trip:request:create:referral_eligibility_closed");

        await fixture.Reset(Cancellation.Token());
        var lateCode = (await client.ReferralSummary(Api.Rider())).ReferralCode;
        var unattributedRider = Api.Rider();
        var unattributed = await (await client.RequestRide(
            unattributedRider,
            await client.QuoteId())).Read<RequestRide.Response>();
        await client.Move(unattributed.TripId, "cancel", unattributedRider);
        await (await client.RequestRide(unattributedRider, await client.QuoteId(), lateCode))
            .ShouldBeBusinessRuleError("trip:request:create:referral_eligibility_closed");

        await fixture.Reset(Cancellation.Token());
        var contendedRider = Api.Rider();
        var codes = new List<string>();
        for (var index = 0; index < 8; index++)
        {
            codes.Add((await client.ReferralSummary(Api.Rider())).ReferralCode);
        }
        var quotes = await Task.WhenAll(codes.Select(_ => client.QuoteId()));
        var responses = await Task.WhenAll(codes.Select((code, index) =>
            client.RequestRide(contendedRider, quotes[index], code)));

        responses.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
        (await fixture.Database.Count<ReferralAttribution>(
            x => x.ReferredRiderId == contendedRider,
            Cancellation.Token())).Should().Be(1);
        (await fixture.Database.Count<RiderAdmission>(
            x => x.RiderId == contendedRider,
            Cancellation.Token())).Should().Be(1);
    }

    [Fact]
    [Covers("referrals/rewards", "no-reward-before-capture", Scope.Component,
        Quantification.Universal)]
    public async Task No_lifecycle_state_grants_a_reward_without_a_capture_fact()
    {
        var client = fixture.HttpClient.CreateClient();

        foreach (var state in TripStateMachine.States)
        {
            await fixture.Reset(Cancellation.Token());
            await fixture.Database.Save(Api.AvailableDriver("driver-0"));
            var referrer = Api.Rider();
            var referred = Api.Rider();
            var code = (await client.ReferralSummary(referrer)).ReferralCode;
            var trip = await (await client.RequestRide(
                referred,
                await client.QuoteId(),
                code)).Read<RequestRide.Response>();

            await DriveTo(client, trip.TripId, state);

            (await fixture.Database.Count<ReferralCredit>(
                x => x.BeneficiaryRiderId == referrer || x.BeneficiaryRiderId == referred,
                Cancellation.Token())).Should().Be(0);
        }
    }

    [Fact]
    [Covers("referrals/rewards", "first-capture-awards-pair", Scope.Component,
        Quantification.Example)]
    [Covers("referrals/rewards", "capture-redelivery-does-not-duplicate-reward", Scope.Component,
        Quantification.Universal, Oracle.Relational)]
    public async Task Duplicate_and_concurrent_capture_facts_leave_one_credit_per_participant()
    {
        var client = fixture.HttpClient.CreateClient();
        var (tripId, referrer, referred) = await AttributedTrip(client);
        var capture = Captured(tripId, captureId: 80_001);

        for (var delivery = 0; delivery < 7; delivery++)
        {
            await fixture.RabbitMq.Publish(capture, Cancellation.Token());
        }
        await WaitForCredits(2);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Send(
            new ConsumePaymentCaptured.Request(capture),
            Cancellation.Token())));
        var concurrent = Enumerable.Range(0, 8)
            .Select(_ => Captured(tripId, capture.CaptureId))
            .Select(message => fixture.Send(
                new ConsumePaymentCaptured.Request(message),
                Cancellation.Token()));
        await Task.WhenAll(concurrent);

        var credits = await fixture.Database.Execute(async db =>
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.ReferralCredits.Where(x => x.AttributionId > 0)));
        credits.Should().HaveCount(2);
        credits.Select(x => x.BeneficiaryRiderId).Should().BeEquivalentTo([referrer, referred]);
        credits.Should().OnlyContain(x => x.AmountMinor == 500 && x.Currency == "EUR");
        var referredSummary = await client.ReferralSummary(referred);
        referredSummary.AttributionStatus.Should().Be("qualified");
        referredSummary.Credits.Should().ContainSingle(x =>
            x.AmountMinor == 500 && x.Currency == "EUR" && x.Status == "available");
        (await client.ReferralSummary(referrer)).Credits.Should().ContainSingle(x =>
            x.AmountMinor == 500 && x.Currency == "EUR" && x.Status == "available");
    }

    [Fact]
    [Covers("referrals/rewards", "unavailable-credit-is-rejected", Scope.Component,
        Quantification.Universal)]
    public async Task Every_unavailable_credit_shape_rejects_without_a_trip_or_reservation()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();
        var random = new Random(20260810);

        for (var trial = 0; trial < 8; trial++)
        {
            var unknown = IdEncoding.Encode(random.NextInt64(1, long.MaxValue));
            await (await client.RequestRide(rider, await client.QuoteId(), referralCreditId: unknown))
                .ShouldBeBusinessRuleError("trip:referral:reserve:unknown_credit");
        }

        foreach (var state in Enum.GetValues<ReferralCreditState>())
        {
            await fixture.Reset(Cancellation.Token());
            var credit = await SeedCredit(rider, state);
            if (state == ReferralCreditState.Available)
            {
                credit.BeneficiaryRiderId = Api.Rider();
                await fixture.Database.Execute(async db =>
                {
                    db.ReferralCredits.Update(credit);
                    await db.SaveChangesAsync();
                });
                await (await client.RequestRide(
                    rider,
                    await client.QuoteId(),
                    referralCreditId: IdEncoding.Encode(credit.Id)))
                    .ShouldBeBusinessRuleError("trip:referral:reserve:foreign_credit");
            }
            else
            {
                await (await client.RequestRide(
                    rider,
                    await client.QuoteId(),
                    referralCreditId: IdEncoding.Encode(credit.Id)))
                    .ShouldBeBusinessRuleError("trip:referral:reserve:unavailable_credit");
            }
        }
    }

    [Fact]
    [Covers("referrals/rewards", "capture-redelivery-does-not-redeem-twice", Scope.Component,
        Quantification.Universal, Oracle.Relational)]
    public async Task Reservation_releases_on_cancellation_and_one_capture_uses_it_once()
    {
        var client = fixture.HttpClient.CreateClient();
        var rider = Api.Rider();
        var credit = await SeedCredit(rider, ReferralCreditState.Available);
        var creditId = IdEncoding.Encode(credit.Id);

        var reserved = await (await client.RequestRide(
            rider,
            await client.QuoteId(),
            referralCreditId: creditId)).Read<RequestRide.Response>();
        await client.Move(reserved.TripId, "cancel", rider);
        IdEncoding.TryDecode(reserved.TripId, out var releasedTripId).Should().BeTrue();
        var releasedTrip = await fixture.Database.SingleOrDefault<Trip>(
            x => x.Id == releasedTripId,
            Cancellation.Token());
        var signed = new ReferralCreditAuthorityCodec(
            "azimuth-demo-referral-credit-signing-key").Decode(releasedTrip!.ReferralCreditAuthority!);
        signed.Should().Be(new ReferralCreditAuthority(credit.Id, releasedTripId, 500, "EUR"));
        var authorities = await fixture.Database.Execute(async db =>
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.TripEvents.Where(x => x.TripId == releasedTripId)
                    .Select(x => x.ReferralCreditAuthority)));
        authorities.Should().HaveCount(2).And.OnlyContain(x => x == releasedTrip.ReferralCreditAuthority);
        (await fixture.Database.SingleOrDefault<ReferralCredit>(
            x => x.Id == credit.Id,
            Cancellation.Token()))!.State.Should().Be(ReferralCreditState.Available);

        var next = await (await client.RequestRide(
            rider,
            await client.QuoteId(),
            referralCreditId: creditId)).Read<RequestRide.Response>();
        IdEncoding.TryDecode(next.TripId, out var tripId).Should().BeTrue();
        var capture = Captured(tripId, 90_001, credit.Id, credit.AmountMinor);
        var deliveries = Enumerable.Range(0, 8)
            .Select(_ => fixture.Send(
                new ConsumePaymentCaptured.Request(capture with { EventId = Guid.NewGuid() }),
                Cancellation.Token()));
        await Task.WhenAll(deliveries);

        var used = await fixture.Database.SingleOrDefault<ReferralCredit>(
            x => x.Id == credit.Id,
            Cancellation.Token());
        used!.State.Should().Be(ReferralCreditState.Used);
        used.ReservedTripId.Should().Be(tripId);
        used.UsedCaptureId.Should().Be(capture.CaptureId);
    }

    private async Task<(long TripId, string Referrer, string Referred)> AttributedTrip(HttpClient client)
    {
        var referrer = Api.Rider();
        var referred = Api.Rider();
        var code = (await client.ReferralSummary(referrer)).ReferralCode;
        var response = await (await client.RequestRide(
            referred,
            await client.QuoteId(),
            code)).Read<RequestRide.Response>();
        IdEncoding.TryDecode(response.TripId, out var tripId).Should().BeTrue();
        return (tripId, referrer, referred);
    }

    private async Task<ReferralCredit> SeedCredit(string rider, ReferralCreditState state)
    {
        var seed = DateTime.UtcNow.Ticks;
        var attribution = new ReferralAttribution
        {
            Id = seed,
            ReferredRiderId = $"source-{seed}",
            ReferrerRiderId = $"referrer-{seed}",
            FirstTripId = seed,
            QualificationCaptureId = seed,
        };
        var credit = new ReferralCredit
        {
            Id = seed + 1,
            AttributionId = attribution.Id,
            BeneficiaryRiderId = rider,
            AmountMinor = 500,
            Currency = "EUR",
            State = state,
            ReservedTripId = state == ReferralCreditState.Available ? null : seed + 2,
            UsedCaptureId = state == ReferralCreditState.Used ? seed + 3 : null,
            CreatedAt = TripTestFixture.Start,
        };
        await fixture.Database.Save(
            new RiderAdmission { RiderId = rider, FirstTripId = seed + 4 },
            attribution,
            credit);
        return credit;
    }

    private static PaymentCaptured Captured(
        long tripId,
        long captureId,
        long? creditId = null,
        long creditMinor = 0) => new(
        Guid.NewGuid(),
        captureId,
        tripId,
        1500,
        creditMinor,
        1500 - creditMinor,
        "EUR",
        creditId,
        TripTestFixture.Start);

    private async Task WaitForCredits(int expected)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await fixture.Database.Count<ReferralCredit>(x => true, Cancellation.Token()) == expected)
            {
                return;
            }
            await Task.Delay(25);
        }

        throw new TimeoutException("referral credits were not consumed from the payment queue");
    }

    private static async Task DriveTo(HttpClient client, string tripId, TripState state)
    {
        if (state == TripState.Requested)
        {
            return;
        }
        if (state == TripState.Cancelled)
        {
            await client.Move(tripId, "cancel", "rider");
            return;
        }

        await client.Accept(tripId, "driver-0");
        if (state == TripState.Assigned)
        {
            return;
        }
        await client.Move(tripId, "start", "driver-0");
        if (state == TripState.InProgress)
        {
            return;
        }
        await client.Move(tripId, "complete", "driver-0");
    }

    public sealed class ValidatorTests
    {
        private readonly GetReferralSummary.RequestValidator _summary = new();

        [Fact]
        public void A_referral_summary_names_a_rider()
        {
            _summary.TestValidate(new GetReferralSummary.Request(string.Empty))
                .ShouldHaveValidationErrorFor(x => x.RiderId);
        }

        [Fact]
        public void A_named_rider_passes_summary_validation()
        {
            _summary.TestValidate(new GetReferralSummary.Request("rider-1"))
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
