# Deployment

The workflow in `.github/workflows/ci.yml` runs the tests and publishes a public image to
`ghcr.io/erloon/ai-shopping-readiness-lab` (`:latest` and `:<commit-sha>`). It holds no host
address, SSH key or other server secret.

The host pulls new images itself:

| File on the host | Source |
| --- | --- |
| `/opt/ai-shopping-readiness-lab/pull-update.sh` | `deploy/pull-update.sh` |
| `/etc/systemd/system/ai-shopping-readiness-lab-update.service` | `deploy/systemd/*.service` |
| `/etc/systemd/system/ai-shopping-readiness-lab-update.timer` | `deploy/systemd/*.timer` |
| `/etc/ai-shopping-readiness-lab.env` | `PUBLIC_BASE_URL=https://<host>/ai-shopping-readiness-lab` |

```sh
systemctl daemon-reload
systemctl enable --now ai-shopping-readiness-lab-update.timer
systemctl start ai-shopping-readiness-lab-update.service   # deploy now
```

The script pulls `:latest`, replaces the container only when the image changed, checks
`/health`, and restores the previous image if the check fails. The container is read-only,
non-root, capped at 256 MB / 0.5 CPU, has `--restart=always`, and listens on loopback only;
a reverse proxy publishes it under the path base.

Run a specific version: `docker run -p 8080:8080 ghcr.io/erloon/ai-shopping-readiness-lab:<sha>`.
