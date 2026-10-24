# R-Biz — project context

Billing and accounting API. .NET 10, Clean Architecture, SQL Server.

> Filename note: this is `CLAUDE.md` because Claude Code loads it automatically at the
> start of every session, so the decisions below are actually applied instead of sitting
> in an unread file. Rename it if you would rather it were not Claude-specific — the
> content stands on its own either way.

---

## Status — 2026-10-24

| Branch | Commit | State |
| --- | --- | --- |
| `development/code_backend` | `b70c880` | Integration branch. Now carries sync + refresh tokens. |
| `feature/company-creation` | current | Company module: mobile login, registration, companies. |
| `feature/pwa-offline-sync` | `b70c880` | Merged into development/code_backend. Can be deleted. |
| `claude/pensive-feynman-guggpn` | `9344ce9` | Dead. Superseded; delete by hand on GitHub. |

Three commits on the feature branch: offline sync (`ace46ea`), a merge of the base
(`fbe40eb`), refresh tokens (`d0fdbb2`).

**Built:** 4-project Clean Architecture solution; Serilog; global error handling via
`IExceptionHandler` + ProblemDetails; dynamic API versioning; Swagger UI; JWT auth;
Tenant/User/AuditLog entities with automatic audit trail; offline sync (rowversion
deltas, tombstones, idempotent push, server-authoritative conflicts); refresh tokens with
rotation and reuse detection.

**Migrations:** consolidated to a single `InitialCreate` (2026-10-24). The three earlier
migrations were deleted, so an existing `RBiz_Dev` must be dropped before applying it.

See `README.md` for how it all works. This file is only what a future session needs in
order not to redo or re-ask.

---

## Settled decisions — do not re-ask

| Topic | Decision |
| --- | --- |
| Database | **SQL Server**, EF Core 10.0.12. Dev uses LocalDB (`Data Source=(localdb)\MSSQLLocalDB`). |
| Core model | **User → many Companies** via `UserCompany`. There is no Tenant; `Company` replaced it. The active company travels in the JWT as `company_id`. |
| Login identity | **Mobile number**, globally unique, `^[6-9][0-9]{9}$`. Email is optional and not used to sign in. |
| GSTIN | Format + state code + **mod-36 check digit**, all offline, in `Domain/Utilities/GstinValidator`. No open-source service verifies against the live register; `IGstinVerificationService` is the seam for a licensed GSP. |
| Logging | **Serilog** (log4net was declined). Inner layers log via `ILogger<T>` only. |
| Naming | Namespaces and projects `RBiz.*`. "R-Biz" has a hyphen, illegal in C# identifiers. |
| Solution file | Classic **`RBiz.sln`**, not .NET 10's default `.slnx` — older Visual Studio cannot open `.slnx`. |
| Frontend | **Angular 18+**, its own repo. This repo stays client-agnostic; no frontend code here. |
| Sync entities | `Tenant` + `User` now. **Billing and Stock join later** — the user will say when. |
| Sync code layout | Isolated in a `Sync/` folder per layer, at the user's request, so the feature can be found and changed as a unit. |
| Conflicts | **Server-authoritative**, never last-write-wins. Financial data is not silently overwritten. |
| Dependencies | Deliberately **no** AutoMapper, MediatR or FluentValidation. Mapping is hand-written. Keep it that way unless asked. |
| Package versions | Central Package Management in `Directory.Packages.props`. `PackageReference` carries no `Version`. |
| Primary keys | `Guid.CreateVersion7()` — time-ordered, client-generatable, index-friendly. |
| Pull requests | **Do not open one** unless explicitly asked. |

Standing conventions: `TreatWarningsAsErrors` is on and the build is kept at zero
warnings; list endpoints are paged; Swagger is Development-only; seed data comes from
configuration, never a migration's `HasData`.

---

## Environment facts — cost real time to discover

- **No `dotnet` preinstalled.** `apt-get install -y dotnet-sdk-10.0` works (Ubuntu 24.04).
  Gives SDK 10.0.112 / runtime 10.0.12.
- **`dotnet-ef`:** `dotnet tool install --global dotnet-ef --version 10.0.12`, then add
  `/root/.dotnet/tools` to `PATH`.
- **No Docker daemon** — no SQL Server container.
- **SQL Server cannot be installed.** `packages.microsoft.com` redirects `.deb` downloads
  to `pmc-geofence.trafficmanager.net`, which the egress policy denies. Do not retry.
