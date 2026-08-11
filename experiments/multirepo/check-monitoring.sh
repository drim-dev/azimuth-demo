#!/usr/bin/env bash
# Uses the same pinned evaluator as the monorepo gate without requiring a host promtool install.
set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
docker run --rm \
  -v "$REPOSITORY_ROOT/app/monitoring:/etc/prometheus:ro" \
  -w /etc/prometheus \
  --entrypoint promtool \
  prom/prometheus:v3.12.0 test rules payments.rules.test.yml
docker run --rm \
  -v "$REPOSITORY_ROOT/app/monitoring:/etc/prometheus:ro" \
  -w /etc/prometheus \
  --entrypoint promtool \
  prom/prometheus:v3.12.0 test rules trip-events.rules.test.yml
