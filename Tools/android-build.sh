#!/usr/bin/env bash
# Builds Builds/Android/DeepSeaStation.apk without opening the editor. Unity must not have the project open.
# Usage: Tools/android-build.sh [--install]   (--install puts it on a USB-connected phone)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"
UNITY="${UNITY_EDITOR_DIR:-$HOME/Unity/Hub/Editor/$VERSION/Editor}/Unity"
[[ -x "$UNITY" ]] || { echo "Unity $VERSION not found at $UNITY" >&2; exit 2; }

PACKAGE="com.Tony3974.DeepSeaStation"
APK="Builds/Android/DeepSeaStation.apk"
mkdir -p Logs
LOG="Logs/android-build.log"

echo "Building $APK (log: $LOG)..."
if ! "$UNITY" -batchmode -buildTarget Android -projectPath "$ROOT" \
        -executeMethod AndroidBuild.CommandLine -logFile "$LOG"; then
    echo "Build failed. From $LOG:" >&2
    grep -E 'error|Error|Exception|another Unity instance' "$LOG" | grep -v 'ErrorCheck\|errorCode\|0 errors' | sort -u | head -30 >&2 || true
    exit 1
fi
grep 'Deep-Sea Station: built' "$LOG" || true

if [[ "${1:-}" == "--install" ]]; then
    adb install -r "$APK"
    adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null
fi
