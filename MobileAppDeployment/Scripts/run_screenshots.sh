#!/usr/bin/env bash
# Captures iOS App Store screenshots with Maestro on configured simulators.
#
# Intended to run from Base-client-deployment.yml AFTER:
#   npm install, npm run build, npx cap sync ios, pod install
#
# This script does NOT repeat those steps. It only:
#   1. Builds a Debug iphonesimulator .app via xcodebuild
#   2. Reads the REAL CFBundleIdentifier from the built .app
#   3. Boots each iOS simulator by UDID, installs the app, runs Maestro
#
# Environment variables (optional):
#   PROJECT_ROOT          - Client repo root (default: directory containing this script)
#   SCREENSHOT_OUTPUT_DIR - Where to write screenshots (default: $PROJECT_ROOT/screenshots)
#   MAESTRO_BIN           - Path to maestro CLI (default: maestro on PATH)
#   MAESTRO_TIMEOUT_SEC   - Max seconds per device Maestro run (default: 900)
#
# In the client repo this script lives at the repository root next to maestro_screenshots.yaml.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="${PROJECT_ROOT:-$SCRIPT_DIR}"
BUILD_ENV_FILE="$PROJECT_ROOT/sys.config/build.environment.json"
FLOW_TEMPLATE="$PROJECT_ROOT/maestro_screenshots.yaml"
FLOW_WORK_FILE="$PROJECT_ROOT/.maestro/ios_screenshots.yaml"
OUTPUT_DIR="${SCREENSHOT_OUTPUT_DIR:-$PROJECT_ROOT/screenshots}"
IOS_WORKSPACE="$PROJECT_ROOT/ios/App/App.xcworkspace"
IOS_SCHEME="${IOS_SCHEME:-App}"
MAESTRO_TIMEOUT_SEC="${MAESTRO_TIMEOUT_SEC:-900}"
BUILT_IOS_APP_PATH=""
ACTIVE_DEVICE_UDID=""

# Simulator display names — must match `xcrun simctl list devices available`.
IOS_DEVICES=(
  "iPhone 14 Plus"
  "iPad Pro 13-inch (M4)"
)

cleanup() {
  local exit_code=$?
  # Stop hung Maestro / Java children so the GitHub Actions step cannot stick forever.
  pkill -f '[m]aestro' 2>/dev/null || true
  if [[ -n "${ACTIVE_DEVICE_UDID:-}" ]]; then
    xcrun simctl shutdown "$ACTIVE_DEVICE_UDID" 2>/dev/null || true
  fi
  exit "$exit_code"
}
trap cleanup EXIT

get_folder_name() {
  echo "$1" | tr '[:upper:]' '[:lower:]' | tr -d ' ' | tr -d '(' | tr -d ')' | tr -d '-'
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "❌ Required command not found: $1"
    exit 1
  fi
}

resolve_maestro() {
  if [[ -n "${MAESTRO_BIN:-}" && -x "$MAESTRO_BIN" ]]; then
    echo "$MAESTRO_BIN"
    return
  fi

  if command -v maestro >/dev/null 2>&1; then
    command -v maestro
    return
  fi

  if [[ -x "$HOME/.maestro/bin/maestro" ]]; then
    echo "$HOME/.maestro/bin/maestro"
    return
  fi

  echo "❌ Maestro CLI not found. Install Maestro or set MAESTRO_BIN."
  exit 1
}

# Prefer the bundle ID baked into the built .app — that is what simctl/Maestro can see.
# build.environment.json may differ if PRODUCT_BUNDLE_IDENTIFIER was not updated in Xcode.
read_app_bundle_id() {
  local app_path="$1"
  local plist="$app_path/Info.plist"

  if [[ ! -f "$plist" ]]; then
    echo "❌ Info.plist not found in app bundle: $plist" >&2
    exit 1
  fi

  /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist"
}

read_configured_bundle_id() {
  if [[ ! -f "$BUILD_ENV_FILE" ]]; then
    echo ""
    return
  fi

  if ! command -v jq >/dev/null 2>&1; then
    echo ""
    return
  fi

  jq -r '.release.appBundleId // empty' "$BUILD_ENV_FILE"
}

prepare_maestro_flow() {
  local bundle_id="$1"

  if [[ ! -f "$FLOW_TEMPLATE" ]]; then
    echo "❌ Maestro flow template not found: $FLOW_TEMPLATE"
    exit 1
  fi

  mkdir -p "$(dirname "$FLOW_WORK_FILE")"
  # Replace both ${BUNDLE_ID} and "${BUNDLE_ID}" placeholders from the template.
  sed "s|\${BUNDLE_ID}|${bundle_id}|g" "$FLOW_TEMPLATE" > "$FLOW_WORK_FILE"
  echo "✅ Prepared Maestro flow for bundle ID: $bundle_id"
  echo "   Flow file: $FLOW_WORK_FILE"
}

