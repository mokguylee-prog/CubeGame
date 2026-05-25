#!/usr/bin/env bash
# build-app-bundle.sh — 빌드 후 루트에 CubeGame.app 번들 생성
# 인자: <APP_DIR> <OUT_DIR> <ICNS_SRC> <ROOT_DIR>
set -euo pipefail

APP_DIR="$1"
OUT_DIR="$2"
ICNS_SRC="$3"
ROOT_DIR="$4"

# DOTNET_ROOT 자동 감지
SDK_PATH="$(dotnet --list-sdks 2>/dev/null | awk -F'[][]' 'NR==1{print $2}')"
DOTNET_ROOT_DIR="$(dirname "$SDK_PATH")"

# 디렉터리 구조
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

# 아이콘
cp "$ICNS_SRC" "$APP_DIR/Contents/Resources/AppIcon.icns"

# Info.plist
cat > "$APP_DIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>CubeGame</string>
  <key>CFBundleIdentifier</key><string>local.cubegame.avalonia</string>
  <key>CFBundleName</key><string>CubeGame</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

# 런처 스크립트
cat > "$APP_DIR/Contents/MacOS/CubeGame" <<LAUNCHER
#!/usr/bin/env bash
mkdir -p "$ROOT_DIR/.run"
exec > "$ROOT_DIR/.run/CubeGame.log" 2>&1
export DOTNET_ROOT="$DOTNET_ROOT_DIR"
export PATH="$DOTNET_ROOT_DIR:\$PATH"
cd "$OUT_DIR"
exec "$OUT_DIR/CubeGame.Avalonia"
LAUNCHER

chmod +x "$APP_DIR/Contents/MacOS/CubeGame"

# Finder에 아이콘 캐시 갱신
touch "$APP_DIR"
