# Connect: Git-based Task Channel for mini-speedy

A secure, pull-based "git mailbox" that lets a cloud-side Claude session issue a
**curated set of tasks** to the home server (mini-speedy), triggered entirely
through git. No inbound ports, no exposed SSH, no live connection — the server
only ever makes *outbound* pulls to GitHub.

This document is the design/plan. The server side is built and installed by Sam
from here.

## Why this works

mini-speedy already pulls this repo (`/docker/composers/teslacamviewer/src`,
see `DEPLOY.md`). The task channel reuses that same trust path: GitHub is a
shared mailbox both sides can reach, the server polls it, and writes results
back as commits.

```
me ──push request (task name + params)──▶ repo [server-channel branch]
                                                      │
                              mini-speedy agent pulls │ (systemd timer, ~60s)
                                                      ▼
                          validate name ∈ allowlist ──▶ run tasks/<name>.sh
                                                      │
me ◀── read result ◀── push result ◀── capture output┘
```

## Core security principle

The git channel **never carries commands to run** — only the **name** of a
pre-approved task plus tightly-validated parameters. The actual code lives in
version-controlled, reviewed scripts (`tasks/*.sh`). Worst case, a tampered
mailbox can only invoke an already-safe, predefined action.

## Components (under `server-agent/` in the repo)

| File | Role |
|------|------|
| `agent.sh` | The loop: fetch channel branch -> read new requests -> validate -> dispatch -> write results -> commit/push |
| `tasks/*.sh` | The **only** things that can execute. One small script per allowed action. Reviewed, in git. |
| `allowlist` | Explicit list of permitted task names — defense in depth |
| `systemd/teslacam-agent.{service,timer}` | Runs the agent on a timer as a non-root user |
| `INSTALL.md` | Setup, auth, how to enable mutating tasks, kill switch |

## The channel

A dedicated branch (`server-channel`), isolated from `main`/`develop` so it
never touches app code or triggers CI:

- `inbox/` — request files pushed by the cloud session (task name + params + unique id)
- `outbox/` — results the server commits back, then read by the cloud session

### Request file format (proposed)

```json
{
  "id": "2026-06-20T13-05-00Z-a1b2",
  "task": "logs",
  "params": { "lines": 100 }
}
```

### Result file format (proposed)

```json
{
  "id": "2026-06-20T13-05-00Z-a1b2",
  "task": "logs",
  "exit_code": 0,
  "started_at": "2026-06-20T13:05:12Z",
  "finished_at": "2026-06-20T13:05:13Z",
  "output": "...captured stdout/stderr..."
}
```

## Curated task set

### Read-only (safe, on by default)

- `status` — is the container up? health, uptime (`docker compose ps`)
- `disk` — usage of `/data/teslacam` + docker volumes
- `logs` — last N lines of container logs (N validated as integer <= cap)
- `sys` — load / memory snapshot

### Mutating (opt-in, individually enabled)

- `restart` — `docker compose restart teslacamviewer`
- `update` — the existing pull-src -> rebuild -> `up -d` flow from `DEPLOY.md`

## Guardrails

1. **No arbitrary exec** — mailbox carries a task *name*, never a shell string.
2. **Strict param validation** — each task declares/validates its params; nothing passed unquoted to the shell.
3. **Allowlist enforced** — unknown task names are rejected and logged, never run.
4. **Least privilege** — agent runs as a dedicated non-root user; mutating tasks gated separately (narrow sudoers / docker-group rule only if needed).
5. **Replay protection** — each request has a unique id; processed ids are recorded so a request runs once, not on every poll.
6. **Kill switch** — a `PAUSE` file the agent honors to instantly stop executing anything.
7. **Full audit trail** — every request and result is a commit. Exactly what was asked and what happened.

### Trust boundary

The repo is private, so "who can issue tasks" = "who has write access" = Sam +
the cloud session. **Optional hardening (v2):** require GPG-signed commits and
have the agent verify the signature before executing.

### Secrets

The server's push-back credential (deploy key or scoped PAT) is configured on
mini-speedy by Sam; it never goes in the repo. `.gitignore` guards against
accidental commits.

## Server-side flow (what the agent does each cycle)

1. Check for `PAUSE` file -> if present, exit (kill switch).
2. `git fetch` the `server-channel` branch.
3. List `inbox/` request files; skip any id already in the processed log.
4. For each new request:
   - Parse JSON; reject if `task` not in `allowlist`.
   - Validate params per the task's declared schema.
   - Run `tasks/<task>.sh` as the non-root user, capturing stdout/stderr + exit code.
   - Write a result file to `outbox/`.
   - Record the id in the processed log.
5. Commit + push results to `server-channel`.

## Trigger on the server

systemd timer (preferred — clean journald logs, easy enable/disable) running
`agent.sh` every ~60s. Plain cron is a fallback.

## Open decisions (to confirm before/while building the server side)

1. **Task scope:** read-only set only for v1, or include `restart`/`update`?
2. **Trigger:** systemd timer (recommended) or cron?
3. **Signed-commit verification:** include in v1, or rely on private-repo-write trust and add later?
4. **Poll interval:** default 60s — faster/slower?

## Build split

- **Cloud session (me):** repo side — `server-agent/` scripts, allowlist,
  systemd units, channel branch + `inbox/`/`outbox/` conventions, request/result
  helpers, docs.
- **Sam (on mini-speedy):** install per `INSTALL.md`, set up push-back auth,
  enable the timer, choose which mutating tasks (if any) are turned on.

## How to kick off the build (next session)

The cloud session can scaffold the **entire repo side** once the four open
decisions above are answered. To start, just reply with answers to:

1. Read-only tasks only for v1, or include `restart`/`update`?
2. systemd timer (recommended) or cron?
3. Signed-commit verification now, or later?
4. Poll interval (default 60s)?

Then the cloud session writes `server-agent/agent.sh`, `tasks/*.sh`,
`allowlist`, the systemd units, the `server-channel` branch conventions, and
`INSTALL.md`.

## The one piece the cloud session cannot do

The agent needs a **push-back credential** on mini-speedy so it can commit
results to the `server-channel` branch — a **deploy key** (SSH, write-scoped to
this repo) or a **fine-scoped PAT**. This is set up by Sam on the server and
**never** stored in the repo. mini-speedy already has SSH access to private
repos (per `DEPLOY.md` / `feedback_mini_speedy_deploy_key.md`), so a write-
enabled deploy key is the natural choice.

Everything else — the cloud session can't reach mini-speedy directly, so the
install + auth steps are run by Sam from `INSTALL.md`.
