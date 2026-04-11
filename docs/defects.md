# Defects log

Tracks bugs discovered during development. Entries are added as issues are found and kept after fixes land so future work can reference them.

---

## DEF-001 — Seeder idempotency short-circuits data migrations

**Discovered**: 2026-04-11 during Wave 4 post-integration testing
**Status**: Partially fixed in commit `cfb053d` (heal pass for `passwordHash` and `emailAddress`)
**Severity**: Medium — blocked email/password login end-to-end testing until healed
**Owner**: backend

### Symptom

After Wave 3 (WP-11) added password hashing to the seeded dev test players, the three test players (`carey`, `george`, `carey_b`) in an existing MongoDB volume still had `passwordHash: ''` (empty string) and `emailAddress: ''` in their documents. Email/password login against any of them failed silently with "Invalid email or password." (from the auth service's no-enumeration generic error). Only the mock picker (which bypasses password validation) worked.

### Root cause

`DataSeeder.SeedAsync` uses a single-check idempotency guard:

```csharp
var existing = await playerRepository.GetByDisplayNameAsync(
    IDataSeeder.TestPlayerDisplayNames[0], ct);
if (existing is not null) return;  // short-circuit
```

When the `carey` player already exists (from an earlier seed run), the method returns early. This means any schema-level changes to seeded entities shipped in later WPs — new required fields, hashed credentials, visibility defaults — don't reach the existing records. The seeder is "idempotent for creation" but "non-reconciling for updates".

### Partial fix (cfb053d)

Added a `HealExistingTestPlayersAsync` pass that runs **before** the idempotency check. For each known test display name, if `PasswordHash` or `EmailAddress` is empty, it backfills them and saves. The heal itself is idempotent (zero-ops after the first successful run).

This covers the specific case we hit but does not address the underlying pattern.

### Remaining work (not yet scheduled)

The seeder should be refactored from "create once, skip forever" to **reconcile every startup**. Concretely:

1. For each known test player display name, either create or update the player document so that all fields match the seeded template (not just the ones currently in the heal pass).
2. Same for venues and games if we extend their schema in the future.
3. Keep idempotency: running the seeder twice on a fresh DB must not duplicate records (current state: ok, because we check existence before create).
4. Keep startup fast: if nothing changes, no writes should hit MongoDB (current state: the heal pass short-circuits on non-empty fields, so no write per unchanged record).
5. Consider a `SchemaVersion` field on seeded records so the seeder can cheaply detect when a reconcile is needed rather than field-by-field comparison.

### Why this isn't yet fully fixed

The heal-pass solution is tactical: it patches the fields we know are broken (`passwordHash`, `emailAddress`) and unblocks dev testing. A full reconcile rewrite is a small-to-medium work package and should land in a dedicated defect-fix WP, probably after the remaining redesign waves complete so it doesn't compete with UI work for ownership boundaries.

### Related risks surfaced during investigation

During the investigation two other bugs were found and fixed in commit `ef2a3b9`:

- **NavigationException swallowed in `HandleSubmitAsync`** — `New.razor` wrapped `Navigation.NavigateTo` inside `try/catch (Exception)`, catching the Blazor-internal `NavigationException` that signals the redirect. Fixed by moving `NavigateTo` outside the catch. Audit confirmed this was the only occurrence in the codebase (Login/Register/etc. use `try/finally` which is safe).
- **Data Protection key ring ephemeral across Docker rebuilds** — every `./deploy.sh rebuild` regenerated the DP key ring, invalidating all browser cookies (auth + antiforgery). Fixed by persisting `/var/ninetynine/keys` on a named volume, `chown $APP_UID` in the Dockerfile, and `AddDataProtection().PersistKeysToFileSystem(...)` in `Program.cs`.

These three issues surfaced together because the DP-keys issue caused the original antiforgery failure, which masked the New.razor NavigationException swallow, which in turn masked the seeder idempotency bug. Each layer had to be stripped away to find the underlying data defect.
