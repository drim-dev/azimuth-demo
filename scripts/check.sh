#!/usr/bin/env bash
# Builds and tests everything, then runs azimuth over the repo's own artifacts.
#
# Not a CI config: the ratchet and the severity gate belong there. This is the loop a person runs.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== core =="
cargo test --quiet --manifest-path tools/azimuth/Cargo.toml

echo "== extractor: dotnet =="
dotnet test -v q --nologo tools/extractors/dotnet/Azimuth.Emit.Tests

echo "== extractor: typescript =="
(cd tools/extractors/typescript && npx tsc -p tsconfig.json && node --test dist/emitter.test.js)

echo "== azimuth =="
cargo run --quiet --manifest-path tools/azimuth/Cargo.toml -- check "$@"
