# NinetyNine — Production Deployment Runbook

This document is the authoritative runbook for standing up the NinetyNine production environment from scratch. It covers Azure VM provisioning, Docker runtime setup, MongoDB Atlas configuration, Google OAuth credentials, and the GitHub Actions deployment pipeline.

For the overall system blueprint, see [architecture.md](./architecture.md). Section 10 of that document describes the deployment topology; Section 11 describes the CI/CD pipeline.

---

## Overview

### Architecture Diagram

```
Browser
  │
  │  HTTP :80
  ▼
┌─────────────────────────────────────────────┐
│  Azure Linux VM (Ubuntu 22.04, Standard_B2s) │
│                                              │
│  ┌──────────────────────────────────────┐    │
│  │  Docker: web container               │    │
│  │  NinetyNine.Web (Blazor Server :8080)│    │
│  │  Mapped → host port 80               │    │
│  └────────────────┬─────────────────────┘    │
└───────────────────┼──────────────────────────┘
                    │  MongoDB Atlas connection string (TLS)
                    ▼
          ┌──────────────────────┐
          │  MongoDB Atlas M0    │
          │  (shared cluster)    │
          └──────────────────────┘

Auth flow (separate):
Browser ──► Google OAuth ──► /signin-google ──► cookie issued

CI/CD:
GitHub push to master
  → Actions: build image → push to GHCR
  → Actions: SSH into VM → docker pull → docker compose up -d
  → Actions: smoke test /healthz
```

### Trust Boundaries

| Boundary | Protocol | Auth |
|---|---|---|
| Browser → VM | HTTP (TLS deferred) | Session cookie (`NinetyNine.Auth`) |
| VM → MongoDB Atlas | TLS (embedded in connection string) | Username + password in connection string |
| Browser → Google OAuth | HTTPS | Handled by Google |
| GitHub Actions → VM | SSH | Private key (`AZURE_VM_SSH_KEY` secret) |
| Actions runner → GHCR | HTTPS | `GHCR_TOKEN` secret |

---

## Quick Start (Automated)

This is the **primary deployment path**. The `infra/main.bicep` template plus the three scripts under `scripts/` replace the manual `az` and SSH steps that used to live in Parts 1 and 2 of this runbook (those are preserved as Appendix C: Manual Provisioning Reference for fallback/reference).

