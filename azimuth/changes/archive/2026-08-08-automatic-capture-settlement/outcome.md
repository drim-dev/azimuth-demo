# Outcome: automatic-capture-settlement

Status: accepted

## Result

Payments now drains the transactional outbox without an operator calling `/dispatch`. The rider
receipt obtains a payment projection from Payments and names pending, captured, declined and
no-payment states in text. A Prometheus endpoint exposes pending count, overdue count, oldest age
and worker heartbeat; repository-owned rules cover overdue work and detector silence.

## Departures

The change added an explicit payment-method replacement endpoint. The agent-tier read found that
the existing retry scenario said “different instrument,” while its test only changed a scripted
provider answer and the application carried no instrument at all. A decline now pauses settlement;
replacement clears the prior failure, reopens the intent and proves the provider receives a
different opaque method token.

The manual charter was not executed because no human was available. It is retained as a procedure
and contributes no evidence. Automated component and e2e evidence satisfy the current standard
claim; no manual result is invented.

## Residual decisions

Prometheus rule evaluation is tested with `promtool`, but Alertmanager delivery has no deployed
receiver in this repository. The verification plan records that boundary explicitly.

The operations metrics endpoint has no service authentication. Authentication is absent from the
fixture generally; exposing this endpoint outside an isolated deployment requires an operations
network or service policy.

## Measurements

The accepted model contains 63 claims, including one new standard claim. Payments has 16 passing
component tests and the composed stack has 10 passing e2e tests. The detector chain exports four
metrics and defines two alert rules; `promtool` both rejected an initially mistimed dead-man
expectation and accepted the corrected rule test.

The change expired 36 judgments: eleven payment claims changed substantively, while twenty-five
unrelated or only indirectly affected claims expired because freshness hashes whole shared files.
That over-invalidation is the measured reason for the next framework change. The agent tier made
one product finding—the absent notion of a replacement instrument—and implementation changed
before acceptance. It invented no manual-test result.
