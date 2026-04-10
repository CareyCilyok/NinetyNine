# NinetyNine — Local Development Guide

This guide gets you from a fresh clone to a running local environment. It covers Docker-based setup, running tests, hot-reload development, and database inspection.

For the overall system design, see [architecture.md](./architecture.md). For production deployment, see [deployment.md](./deployment.md).

---

## Prerequisites

You need the following installed before starting:

| Tool | Minimum version | Notes |
|---|---|---|
| Git | Any recent version | |
| Docker Engine or Docker Desktop | 24.x+ with Compose v2 | On Linux: Docker Engine + `docker-compose-plugin`. On macOS/Windows: Docker Desktop includes both. Verify Compose v2 with `docker compose version` (note: no hyphen) |
| .NET 8 SDK | 8.0.x | Required for IDE integration, running tests outside Docker, and `dotnet watch`. Download from [dot.net](https://dotnet.microsoft.com/en-us/download) |
| Text editor or IDE | — | Rider, VS Code with C# Dev Kit, or Visual Studio 2022 all work |

**Optional but recommended**:

- **MongoDB Compass** ([mongodb.com/products/compass](https://www.mongodb.com/products/compass)) — a GUI for browsing and querying the local MongoDB database, as an alternative to mongo-express in the browser
- **dotnet-coverage** — for generating local coverage reports (`dotnet tool install -g dotnet-coverage`)

### Verify prerequisites

```bash
git --version
docker compose version   # should print v2.x.x or higher
dotnet --version         # should print 8.x.x
```

---

## Clone and First Run

```bash
git clone https://github.com/<owner>/NinetyNine.git
cd NinetyNine
cp .env.example .env
```

Open `.env` in your editor and fill in your Google OAuth credentials. See the section below on getting those if you do not have them yet.

Once `.env` is populated:

```bash
./deploy.sh up
```

Expected output:

```
[+] Building ...
[+] Running 3/3
  ✔ Container ninetynine-mongo-1          Healthy
  ✔ Container ninetynine-mongo-express-1  Started
  ✔ Container ninetynine-web-1            Started
Web:           http://localhost:8080
Mongo Express: http://localhost:8081
```

Open [http://localhost:8080](http://localhost:8080) — you should see the NinetyNine landing page.

The first build takes 3–5 minutes while the .NET SDK image downloads and packages restore. Subsequent builds are faster thanks to Docker layer caching.

### Getting Google OAuth credentials for local development

You need a Google OAuth client to use the sign-in flow. Create a test credential in [Google Cloud Console](https://console.cloud.google.com):

1. Navigate to **APIs & Services** → **Credentials** → **Create Credentials** → **OAuth client ID**
2. Application type: **Web application**
3. Name: `NinetyNine Local Dev`
4. Authorized redirect URIs: add `http://localhost:8080/signin-google`
5. Click **Create** and copy the Client ID and Client Secret into your `.env` file:
   ```
   GOOGLE_CLIENT_ID=<your-client-id>
   GOOGLE_CLIENT_SECRET=<your-client-secret>
   ```

**If you want to inspect the UI without a working Google login**, the dev compose configuration sets `Auth__Google__ClientId` and `Auth__Google__ClientSecret` to `dev-noop` if the environment variables are not provided. The application will start and render, but any attempt to sign in will fail because Google will reject the fake credentials. This is useful for testing purely static UI components but not for any authenticated flows.

---

## Everyday Commands

All subcommands go through `deploy.sh`, which wraps `docker compose -f docker-compose.dev.yml`:

| Command | What it does |
|---|---|
| `./deploy.sh up` | Build (if needed) and start all services in the background |
| `./deploy.sh down` | Stop and remove containers (volumes are preserved) |
| `./deploy.sh rebuild` | Force a full image rebuild with `--no-cache`, then start |
| `./deploy.sh logs` | Follow logs for the `web` service |
| `./deploy.sh logs mongo` | Follow logs for a specific service (pass name as second argument) |
| `./deploy.sh seed` | Run the application seed command (if implemented) |
| `./deploy.sh clean` | Stop containers AND delete the `mongo_data` volume — destructive, starts fresh |

Run `./deploy.sh` with no arguments to see usage.

---

## Running Tests

### Pure unit tests (no Docker required)

```bash
dotnet test tests/NinetyNine.Model.Tests
```

These tests cover domain entity logic and game scoring rules with no external dependencies. They run in under a second.

### Integration tests (require Docker)

```bash
dotnet test tests/NinetyNine.Repository.Tests
```

These tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up a real MongoDB 7 container per test class. Docker must be running. The first run takes ~30 seconds while it pulls the MongoDB image; subsequent runs use the cached image.

### Service-layer tests

```bash
dotnet test tests/NinetyNine.Services.Tests
```

Also uses Testcontainers. Tests the full scoring flow and player registration against a real database.

### Component tests (Blazor)

```bash
dotnet test tests/NinetyNine.Web.Tests
```

Uses [bUnit](https://bunit.dev/) to test Razor components in isolation. No Docker required.

### All tests

```bash
dotnet test NinetyNine.sln
```

Runs all test projects in the solution. Integration tests require Docker.

### Coverage report

Install the coverage tool if you have not already:

```bash
dotnet tool install -g dotnet-coverage
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Generate and view a report:

```bash
dotnet-coverage collect \
  "dotnet test NinetyNine.sln" \
  --output coverage.xml \
  --output-format cobertura

reportgenerator \
  -reports:coverage.xml \
  -targetdir:coverage-report \
  -reporttypes:Html

# Open the report
xdg-open coverage-report/index.html    # Linux
open coverage-report/index.html         # macOS
```

The quality gate requires ≥ 70% coverage on `NinetyNine.Model` and `NinetyNine.Services`. See [architecture.md §12.5](./architecture.md) for the full quality gate definition.

---

## Hot Reload During Development

### Option A: dotnet watch (recommended for active UI development)

Run the web application directly on your host machine, using only the MongoDB container from Docker Compose:

```bash
# Terminal 1: start only the database services
docker compose -f docker-compose.dev.yml up -d mongo mongo-express

# Terminal 2: run the web app with hot reload
dotnet watch --project src/NinetyNine.Web
```

The application connects to the MongoDB container on `localhost:27017` using the credentials from `docker-compose.dev.yml` (`root` / `devpassword`). Your `.env` file should have:

```
MONGO_CONNECTION_STRING=mongodb://root:devpassword@localhost:27017/?authSource=admin
MONGO_DATABASE_NAME=NinetyNine
```

Note: this overrides the `MONGO_CONNECTION_STRING` value set by the compose file. Adjust as needed.

`dotnet watch` detects file changes and hot-reloads the Blazor application. For Blazor Server, component changes trigger a browser refresh automatically. CSS and Razor markup changes are typically reflected within a second or two.

**Pros**: fast feedback loop, full IDE debugger support, no Docker rebuild on code change
**Cons**: you are responsible for managing the `.env` values for the host runtime; Docker and host environments can drift

### Option B: full containerized development loop

Keep everything in Docker:

```bash
./deploy.sh rebuild
```

Use this when you want true dev-prod parity or when debugging a Docker-specific issue (networking, file permissions, environment variable injection).

**Pros**: environment matches production exactly; no host-side .NET SDK version concerns
**Cons**: each code change requires a full Docker rebuild (typically 30–60 seconds); no step-debugger in the IDE without remote attach configuration

**Choosing between the two**: use Option A for everyday feature development and component work. Switch to Option B before opening a PR to verify the full containerized build passes.

---

## Database Inspection

### mongo-express (browser UI)

Open [http://localhost:8081](http://localhost:8081) while the dev compose stack is running. No login required (auth is disabled in the dev compose configuration).

You can browse collections, view documents, run queries, and manually insert or delete records. Useful for verifying that data is being written correctly.

### mongosh (command-line shell)

```bash
docker compose -f docker-compose.dev.yml exec mongo mongosh \
  --username root \
  --password devpassword \
  --authenticationDatabase admin
```

Once in the shell:

```javascript
use NinetyNine
db.players.find().pretty()
db.games.find({ gameState: "Completed" }).count()
db.games.find().sort({ whenPlayed: -1 }).limit(5).pretty()
```

### MongoDB Compass

Connect using the connection string:

```
mongodb://root:devpassword@localhost:27017/?authSource=admin
```

Compass provides a richer GUI than mongo-express with schema visualization, index management, and query explain plans.

### Resetting the database

```bash
./deploy.sh clean
```

This removes the `mongo_data` Docker volume, which wipes all data. The next `./deploy.sh up` starts with a completely empty database. Use this when you want to test the first-run registration flow or when migrations have left the schema in an inconsistent state.

---

## IDE Setup

### JetBrains Rider

Open `NinetyNine.sln` directly. Rider detects the multi-project structure automatically. Run/debug configurations for each project are auto-generated.

For Docker Compose integration, install the **Docker** plugin (bundled in recent versions). You can start/stop compose services from the Services tool window.

Hot reload: Rider's built-in hot reload works with `dotnet watch`. Configure a run configuration with the **Watch** option enabled, or run `dotnet watch` from the terminal and attach to the process for debugging.

### VS Code with C# Dev Kit

1. Install the **C# Dev Kit** extension (includes C# language server, test explorer, and solution explorer)
2. Open the repo root folder in VS Code
3. When prompted, select `NinetyNine.sln` as the active solution
4. Install the **Docker** extension for compose management from the sidebar

Recommended additional extensions:

- **MongoDB for VS Code** — connect to the local MongoDB and browse collections from within the editor
- **.NET MAUI / Blazor WASM debugger** — not strictly required for Blazor Server, but the C# Dev Kit handles breakpoints in Blazor Server components natively

Launch configurations: add a `.vscode/launch.json` if one does not exist — VS Code should offer to generate one when you open a `.cs` file and press F5.

### Visual Studio 2022

Open `NinetyNine.sln`. Visual Studio 2022 (17.8+) has built-in .NET 8 support. Use the Docker Compose launch profile if you want to start the full stack from VS; otherwise, start compose manually and run/debug the `NinetyNine.Web` project directly.

Note: Visual Studio is Windows-only. If you are on Linux, use Rider or VS Code.

---

## Troubleshooting

### Port 8080 is already in use

```
Error: port is already allocated
```

Something else is listening on port 8080 (another dev server, a previous container that was not stopped cleanly, etc.).

Option 1 — find and stop the conflicting process:

```bash
sudo lsof -i :8080
# or
sudo ss -tlnp | grep 8080
```

Kill the process, then retry `./deploy.sh up`.

Option 2 — change the host port in `docker-compose.dev.yml`:

```yaml
ports:
  - "9090:8080"   # map to 9090 on host instead
```

Then update your `.env`'s `GOOGLE_CLIENT_ID` redirect URI in Google Cloud Console if you change the port.

### Docker daemon is not running

```
Cannot connect to the Docker daemon at unix:///var/run/docker.sock
```

Start Docker:

- **Docker Desktop (macOS/Windows)**: open the Docker Desktop application
- **Linux systemd**: `sudo systemctl start docker`

### MongoDB container fails to start or web cannot connect

```bash
./deploy.sh logs mongo
```

Common causes:

- The `mongo_data` volume is from an incompatible MongoDB version. Run `./deploy.sh clean` to wipe the volume, then `./deploy.sh up` to start fresh.
- Another process is using port 27017. Check with `sudo lsof -i :27017`.

If the web service starts before MongoDB is healthy, the healthcheck and `depends_on: condition: service_healthy` in `docker-compose.dev.yml` should prevent this. If you are seeing connection errors on startup, check that the healthcheck is passing: `docker compose -f docker-compose.dev.yml ps` — the mongo service should show `(healthy)`.

### Google OAuth redirect URI mismatch

```
Error 400: redirect_uri_mismatch
```

The redirect URI registered in Google Cloud Console does not match what the application is sending. The application sends `/signin-google` as the callback path; the full URI must match exactly.

Verify that `http://localhost:8080/signin-google` is in the **Authorized redirect URIs** list for your OAuth client in [Google Cloud Console](https://console.cloud.google.com/apis/credentials). Note: `http://localhost` (port 80) and `http://localhost:8080` are treated as different origins.

### Build fails on fresh clone

If `./deploy.sh up` fails with restore or build errors immediately after cloning:

```bash
dotnet restore NinetyNine.sln
dotnet build NinetyNine.sln
```

Review the output for specific errors. Common causes:

- .NET SDK version mismatch: ensure you have .NET 8 SDK (`dotnet --version`)
- NuGet feed unreachable: check your internet connection or corporate proxy settings
- Missing global.json: the repo may pin a specific SDK patch version; install the required version from [dot.net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### deploy.sh: permission denied

```bash
chmod +x deploy.sh
```

### mongo-express shows a blank page or connection error

mongo-express depends on the mongo service being healthy. If mongo is still starting up, mongo-express may fail its initial connection. Wait a few seconds and refresh the browser, or restart the mongo-express container:

```bash
docker compose -f docker-compose.dev.yml restart mongo-express
```

### "dotnet watch" does not reflect changes

- Ensure you saved the file — watch polls the filesystem
- Check that `DOTNET_WATCH_POLL_INTERVAL` is not set to an unusually high value
- On Linux with inotify limits hit: `echo fs.inotify.max_user_watches=524288 | sudo tee -a /etc/sysctl.conf && sudo sysctl -p`
