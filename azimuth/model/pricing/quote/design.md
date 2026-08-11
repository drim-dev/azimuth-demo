# Design: pricing/quote

Pricing is a process with its own tables. `POST /market-pressure` records observations;
`POST /quotes` selects the newest observation for the pickup market within five minutes, computes
base, distance and surge, persists an immutable quote and returns both a display projection and an
opaque signed token. `GET /quotes/{id}` reads the immutable record.

Policy `surge-v1` adds 20% of base plus distance when open requests exceed available drivers,
otherwise zero. All arithmetic is integer minor units and division rounds down. The fixed policy is
deliberate: no second pricing concern yet justifies policy-configuration machinery.

The token signs quote identity, route, lifetime, policy and pressure identities, currency, ordered
components and total with HMAC-SHA256. Trips and Payments share the codec and value contract, but
neither calls Pricing while consuming a quote. Each validates the token and its component sum.

## Requirement: money-representation
Mechanism: decimal-money-type
Enforcement: type
Binding: dotnet-symbol:Pricing.Money

Violation is unrepresentable within .NET. Currency agreement remains a runtime check.

## Requirement: quote-components-sum-to-total
Mechanism: quote-token-encoder
Enforcement: choke-point
Binding: dotnet-symbol:Pricing.QuoteTokenCodec.Encode
Mechanism: quote-token-decoder
Enforcement: guard
Binding: dotnet-symbol:Pricing.QuoteTokenCodec.Decode

`Money.Sum` also constructs the total in Pricing. Rechecking the token means a correctly signed but
internally inconsistent payload is still unusable, rather than treating possession of the key as
permission to violate quote structure.

## Requirement: surge-policy-applied
Mechanism: quote-issuance-handler
Enforcement: choke-point
Binding: dotnet-symbol:Pricing.Service.Features.Quotes.IssueQuote.RequestHandler.Handle

The latest observation qualifies only when `observed_at` is on the near side of the freshness
boundary. Missing and stale pressure select zero surge rather than preventing quotation.

## Residue

**The pressure reporter is trusted to describe the market honestly.** Authentication and
derivation from trip and driver events are absent. Tests establish freshness and policy mapping,
not that the observation represents production reality. Revisit when a second behavior consumes
market pressure.

**The type protects each language separately.** The rider app carries minor units as JavaScript
numbers. A mobile or high-value boundary could lose integer precision; contract evidence must be
revisited before amounts can approach that limit.

**Quotes are immutable by absence of an update path, not by a storage rule.** A repair script could
change `pricing_quotes` without touching code or invalidating an already issued token. The token
would preserve what consumers accepted, while lookup and storage would disagree.