End-to-end time once Atlas exists: ~10 minutes. (Google OAuth setup is **deferred** for the initial deploy — see [Part 4](#part-4-google-oauth-setup) for the rationale and the tracked work in the v2 roadmap backlog.)

### What gets created

The Bicep template provisions everything in a single resource group:

| Resource | Bicep type | Notes |
|---|---|---|
| Standard_B2s VM | `Microsoft.Compute/virtualMachines` | Ubuntu 22.04 LTS Gen2, SSH key auth only, cloud-init bootstrap |
| Standard SSD OS disk | (inline on VM) | 30 GB; deleted with VM |
| Static public IP | `Microsoft.Network/publicIPAddresses` | Standard SKU, IPv4 |
| Network interface | `Microsoft.Network/networkInterfaces` | Bound to vnet + public IP + NSG |
| Virtual network | `Microsoft.Network/virtualNetworks` | 10.0.0.0/16 with subnet 10.0.1.0/24 |
| Network security group | `Microsoft.Network/networkSecurityGroups` | SSH from operator IP only; HTTP :80 from Internet |

Cloud-init (`infra/cloud-init.yaml`) runs on first boot and installs Docker Engine + Compose v2 from the official Docker apt repo, adds `azureuser` to the `docker` group, creates `/opt/ninetynine`, and enables `fail2ban` + `unattended-upgrades` (security-only). After it finishes, the VM is ready for the `deploy.yml` workflow to SSH in.

### Prerequisites for the automated path

1. Azure free or PAYG account, signed in via `az login`. Verify with `az account show`.
2. GitHub CLI installed and authenticated (`gh auth status`).
3. Deploy SSH keypair generated at `~/.ssh/ninetynine_deploy` (see `ssh-keygen` command in [Prerequisites](#prerequisites) below).
4. MongoDB Atlas M0 cluster created (still manual — see [Part 3](#part-3-mongodb-atlas-setup)). Copy the connection string with `/NinetyNine` in the path.
5. ~~Google OAuth client created~~ **Deferred — skip for the initial deploy.** Google OAuth is not yet wired into the app code. See [Part 4](#part-4-google-oauth-setup) for the rationale and the tracked deferred work in the v2 roadmap backlog. `bootstrap-secrets.sh` writes documented placeholder values for the two `GOOGLE_*` secrets so the deploy pipeline still renders a valid `.env`.

### Three commands to deploy

```bash
# 1. Provision Azure infrastructure (creates RG, VM, IP, NSG; runs cloud-init).
#    Auto-detects your operator IP for the SSH NSG rule, prompts to confirm
#    parameters before deploying. Takes 2-4 minutes.
./scripts/provision-azure.sh
# → prints VM_PUBLIC_IP

# 2. Update Atlas Network Access: add VM_PUBLIC_IP to the allowlist.
# 3. (Deferred — Google OAuth not yet wired in app; skip for initial deploy.)

# 4. Populate GitHub Actions secrets (auto-detects VM IP from latest deployment).
./scripts/bootstrap-secrets.sh
# → prompts for MONGO_CONNECTION_STRING only; writes Google placeholders

# 5. Trigger the deploy workflow.
git push origin master           # if there are unpushed commits
# or
gh workflow run deploy.yml       # manual dispatch
gh run watch                     # follow live

# 6. Verify health.
curl -fsS http://VM_PUBLIC_IP/healthz
```

Then sign in with email/password (the production-ready auth path) and create a test game per [Part 7: Verification](#part-7-verification). (Google OAuth is deferred — see [Part 4](#part-4-google-oauth-setup).)

### Useful overrides

`provision-azure.sh` accepts flags for cases where defaults don't fit. See `./scripts/provision-azure.sh --help`. Common ones:

```bash
--location westus2                          # different Azure region
--operator-ip 1.2.3.4                       # override IP auto-detect (e.g. behind CGNAT)
--vm-size Standard_B1s                      # downgrade for free-tier 12-month VM hours
--resource-group rg-ninetynine-prod-westus2 # custom RG name
```

### Re-running the provision script

The Bicep template is idempotent. Re-running `provision-azure.sh` against an existing deployment will reconcile drift (e.g., re-applies your operator IP to the SSH NSG rule if it changed). It will not destroy or recreate the VM unless you change a property that requires replacement (VM size changes are in-place; image changes are not).

### Tearing it all down

```bash
./scripts/teardown-azure.sh
# Lists everything in the RG, requires you to type the RG name to confirm,
# then deletes the entire group asynchronously. External services (Atlas,
# Google OAuth, GHCR images) are untouched.
```

### Troubleshooting the automated path

| Symptom | Cause | Fix |
|---|---|---|
| `provision-azure.sh` fails with `SkuNotAvailable` | B2s not available in your subscription's region/zone | Pass `--location <other-region>` (try `westus2`, `centralus`, `northeurope`) |
| `provision-azure.sh` fails with `OperationNotAllowed: vCPU quota exceeded` | New subscriptions have low default CPU quotas in some regions | Request a quota increase via the portal: Subscriptions → Usage + quotas → Compute (typically approved within minutes for B-series) |
| `provision-azure.sh` succeeds but SSH fails with "connection refused" | Cloud-init is still running on the VM | Wait 3-5 minutes; check `sudo cloud-init status --long` after SSH connects |
| `deploy.yml` step "Trust VM host key" fails with `ssh-keyscan` error | NSG SSH rule blocks GitHub Actions runner IPs | Open SSH temporarily to `0.0.0.0/0` for the deploy, or use the host-key-known-good pattern documented in `.github/workflows/deploy.yml` comments |
| `bootstrap-secrets.sh` can't auto-detect VM IP | Wrong `--resource-group` (you used a non-default region) | Pass `--resource-group rg-ninetynine-prod-<your-region>` or `--vm-ip <ip>` |
| Bicep deployment fails with `InvalidParameter` on `sshPublicKey` | Public key isn't a valid OpenSSH format | Confirm the key starts with `ssh-ed25519` or `ssh-rsa`. Do NOT pass the private key. |
| `cloud-init status` shows `error` after VM provisions | Docker apt repo unreachable, or apt lock contention with unattended-upgrades during first-boot | SSH in, `sudo cat /var/log/cloud-init-output.log` for the failing step. Often re-runnable: `sudo cloud-init clean --logs && sudo cloud-init init && sudo cloud-init modules --mode=final` |

### When to use the manual path instead

Use the Manual Provisioning Reference (Appendix C, originally Parts 1–2 of this doc) when:

- You're debugging the automated path and need to bisect which step is failing
- You're learning Azure resource creation and want to see each command explicitly
- You need a one-off resource configuration that the Bicep template doesn't parameterize

The manual and automated paths produce identical end states; you can mix them (e.g., provision via Bicep, then add an additional resource manually).

---

## Prerequisites

Before starting, confirm you have:

- An Azure subscription with permission to create resource groups and virtual machines (Contributor role or equivalent)
- A MongoDB Atlas account (free at [cloud.mongodb.com](https://cloud.mongodb.com))
- A Google Cloud project with the OAuth 2.0 API enabled
- A GitHub repository with Actions enabled and the ability to create repository secrets
- An SSH keypair generated locally (see below if you need to create one)

**Generate an SSH keypair** (skip if you already have one):

```bash
ssh-keygen -t ed25519 -C "ninetynine-deploy" -f ~/.ssh/ninetynine_deploy
```

This produces `~/.ssh/ninetynine_deploy` (private key) and `~/.ssh/ninetynine_deploy.pub` (public key). You will use the public key when creating the VM and the private key as a GitHub Actions secret.

---

## Parts 1 + 2 — moved to Appendix C

The manual Azure CLI walkthrough and VM bootstrap steps that used to live here have been moved to **[Appendix C: Manual Provisioning Reference](#appendix-c-manual-provisioning-reference)** at the end of this document. The primary path is now [Quick Start (Automated)](#quick-start-automated) above, which uses `infra/main.bicep` + `scripts/provision-azure.sh` to perform the same work in two commands.

If the automated path fails or you want to understand the underlying steps, jump to Appendix C.

---

## Part 3: MongoDB Atlas Setup

### Create a free tier cluster

1. Log in to [cloud.mongodb.com](https://cloud.mongodb.com)
2. Click **Create** → **M0 Free**
3. Select the cloud provider and region closest to your Azure VM region
   - If your VM is in Azure East US, choose AWS us-east-1 or Azure East US if available
   - Exact region match is not always possible on the free tier; pick the geographically closest option
4. Name the cluster (e.g., `ninetynine-prod`) and click **Create Deployment**

### Create a dedicated database user

Do not use the Atlas admin account for the application. Create a least-privilege user:

1. In Atlas, go to **Database Access** → **Add New Database User**
2. Authentication method: **Password**
3. Username: `ninetynine-app`
4. Password: generate a strong random password and record it securely
5. Database user privileges: select **Custom Role** or use **Built-in Role** → choose **Read and write to any database**, then scope it:
   - Click **Add Specific Privilege**
   - Privilege: `readWrite`
   - Database: `NinetyNine`
   - Collection: leave blank (applies to all collections in that database)
6. Click **Add User**

This user can only read and write data in the `NinetyNine` database. It cannot create or drop databases, access Atlas admin functions, or read other databases.

### Allow the VM's IP in Atlas network access

Atlas blocks all connections by default. You must add the VM's public IP to the allowlist:

1. In Atlas, go to **Network Access** → **Add IP Address**
2. Enter the VM's public IP (the one you recorded in Part 1)
3. Add an optional comment: `Azure VM prod - vm-ninetynine-prod`
4. Click **Confirm**

**Alternative for initial setup**: allow `0.0.0.0/0` (any IP). This is less secure but can unblock debugging if you are unsure of the correct IP. Switch to the specific VM IP once confirmed working, or leave it if you accept the risk (Atlas auth still protects the data).

If you switch from a dynamic to a static IP, or if Azure reassigns the IP for any reason, you must update the Atlas allowlist.

### Get the connection string

1. In Atlas, click **Connect** on your cluster → **Drivers**
2. Select **Python** or any driver — the format is the same; you only need the connection string
3. Copy the `mongodb+srv://` string; it looks like:
   ```
   mongodb+srv://ninetynine-app:<password>@ninetynine-prod.xxxxx.mongodb.net/?retryWrites=true&w=majority
   ```
4. Replace `<password>` with the actual password you set for `ninetynine-app`
5. Add the database name to the path:
   ```
   mongodb+srv://ninetynine-app:<password>@ninetynine-prod.xxxxx.mongodb.net/NinetyNine?retryWrites=true&w=majority
   ```

This is the value you will store as the `MONGO_CONNECTION_STRING` GitHub Actions secret.

### Atlas free tier limits

| Limit | Value | Impact |
|---|---|---|
| Storage | 512 MB | Sufficient for hundreds of players and thousands of games; avatar GridFS storage is the main consumer |
| Connections | 500 max | More than enough for Blazor Server with a small user base |
| Shared cluster | No dedicated compute | Occasional cold-start latency; not suitable for high-traffic production use |

When you outgrow the free tier, upgrade to M2 ($9/mo) or M5 ($25/mo) in the Atlas UI with no application changes required — the connection string format remains the same.

---

## Part 4: Google OAuth Setup

> **Deferred for the initial production deploy.** The app does not currently consume `Auth:Google:ClientId` / `Auth:Google:ClientSecret` config — there is no `AddGoogle()` in `src/NinetyNine.Web/Program.cs` and no Google authentication NuGet package is referenced. Email/password authentication is fully wired and works in production without this step.
>
> The work to wire up Google OAuth is tracked in [`docs/plans/v2-roadmap.md`](./plans/v2-roadmap.md) → **Deferred / unscheduled backlog** → **TD-1: Google OAuth integration**, which captures the package add, `Program.cs` changes, endpoint wiring, UI, and tests required.
>
> `scripts/bootstrap-secrets.sh` writes the literal placeholder string `deferred-see-v2-roadmap-backlog` for both `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` so that `deploy.yml`'s `.env` rendering stays valid; the app reads neither value.
>
> The walkthrough below remains accurate and is preserved for the eventual integration. **Skip this entire Part 4 for the initial deploy** and proceed to Part 5.

### Create the OAuth 2.0 credentials

1. Go to [console.cloud.google.com](https://console.cloud.google.com)
2. Select your project (or create one: **New Project** → name it `NinetyNine`)
3. Navigate to **APIs & Services** → **OAuth consent screen**

### Configure the OAuth consent screen

1. User type: **External** (allows any Google account to sign in, not just accounts in your org)
2. App name: `NinetyNine`
3. User support email: your email address
4. Developer contact information: your email address
5. Scopes: add the following three scopes:
   - `openid`
   - `https://www.googleapis.com/auth/userinfo.email`
   - `https://www.googleapis.com/auth/userinfo.profile`
   These are the minimum required. NinetyNine does not request any Google Drive, Calendar, or other sensitive scopes.
6. Test users: in **Testing** publishing status, add the Google accounts that should be able to sign in during testing
7. Save and continue

**Publishing status tradeoffs**:

| Status | Who can log in | Google verification required |
|---|---|---|
| Testing | Only accounts on the test user list (up to 100) | No |
| In production | Any Google account | Yes, for sensitive/restricted scopes (not required for openid/email/profile) |

For a personal project or small group, **Testing** mode is fine indefinitely. Users not on the test list will see an "Access blocked: app has not completed Google verification" error. For openid/email/profile scopes, you can publish to production without going through the full verification process — Google only requires verification for sensitive scopes.

### Create the credentials

1. Go to **APIs & Services** → **Credentials** → **Create Credentials** → **OAuth client ID**
2. Application type: **Web application**
3. Name: `NinetyNine Web`
4. Authorized redirect URIs — add:
   ```
   http://<VM_PUBLIC_IP>/signin-google
   ```
   Replace `<VM_PUBLIC_IP>` with the IP from Part 1.

   **Note**: when you add a custom domain and TLS later, add a second redirect URI:
   ```
   https://<your-domain>/signin-google
   ```
   You can have multiple redirect URIs on one credential; the old one can remain.
5. Click **Create**
6. Copy the **Client ID** and **Client Secret** from the popup — you will not be able to retrieve the secret again without regenerating it

---

## Part 5: Configure GitHub Secrets

All secrets are stored under **Settings → Secrets and variables → Actions** in your GitHub repository. Click **New repository secret** for each one.

| Secret name | How to obtain | Notes |
|---|---|---|
| `AZURE_VM_HOST` | The VM's public IP from Part 1 | Used in SSH commands and smoke test curl |
| `AZURE_VM_USER` | `azureuser` (or whatever you set with `--admin-username`) | The user the deploy workflow SSHes as |
| `AZURE_VM_SSH_KEY` | Contents of `~/.ssh/ninetynine_deploy` (the **private** key) | Paste the entire key including `-----BEGIN...` and `-----END...` lines |
| `MONGO_CONNECTION_STRING` | The Atlas connection string from Part 3 | Must have the database name in the path and the password substituted |
| `GOOGLE_CLIENT_ID` | From Google Cloud Console credentials (Part 4) | Ends in `.apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | From Google Cloud Console credentials (Part 4) | Generated at credential creation time |
| `GHCR_TOKEN` | GitHub Personal Access Token with `write:packages` scope, or use `GITHUB_TOKEN` | If using `GITHUB_TOKEN`, the workflow already has access and this secret may not be needed — check your workflow configuration |

### How to create a GHCR personal access token

If your workflow uses a PAT rather than `GITHUB_TOKEN`:

1. Go to GitHub → **Settings** → **Developer settings** → **Personal access tokens** → **Tokens (classic)**
2. **Generate new token** → give it a name (`ninetynine-ghcr-push`)
3. Expiration: set a reasonable expiry (90 days) and add a calendar reminder to rotate it
4. Scopes: check `write:packages` (this also enables `read:packages`)
5. Generate and copy the token — store it as `GHCR_TOKEN`

### Secret rotation best practice

- Rotate the SSH key and GHCR token on a schedule (every 90 days is reasonable)
- If the VM is reprovisioned, generate a new keypair and update `AZURE_VM_SSH_KEY` and the VM's `authorized_keys`
- If the Atlas password changes, update `MONGO_CONNECTION_STRING` immediately — the running container will fail to connect until the secret is updated and the workflow re-runs

---

## Part 6: First Deployment

Once all secrets are configured, trigger the first deployment by pushing to the `master` branch (or if `master` already has commits, the secrets were the missing piece — re-run the latest failed `deploy.yml` workflow run from the Actions tab).

### What each job does

The `deploy.yml` workflow (see [architecture.md §11.2](./architecture.md)) runs three jobs:

**Job 1: `build-and-push`**

- Checks out the repository
- Builds the Docker image using the multi-stage `Dockerfile`
- Tags it as both `ghcr.io/{owner}/{repo}:${{ github.sha }}` and `:latest`
- Pushes both tags to GHCR

**Job 2: `deploy`**

- SSHes into the VM using `AZURE_VM_HOST`, `AZURE_VM_USER`, and `AZURE_VM_SSH_KEY`
- Writes `docker-compose.yml` and the `.env` file to `/opt/ninetynine/` using the stored secrets
- Runs `docker login ghcr.io`
- Runs `IMAGE_TAG=${{ github.sha }} docker compose pull`
- Runs `docker compose up -d --remove-orphans`
- The `--remove-orphans` flag removes any containers from previous compose configs that are no longer defined

**Job 3: `smoke-test`**

- Waits for the container to be ready (small sleep or retry loop in the workflow)
- Runs `curl -f http://${AZURE_VM_HOST}/healthz`
- Asserts HTTP 200; fails the workflow if the application did not come up

### Where to find deployment logs

- Go to your repository on GitHub → **Actions** tab
- Click the workflow run for the relevant commit
- Expand any job to see step-by-step logs

### Expected duration

| Job | Typical duration |
|---|---|
| `build-and-push` | 3–5 minutes (Docker layer caching on runners reduces subsequent builds) |
| `deploy` | 1–2 minutes (pull + compose up) |
| `smoke-test` | Under 30 seconds |

Total end-to-end: roughly 5–8 minutes from push to verified deployment.

---

## Part 7: Verification

### Functional checks

**1. Landing page**

Open a browser to `http://<VM_PUBLIC_IP>/`. You should see the NinetyNine landing page. If you get a 502 or connection refused, the container may still be starting — wait 30 seconds and retry.

**2. Google sign-in**

Click "Sign in with Google." You will be redirected to Google's OAuth flow. After authorizing, you should land on the `/register` page (first time) or the home page (subsequent logins). Complete the registration by choosing a display name.

**3. Create a test game**

Navigate to `/games/new`, select a venue and table size, and start a game. Score at least one frame to verify the scoring flow and database write path work end to end.

**4. Health check endpoint**

```bash
curl -v http://<VM_PUBLIC_IP>/healthz
```

Expected: `HTTP/1.1 200 OK`

**5. Container logs**

SSH to the VM and check for errors:

```bash
ssh -i ~/.ssh/ninetynine_deploy azureuser@<VM_PUBLIC_IP>
cd /opt/ninetynine
docker compose logs --tail 50 web
```

Expected: structured JSON log lines, no `ERROR` or `CRITICAL` entries, no MongoDB connection failures.

---

## Part 8: Operations and Troubleshooting

### Day-to-day commands

All commands assume you are SSH'd into the VM and in `/opt/ninetynine/`.

**View live logs:**

```bash
docker compose logs -f web
```

**Check container status:**

```bash
docker compose ps
```

**Restart the web container (without redeploying):**

```bash
docker compose restart web
```

**Manual deploy (bypassing GitHub Actions):**

```bash
# Pull the latest image and restart
docker compose pull
docker compose up -d

# Or deploy a specific image tag
IMAGE_TAG=<sha> docker compose pull
IMAGE_TAG=<sha> docker compose up -d
```

### Rollback procedure

Each deployment tags the image with the Git commit SHA. To roll back to a previous version:

1. Find the SHA of the last known good deployment in your GitHub Actions history or Git log
2. On the VM:

```bash
IMAGE_TAG=<previous-sha> docker compose up -d
```

This works as long as the previous image still exists in GHCR. GHCR retains all pushed images until you delete them. Be aware that if significant time has passed and you have pruned old images, the rollback image may no longer be available — in that case, re-run the Actions workflow for that commit to rebuild and push.

### Disk usage

```bash
# Host disk usage
df -h

# Docker-specific disk usage (images, containers, volumes, build cache)
docker system df
```

**Cleaning up old images:**

```bash
docker image prune -a
```

**Warning**: this deletes all images that are not currently referenced by a running or stopped container. If you have not yet pulled the new image and are still running an older one, `prune -a` will remove other cached images but not the running one. Run this only after confirming the current deployment is healthy, and be aware it will remove the fallback image for rollback — you would need to re-pull it from GHCR if a rollback is needed.

### Atlas metrics

Log in to [cloud.mongodb.com](https://cloud.mongodb.com) and navigate to your cluster → **Metrics** tab. Key metrics to watch:

- **Connections**: should be well under 500 for v1 load
- **Storage**: approaching 512 MB signals you need to upgrade the tier
- **Query executors**: slow queries indicate missing indexes

### Common failure modes

| Symptom | Likely cause | Resolution |
|---|---|---|
| Container starts then immediately exits | Missing or malformed `.env` values | `docker compose logs web` for the error; verify all secrets in GitHub; re-run the workflow |
| `MongoConnectionException` or timeout in logs | VM IP not on Atlas allowlist, or wrong connection string | Verify Atlas Network Access includes the VM's current public IP; re-check `MONGO_CONNECTION_STRING` secret |
| HTTP 502 or 503 on first load after deployment | Container is still starting (ASP.NET cold start) | Wait 15–30 seconds and refresh |
| Google OAuth: "redirect_uri_mismatch" | Redirect URI in Google Console does not match the actual URL | Add `http://<VM_PUBLIC_IP>/signin-google` to the authorized redirect URIs in Google Cloud Console |
| Google OAuth: "This app isn't verified" warning | OAuth consent screen is in **Testing** mode and the signing account is not on the test user list | Add the account to the test user list in Google Cloud Console → OAuth consent screen |
| SSH connection refused from Actions | NSG rule restricts SSH to your operator IP, not the GitHub Actions runner IPs | Either add GitHub's IP ranges to the NSG, or temporarily open SSH to `0.0.0.0/0` during the deploy then lock it back down — or configure the workflow to use a jump host or Azure Bastion |
| Old image runs after deploy | `docker compose up -d` without `pull` uses cached image | Ensure the workflow runs `docker compose pull` before `up -d` |

**Note on SSH NSG rules and GitHub Actions**: GitHub Actions runners use a published set of IP ranges ([https://api.github.com/meta](https://api.github.com/meta), `actions` key). An alternative pattern is to open port 22 only for the duration of the deploy step using the `az network nsg rule update` command in the workflow, then lock it back down — though this adds complexity. For simplicity in v1, restricting SSH to your operator IP is fine; deploy-time SSH issues can be debugged by temporarily adding your current IP.

---

## Part 9: Future Hardening

These items are out of scope for v1 but documented here so the upgrade path is clear.

**TLS via Caddy sidecar**

Add a `caddy` service to `docker-compose.yml` that proxies port 443 to the web container and handles Let's Encrypt certificate issuance automatically. Requires a custom domain pointed at the VM IP. Update the Google OAuth redirect URI to `https://` when adding TLS. Also update `CookieSecurePolicy` from `SameAsRequest` to `Always` in `Program.cs`.

**Application Insights integration**

Add the `Microsoft.ApplicationInsights.AspNetCore` NuGet package, provision an Application Insights resource in Azure, and configure the instrumentation key as an environment variable. This enables request tracing, exception tracking, and live metrics dashboards without changing the application's core logging approach.

**Automated MongoDB backups**

Atlas M0 provides on-demand snapshots in the Atlas UI. For automated backups, either upgrade to a paid tier (Atlas Continuous Cloud Backup) or configure a nightly cron job on the VM:

```bash
# Rough example — customize connection string and destination
mongodump --uri="$MONGO_CONNECTION_STRING" --archive | gzip > /opt/ninetynine/backups/$(date +%Y%m%d).gz
```

A more robust solution would ship the backup to Azure Blob Storage.

**Azure Key Vault for secrets**

Replace the `.env` file approach with Key Vault references. The VM would need a system-assigned managed identity, and the ASP.NET application would use the `Azure.Extensions.AspNetCore.Configuration.Secrets` package to pull secrets at startup. This eliminates plaintext secrets on disk.

**Blue/green or canary deployment**

Run two compose stacks (`web-blue`, `web-green`) behind a lightweight reverse proxy (Caddy or nginx). The deploy workflow switches traffic after the new version passes health checks, enabling zero-downtime deployments and instant rollback.

**OpenTelemetry observability**

Add an OpenTelemetry collector sidecar to the compose stack. The application emits traces and metrics via the OTLP protocol; the collector forwards to Azure Monitor, Grafana Cloud, or another backend. This replaces ad hoc `docker logs` tailing with structured observability.

**Rate limiting hardening**

The application already implements ASP.NET rate limiting middleware on `/login` and `/register`. A reverse proxy (Caddy/nginx) can enforce broader per-IP rate limits earlier in the request pipeline, before traffic reaches the application layer.

---

## Appendix A: Full Architecture Diagram

```
  ┌──────────────────────────────────────────────────────────────────┐
  │  Developer workstation                                            │
  │  git push → master                                               │
  └───────────────────────────┬──────────────────────────────────────┘
                              │
                              ▼
  ┌──────────────────────────────────────────────────────────────────┐
  │  GitHub                                                          │
  │                                                                  │
  │  ┌─────────────────────┐        ┌─────────────────────────────┐  │
  │  │  Actions: ci.yml    │        │  GitHub Container Registry  │  │
  │  │  (PR validation)    │        │  ghcr.io/{owner}/{repo}     │  │
  │  └─────────────────────┘        │  :latest  :abc123def  ...   │  │
  │                                 └────────────┬────────────────┘  │
  │  ┌─────────────────────────────────┐         │                   │
  │  │  Actions: deploy.yml            │         │                   │
  │  │  1. build + push image → GHCR   ├─────────┘                   │
  │  │  2. SSH → VM: pull + compose up │                             │
  │  │  3. smoke test /healthz         │                             │
  │  └────────────────┬────────────────┘                             │
  └───────────────────┼──────────────────────────────────────────────┘
                      │ SSH (port 22, AZURE_VM_SSH_KEY)
                      ▼
  ┌──────────────────────────────────────────────────────────────────┐
  │  Azure — rg-ninetynine-prod-eastus                               │
  │                                                                  │
  │  ┌────────────────────────────────────────────────────────────┐  │
  │  │  vm-ninetynine-prod  (Standard_B2s, Ubuntu 22.04 LTS)      │  │
  │  │                                                            │  │
  │  │  NSG: allow 22 (operator IP), allow 80 (0.0.0.0/0)        │  │
  │  │                                                            │  │
  │  │  /opt/ninetynine/                                          │  │
  │  │    docker-compose.yml                                      │  │
  │  │    .env  (written by deploy workflow)                      │  │
  │  │                                                            │  │
  │  │  ┌──────────────────────────────────────┐                 │  │
  │  │  │  Docker: web                         │                 │  │
  │  │  │  NinetyNine.Web (Blazor Server)       │                 │  │
  │  │  │  port 80 → 8080                      │                 │  │
  │  │  └──────────────────┬───────────────────┘                 │  │
  │  └─────────────────────┼──────────────────────────────────────┘  │
  └────────────────────────┼─────────────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
          ▼                ▼                ▼
  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐
  │  Browser     │  │  MongoDB     │  │  Google OAuth    │
  │              │  │  Atlas M0    │  │  accounts.google │
  │  HTTP :80    │  │  (TLS/SRV)   │  │  .com            │
  │  /signin-    │  │  NinetyNine  │  │                  │
  │  google      │  │  database    │  │  redirect back   │
  │  callback    │  │              │  │  to /signin-     │
  └──────────────┘  └──────────────┘  │  google          │
                                      └──────────────────┘
```

---

## Appendix B: Cost Estimate

Prices are approximate as of April 2026. Azure pricing varies by region and changes over time. Verify current prices at [azure.microsoft.com/en-us/pricing/calculator](https://azure.microsoft.com/en-us/pricing/calculator) before committing.

| Resource | Tier | Approx. monthly cost |
|---|---|---|
| Azure Standard_B2s VM (East US) | Pay-as-you-go | ~$30/mo |
| Azure Standard Static Public IP | Standard SKU | ~$4/mo |
| Azure Standard SSD OS disk (30 GB) | Included with VM | $0 (included) |
| MongoDB Atlas cluster | M0 free tier | $0/mo |
| GitHub Container Registry | Free for public repos; 500 MB free for private | $0/mo (within limits) |
| GitHub Actions minutes | 2,000 min/mo free on free plan; unlimited on paid plans | $0/mo (within limits) |
| Google Cloud OAuth | No charge for standard OAuth usage | $0/mo |

**Total: approximately $34/mo** for a fully functioning development/personal deployment.

**Upgrade paths and their costs**:

| Upgrade | Cost impact |
|---|---|
| Larger VM (Standard_B4ms: 4 vCPU, 16 GiB) | ~$72/mo |
| MongoDB Atlas M2 (2 GB storage, dedicated) | +$9/mo |
| MongoDB Atlas M5 (5 GB storage, dedicated) | +$25/mo |
| Static IP → Azure DDoS Basic | No cost (included) |
| Azure Bastion for SSH (instead of open port 22) | +$140/mo — not recommended for this scale |
| Custom domain (varies by registrar) | ~$10–15/yr |

---

## Appendix C: Manual Provisioning Reference

The Quick Start (Automated) section is the primary deployment path. This appendix preserves the original manual `az` and SSH walkthrough for use as a fallback, for debugging the automated path step-by-step, or for understanding what `infra/main.bicep` and `infra/cloud-init.yaml` are doing under the hood.

The end state produced by these manual steps is identical to running `./scripts/provision-azure.sh`.

### C.1 Provision the Azure VM (manual)

#### Resource group and naming

Create a dedicated resource group. Suggested naming convention:

```
rg-ninetynine-prod-<region-shortcode>
```

Examples: `rg-ninetynine-prod-eastus`, `rg-ninetynine-prod-westeurope`

Choose a region near your expected users. The VM and MongoDB Atlas cluster should be in the same or adjacent regions to minimize round-trip latency.

#### Public IP: static vs. dynamic

Azure assigns a dynamic public IP by default. This is acceptable for initial deployment, but the IP will change if you deallocate (stop) the VM. A static IP costs a small additional amount (~$3–4/mo) but avoids having to update your GitHub secrets and MongoDB Atlas allowlist after a VM restart.

**Recommendation**: allocate a static IP from the start (`--public-ip-sku Standard` with `--public-ip-address-allocation Static`). The cost is minor relative to debugging a broken deployment after an accidental deallocation.

#### Azure CLI: step-by-step

Install the Azure CLI from [https://learn.microsoft.com/en-us/cli/azure/install-azure-cli](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) if you do not already have it, then log in:

```bash
az login
```

Set your default subscription if you have more than one:

```bash
az account set --subscription "<your-subscription-name-or-id>"
```

**Step 1 — Create the resource group:**

```bash
az group create \
  --name rg-ninetynine-prod-eastus \
  --location eastus
```

- `--name`: the resource group name (follow the convention above)
- `--location`: Azure region identifier; run `az account list-locations -o table` for the full list

**Step 2 — Create the VM:**

```bash
az vm create \
  --resource-group rg-ninetynine-prod-eastus \
  --name vm-ninetynine-prod \
  --image Ubuntu2204 \
  --size Standard_B2s \
  --admin-username azureuser \
  --ssh-key-values ~/.ssh/ninetynine_deploy.pub \
  --public-ip-sku Standard \
  --public-ip-address-allocation Static \
  --public-ip-address pip-ninetynine-prod \
  --nsg nsg-ninetynine-prod \
  --output table
```

Flag explanations:

| Flag | Purpose |
|---|---|
| `--image Ubuntu2204` | Ubuntu 22.04 LTS — current stable LTS, supported until 2027 |
| `--size Standard_B2s` | 2 vCPU, 4 GiB RAM; burstable B-series handles low/medium traffic without wasted spend |
| `--admin-username azureuser` | The login user on the VM; avoid `root` |
| `--ssh-key-values` | Your public key — Azure injects it into `~/.ssh/authorized_keys` |
| `--public-ip-sku Standard` | Required for static allocation |
| `--public-ip-address-allocation Static` | IP will not change on deallocation |
| `--public-ip-address pip-ninetynine-prod` | Names the public IP resource for easy reference |
| `--nsg nsg-ninetynine-prod` | Creates a new Network Security Group attached to the NIC |

**Step 3 — Open inbound ports:**

The VM creation above creates an NSG but does not open any ports. Add the required rules:

```bash
# SSH — restricted to your operator IP only
az network nsg rule create \
  --resource-group rg-ninetynine-prod-eastus \
  --nsg-name nsg-ninetynine-prod \
  --name allow-ssh \
  --protocol Tcp \
  --direction Inbound \
  --priority 100 \
  --source-address-prefixes "<your-operator-ip>/32" \
  --destination-port-ranges 22 \
  --access Allow

# HTTP — open to the internet
az network nsg rule create \
  --resource-group rg-ninetynine-prod-eastus \
  --nsg-name nsg-ninetynine-prod \
  --name allow-http \
  --protocol Tcp \
  --direction Inbound \
  --priority 110 \
  --source-address-prefixes "*" \
  --destination-port-ranges 80 \
  --access Allow
```

Replace `<your-operator-ip>` with your current public IP. You can find it with `curl -s ifconfig.me`.

**Step 4 — Record the VM's public IP:**

```bash
az network public-ip show \
  --resource-group rg-ninetynine-prod-eastus \
  --name pip-ninetynine-prod \
  --query ipAddress \
  --output tsv
```

Save this IP. You will need it for MongoDB Atlas allowlisting and GitHub secrets.

#### Alternative: Azure Portal walkthrough

If you prefer the portal:

1. Navigate to **Virtual Machines** → **Create** → **Azure virtual machine**
2. Resource group: create new, use the naming convention above
3. VM name: `vm-ninetynine-prod`
4. Region: your target region
5. Image: **Ubuntu Server 22.04 LTS**
6. Size: **Standard_B2s** (search "B2s" in the size picker)
7. Authentication type: **SSH public key** — paste the contents of `~/.ssh/ninetynine_deploy.pub`
8. Inbound port rules: select **SSH (22)** for now; you will restrict the source IP and add port 80 after creation
9. On the **Networking** tab: create a new NSG, set the public IP to **Static**
10. Review + Create, then post-creation go to the NSG to add the HTTP rule and restrict SSH to your IP

### C.2 VM Initial Setup (manual)

SSH into the VM using the IP you recorded above:

```bash
ssh -i ~/.ssh/ninetynine_deploy azureuser@<VM_PUBLIC_IP>
```

#### Update packages

```bash
sudo apt update && sudo apt upgrade -y
```

This may take a few minutes on a fresh VM. Reboot if the upgrade includes a kernel update:

```bash
sudo reboot
```

Reconnect via SSH after the reboot.

#### Install Docker Engine

Use the official Docker repository, not the Ubuntu snap or the default apt package (which is typically outdated):

```bash
# Remove any old versions
sudo apt remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true

# Install prerequisites
sudo apt install -y ca-certificates curl gnupg lsb-release

# Add Docker's GPG key
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Add the repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
  https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker Engine and the Compose v2 plugin
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

#### Add the deploy user to the docker group

```bash
sudo usermod -aG docker azureuser
```

Log out and back in for the group change to take effect:

```bash
exit
# reconnect
ssh -i ~/.ssh/ninetynine_deploy azureuser@<VM_PUBLIC_IP>
```

#### Verify Docker is working

```bash
docker run hello-world
docker compose version
```

Both commands should succeed without `sudo`. If `hello-world` fails with a permission denied error, the group change has not taken effect yet — log out and reconnect.

#### Create the application directory

```bash
sudo mkdir -p /opt/ninetynine
sudo chown azureuser:azureuser /opt/ninetynine
```

The GitHub Actions deploy workflow writes `docker-compose.yml` and `.env` into this directory on each deployment.

#### Log rotation

Log rotation is already configured in `docker-compose.yml` via the `json-file` driver with `max-size: 10m` and `max-file: 5`. No additional host-level configuration is required — Docker handles rotation automatically at 10 MB per file, keeping a maximum of 5 rotated files (50 MB total per service).

#### Optional: unattended security upgrades

Enable automatic security patches to reduce the maintenance burden:

```bash
sudo apt install -y unattended-upgrades
sudo dpkg-reconfigure --priority=low unattended-upgrades
```

Accept the default (enable automatic updates).

#### Optional: fail2ban for SSH protection

```bash
sudo apt install -y fail2ban
sudo systemctl enable fail2ban
sudo systemctl start fail2ban
```

The default configuration bans IPs after 5 failed SSH login attempts. This complements the NSG rule restricting SSH to your operator IP.
