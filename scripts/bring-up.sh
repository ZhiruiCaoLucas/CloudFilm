#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Brings the stack up and reports the state of EVERY service, in dependency
# order, without stopping at the first failure.
#
#   ./scripts/bring-up.sh              start, then report
#   ./scripts/bring-up.sh --report     report only, start nothing
#
# For each service it prints the container state, the health status, the exit
# code if it died, and the first error in its log. The first error is what
# matters: everything after it is usually consequence, not cause.
#
# Exits non-zero if any service is not running, so it can gate a script.
# ---------------------------------------------------------------------------
set -uo pipefail

COMPOSE="docker compose -f docker-compose.yml -f docker-compose.dev.yml"
SERVICES=(postgres orthanc retrieve api ohif portal)
SETTLE_SECONDS="${SETTLE_SECONDS:-90}"

# Matched against logs to find the first real failure. Ordered by how specific
# they are, so "not found" does not shadow a clearer message.
ERROR_PATTERN='(FATAL|fatal:|Unhandled exception|System\.[A-Za-z.]*Exception|panic:|not found|Connection refused|password authentication failed|no such file|permission denied|ERR_|Error:)'

if [[ "${1:-}" != "--report" ]]; then
    echo "Starting the stack (this pulls images on first run)…"
    $COMPOSE up -d
    echo
    echo "Waiting up to ${SETTLE_SECONDS}s for services to settle…"
    for _ in $(seq 1 "$SETTLE_SECONDS"); do
        starting=$($COMPOSE ps --format '{{.Health}}' 2>/dev/null | grep -c starting)
        [[ "$starting" == "0" ]] && break
        sleep 1
    done
fi

state_of() {
    # Fields are joined with '|', not a tab. Tab is whitespace, and bash's read
    # collapses consecutive whitespace delimiters - so a service with no health
    # check, whose Health field is empty, would have its exit code read as its
    # health status and be reported as failed.
    $COMPOSE ps -a --format '{{.Service}}\t{{.State}}\t{{.Health}}\t{{.ExitCode}}' 2>/dev/null \
        | awk -F'\t' -v s="$1" '$1 == s { print $2 "|" $3 "|" $4; found = 1 } END { if (!found) print "absent||" }'
}

first_error_of() {
    $COMPOSE logs --no-color --tail 400 "$1" 2>&1 \
        | grep -m1 -E "$ERROR_PATTERN" \
        | cut -c1-200
}

printf '\n%-10s %-12s %-11s %-5s %s\n' SERVICE STATE HEALTH EXIT "FIRST ERROR"
printf '%s\n' "-------------------------------------------------------------------------------"

failed=()
for service in "${SERVICES[@]}"; do
    IFS='|' read -r state health exit_code <<<"$(state_of "$service")"
    # An empty Health means the service declares no health check. That is not a
    # failure - ohif deliberately has none.
    [[ -z "${health:-}" ]] && health='none'
    [[ -z "${exit_code:-}" ]] && exit_code='-'

    if [[ "$state" == "running" && ( "$health" == "healthy" || "$health" == "none" ) ]]; then
        error=''
    else
        error="$(first_error_of "$service")"
        failed+=("$service")
    fi

    printf '%-10s %-12s %-11s %-5s %s\n' "$service" "$state" "$health" "$exit_code" "${error:-}"
done

if [[ ${#failed[@]} -eq 0 ]]; then
    echo
    echo "All services are up. Continue with: pnpm verify:imaging"
    exit 0
fi

echo
echo "==============================================================================="
echo "Detail for the services that did not come up, in dependency order."
echo "Fix the earliest one first: the later ones may only be waiting on it."
echo "==============================================================================="
for service in "${failed[@]}"; do
    echo
    echo "----- $service ------------------------------------------------------------"
    $COMPOSE logs --no-color --tail 40 "$service" 2>&1
done

echo
echo "Failed: ${failed[*]}"
exit 1
