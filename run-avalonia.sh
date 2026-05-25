#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT_DIR/CubeGame.Avalonia/CubeGame.Avalonia.csproj"
OUTPUT_DIR="$ROOT_DIR/CubeGame.Avalonia/bin/Debug/net9.0"
APP_DIR="$ROOT_DIR/.run/CubeGame.app"
APP_MACOS_DIR="$APP_DIR/Contents/MacOS"
APP_EXECUTABLE="$APP_MACOS_DIR/CubeGame"
SDK_PATH="$(dotnet --list-sdks | awk -F'[][]' 'NR == 1 { print $2 }')"
DOTNET_ROOT_DIR="$(dirname "$SDK_PATH")"

cd "$ROOT_DIR"

echo "Building CubeGame.Avalonia..."
dotnet build "$PROJECT" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

echo "Running CubeGame.Avalonia..."
mkdir -p "$APP_MACOS_DIR"

cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>CubeGame</string>
  <key>CFBundleIdentifier</key>
  <string>local.cubegame.avalonia</string>
  <key>CFBundleName</key>
  <string>CubeGame</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

cat > "$APP_EXECUTABLE" <<SCRIPT
#!/usr/bin/env bash
exec > "$ROOT_DIR/.run/CubeGame.log" 2>&1
export DOTNET_ROOT="$DOTNET_ROOT_DIR"
export PATH="$DOTNET_ROOT_DIR:$PATH"
cd "$OUTPUT_DIR"
exec "$OUTPUT_DIR/CubeGame.Avalonia"
SCRIPT

chmod +x "$APP_EXECUTABLE"
open -n "$APP_DIR"
