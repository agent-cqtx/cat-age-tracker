# Henry's Age Tracker

A small self-hosted app for tracking Henry (born 6/26/2020, black-and-white
tuxedo American Shorthair): his current age in cat years *and* the
commonly-used human-year equivalent, a scrubbable timeline of his life
stages past and future, a vet/grooming/medication calendar, and a place to
save other details (microchip number, food preferences, weight log,
whatever you want). Editing is behind a single shared password; viewing is
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
  framework). Black/white/gray only, echoing Henry's own coloring.
- **Auth:** ASP.NET Core cookie authentication, single shared password from
  the `HENRY_APP_PASSWORD` environment variable, with a basic brute-force
  lockout on the login endpoint.

## Running it

1. Copy the example environment file and set a real password:

   ```bash
   cp .env.example .env
   # edit .env and set HENRY_APP_PASSWORD to something only you know
   ```

2. Build and start:

   ```bash
   docker compose up -d --build
   ```

3. Open **http://localhost:8080** (or your Docker host's address/port if
   remote — e.g. `http://<mint-box-ip>:8080`).

Data persists in a Docker named volume (`henry-data`) mounted at
`/app/data` inside the container, as three small JSON files
(`profile.json`, `appointments.json`, `details.json`). To back it up, first
find the actual volume name (Compose prefixes it with the project folder
name, so it's usually `henry-tracker_henry-data`, but check to be sure):

```bash
docker volume ls | grep henry-data

docker run --rm -v henry-tracker_henry-data:/data -v "$PWD":/backup \
  alpine tar czf /backup/henry-data-backup.tar.gz -C /data .
```

If you forget to set `HENRY_APP_PASSWORD`, the container **won't silently
run wide open** — it generates a random password each restart and prints it
to the container logs (`docker compose logs henry-tracker`). Set a real one
in `.env` instead.

## Running it without Docker (e.g. for local dev in Codium)

Requires the .NET 8 SDK.

```bash
cd src/HenryTracker
export HENRY_APP_PASSWORD=dev-password
export DATA_DIR=./data
dotnet run
```

Then open http://localhost:5000 (or whatever port `dotnet run` prints).

## Project layout

```
henry-tracker/
├── docker-compose.yml
├── .env.example
├── docs/
│   └── cat-age-research.md      # sourcing for the age math & life stages
└── src/HenryTracker/
    ├── Dockerfile
    ├── Program.cs                # composition root
    ├── Models/                   # CatProfile, Appointment, DetailEntry, LifeStageInfo
    ├── Data/
    │   └── LifeStageCatalog.cs   # the seeded "what's this age about" content
    ├── Services/
    │   ├── CatAgeMath.cs         # human-year conversion + life-stage lookup
    │   ├── JsonFileStore.cs      # the tiny embedded "database"
    │   ├── *Repository.cs        # typed wrappers around JsonFileStore
    │   ├── PasswordProvider.cs
    │   └── LoginThrottle.cs
    ├── Endpoints/                # minimal API route groups
    └── wwwroot/                  # the frontend (index.html, css/, js/)
```

## Customizing

- **Henry's profile** (name, breed, color, birth date) can be edited from
  the app itself once unlocked, or by editing the defaults in
  `Models/CatProfile.cs` before first run.
- **Life-stage descriptions** live in `Data/LifeStageCatalog.cs` as plain
  C# — edit the text or add/split stages freely. See
  `docs/cat-age-research.md` for where the current boundaries came from.
- **The age math** lives in `Services/CatAgeMath.cs` if you want a
  different conversion curve.
- **Password lockout** thresholds are in `Services/LoginThrottle.cs`.

## Notes on the design

- Palette is strictly black/white/gray (no accent color), and follows
  `prefers-color-scheme` for a dark variant automatically.
- The age slider is styled like a tailor's tape measure — a small nod to
  "tuxedo."
- Ages and life-stage descriptions are a general reference based on
  commonly cited veterinary charts, not medical advice — see
  `docs/cat-age-research.md` for sources and caveats.

## Versioning

This directory is a git repo (`git init` was run at creation). Suggested
next step: create an empty repo on your own GitHub/GitLab and push:

```bash
git remote add origin <your-repo-url>
git push -u origin main
```
