# Judgments: security/quote-tokens

Re-judged 2026-08-10 after D28 exposed realization sources. `Encode` establishes issuance and
`Decode` establishes authenticated round-trip, mutation rejection and foreign-authority rejection;
each remaining relation names behavior implemented inside the codec boundary.

Judged 2026-08-10 from the codec, both mechanism implementations, the verification plan and every
covering test body. The same judge wrote the evidence, so the strongest observation is the mutation
it caught before this judgment: a changed final base64url character could decode to the original
signature bytes through ignored padding bits. The position sweep failed until decoding required the
canonical encoding.

## Claim: issued-token-round-trips
Verdict: sound
Fingerprint: c1d242b67c93705c
Judged: 2026-08-10
Judge: codex

The test combines explicit identity, time, Unicode, null-character, zero and maximum-value
boundaries with 64 generated payloads. Its expected object is the independently generated input,
with strict collection ordering. Dropping a field, reordering components, returning a constant or
using a non-round-tripping serializer fails. `Universal` is honest about the generated payload
axes; it does not claim exhaustive enumeration of every string or integer.

## Claim: altered-token-rejected
Verdict: sound
Fingerprint: 59482eb0d88c527a
Judged: 2026-08-10
Judge: codex

For each emitted token the evidence first proves the unmodified control decodes, then changes every
non-delimiter body and signature position. It additionally tries every alternative base64url
character at the final signature position, where unused padding bits create aliases. Removing the
HMAC check, authenticating only part of the body, comparing the wrong bytes or accepting a
non-canonical signature makes the test fail. That last mutation was observed against the preceding
implementation rather than inferred only by inspection.

## Claim: foreign-signature-rejected
Verdict: sound
Fingerprint: e3d2250809023abf
Judged: 2026-08-10
Judge: codex

Across explicit boundaries and 64 generated payloads, the issuer decodes its own token while a
guaranteed-distinct authority rejects it. The keys carry distinct `issuer-` and `other-` prefixes,
so the test does not rely on probabilistic inequality. Ignoring the configured key, using an
unkeyed digest or accepting any well-formed signature fails the paired positive/negative relation.
