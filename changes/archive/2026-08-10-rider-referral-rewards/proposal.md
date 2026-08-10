# Change: rider-referral-rewards

Status: accepted and complete

## Problem

The fixture has no feature large enough to validate whether Azimuth can guide an agent team across
business admission rules, asynchronous service boundaries, money movement, rider experience and
operational evidence in one coherent change.

Riders also have no way to invite another rider, see whether that invitation qualified, or redeem a
reward without trusting an unauthorised payment adjustment.

## Scope

Give every rider a stable referral code. A rider may attach one known, non-self code before their
first admitted trip, and that attribution cannot later be replaced. The referred rider's first
successfully captured trip earns one fixed-value credit for each rider. Broker redelivery and
concurrent processing must not issue the pair twice.

Let a rider reserve one available credit while requesting a later trip. Trips signs the reservation
as part of the trip's lifecycle facts; Payments verifies that authority, applies the credit exactly
once, records the original fare and adjustment, and publishes the successful capture. The referral
state advances only from that capture fact. Expose referral state and payment breakdown in the rider
application.

This change does not introduce campaigns, multilevel referrals, variable rewards, expiry, cash
settlement, transfer between riders, or a generalized promotion engine.

## Completion

- stable codes and single immutable attribution are enforced by storage under concurrency;
- no reward exists before the referred rider's first successful capture;
- the first qualifying capture grants one credit to each participant exactly once;
- only an available credit owned by the rider can authorize an adjustment, and it is redeemed once;
- payment records and the receipt retain original fare, credit and captured amount;
- capture publication and referral consumption tolerate retry and broker redelivery;
- the rider can see their code, attribution, reward status and available or used credits;
- component evidence crosses real Postgres and RabbitMQ boundaries, and composed-stack evidence
  proves the principal referral-to-redemption journey;
- the work-package DAG and integration decisions are recorded so the change evaluates team-shaped
  execution as well as the product behavior.
