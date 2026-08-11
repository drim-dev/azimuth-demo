#!/usr/bin/env bash
# Creates independent histories because a directory split cannot exercise revision skew or receipts.
set -euo pipefail

SOURCE_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DESTINATION="${1:-$(mktemp -d "${TMPDIR:-/tmp}/azimuth-rides-federation.XXXXXX")}"

if [[ -e "$DESTINATION" ]] && [[ -n "$(find "$DESTINATION" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null)" ]]; then
  echo "destination must be absent or empty: $DESTINATION" >&2
  exit 2
fi
mkdir -p "$DESTINATION"

extract() {
  local repository="$1"
  shift
  mkdir -p "$DESTINATION/$repository"
  (
    cd "$SOURCE_ROOT"
    git ls-files --cached --others --exclude-standard -z -- "$@" | tar --null -T - -cf -
  ) | tar -x -C "$DESTINATION/$repository"
}

extract azimuth-engine .gitignore AGENTS.md CLAUDE.md .agents docs tools/azimuth tools/extractors
extract rides-backend .gitignore AGENTS.md CLAUDE.md .agents docs app/services packages/dotnet azimuth/model \
  azimuth/standards azimuth/changes azimuth/formats
extract rides-experience .gitignore AGENTS.md CLAUDE.md .agents docs app/web packages/typescript \
  azimuth/formats azimuth/changes/README.md
extract rides-operations .gitignore AGENTS.md CLAUDE.md .agents docs app/monitoring \
  azimuth/formats azimuth/changes/README.md
extract rides-assurance .gitignore AGENTS.md CLAUDE.md .agents docs app/e2e packages/typescript \
  experiments/multirepo azimuth/formats azimuth/changes/README.md

# Intent follows its authority rather than whichever repository happened to receive the old tree.
mkdir -p "$DESTINATION/rides-experience/azimuth/model"
mv "$DESTINATION/rides-backend/azimuth/model/experience" \
  "$DESTINATION/rides-experience/azimuth/model/experience"

# The routine trial's transition history follows the same experience authority as its intent.
if [[ -d "$DESTINATION/rides-backend/azimuth/changes/federated-routine-display-density" ]]; then
  mv "$DESTINATION/rides-backend/azimuth/changes/federated-routine-display-density" \
    "$DESTINATION/rides-experience/azimuth/changes/federated-routine-display-density"
fi
for archived_change in "$DESTINATION/rides-backend/azimuth/changes/archive/"*-federated-routine-display-density; do
  if [[ -d "$archived_change" ]]; then
    mkdir -p "$DESTINATION/rides-experience/azimuth/changes/archive"
    mv "$archived_change" "$DESTINATION/rides-experience/azimuth/changes/archive/"
  fi
done

mkdir -p "$DESTINATION/rides-assurance/azimuth"
cp "$SOURCE_ROOT/experiments/multirepo/project.json" \
  "$DESTINATION/rides-assurance/azimuth/project.json"

for repository in rides-backend rides-experience rides-operations rides-assurance; do
  mkdir -p "$DESTINATION/$repository/azimuth"
  if [[ "$repository" == "rides-assurance" ]]; then
    catalog="project.json"
  else
    catalog="../../rides-assurance/azimuth/project.json"
  fi
  sed -e "s/__REPOSITORY__/$repository/" -e "s|__CATALOG__|$catalog|" \
    "$SOURCE_ROOT/experiments/multirepo/project-reference.template.json" \
    > "$DESTINATION/$repository/azimuth/project-reference.json"
  if [[ ! -f "$DESTINATION/$repository/azimuth/README.md" ]]; then
    cp "$SOURCE_ROOT/experiments/multirepo/repository-readme.md" \
      "$DESTINATION/$repository/azimuth/README.md"
  fi
  mkdir -p "$DESTINATION/$repository/azimuth/bin"
  cp "$SOURCE_ROOT/experiments/multirepo/azimuth-cli.sh" \
    "$DESTINATION/$repository/azimuth/bin/azimuth"
  chmod +x "$DESTINATION/$repository/azimuth/bin/azimuth"
done

cp "$SOURCE_ROOT/experiments/multirepo/check-monitoring.sh" \
  "$DESTINATION/rides-operations/azimuth/bin/check-monitoring"
chmod +x "$DESTINATION/rides-operations/azimuth/bin/check-monitoring"

for repository in azimuth-engine rides-backend rides-experience rides-operations rides-assurance; do
  git -C "$DESTINATION/$repository" init --quiet
  git -C "$DESTINATION/$repository" config user.name "Azimuth Federation Lab"
  git -C "$DESTINATION/$repository" config user.email "federation@azimuth.invalid"
  git -C "$DESTINATION/$repository" add .
  git -C "$DESTINATION/$repository" commit --quiet -m "Materialize federation baseline"
done

echo "$DESTINATION"
for repository in azimuth-engine rides-backend rides-experience rides-operations rides-assurance; do
  revision="$(git -C "$DESTINATION/$repository" rev-parse HEAD)"
  echo "$repository $revision"
done