get_device_udid() {
  local device_name="$1"
  local udid

  udid="$(
    xcrun simctl list devices available -j \
      | jq -r --arg name "$device_name" '
          .devices
          | to_entries[]
          | .value[]
          | select(.name == $name and .isAvailable == true)
          | .udid
        ' \
      | head -n 1
  )"

  if [[ -z "$udid" || "$udid" == "null" ]]; then
    echo "❌ Simulator not found or unavailable: $device_name" >&2
    echo "   Available devices:" >&2
    xcrun simctl list devices available >&2 || true
    exit 1
  fi

  echo "$udid"
}

run_with_timeout() {
  local timeout_sec="$1"
  shift

  "$@" &
  local cmd_pid=$!

  (
    sleep "$timeout_sec"
    if kill -0 "$cmd_pid" 2>/dev/null; then
      echo "❌ Timed out after ${timeout_sec}s — killing process $cmd_pid" >&2
      kill -TERM "$cmd_pid" 2>/dev/null || true
      sleep 5
      kill -KILL "$cmd_pid" 2>/dev/null || true
      pkill -f '[m]aestro' 2>/dev/null || true
    fi
  ) &
  local watchdog_pid=$!

  local status=0
  wait "$cmd_pid" || status=$?
  kill "$watchdog_pid" 2>/dev/null || true
  wait "$watchdog_pid" 2>/dev/null || true
  return "$status"
}

build_ios_simulator_app() {
  if [[ ! -d "$IOS_WORKSPACE" ]]; then
    echo "❌ iOS workspace not found: $IOS_WORKSPACE" >&2
    exit 1
  fi

  echo "========================================" >&2
  echo "📱 Building iOS Simulator app (Debug)" >&2
  echo "========================================" >&2

  (
    cd "$PROJECT_ROOT/ios/App"
    # Clean derived data for this build so we never install a stale .app.
    rm -rf build
    xcodebuild \
      -workspace App.xcworkspace \
      -scheme "$IOS_SCHEME" \
      -configuration Debug \
      -sdk iphonesimulator \
      -derivedDataPath build \
      CODE_SIGNING_ALLOWED=NO
  )

  BUILT_IOS_APP_PATH="$PROJECT_ROOT/ios/App/build/Build/Products/Debug-iphonesimulator/${IOS_SCHEME}.app"
  if [[ ! -d "$BUILT_IOS_APP_PATH" ]]; then
    echo "❌ Simulator app not found at: $BUILT_IOS_APP_PATH" >&2
    exit 1
  fi

  echo "✅ Simulator app built: $BUILT_IOS_APP_PATH" >&2
}

verify_app_installed() {
  local udid="$1"
  local bundle_id="$2"
  local container=""

  container="$(xcrun simctl get_app_container "$udid" "$bundle_id" app 2>/dev/null || true)"
  if [[ -z "$container" ]]; then
    echo "❌ App is not installed on simulator for bundle ID: $bundle_id" >&2
    echo "   Device UDID: $udid" >&2
    echo "   Installed apps (filtered):" >&2
    xcrun simctl listapps "$udid" 2>/dev/null | grep -E 'CFBundleIdentifier|CFBundleDisplayName' >&2 || true
    return 1
  fi

  echo "✅ Verified install: $bundle_id → $container"
}

