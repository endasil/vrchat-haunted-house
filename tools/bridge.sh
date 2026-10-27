#!/usr/bin/env bash
# Runs ClaudeBridge commands and prints the result when the run is done.
# Usage: tools/bridge.sh 'command | arg' 'command | arg' ...
#        tools/bridge.sh < commands.txt
# Prints the bridge output, then the errors and exceptions that Unity logged during the run.
# BRIDGE_TIMEOUT sets the maximum wait in seconds (default 600).

set -euo pipefail

project_dir="$(cd "$(dirname "$0")/.." && pwd)"
bridge_dir="$project_dir/Temp/ClaudeBridge"
run_file="$bridge_dir/run.txt"
output_file="$bridge_dir/output.txt"
console_file="$bridge_dir/console.log"
timeout_seconds="${BRIDGE_TIMEOUT:-600}"
poll_seconds=0.5

if [ -f "$run_file" ]; then
    echo "bridge.sh: $run_file already exists, so another run has not started yet" >&2
    exit 1
fi

console_start=0
if [ -f "$console_file" ]; then
    console_start=$(wc -c < "$console_file")
fi

rm -f "$output_file"
if [ "$#" -gt 0 ]; then
    printf '%s\n' "$@" > "$run_file"
else
    cat > "$run_file"
fi

deadline=$((SECONDS + timeout_seconds))
until [ -f "$output_file" ] && [ "$(tail -n 1 "$output_file")" = "== DONE ==" ]; do
    if [ "$SECONDS" -ge "$deadline" ]; then
        echo "bridge.sh: no '== DONE ==' after $timeout_seconds s. Is the Unity Editor open?" >&2
        [ -f "$output_file" ] && cat "$output_file"
        exit 1
    fi
    sleep "$poll_seconds"
done

cat "$output_file"

if [ -f "$console_file" ]; then
    # A clearlog in the run empties the file, so the new lines then start at 0.
    console_size=$(wc -c < "$console_file")
    if [ "$console_size" -lt "$console_start" ]; then
        console_start=0
    fi
    errors=$(tail -c +"$((console_start + 1))" "$console_file" | grep -E '\[(Error|Exception|Assert)\]|\[compile error\]' || true)
    if [ -n "$errors" ]; then
        echo "== UNITY ERRORS =="
        echo "$errors"
    fi
fi

if grep -q '^ERROR ' "$output_file"; then
    exit 2
fi
