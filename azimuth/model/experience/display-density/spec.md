# Spec: experience/display-density

## Requirement: density-is-remembered
Criticality: routine

The rider application SHALL remember the selected display density in the browser.

### Scenario: density-survives-reload
WHEN the rider selects compact or comfortable display density and reloads the page
THEN the selected density remains active