capture_ios_screenshots() {
  local bundle_id="$1"
  local ios_app_path="$2"
  local maestro_bin="$3"

  echo "========================================"
  echo "🍏 Capturing iOS simulator screenshots"
  echo "========================================"

  mkdir -p "$OUTPUT_DIR"

  # Avoid "booted" ambiguity when multiple simulators are already running.
  echo "⏹️  Shutting down all simulators for a clean start..."
  xcrun simctl shutdown all 2>/dev/null || true
  sleep 2

  for device in "${IOS_DEVICES[@]}"; do
    local folder
    folder="$(get_folder_name "$device")"
    local device_output="$OUTPUT_DIR/$folder"
    local udid

    echo "▶️  Device: $device"
    udid="$(get_device_udid "$device")"
    ACTIVE_DEVICE_UDID="$udid"
    echo "   UDID: $udid"

    xcrun simctl boot "$udid"
    open -a Simulator --args -CurrentDeviceUDID "$udid" >/dev/null 2>&1 || true

    echo "⏳ Waiting for simulator to boot..."
    xcrun simctl bootstatus "$udid" -b
    sleep 8

    echo "🗑️  Removing any previous install of $bundle_id..."
    xcrun simctl uninstall "$udid" "$bundle_id" 2>/dev/null || true

    echo "📦 Installing app on simulator..."
    xcrun simctl install "$udid" "$ios_app_path"
    verify_app_installed "$udid" "$bundle_id"

    # Keep English locale so Maestro can auto-dismiss the iOS "Allow" notification alert.
    # Must use UDID (not display name) — wrong target is a common CI-only failure mode.
    xcrun simctl spawn "$udid" defaults write "Apple Global Domain" AppleLanguages -array en
    xcrun simctl spawn "$udid" defaults write "Apple Global Domain" AppleLocale -string "en_US"

    # Cold CI installs need extra settle time before WebView/UI automation attaches.
    # Interactive Terminal runs are usually warmer, so this gap is less noticeable there.
    echo "⏳ Settling simulator after install (CI-friendly delay)..."
    sleep 20

    echo "📸 Running Maestro flow (Maestro launches the app)..."
    rm -rf "$device_output"
    mkdir -p "$device_output"

    # Print the resolved flow so Actions logs show exactly what ran (vs Terminal edits).
    echo "----- Maestro flow: $FLOW_WORK_FILE -----"
    sed -n '1,40p' "$FLOW_WORK_FILE" || true
    echo "----------------------------------------"

    if ! run_with_timeout "$MAESTRO_TIMEOUT_SEC" \
      env MAESTRO_DRIVER_STARTUP_TIMEOUT=120000 \
      "$maestro_bin" --device "$udid" test "$FLOW_WORK_FILE" --test-output-dir="$device_output"
    then
      echo "❌ Maestro failed on $device ($udid)"
      echo "   Debug screenshots/logs: $device_output"
      echo "   Open AfterSignIn*.png — if Sign In is still visible, login did not succeed"
      echo "   (tab bar is hidden on /login, so BottomTabBrowse cannot appear)."
      echo "   Open the latest hierarchy/screenshot under that folder for the real UI state."
      xcrun simctl uninstall "$udid" "$bundle_id" 2>/dev/null || true
      xcrun simctl shutdown "$udid" 2>/dev/null || true
      ACTIVE_DEVICE_UDID=""
      exit 1
    fi

    xcrun simctl uninstall "$udid" "$bundle_id" 2>/dev/null || true
    xcrun simctl shutdown "$udid" 2>/dev/null || true
    ACTIVE_DEVICE_UDID=""

    echo "✅ Screenshots saved to: $device_output"
    echo "----------------------------------------"
  done
}

# Actions runners often lack VPN/proxy that an interactive Terminal session has.
# Fail early with a clear message if the demo API is unreachable from this process.
preflight_api_reachability() {
  local host_url=""
  if [[ -f "$BUILD_ENV_FILE" ]]; then
    host_url="$(jq -r '.debug.hostUrl // .release.hostUrl // empty' "$BUILD_ENV_FILE" 2>/dev/null || true)"
  fi
  if [[ -z "$host_url" ]]; then
    echo "⚠️  Skipping API preflight — hostUrl not found in $BUILD_ENV_FILE"
    return 0
  fi

  local api_url="https://${host_url}/api/shared/system/appsettings"
  echo "🌐 API preflight: $api_url"
  if command -v curl >/dev/null 2>&1; then
    if ! curl -fsS -o /dev/null --connect-timeout 15 --max-time 30 "$api_url"; then
      echo "❌ Cannot reach $api_url from this process."
      echo "   Terminal runs often work because your interactive session has VPN/proxy;"
      echo "   the GitHub Actions runner service may not. Fix network for the runner user, then retry."
      exit 1
    fi
    echo "✅ API reachable"
  else
    echo "⚠️  curl not found — skipping API preflight"
  fi
}

main() {
  require_command xcrun
  require_command sed
  require_command jq
  require_command /usr/libexec/PlistBuddy

  echo "Project root : $PROJECT_ROOT"
  echo "Output dir   : $OUTPUT_DIR"

  preflight_api_reachability

  local maestro_bin
  maestro_bin="$(resolve_maestro)"
  echo "Maestro      : $maestro_bin"

  build_ios_simulator_app
  local ios_app_path="$BUILT_IOS_APP_PATH"

  local app_bundle_id
  app_bundle_id="$(read_app_bundle_id "$ios_app_path")"
  if [[ -z "$app_bundle_id" ]]; then
    echo "❌ Could not read CFBundleIdentifier from $ios_app_path"
    exit 1
  fi

  local configured_bundle_id
  configured_bundle_id="$(read_configured_bundle_id)"
  echo "App CFBundleIdentifier : $app_bundle_id"
  if [[ -n "$configured_bundle_id" ]]; then
    echo "build.environment.json : $configured_bundle_id"
    if [[ "$configured_bundle_id" != "$app_bundle_id" ]]; then
      echo "⚠️  Bundle ID mismatch — using the built app's CFBundleIdentifier for Maestro/simctl."
      echo "   Update the iOS PRODUCT_BUNDLE_IDENTIFIER if store screenshots must use the configured ID."
    fi
  fi

  prepare_maestro_flow "$app_bundle_id"
  capture_ios_screenshots "$app_bundle_id" "$ios_app_path" "$maestro_bin"

  echo "🎉 iOS screenshot capture complete."
  echo "📁 All screenshots under: $OUTPUT_DIR"
}

main "$@"
