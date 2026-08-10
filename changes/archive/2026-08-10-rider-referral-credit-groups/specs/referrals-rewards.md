# Intent delta: referrals/rewards

## Add requirement: credit-history-is-grouped-by-state
Criticality: routine

The referral summary SHALL group the complete referral-credit history by each credit's named state.

### Add scenario: every-credit-appears-in-its-state-group
GIVEN a rider with referral credits in one or more states
WHEN the rider opens the referral summary
THEN every credit appears under available, reserved or used according to its state

## Add requirement: empty-credit-groups-are-named
Criticality: routine

The referral summary SHALL name available, reserved and used groups even when a group is empty.

### Add scenario: empty-credit-state-is-explicit
GIVEN a rider with no credits in one of the named states
WHEN the rider opens the referral summary
THEN that state is shown with an explicit empty result
