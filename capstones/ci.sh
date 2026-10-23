#!/usr/bin/env bash
# Builds, tests and smoke-tests every capstone. Used by CI; handy locally too.
set -euo pipefail
cd "$(dirname "$0")"

dotnet --version

echo "== task-tracker =="
(
  cd task-tracker
  dotnet build --nologo -c Release
  export TASKS_FILE="$PWD/smoke-$$.txt"
  trap 'rm -f "$TASKS_FILE"' EXIT
  run() { dotnet run --no-build -c Release -- "$@"; }
  run add "Buy milk"
  run add "Learn C#"
  run done 2
  run list
  if run remove 9; then echo "expected 'remove 9' to fail"; exit 1; fi
)

echo "== link-checker =="
(
  cd link-checker
  dotnet build --nologo -c Release src/linkcheck
  dotnet test --nologo -c Release tests/LinkCheckKit.Tests
  # The course itself must have no broken local links.
  dotnet run --no-build -c Release --project src/linkcheck -- -q ../../docs ../../README.md
)

echo "== file-indexer =="
(
  cd file-indexer
  dotnet build --nologo -c Release src/indexer
  dotnet test --nologo -c Release tests/IndexKit.Tests
  dotnet run --no-build -c Release --project src/indexer -- --stats ../../docs lesson
)

echo "All capstones OK"
