# Intent delta: payments/capture

## Add requirement: capture-batch-isolates-invalid-intents
Criticality: standard

A malformed capture intent SHALL be quarantined without preventing independent valid intents from
being attempted.

### Add scenario: malformed-intent-does-not-starve-batch
GIVEN a malformed capture intent precedes valid intents in the pending batch
WHEN settlement processes the batch
THEN the malformed intent records its terminal failure
AND valid intents behind it are still attempted
AND later settlement cycles do not retry the malformed intent
