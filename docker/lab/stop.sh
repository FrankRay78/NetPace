#!/usr/bin/env bash
# Stop and remove everything the slow-link lab started — the link and both servers.
#
#   ./stop.sh
#
# The lab's Compose project is named, so this removes the lab's own containers whichever servers
# were started, and nothing else.
set -euo pipefail

cd "$(dirname "$0")"

docker compose down --remove-orphans
