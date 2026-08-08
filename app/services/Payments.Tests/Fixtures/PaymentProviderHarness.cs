using System.Collections.Concurrent;
using Common.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Payments.Domain;

namespace Payments.Tests.Fixtures;

/// <summary>
/// The payment provider, substituted — the one external service a component test does not run.
/// </summary>
/// <remarks>
/// D15 puts real persistence inside the component rung and external services outside it, and this
/// is the seam that makes the distinction real: the provider is the thing whose answers a test must
/// dictate, including the answer no caller ever observes. Records what it was asked, so a test can
/// assert how many times a retry actually reached it.
/// </remarks>
public sealed class PaymentProviderHarness : IHarness<Program>, IPaymentProvider
{
    private ProviderOutcome[] _outcomes = [ProviderOutcome.Captured];
    private HashSet<int> _failingCalls = [];
    private int _calls;
    private readonly ConcurrentQueue<string> _paymentMethods = new();

    public int Calls => _calls;

    public IReadOnlyList<string> PaymentMethods => _paymentMethods.ToArray();

    /// <summary>Answers in order, then repeats the last answer for every further call.</summary>
    public void Script(params ProviderOutcome[] outcomes)
    {
        _outcomes = outcomes.Length == 0 ? [ProviderOutcome.Captured] : outcomes;
        Interlocked.Exchange(ref _calls, 0);
        _paymentMethods.Clear();
        _failingCalls = [];
    }

    public void FailOnCalls(params int[] oneBasedCalls) =>
        _failingCalls = oneBasedCalls.ToHashSet();

    public void Reset() => Script(ProviderOutcome.Captured);

    public Task<ProviderOutcome> CaptureAsync(
        long tripId,
        long amountMinor,
        string currency,
        string paymentMethod)
    {
        var index = Interlocked.Increment(ref _calls) - 1;
        _paymentMethods.Enqueue(paymentMethod);
        if (_failingCalls.Contains(index + 1))
        {
            throw new HttpRequestException("scripted provider transport failure");
        }
        return Task.FromResult(index < _outcomes.Length ? _outcomes[index] : _outcomes[^1]);
    }

    public void ConfigureWebHostBuilder(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(this);
        });

    public Task Start(WebApplicationFactory<Program> factory, CancellationToken cancellation) =>
        Task.CompletedTask;

    public Task Stop(CancellationToken cancellation) => Task.CompletedTask;
}
