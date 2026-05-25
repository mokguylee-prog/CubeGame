#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT_DIR/CubeGame.Avalonia/CubeGame.Avalonia.csproj"

cd "$ROOT_DIR"

echo "Building CubeGame.Avalonia..."
dotnet build "$PROJECT" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

echo "Launching CubeGame.app..."
open -n "$ROOT_DIR/CubeGame.app"