- **SQLite substitution** (temporarily swapping `UseSqlServer` for `UseSqlite`) works for
  auth and CRUD testing, but **not for sync**: the delta needs `MIN_ACTIVE_ROWVERSION()`
  and `TOP(n)`, and SQLite never populates `rowversion`. Always back up the patched files
  and verify the revert with `md5sum -c`.
- **No DB? Mint a JWT.** Sign one in Python with HMAC-SHA256 using the dev `Jwt:Key`,
  `iss=RBiz.Api`, `aud=RBiz.Client`, plus `sub` and `tenant_id`. Reaches protected
  endpoints with no login round trip.
- **Remote branch deletion returns 403.** Session git credentials allow create and update,
  not delete. Ask the user to delete from the GitHub UI.
- **`pgrep -f 'RBiz[.]WebAPI'` kills its own shell** when that path appears elsewhere in
  the same command. Use `ps -eo pid,args | grep "[R]Biz" | awk '{print $1}'` in a call
  that mentions nothing else matching.

---

## Library gotchas already hit

- **`InvariantGlobalization` must stay off.** `Microsoft.Data.SqlClient` throws
  "Globalization Invariant Mode is not supported" on connect.
- **Swashbuckle 10.3.0 uses Microsoft.OpenApi v2.** `Microsoft.OpenApi.Models` no longer
  exists (flattened to `Microsoft.OpenApi`); a scheme is referenced via
  `OpenApiSecuritySchemeReference`; `AddSecurityRequirement` takes a factory delegate.
- **`AssumeDefaultVersionWhenUnspecified` is rejected** by analyzer AV0016 when every
  controller declares `[ApiVersion]`.
- **`byte[]` cannot be compared in LINQ**, so the rowversion delta range is parameterised
  raw SQL built from EF model metadata. There is nothing for EF Core to translate.
- **Records need `<param>` docs for every parameter** or none, under warnings-as-errors.
- **NuGet audit fails the build.** `Microsoft.Data.SqlClient` pulls a vulnerable
  `System.Security.Cryptography.Pkcs`; it is pinned to a patched version in
  `Directory.Packages.props`. Pin, never disable the audit.

---

## Not verified — be honest about this

- **The sync delta path has never run against real SQL Server.** `rowversion` assignment,
  `MIN_ACTIVE_ROWVERSION()` and the conflict path are build- and SQL-verified only. Needs
  a smoke test on the user's LocalDB. Everything else was exercised end-to-end.
- Refresh tokens *were* tested end-to-end on SQLite: rotation, replay revoking the family,
  device binding, logout idempotency, and no hash reaching the audit trail or logs.
- **There is no test project.** All verification so far has been throwaway harnesses and
  HTTP smoke tests.

---

## Open items

1. **Angular PWA client** — not started. Manifest, service worker, IndexedDB outbox, the
   sync loop, and an interceptor that **serialises refreshes** (concurrent refreshes
   revoke the token family and sign the user out).
2. **Billing and Stock entities** — then add each to sync: derive from `SyncableEntity`,
   call `ConfigureSyncable()`, add a provider, register it. The sync engine does not change.
3. **Purge jobs** — tombstones and `SyncOperations` grow without bound. Noted, not built.
4. **Absolute session expiry** — each rotation currently extends the refresh window, so an
   active device stays signed in indefinitely. Change only if asked.
5. Sync is read-only for `Tenant`; `User` can be edited but not created over sync.
   Email is changed online only, through `PUT /api/v1/Users/{id}/email`, because tenant
   uniqueness cannot be settled on a device.
6. A sync provider's `WritableFields` is its entire write surface. Unknown payload fields
   are **rejected**, never ignored, so a device is never told a discarded change was
   applied. Keep that rule when adding Billing and Stock.
7. **Company address editing** has no endpoint yet (`PUT /companies/{id}/address`), and
   nor do `CompanyAddress`, `CompanyGstRegistration` or `Location` have sync providers.
   The schema is sync-ready; the providers are added when the PWA needs them.
8. The wireframe's example GSTIN `24ABCDE1234F1Z5` **fails its own check digit**. The
   backend uses `24ABCDE1234F1Z6` in its message; the front end should be corrected too.

---

## Working agreement

The user wants a **plan before implementation** on anything substantial, and confirms
before code is written. Scope is held tight — "strictly do not add any other things" was
said explicitly, so flag extras as recommendations instead of building them. Work on the
named feature branch, commit with a descriptive message, push, and do not open a PR.
