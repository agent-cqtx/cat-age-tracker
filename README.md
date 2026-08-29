# Cat Age Tracker

A small self-hosted app for tracking a cat's age: current age in cat years
*and* the commonly-used human-year equivalent, a scrubbable timeline of
their life stages past and future, a vet/grooming/medication calendar, and
a place to save other details (microchip number, food preferences, weight
log, whatever you want).

On first launch it walks you through a short setup: your cat's name,
birthdate, and a visual theme picked to roughly match their coloring
(Tuxedo, Black, White, Gray, or Ginger). You can change any of that later
from the gear icon. Editing is behind a single shared password; viewing is
open.

## Stack

- **Backend:** ASP.NET Core 8 minimal API (C#). Deliberately has **zero
  external NuGet packages** — cookie authentication and JSON are both part
  of the ASP.NET Core / .NET base class library, so builds are fast,
  reproducible, and don't depend on NuGet being reachable at build time.
- **Storage:** a small hand-rolled JSON-file store (`Services/JsonFileStore.cs`)
  instead of a SQL database. For one person's cat, a handful of appointments
  and notes doesn't need a database engine, and this keeps the whole stack
  dependency-free. If you ever want to swap it for EF Core + SQLite or
  Postgres, the repository classes (`ProfileRepository`,
  `AppointmentRepository`, `DetailRepository`) are the seam to do it behind
  — nothing else in the app talks to storage directly.
- **Frontend:** plain HTML/CSS/JS served from `wwwroot/` (no build step, no
  framework). Each theme is a small CSS variable palette plus a couple of
  class toggles on one shared cat-silhouette icon — see "Theming" below.
- **Auth:** ASP.NET Core cookie authentication, single shared password from
  the `APP_PASSWORD` environment variable, with a basic brute-force lockout
  on the login endpoint.

## Running it

1. Copy the example environment file and set a real password:

   ```bash
   cp .env.example .env
   # edit .env and set APP_PASSWORD to something only you know
   ```

2. Build and start:

   ```bash
   docker compose up -d --build
   ```

3. Open **http://localhost:8080** (or your Docker host's address/port if
   remote — e.g. `http://<mint-box-ip>:8080`) and follow the on-screen setup.

Data persists in a Docker named volume (`cat-data`) mounted at `/app/data`
inside the container, as three small JSON files (`profile.json`,
`appointments.json`, `details.json`). To back it up, first find the actual
volume name (Compose prefixes it with the project folder name, so it's
usually `cat-tracker_cat-data`, but check to be sure):

```bash
docker volume ls | grep cat-data

docker run --rm -v cat-tracker_cat-data:/data -v "$PWD":/backup \
  alpine tar czf /backup/cat-data-backup.tar.gz -C /data .
```

If you forget to set `APP_PASSWORD`, the container **won't silently run
wide open** — it generates a random password each restart and prints it to
the container logs (`docker compose logs cat-tracker`). Set a real one in
`.env` instead.

## Running it without Docker (e.g. for local dev in Codium)

Requires the .NET 8 SDK.

```bash
cd src/CatTracker
export APP_PASSWORD=dev-password
export DATA_DIR=./data
dotnet run
```

Then open http://localhost:5000 (or whatever port `dotnet run` prints).

## Project layout

```
cat-tracker/
├── docker-compose.yml
├── .env.example
├── docs/
│   └── cat-age-research.md      # sourcing for the age math & life stages
└── src/CatTracker/
    ├── Dockerfile
    ├── Program.cs                # composition root
    ├── Models/                   # CatProfile, ThemeInfo, Appointment, DetailEntry, LifeStageInfo
    ├── Data/
    │   ├── LifeStageCatalog.cs   # the seeded "what's this age about" content
    │   └── ThemeCatalog.cs       # the 5 selectable themes (key, name, swatch color)
    ├── Services/
    │   ├── CatAgeMath.cs         # human-year conversion + life-stage lookup
    │   ├── JsonFileStore.cs      # the tiny embedded "database"
    │   ├── *Repository.cs        # typed wrappers around JsonFileStore
    │   ├── PasswordProvider.cs
    │   └── LoginThrottle.cs
    ├── Endpoints/                # minimal API route groups
    └── wwwroot/                  # the frontend (index.html, css/, js/)
```

## Onboarding, under the hood

There's no separate "has this been set up?" flag to fall out of sync —
`ProfileRepository.GetWithStatusAsync` just checks whether `profile.json`
exists on disk yet. No file means a fresh install, so `GET /api/cat`
returns `needsOnboarding: true` and the frontend shows the setup card
instead of the dashboard. The moment a profile is saved (via onboarding or
the gear-icon edit form later), that flag flips for good.

## Theming

Each theme is a `[data-theme="key"]` block in `wwwroot/css/styles.css`
setting a handful of CSS variables — `--ink`/`--paper`/etc. for page
chrome (always kept high-contrast for readability), plus `--mark` and
`--mark-patch` for the cat icon and a couple of accent touches, which carry
more of the actual color. The icon itself is one shared SVG shape in
`index.html` with class names (`mark-body`, `mark-eye`, `mark-patch`, ...)
that each theme's CSS recolors, hides, or (for the White theme) switches to
an outlined style so a light-colored cat is still visible on a light page.

Dark mode applies one unified dark palette to the neutral tokens regardless
of theme, but leaves `--mark`/`--mark-patch` alone so the icon keeps its
color.

**Adding a theme:**
1. Add a `[data-theme="yourkey"]` block in `wwwroot/css/styles.css`.
2. Add a matching entry (key, name, description, swatch hex) to
   `Data/ThemeCatalog.cs`.
3. Add the same swatch hex to `THEME_FAVICON_COLORS` in `wwwroot/js/app.js`
   so the browser-tab favicon matches too (it's a standalone file the
   browser loads on its own, so it can't read the page's CSS variables —
   it's built from a literal hex value instead).

Only solid-color palettes are included for now (no tabby stripes or calico
patches) — that would mean drawing pattern fills rather than just
recoloring the shared silhouette, which is a bigger lift than the palette
system above.

## Customizing

- **Life-stage descriptions** live in `Data/LifeStageCatalog.cs` as plain
  C# — edit the text or add/split stages freely. See
  `docs/cat-age-research.md` for where the current boundaries came from.
- **The age math** lives in `Services/CatAgeMath.cs` if you want a
  different conversion curve.
- **Password lockout** thresholds are in `Services/LoginThrottle.cs`.

## Notes on the design

- Each theme keeps body text high-contrast regardless of how vivid its
  accent color is — legibility never depends on which theme is picked.
- The age slider is styled like a tailor's tape measure — a small nod to
  the Tuxedo theme's namesake.
- Ages and life-stage descriptions are a general reference based on
  commonly cited veterinary charts, not medical advice — see
  `docs/cat-age-research.md` for sources and caveats.

## Versioning

This directory is a git repo. Suggested next step: create an empty repo on
your own GitHub/GitLab and push:

```bash
git remote add origin <your-repo-url>
git push -u origin main
```
