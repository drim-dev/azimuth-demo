# Intent delta: payments/capture

## Add requirement: rider-sees-payment-status
Criticality: standard

A completed trip receipt SHALL communicate whether payment is pending, captured or declined,
without relying on color alone.

### Add scenario: receipt-explains-payment-state
GIVEN payment is pending, captured or declined
WHEN the rider opens the completed trip receipt
THEN the current payment state is named
AND a declined state explains what happens next
AND the state remains understandable without color
