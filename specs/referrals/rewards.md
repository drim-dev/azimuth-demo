# Spec: referrals/rewards

Rewarding two riders after a referred rider becomes a paying participant.

Owns referral codes, attribution, reward qualification and credit lifecycle. Does not own trip
admission generally (`trips/request`), fare computation (`pricing/quote`) or capture
(`payments/capture`).

## Requirement: attribution-is-single
Criticality: standard

A rider MAY be attributed to one known other rider before their first admitted trip, and that
attribution SHALL NOT subsequently be replaced.

### Scenario: known-code-is-attributed
GIVEN a rider who has not had an admitted trip and another rider's known referral code
WHEN the rider requests a trip with that code
THEN the rider is attributed to the code owner

### Scenario: unknown-code-is-rejected
GIVEN a rider who has not had an admitted trip
WHEN the rider requests a trip with an unknown referral code
THEN the request is rejected
AND no trip or attribution is created

### Scenario: self-referral-is-rejected
GIVEN a rider's own referral code
WHEN that rider requests a trip with the code
THEN the request is rejected
AND no trip or attribution is created

### Scenario: attribution-cannot-be-replaced
GIVEN a rider who has already been attributed or had an admitted trip
WHEN that rider later presents a different referral code
THEN the code does not replace the existing attribution

## Requirement: reward-follows-first-capture
Criticality: critical

The referred rider's first successfully captured trip SHALL grant one referral credit to the
referrer and one to the referred rider, at most once for that attribution.

### Scenario: no-reward-before-capture
GIVEN an attributed rider whose trip has not been successfully captured
WHEN either participant's referral state is examined
THEN neither participant has a credit from that attribution

### Scenario: first-capture-awards-pair
GIVEN an attributed rider with no previously captured trip
WHEN that rider's first trip is successfully captured
THEN the referrer receives one referral credit
AND the referred rider receives one referral credit

### Scenario: capture-redelivery-does-not-duplicate-reward
GIVEN a qualifying capture whose referral credits were granted
WHEN that capture is delivered or processed any number of further times or concurrently
THEN each participant still has exactly one credit from the attribution

## Requirement: credit-redemption-is-authorized-once
Criticality: critical

A referral credit SHALL reduce only a later trip of its owning rider, only when Trips authorized
that reservation, and SHALL be redeemed by at most one successful capture.

### Scenario: owned-credit-reduces-capture
GIVEN a rider with an available referral credit no greater than a later trip's fare
WHEN the rider requests that trip with the credit and payment is captured
THEN the capture equals the original fare minus the credit
AND the original fare, credit and referral reason are recorded

### Scenario: unavailable-credit-is-rejected
GIVEN a referral credit that is unknown, belongs to another rider, is already reserved or is used
WHEN a rider requests a trip with that credit
THEN the request is rejected
AND no trip or new reservation is created

### Scenario: forged-credit-authority-is-rejected
GIVEN a trip lifecycle fact with altered or foreign referral credit authority
WHEN Payments examines that fact
THEN no adjusted capture is created from that authority
AND the failure remains visible

### Scenario: capture-redelivery-does-not-redeem-twice
GIVEN a referral credit redeemed by a successful capture
WHEN that capture is delivered or processed any number of further times or concurrently
THEN the credit remains used by exactly that trip
AND no further value is granted or deducted

## Requirement: rider-sees-referral-state
Criticality: standard

A rider SHALL be able to see their stable code, attribution or qualification state, and each
available, reserved or used referral credit without relying on color alone.

### Scenario: referral-summary-explains-state
GIVEN a rider with any referral history
WHEN the rider opens their referral summary
THEN the stable code is shown
AND attribution or qualification state is named
AND every referral credit names its amount, currency and state

## Requirement: credit-history-is-grouped-by-state
Criticality: routine

The referral summary SHALL group the complete referral-credit history by each credit's named state.

### Scenario: every-credit-appears-in-its-state-group
GIVEN a rider with referral credits in one or more states
WHEN the rider opens the referral summary
THEN every credit appears under available, reserved or used according to its state

## Requirement: empty-credit-groups-are-named
Criticality: routine

The referral summary SHALL name available, reserved and used groups even when a group is empty.

### Scenario: empty-credit-state-is-explicit
GIVEN a rider with no credits in one of the named states
WHEN the rider opens the referral summary
THEN that state is shown with an explicit empty result
