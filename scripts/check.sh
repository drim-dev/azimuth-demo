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
(cd tools/extractors/typescript && npx tsc -p tsconfig.json && node --test dist/emitter.test.js)

echo "== app =="
dotnet test -v q --nologo app/services/Trip.Tests
dotnet test -v q --nologo app/services/Payments.Tests
(cd app/bff/rider && npx tsc -p tsconfig.json && node --test dist/e2e.test.js)

echo "== emit =="
EMIT="$ROOT/tools/extractors/dotnet/Azimuth.Emit/bin/Debug/net10.0/azimuth-emit-dotnet"
"$EMIT" --output "$OUT/dotnet.json" --root "$ROOT" --traced-root Trip.Tests --traced-root Payments.Tests \
  app/services/Trip/bin/Debug/net10.0/Trip.dll \
  app/services/Trip.Tests/bin/Debug/net10.0/Trip.Tests.dll \
  app/services/Payments/bin/Debug/net10.0/Payments.dll \
  app/services/Payments.Tests/bin/Debug/net10.0/Payments.Tests.dll

node tools/extractors/typescript/dist/cli.js --output "$OUT/rider-bff.json" --root "$ROOT" app/bff/rider/src

echo "== azimuth =="
MANIFESTS=()
for m in "$OUT"/*.json; do MANIFESTS+=(--manifest "$m"); done
cargo run --quiet --manifest-path tools/azimuth/Cargo.toml -- check "${MANIFESTS[@]}" "$@"
