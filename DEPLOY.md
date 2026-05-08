# Deploy on mini-speedy

This fork builds to `ghcr.io/samtinkers/teslacamviewer:develop` (and a `:sha-XXXXXXX` tag per commit). Both .NET 5 SDK and runtime base images still pull cleanly from MCR; build pins Debian buster sources to `archive.debian.org` and installs Node 14 from the official tarball (nodesource's old buster integration is no longer GPG-signed post-2023).

## 1. GHCR access (one-time)

The fork is **private** so the container package on GHCR is also private by default. Two options:

**Option A — make the package public** (simpler, recommended; image bytes are not sensitive):

1. https://github.com/users/SamTinkers/packages/container/teslacamviewer/settings
2. Danger Zone → "Change visibility" → Public

**Option B — keep private, authenticate mini-speedy to GHCR** (PAT required):

1. Create a PAT with `read:packages` scope at https://github.com/settings/tokens/new
2. On mini-speedy:
   ```bash
   echo "$GHCR_PAT" | docker login ghcr.io -u SamTinkers --password-stdin
   ```

## 2. Compose service

Create `/docker/composers/teslacamviewer/compose.yml` on mini-speedy (matches the layout in `project_mini_speedy_inventory.md`):

```yaml
services:
  teslacamviewer:
    image: ghcr.io/samtinkers/teslacamviewer:develop
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
      - authorizationEnabled=true   # require login on the web UI
      # If authorizationEnabled=true, the container will prompt to set
      # admin credentials on first browse to /
    user: "1001:1001"

volumes:
  teslacamviewer-config:
```

Notes:
- Mounted **read-only** so the viewer can't accidentally delete your archive.
- `user: 1001:1001` matches the rest of the mini-speedy stack (per `feedback_uid_1001_default.md`).
- Authorization is enabled by default — the web UI will prompt for admin user/pass on first visit. If you want it open on local LAN only, set `authorizationEnabled=false`.

## 3. Bring it up

```bash
ssh mini-speedy
cd /docker/composers/teslacamviewer
docker compose pull
docker compose up -d
docker compose logs -f teslacamviewer    # watch first boot
```

Browse `http://mini-speedy:7544` from the LAN. On first load, you'll be prompted to create the admin user (if `authorizationEnabled=true`).

## 4. Optional: Cloudflare Tunnel route

To expose at `viewer.kernot.au` (per `project_kernot_tunnel.md`):

1. Cloudflare Tunnel → Public Hostname → add `viewer.kernot.au` → service `http://mini-speedy:7544`.
2. CF Access policy → restrict to your Google IdP or Sam-only allowlist.

## 5. What to verify after deploy

- All 6 cameras render in the grid (front, back, left/right repeater, left/right pillar) when you click into a SavedClip event.
- Videos play in Chrome and Edge (previously broken — was MIME `application/octet-stream`).
- Range-request scrubbing works (drag the video timeline) — depends on the existing `enableRangeProcessing: true` on `PhysicalFile`.

## 6. If a future teslausb update or reflash creates new clips that don't appear

Cause: the parser's regex is the source of truth for camera-side detection. If Tesla adds a new camera angle (rare but firmware updates have done it before), the regex in `teslacamviewer.web/Helpers/TeslaFolderHelper.cs` and the SideEnum need new entries. Pull the fork, edit, push — CI rebuilds and republishes.

## 7. Future maintenance debt (eyes-open)

- `.NET 5` is EOL since May 2022. `mcr.microsoft.com/dotnet/sdk:5.0` still works as of 2026-05-08 but Microsoft could remove it any time. If/when that happens, the fork will need a real .NET 8 + Angular (8 → 17+) bump — a multi-day refactor, not a copy-paste-vibe-job.
- Node 14 is also EOL. Same caveat — currently builds fine, but its tarball URL is on nodejs.org which keeps old releases available indefinitely, so this is less time-sensitive.
- If the fork stops building, the runtime container that's already on mini-speedy keeps working — it just can't be rebuilt.
