#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$REPO_ROOT" ]]; then
  REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
fi

SOURCE_FUNCTIONS_DIR="${SOURCE_FUNCTIONS_DIR:-$REPO_ROOT/Learning Path/02-Functions/ConferenceHubFunctions}"
TARGET_FUNCTIONS_DIR="${TARGET_FUNCTIONS_DIR:-$REPO_ROOT/ConferenceHubFunctions}"
SOURCE_WEBAPP_MODELS_DIR="${SOURCE_WEBAPP_MODELS_DIR:-$REPO_ROOT/Learning Path/02-Functions/ConferenceHub/Models}"
TARGET_WEBAPP_MODELS_DIR="${TARGET_WEBAPP_MODELS_DIR:-$REPO_ROOT/ConferenceHub/Models}"

if [[ ! -d "$SOURCE_FUNCTIONS_DIR" ]]; then
  echo "Source functions directory not found: $SOURCE_FUNCTIONS_DIR" >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required." >&2
  exit 1
fi

if ! dotnet new list | rg -q "Functions" ; then
  dotnet new install Microsoft.Azure.Functions.Worker.ProjectTemplates
fi

if [[ ! -d "$TARGET_FUNCTIONS_DIR" ]]; then
  dotnet new func \
    --Framework net10.0 \
    --output "$TARGET_FUNCTIONS_DIR"
fi

mkdir -p "$TARGET_FUNCTIONS_DIR/Models"
cp -R "$SOURCE_FUNCTIONS_DIR/"*.cs "$TARGET_FUNCTIONS_DIR/" || true
cp -R "$SOURCE_FUNCTIONS_DIR/Models/"*.cs "$TARGET_FUNCTIONS_DIR/Models/" || true

if [[ -f "$SOURCE_WEBAPP_MODELS_DIR/AzureFunctionsConfig.cs" ]]; then
  mkdir -p "$TARGET_WEBAPP_MODELS_DIR"
  cp "$SOURCE_WEBAPP_MODELS_DIR/AzureFunctionsConfig.cs" "$TARGET_WEBAPP_MODELS_DIR/AzureFunctionsConfig.cs"
fi

echo "Functions project ready at: $TARGET_FUNCTIONS_DIR"
