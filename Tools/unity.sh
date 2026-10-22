#!/usr/bin/env bash
# Runs the pinned Unity editor in batch mode against this project.
#   Tools/unity.sh compile                 import + compile, fail on any C# error
#   Tools/unity.sh test [EditMode|PlayMode] [filter]
#   Tools/unity.sh exec <Class.Method> [extra args]   run an editor method (builds, codegen, setup)
# Logs and results go to Artifacts/. Exit code is non-zero on failure.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
OUT="$ROOT/Artifacts"
mkdir -p "$OUT"

if [ -f "$ROOT/Temp/UnityLockfile" ] && pgrep -f "Unity.*-projectPath.*$ROOT|Unity.app.*$ROOT" >/dev/null 2>&1; then
  echo "The project is open in another Unity instance. Close it and retry." >&2
  exit 3
fi

cmd="${1:-compile}"
shift || true

compile_errors() {
  grep -E "error CS[0-9]+|Scripts have compiler errors|error: " "$1" | grep -v "^\s*$" | sort -u | head -60
}

case "$cmd" in
  compile)
    LOG="$OUT/compile.log"
    "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -logFile "$LOG"
    code=$?
    errors="$(compile_errors "$LOG")"
    if [ -n "$errors" ]; then echo "$errors"; exit 1; fi
    [ $code -ne 0 ] && { tail -40 "$LOG"; exit $code; }
    echo "compile: ok"
    ;;
  test)
    PLATFORM="${1:-EditMode}"
    FILTER="${2:-}"
    LOG="$OUT/test-$PLATFORM.log"
    XML="$OUT/test-$PLATFORM.xml"
    rm -f "$XML"
    args=(-batchmode -projectPath "$ROOT" -runTests -testPlatform "$PLATFORM" -testResults "$XML" -logFile "$LOG")
    [ "$PLATFORM" = "EditMode" ] && args+=(-nographics)
    [ -n "$FILTER" ] && args+=(-testFilter "$FILTER")
    "$UNITY" "${args[@]}"
    code=$?
    errors="$(compile_errors "$LOG")"
    if [ -n "$errors" ]; then echo "$errors"; exit 1; fi
    if [ ! -f "$XML" ]; then echo "No test results written. See $LOG"; tail -30 "$LOG"; exit 1; fi
    python3 -I - "$XML" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print(f"tests: {root.get('total')} total, {root.get('passed')} passed, {root.get('failed')} failed, {root.get('skipped')} skipped")
for case in root.iter('test-case'):
    if case.get('result') == 'Failed':
        msg = case.find('failure/message')
        print(f"FAIL {case.get('fullname')}\n    {(msg.text or '').strip()[:800]}")
PY
    exit $code
    ;;
  exec)
    METHOD="$1"; shift
    LOG="$OUT/exec-${METHOD##*.}.log"
    "$UNITY" -batchmode -quit -projectPath "$ROOT" -executeMethod "$METHOD" -logFile "$LOG" "$@"
    code=$?
    errors="$(compile_errors "$LOG")"
    if [ -n "$errors" ]; then echo "$errors"; exit 1; fi
    grep -E "^\[Tileworks\]" "$LOG" | tail -40
    [ $code -ne 0 ] && { echo "exit $code, see $LOG"; tail -30 "$LOG"; }
    exit $code
    ;;
  *)
    echo "unknown command: $cmd" >&2; exit 2 ;;
esac
