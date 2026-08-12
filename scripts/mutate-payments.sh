#!/usr/bin/env bash
# Refreshes the targeted Payments mutation challenge used by the Azimuth agent tier.
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$PWD"
OUT="$ROOT/.azimuth"
STRYKER_OUT="$OUT/stryker-payments"
REPORT="$STRYKER_OUT/reports/payments-capture-mutation.json"
CONFIG="$ROOT/app/services/Payments.Tests/stryker-config.json"
LINKAGE="$OUT/payments-linkage.json"
MUTATION="$ROOT/app/services/Payments.Tests/mutation-assessment.json"
EMIT="$ROOT/tools/extractors/dotnet/Azimuth.Emit/bin/Debug/net10.0/azimuth-emit-dotnet"

mkdir -p "$OUT"

dotnet tool restore
dotnet test -v q --nologo app/services/Payments.Tests
dotnet build -v q --nologo tools/extractors/dotnet/Azimuth.Emit
(cd tools/extractors/typescript && npx tsc -p tsconfig.json)

(cd app/services/Payments.Tests && \
  dotnet tool run dotnet-stryker -- -f stryker-config.json -O "$STRYKER_OUT" --skip-version-check)

"$EMIT" --output "$LINKAGE" --root "$ROOT" \
  app/services/Payments/bin/Debug/net10.0/Payments.dll \
  app/services/Payments.Tests/bin/Debug/net10.0/Payments.Tests.dll

node tools/extractors/typescript/dist/mutation-cli.js \
  "$REPORT" "$LINKAGE" "$MUTATION" \
  --root "$ROOT" --config "$CONFIG" --tool-version 4.16.0

printf 'Mutation challenge refreshed at %s\nRun ./scripts/check.sh before recording judgments.\n' "$MUTATION"
