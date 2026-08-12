# Intent delta: analytics/trip-activity

## Add requirement: trip-delivery-health-is-alerted
Criticality: standard

Operators SHALL be alerted when trip lifecycle delivery is persistently backlogged or has
dead-lettered messages.

### Add scenario: relay-backlog-raises-alert
GIVEN trip lifecycle delivery has remained backlogged beyond its declared threshold
WHEN Prometheus evaluates the delivery-health rules
THEN a trip delivery backlog alert is active

### Add scenario: dead-letter-presence-raises-alert
GIVEN a trip lifecycle dead-letter queue contains a message
WHEN Prometheus evaluates the delivery-health rules
THEN a trip delivery dead-letter alert is active
