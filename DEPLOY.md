# Deploy on mini-speedy

Source-build deployment — mini-speedy clones this fork and builds the image locally. No registry, no auth, no PAT. GitHub holds the source as a remote backup; CI verifies the Dockerfile still builds on every push but doesn't publish anything.

## One-time setup

```bash
ssh mini-speedy
sudo mkdir -p /docker/composers/teslacamviewer
sudo chown mini-docker:mini-docker /docker/composers/teslacamviewer
cd /docker/composers/teslacamviewer
git clone https://github.com/SamTinkers/teslacamviewer.git src
```

(Repo is private, so the clone needs an authenticated git method — either an SSH key on mini-speedy with read access, or a PAT-embedded HTTPS URL. mini-speedy already has SSH access to other private repos per `feedback_mini_speedy_deploy_key.md`.)

Then create `/docker/composers/teslacamviewer/compose.yml`:

```yaml
services:
  teslacamviewer:
    build:
      context: ./src
      dockerfile: Dockerfile
    image: teslacamviewer:local
    container_name: teslacamviewer
    restart: unless-stopped
    ports:
      - "7544:80"
    volumes:
      - /data/teslacam:/teslacamdata:ro
      - teslacamviewer-config:/app/config
    environment:
      - TZ=Australia/Sydney
      - PUID=1001
      - PGID=1001
      - authorizationEnabled=true
    user: "1001:1001"

volumes:
  teslacamviewer-config:
```

Notes:
- `/data/teslacam` mounted **read-only** — viewer can't accidentally delete archive.
- `user: 1001:1001` matches the rest of the mini-speedy stack (per `feedback_uid_1001_default.md`).
- `authorizationEnabled=true` prompts to create an admin user/pass on first browse. Set to `false` if you want it open on local LAN only.
- Build takes ~2 min the first time (Angular 8 npm install dominates), then is cached for subsequent rebuilds.

## Bring it up

```bash
cd /docker/composers/teslacamviewer
docker compose build
docker compose up -d
docker compose logs -f teslacamviewer
```

Browse `http://mini-speedy:7544` from the LAN. First load prompts to create the admin user (if auth enabled).

## Updating after I push patches

```bash
cd /docker/composers/teslacamviewer/src
git pull
cd ..
docker compose build
docker compose up -d
```

The `git pull` fetches the new code from GitHub; `docker compose build` rebuilds with cache (only changed layers re-execute); `up -d` restarts with the new image.

## Optional: Cloudflare Tunnel route

To expose at `viewer.kernot.au` (per `project_kernot_tunnel.md`):

1. Cloudflare Tunnel → Public Hostname → `viewer.kernot.au` → service `http://mini-speedy:7544`
2. CF Access policy → restrict to your Google IdP or Sam-only allowlist

## What to verify after deploy

- All 6 cameras render in the grid (front, back, left/right repeater, left/right pillar) when you click into a SavedClip event
- Videos play in Chrome and Edge (previously broken — was MIME `application/octet-stream`)
- Range-request scrubbing works (drag the video timeline)

## Rollback

```bash
cd /docker/composers/teslacamviewer
docker compose down
docker rmi teslacamviewer:local
# Edit src/ or revert to a known-good commit:
cd src && git checkout <prior-sha>
cd .. && docker compose build && docker compose up -d
```

## Maintenance debt (eyes-open)

- `.NET 5` is EOL since May 2022. `mcr.microsoft.com/dotnet/sdk:5.0` still pulls cleanly as of 2026-05-08 but Microsoft could remove it any time. If/when that happens, the fork needs a real `.NET 8` + Angular (8 → 17+) bump — multi-day refactor, not a copy-paste-vibe-job.
- Node 14 is also EOL. Same caveat — currently builds fine, but its tarball URL is on nodejs.org which keeps old releases available indefinitely.
- If the source ever stops building on mini-speedy, the *running* container keeps working — Docker doesn't tear down a container just because its source can't be rebuilt. Plenty of warning before anything breaks.
