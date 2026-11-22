# cat-tracker test suite

Automated regression coverage for attachments (general + per-appointment) and
appointment CRUD, added after commit `c31740d`.

**These suites perform POST/PUT/DELETE requests. They default to the isolated
test instance and hard-refuse to run against the user's live instance.**

## What runs against what

| Suite | Tooling | Coverage |
|---|---|---|
| `run-tests.sh` | bash + curl | API: auth (login/logout/throttle-friendly single wrong-password attempt), appointment CRUD round-trip, general attachment upload→list→download (md5 equality)→delete→disk-blob check, appointment attachment lifecycle, appointment-DELETE cascade (metadata **and** blob on disk), validation matrix (401/415/413/404/400), idempotent fixture sweep |
| `ui-smoke.py` | Playwright (Chromium, headless) | UI: modal login, Documents tab lists uploads, Calendar appointment shows a decoding thumbnail, tab switch hides appointment-specific DOM, logout removes upload controls, open-read listing survives lock |

## Prerequisites

- Test container running:
  `docker run -d --name cat-tracker-test -p 8094:8080 -e APP_PASSWORD=$(cat ~/.hermes/cache/scratch/cat-tracker-test/testpw) -v ~/.hermes/cache/scratch/cat-tracker-test/data:/app/data cat-tracker-cat-tracker:latest`
- `curl`, `python3`, `md5sum` (suite generates its own PNG/PDF/txt/oversized fixtures into a temp dir under `~/.hermes/cache/scratch` at run time — nothing binary is committed).
- For the UI suite only: the Playwright venv
  (`~/.hermes/installs/5e5705019918c98b/environments/97f21d4921164c2fb981b1c5d2dc45ac/venv/bin/python3`, browsers via `playwright install chromium`).
- No NuGet packages are added to the app — the suites live entirely outside the .NET project.

## Running

```bash
# API suite (52 checks) — defaults to http://localhost:8094
bash tests/run-tests.sh

# UI smoke (15 checks)
~/.hermes/installs/5e5705019918c98b/environments/97f21d4921164c2fb981b1c5d2dc45ac/venv/bin/python3 tests/ui-smoke.py
```

Both print per-test `PASS`/`FAIL  <name>` lines and exit non-zero on any failure.
Both are idempotent: every fixture is named `ZZ-AUTOTEST*` / `ZZ-UI*`, is
swept at start *and* end, and UI-screenshots (only on failure) go to
`~/.hermes/cache/scratch/cat-tracker-ui-screenshots/`.

### Environment overrides

| Variable | Default | Purpose |
|---|---|---|
| `BASE_URL` | `http://localhost:8094` | target instance |
| `TESTPW_FILE` | `~/.hermes/cache/scratch/cat-tracker-test/testpw` | login password file |
| `DATA_DIR` | `~/.hermes/cache/scratch/cat-tracker-test/data` | host-side mount, **read-only** use for blob-existence assertions |
| `SCRATCH_DIR` / `SHOT_DIR` | `~/.hermes/cache/scratch` | fixtures / screenshots |

## The 8093 guard — rationale

Port **8093 is the user's live instance** (their real cat data). This suite
deletes appointments and files. Both scripts therefore scan `BASE_URL` for
`:8093` and exit immediately without sending a single request; `run-tests.sh`
additionally refuses if `DATA_DIR` resolves into the live project's
`dev-projects/cat-tracker/data`. Any future host/port split must keep these
guards: point the suites at a throwaway container instead.

## Notes found while writing the tests

- **Test-env pitfall (not an app-code bug, but real):** `docker-entrypoint.sh`
  chowns only `/app/data` itself, **not recursively**. If the bind-mounted
  `data/attachments/` subdir exists on the host with a different owner, every
  upload 500s with `UnauthorizedAccessException`. Fix the mount:
  `docker exec cat-tracker-test chown -R appuser:appuser /app/data`.
- Unauthenticated writes return plain `401` (no redirect) because
  `Program.cs` overrides `OnRedirectToLogin` — asserted by test 39/40/48.
- Login throttle locks an IP for 5 min after 5 wrong passwords; the suites
  make exactly **one** wrong-password attempt and always recover with a
  successful login (which resets the counter), so repeated runs never trip it.
