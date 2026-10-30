#!/usr/bin/env bash
# Pull-based updater, run on the host by a systemd timer (see deploy/README.md).
# The GitHub workflow only publishes a public image; it never connects to the host.
set -euo pipefail

IMAGE=${IMAGE:-ghcr.io/erloon/ai-shopping-readiness-lab:latest}
NAME=${NAME:-ai-shopping-readiness-lab}
BIND=${BIND:-127.0.0.1:20149}
PATH_BASE=${PATH_BASE:-/ai-shopping-readiness-lab}
PUBLIC_BASE_URL=${PUBLIC_BASE_URL:?set PUBLIC_BASE_URL, e.g. https://examples.example.com/ai-shopping-readiness-lab}

exec 9>"/run/$NAME.lock"
flock -n 9 || exit 0

# Pull anonymously: host-wide registry credentials may be scoped to other packages and get denied.
export DOCKER_CONFIG=${DOCKER_CONFIG:-/var/lib/$NAME/docker-config}
mkdir -p "$DOCKER_CONFIG"

docker pull -q "$IMAGE" >/dev/null
new=$(docker image inspect -f '{{.Id}}' "$IMAGE")
current=$(docker inspect -f '{{.Image}}' "$NAME" 2>/dev/null || true)
if [ "$new" = "$current" ]; then
  exit 0
fi

run() {
  docker run -d --name "$NAME" --restart=always \
    -p "$BIND:8080" \
    --read-only --tmpfs /tmp:size=32m \
    --memory 256m --cpus 0.5 --pids-limit 200 \
    --cap-drop ALL --security-opt no-new-privileges \
    --log-opt max-size=5m --log-opt max-file=2 \
    -e Lab__PublicBaseUrl="$PUBLIC_BASE_URL" -e Lab__PathBase="$PATH_BASE" \
    "$1" >/dev/null
}

if [ -n "$current" ]; then
  docker rm -f "$NAME" >/dev/null
fi
run "$new"

for _ in $(seq 1 30); do
  if curl -fsS "http://$BIND$PATH_BASE/health" >/dev/null 2>&1; then
    echo "Deployed $new"
    exit 0
  fi
  sleep 2
done

echo "Health check failed for $new; rolling back" >&2
docker rm -f "$NAME" >/dev/null
if [ -n "$current" ]; then
  run "$current"
fi
exit 1
