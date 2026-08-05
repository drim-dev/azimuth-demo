using Azimuth.Annotations;

namespace Pricing;

/// <summary>
/// An amount in integer minor units of a stated currency.
/// </summary>
/// <remarks>
/// There is no floating-point constructor, conversion, or arithmetic operator, so an amount that
/// is not a whole count of minor units cannot be constructed. That is the mechanism behind
/// <c>pricing/quote#total-in-minor-units</c>, and it is why the verification plan records proof
/// strength there and no runtime test.
/// <para>
/// Currency agreement is checked at runtime rather than in the type system. See the residue in
/// <c>design/pricing/quote.md</c>.
/// </para>
/// </remarks>
[Realizes("pricing/quote", "total-in-minor-units")]
public readonly record struct Money
{
    private Money(long minorUnits, string currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public long MinorUnits { get; }

    public string Currency { get; }

    public static Money Of(long minorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("an amount states its currency", nameof(currency));
        }

        return new Money(minorUnits, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency) => Of(0, currency);

    /// <summary>Sums components, refusing a mix of currencies.</summary>
    /// <remarks>
    /// The sum relation is what <c>pricing/quote#total-equals-components</c> asserts, and the plan
    /// requires it at <c>invariant</c> quantification with a metamorphic oracle: generate component
    /// sets and assert the relation, rather than asserting one arithmetic result that a
    /// reimplementation of the same bug would also produce.
    /// </remarks>
    [Realizes("pricing/quote", "total-equals-components")]
    public static Money Sum(string currency, IEnumerable<Money> components)
    {
        var total = 0L;
        foreach (var component in components)
        {
            if (!string.Equals(component.Currency, currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"cannot sum {component.Currency} into a {currency} total");
            }

            total += component.MinorUnits;
        }

        return Of(total, currency);
    }

    public override string ToString() => $"{MinorUnits} {Currency}";
}
