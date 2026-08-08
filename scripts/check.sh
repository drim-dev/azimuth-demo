#!/usr/bin/env bash
# Builds and tests everything, emits the linkage manifests, then runs azimuth over the result.
#
# Not a CI config: the ratchet and the severity gate belong there. This is the loop a person runs.
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$PWD"
OUT="$ROOT/.azimuth"
mkdir -p "$OUT"

echo "== core =="
cargo test --quiet --manifest-path tools/azimuth/Cargo.toml

echo "== extractors =="
dotnet test -v q --nologo tools/extractors/dotnet/Azimuth.Emit.Tests
(cd tools/extractors/typescript && npx tsc -p tsconfig.json && npm test --silent)

echo "== app =="
dotnet test -v q --nologo app/services/Pricing.Tests
dotnet test -v q --nologo app/services/Trips.Tests
dotnet test -v q --nologo app/services/Payments.Tests
dotnet test -v q --nologo app/services/Analytics.Tests

echo "== monitoring =="
docker run --rm \
  -v "$ROOT/app/monitoring:/etc/prometheus:ro" \
  -w /etc/prometheus \
  --entrypoint promtool \
  prom/prometheus:v3.12.0 test rules payments.rules.test.yml
docker run --rm \
  -v "$ROOT/app/monitoring:/etc/prometheus:ro" \
  -w /etc/prometheus \
  --entrypoint promtool \
  prom/prometheus:v3.12.0 test rules trip-events.rules.test.yml

# The e2e rung speaks HTTP to built Next apps, so they are built rather than dev-served: a dev
# server would test a different bundle from the one a rider would be given.
(cd app/web/rider && npm run build --silent)
(cd app/web/driver && npm run build --silent)
(cd app/e2e && npx tsc -p tsconfig.json && node --test dist/e2e.test.js)

echo "== emit =="
EMIT="$ROOT/tools/extractors/dotnet/Azimuth.Emit/bin/Debug/net10.0/azimuth-emit-dotnet"
"$EMIT" --output "$OUT/dotnet.json" --root "$ROOT" \
  app/services/Pricing/bin/Debug/net10.0/Pricing.dll \
  app/services/Pricing.Service/bin/Debug/net10.0/Pricing.Service.dll \
  app/services/Pricing.Tests/bin/Debug/net10.0/Pricing.Tests.dll \
  app/services/Analytics/bin/Debug/net10.0/Common.dll \
  app/services/Trips/bin/Debug/net10.0/Trips.dll \
  app/services/Trips.Tests/bin/Debug/net10.0/Trips.Tests.dll \
  app/services/Payments/bin/Debug/net10.0/Payments.dll \
  app/services/Payments.Tests/bin/Debug/net10.0/Payments.Tests.dll \
  app/services/Analytics/bin/Debug/net10.0/Analytics.dll \
  app/services/Analytics.Tests/bin/Debug/net10.0/Analytics.Tests.dll

# The two apps are enumerated as classes: membership comes from the built route table, so a route
# that exists is in the class whether or not anyone tagged it. A tag-derived class only ever reaches
# files somebody already annotated (D13.1).
node tools/extractors/typescript/dist/cli.js --output "$OUT/web.json" --root "$ROOT" \
  --next-app trips/rider-view=app/web/rider \
  --next-app trips/driver-view=app/web/driver \
  --prometheus app/monitoring/payments.rules.yml,app/monitoring/payments.rules.test.yml \
  --prometheus app/monitoring/trip-events.rules.yml,app/monitoring/trip-events.rules.test.yml \
  app/web/rider/src app/web/driver/src app/e2e/src

echo "== azimuth =="
# Historical exports also live in .azimuth. Feeding them back into the model duplicates sites and
# makes the check depend on local leftovers rather than this run's compiler output.
cargo run --quiet --manifest-path tools/azimuth/Cargo.toml -- check \
  --manifest "$OUT/dotnet.json" \
  --manifest "$OUT/web.json" \
  "$@"
