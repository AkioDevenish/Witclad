#!/bin/bash
# Builds Wildtide and installs it on the iPhone plugged into this Mac.
#
#   curl -fsSL https://raw.githubusercontent.com/AkioDevenish/Witclad/claude/bold-curie-0aqljk/unity/Wildtide/build-iphone.sh -o /tmp/wt.sh && bash /tmp/wt.sh
#
# Each run downloads the latest game from GitHub into ~/Wildtide-build (keeping Unity's cache, so later builds
# are faster), builds it and installs it. Run it again whenever the game changes, or every 7 days on a free Apple ID.
# Needs: Unity 6.0 with iOS Build Support, Xcode signed in to your Apple ID (Xcode > Settings > Accounts),
# and the iPhone plugged in, unlocked, trusted, with Developer Mode on.
# Optional: BRANCH=some-branch (which version to download)  PROJECT=/path/to/unity/Wildtide (build a local copy instead)
#           TEAM_ID=ABCDE12345  BUNDLE_ID=com.you.wildtide
set -euo pipefail

say()  { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31mSTOPPED: %s\033[0m\n' "$*"; exit 1; }

# ---- 1. Get the latest game ---------------------------------------------------------------------
BRANCH=${BRANCH:-claude/bold-curie-0aqljk}
if [ -z "${PROJECT:-}" ]; then
  say "Downloading the latest Wildtide ($BRANCH)"
  WORK="$HOME/Wildtide-build"
  PROJECT="$WORK/Wildtide"
  ZIP=$(mktemp -d)
  curl -fsSL "https://codeload.github.com/AkioDevenish/Witclad/zip/refs/heads/$BRANCH" -o "$ZIP/game.zip" \
    || fail "Couldn't download the game from GitHub. Check your internet connection and try again."
  unzip -q "$ZIP/game.zip" -d "$ZIP"
  SRC=$(find "$ZIP" -maxdepth 4 -type d -path '*/unity/Wildtide' | head -n 1)
  [ -n "$SRC" ] || fail "The download didn't contain unity/Wildtide."
  mkdir -p "$PROJECT"
  # Replace the code but keep Unity's Library cache and past builds.
  rsync -a --delete --exclude /Library --exclude /Temp --exclude /Logs --exclude /UserSettings --exclude /Builds "$SRC/" "$PROJECT/"
  rm -rf "$ZIP"
fi
[ -n "${PROJECT:-}" ] && [ -d "$PROJECT/Assets" ] || fail "No Unity project at $PROJECT."
echo "Project: $PROJECT"

[ -f "$PROJECT/Assets/Wildtide/Editor/CiBuild.cs" ] || fail "That copy of the project is too old for this script. Run it without PROJECT= to download the latest."

if [ -f "$PROJECT/Temp/UnityLockfile" ] && lsof "$PROJECT/Temp/UnityLockfile" >/dev/null 2>&1; then
  fail "The project is open in Unity. Quit Unity (Cmd+Q), then run this again."
fi

# ---- 2. Find Unity ------------------------------------------------------------------------------
say "Looking for Unity 6"
UNITY=$(ls -d /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort | tail -n 1 || true)
[ -n "$UNITY" ] || fail "No Unity 6 found in /Applications/Unity/Hub/Editor. Install Unity 6.0 LTS from Unity Hub."
UNITY_DIR=$(dirname "$(dirname "$(dirname "$(dirname "$UNITY")")")")
[ -d "$UNITY_DIR/PlaybackEngines/iOSSupport" ] || fail "Unity has no iOS Build Support. In Unity Hub > Installs > (gear) > Add modules, tick iOS Build Support."
echo "Unity: $UNITY_DIR"

# ---- 3. Find the iPhone -------------------------------------------------------------------------
say "Looking for your iPhone"
xcrun --find devicectl >/dev/null 2>&1 || fail "Xcode isn't set up. Open Xcode once, accept the license, then run: sudo xcode-select -s /Applications/Xcode.app"
DEVICES_JSON=$(mktemp)
xcrun devicectl list devices --json-output "$DEVICES_JSON" >/dev/null 2>&1 || true
read -r DEVICE_ID DEVICE_UDID DEVICE_NAME < <(/usr/bin/python3 - "$DEVICES_JSON" <<'PY'
import json, sys
try:
    devices = json.load(open(sys.argv[1]))["result"]["devices"]
except Exception:
    devices = []
for d in devices:
    hw, props = d.get("hardwareProperties", {}), d.get("deviceProperties", {})
    if hw.get("platform") == "iOS" and hw.get("reality") == "physical":
        print(d["identifier"], hw.get("udid", ""), props.get("name", "iPhone").replace(" ", "_"))
        break
PY
) || true
[ -n "${DEVICE_ID:-}" ] || fail "No iPhone found. Plug it in with a cable, unlock it, tap Trust, and check Settings > Privacy & Security > Developer Mode is on."
echo "iPhone: ${DEVICE_NAME//_/ } ($DEVICE_UDID)"

# ---- 4. Find your Apple team --------------------------------------------------------------------
say "Looking for your Apple ID in Xcode"
if [ -z "${TEAM_ID:-}" ]; then
  TEAM_ID=$(defaults read com.apple.dt.Xcode IDEProvisioningTeamByIdentifier 2>/dev/null | grep -Eo 'teamID = "?[A-Z0-9]{10}' | head -n 1 | grep -Eo '[A-Z0-9]{10}$' || true)
fi
if [ -z "${TEAM_ID:-}" ]; then
  TEAM_ID=$(defaults read com.apple.dt.Xcode IDEProvisioningTeams 2>/dev/null | grep -Eo 'teamID = "?[A-Z0-9]{10}' | head -n 1 | grep -Eo '[A-Z0-9]{10}$' || true)
fi
[ -n "${TEAM_ID:-}" ] || fail "Xcode isn't signed in to an Apple ID. Open Xcode > Settings > Accounts > + > Apple ID, sign in, then run this again."
echo "Team: $TEAM_ID"
# Free Apple IDs need a bundle id nobody else has registered; make it personal.
BUNDLE_ID=${BUNDLE_ID:-com.witclad.wildtide.$(echo "$TEAM_ID" | tr '[:upper:]' '[:lower:]')}
echo "Bundle id: $BUNDLE_ID"

# ---- 5. Unity -> Xcode project ------------------------------------------------------------------
BUILD="$PROJECT/Builds"
mkdir -p "$BUILD"
LOG="$BUILD/unity-build.log"
say "Building with Unity (first time: 5-15 minutes). Log: $LOG"
set +e
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod Wildtide.EditorTools.CiBuild.Build \
  -customBuildTarget iOS -customBuildPath "$BUILD/iOS" -bundleId "$BUNDLE_ID" \
  -logFile "$LOG"
UNITY_STATUS=$?
set -e
if [ $UNITY_STATUS -ne 0 ] || [ ! -d "$BUILD/iOS/Unity-iPhone.xcodeproj" ]; then
  printf '\n\033[1;31mUnity build failed. Copy everything between the lines and send it to Claude:\033[0m\n'
  echo "------------------------------------------------------------"
  grep -E "error CS|Error|error:|Exception|Wildtide CI" "$LOG" | grep -v "^\s*$" | sort -u | head -n 60
  echo "------------------------------------------------------------"
  exit 1
fi

# ---- 6. Xcode: sign and build -------------------------------------------------------------------
say "Signing and building with Xcode (a few minutes)"
XLOG="$BUILD/xcode-build.log"
set +e
xcodebuild build \
  -project "$BUILD/iOS/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination "id=$DEVICE_UDID" \
  -derivedDataPath "$BUILD/DerivedData" \
  -allowProvisioningUpdates \
  DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic \
  > "$XLOG" 2>&1
XCODE_STATUS=$?
set -e
if [ $XCODE_STATUS -ne 0 ]; then
  printf '\n\033[1;31mXcode build failed. Copy everything between the lines and send it to Claude:\033[0m\n'
  echo "------------------------------------------------------------"
  grep -E "error:|Signing|provisioning|BUILD FAILED" "$XLOG" | sort -u | head -n 40
  echo "------------------------------------------------------------"
  exit 1
fi
APP=$(ls -d "$BUILD"/DerivedData/Build/Products/Release-iphoneos/*.app | head -n 1)

# ---- 7. Install and launch ----------------------------------------------------------------------
say "Installing on your iPhone"
xcrun devicectl device install app --device "$DEVICE_ID" "$APP"
say "Launching"
if ! xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE_ID" >/dev/null 2>&1; then
  printf '\n\033[1;33mInstalled! The first time, iOS blocks apps from a new developer:\033[0m\n'
  echo "  On the iPhone: Settings > General > VPN & Device Management > (your Apple ID) > Trust."
  echo "  Then tap the Wildtide icon on your home screen."
  exit 0
fi
printf '\n\033[1;32mDone! Wildtide is running on your iPhone.\033[0m\n'
echo "With a free Apple ID it stops opening after 7 days. Plug in and run this command again to refresh it."
