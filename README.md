# R-Biz

Billing and accounting API built on .NET 10 using Clean Architecture.

## Architecture

Dependencies always point inward. Each arrow is a project reference, so the rule is
enforced by the compiler rather than by convention:

```
WebAPI  ->  Infrastructure  ->  Application  ->  Domain
   \________________________________/
            (also references Application to call its services)
```

| Project | Role | References |
| --- | --- | --- |
| `RBiz.Domain` | Entities, the auditable base class and business exceptions. | none |
| `RBiz.Application` | Services plus the interfaces Infrastructure implements. | Domain |
| `RBiz.Infrastructure` | EF Core `DbContext`, migrations, repositories, JWT issuing, password hashing. | Application |
| `RBiz.WebAPI` | Controllers, Swagger, versioning, logging, global error handling. | Application, Infrastructure |

`RBiz.Domain` has no NuGet packages at all, and `RBiz.Application` has only abstractions.
`AppDbContext` is physically unreachable from `RBiz.Application`, so the layering cannot
quietly erode.

```
RBiz.sln
├── Directory.Build.props            shared build settings (framework, nullable, warnings)
├── Directory.Packages.props         every NuGet version, in one place
└── src/
    ├── RBiz.Domain/
    │   ├── Common/                  AuditableEntity, INonAuditable
    │   ├── Entities/                Tenant, User, AuditLog, RefreshToken
    │   ├── Exceptions/              Domain, NotFound, Validation, Conflict
    │   └── Sync/                    ISyncable, SyncableEntity, SyncOperation
    ├── RBiz.Application/
    │   ├── Common/                  paging, claim names
    │   ├── Dtos/
    │   ├── Interfaces/              services + repository contracts
    │   ├── Mapping/
    │   ├── Services/
    │   ├── Sync/                    offline-sync engine, cursor, contracts, DTOs
    │   └── DependencyInjection.cs
    ├── RBiz.Infrastructure/
    │   ├── Authentication/          JWT, password + secure-token hashing, lifetimes
    │   ├── Persistence/
    │   │   ├── AppDbContext.cs      audit columns, audit trail, soft-delete interception
    │   │   ├── AppDbContextFactory.cs
    │   │   ├── Configurations/
    │   │   ├── Migrations/
    │   │   ├── Repositories/
    │   │   └── Sync/                providers, registry, store, delta query
    │   └── DependencyInjection.cs
    └── RBiz.WebAPI/
        ├── Authentication/CurrentUser.cs
        ├── Controllers/             includes SyncController
        ├── Errors/GlobalExceptionHandler.cs
        ├── Extensions/              Serilog, versioning, Swagger, auth, CORS, startup
        └── Program.cs
```

## Getting started

### 1. Configure

`appsettings.json` ships with blank secrets on purpose. Set these for local development
in `appsettings.Development.json` (already filled in with local defaults) or, better, in
user secrets:

```bash
cd src/RBiz.WebAPI
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=RBiz_Dev;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=False"
dotnet user-secrets set "Jwt:Key" "<at least 32 characters>"
```

`Jwt:Key` must be at least 32 characters; the application refuses to start otherwise.

### 2. Create the database

```bash
dotnet tool install --global dotnet-ef --version 10.0.12

# From the repository root:
dotnet ef database update \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI
```

### 3. Run

```bash
dotnet run --project src/RBiz.WebAPI
```

Swagger UI is at `http://localhost:5080/swagger` (Development only).

On first run in Development, `Database:SeedOnStartup` creates one tenant and one
administrator from the `Database:Seed` values in `appsettings.Development.json`:

| Field | Default |
| --- | --- |
| Tenant code | `RUHAAN` |
| Email | `admin@ruhaantech.local` |
| Password | `Admin@12345` |

Change these before using the application anywhere real. Both
`ApplyMigrationsOnStartup` and `SeedOnStartup` default to `false` outside Development.

### 4. Call the API

```bash
curl -X POST http://localhost:5080/api/v1/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"tenantCode":"RUHAAN","email":"admin@ruhaantech.local","password":"Admin@12345"}'
```

Paste the returned `accessToken` into the **Authorize** box in Swagger UI, or send it as
`Authorization: Bearer <token>`.

## EF Core migrations

A design-time factory (`AppDbContextFactory`) means these commands do not need the web
host to start, so an unrelated setting cannot break them.

```bash
# Add a migration after changing an entity
dotnet ef migrations add <Name> \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI \
  --output-dir Persistence/Migrations

# Apply pending migrations
dotnet ef database update \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI

# Roll back to a specific migration
dotnet ef database update <PreviousMigrationName> \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI

# Remove the last, unapplied migration
dotnet ef migrations remove \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI

# Produce a SQL script instead of applying it (preferred for production)
dotnet ef migrations script --idempotent \
  --project src/RBiz.Infrastructure \
  --startup-project src/RBiz.WebAPI \
  --output migrate.sql
```

The connection string comes from `ConnectionStrings:DefaultConnection`, and the
`ConnectionStrings__DefaultConnection` environment variable overrides it, which is how CI
should supply it.

## How the cross-cutting concerns work

**Logging.** Serilog writes to the console and to a daily rolling file under `logs/`,
configured entirely from the `Serilog` section of `appsettings.json`. Only `RBiz.WebAPI`
references Serilog: the inner layers log through `ILogger<T>`, so replacing the backend
touches one file. `UseSerilogRequestLogging` emits one line per request instead of the
three or four ASP.NET Core produces by default.

**Global error handling.** `GlobalExceptionHandler` implements `IExceptionHandler`, so it
runs only on the failure path. Every error is an RFC 9457 `ProblemDetails` with a
`traceId` that also appears in the logs. Domain exceptions map to 400/401/404/409 and
carry a message written for an end user; anything unexpected returns a generic apology
and the real exception goes only to the log, never to the caller.

**API versioning.** Versions live in the URL (`/api/v1/...`). Version discovery is
dynamic: `ConfigureSwaggerOptions` enumerates `IApiVersionDescriptionProvider`, which
reflects over the `[ApiVersion]` attributes. Adding `[ApiVersion("2.0")]` to a controller
makes a v2 entry appear in the Swagger dropdown with no change to `Program.cs`.
Deprecated versions are labelled automatically and advertised in the
`api-deprecated-versions` response header.

**Swagger UI.** One document per discovered version, with a JWT **Authorize** button and
endpoint descriptions pulled from the XML doc comments on the controllers.

**Authentication.** JWT bearer tokens signed with HMAC-SHA256. Issuer, audience, lifetime
and signing key are all validated, with `ClockSkew` set to zero so an expired token is
actually expired. `ApiControllerBase` carries `[Authorize]`, making authentication
opt-out: a new controller is protected the moment it is created.

**Multi-tenancy.** Every token carries a `tenant_id` claim. Services read the tenant from
`ICurrentUser` (the validated token) and never from the request body, so a caller cannot
reach another tenant's rows. The repository queries include the tenant in the predicate,
rather than relying on a global query filter, so the boundary is visible in the code.

**Audit trail.** `AppDbContext.SaveChangesAsync` stamps `CreatedAt/CreatedBy` and
`ModifiedAt/ModifiedBy`, and writes an `AuditLog` row per changed entity in the same
transaction. Updates record only the columns that actually changed. `PasswordHash` is
redacted. There is no write path to `AuditLogs` through the API by design.

## Notes

- `InvariantGlobalization` is deliberately left off: `Microsoft.Data.SqlClient` throws
  `Globalization Invariant Mode is not supported` when opening a connection.
- Primary keys use `Guid.CreateVersion7()`, which is time-ordered and therefore avoids the
  index fragmentation random GUIDs cause in SQL Server.
- List endpoints are paged (default 20, maximum 100), so no query can return an unbounded
  number of rows.

## PWA and offline synchronisation

The API carries the server half of offline sync. The Angular PWA consumes it; nothing
here assumes a particular client. Everything for this feature lives in a `Sync` folder
in each layer, so it can be found or changed as one unit.

### Why it is built this way

Offline sync is easy to get almost right and silently lose data. Four specific failure
modes are handled deliberately:

**"What changed since I last synced?"** Timestamps are the obvious answer and the wrong
one: clock skew, two rows in the same millisecond, and rows becoming visible after you
have read past their timestamp all cause silently skipped records. The change marker is
a SQL Server **`rowversion`** — an 8-byte counter the engine assigns, strictly increasing
database-wide, which doubles as the concurrency token.

**The missed-change race.** `rowversion` alone is still unsafe: a transaction that starts
early but commits late writes a low row version that appears *after* a client has synced
past it, and that change is then lost forever. Every delta is therefore capped at
**`MIN_ACTIVE_ROWVERSION()`**, the lowest row version still in flight, so no committed
change can appear below a cursor a client already holds.

**Deletes are invisible to an offline client.** A hard-deleted row simply stops appearing
in the delta, leaving every disconnected device with a stale copy. Deletes are therefore
soft: `AppDbContext` converts a `Remove()` into a tombstone, which ships in the next
delta. A global query filter keeps tombstones out of ordinary queries; the delta
deliberately bypasses it with `IgnoreQueryFilters`.

**Retries must not double-apply.** A device on a weak connection retries a push it never
saw acknowledged. Each change carries a client `operationId`, recorded in the
`SyncOperations` ledger in the same transaction as the change, so a replay is recognised
and reported as `Duplicate` rather than applied twice.

### Conflict handling

Server-authoritative, never last-write-wins — silently overwriting a financial record is
not an acceptable default. A client sends the `baseRowVersion` it last saw; if the
server's differs, the item comes back as `Conflict` with the server's current row
attached, and nothing is overwritten. The client decides what to do.

Conflicts are returned as data, not exceptions, so one contested row does not stop a
device from draining the rest of its outbox.

### Endpoints

```http
GET  /api/v1/sync/status
     → server time, synchronisable entity names, current limits

GET  /api/v1/sync/changes?cursor={opaque}&entities=users&limit=200
     → { entities: [ { entity, changes[], nextCursor, hasMore } ], serverTimeUtc }

POST /api/v1/sync/push
     → { results: [ { operationId, status, message, serverRow } ], serverTimeUtc }
```

The client loop: `status` to confirm it is genuinely online, `changes` repeatedly while
`hasMore` is true, then `push` to drain the outbox. Omit the cursor for a first full
download. Each entity carries its own cursor so a large backlog in one does not hold up
another.

Paging is keyset on the row version — no `OFFSET`, so the thousandth page costs the same
as the first and is served straight off the `(IsDeleted, RowVersion)` index.

### Adding an entity to sync

When Billing or Stock arrives, three steps and nothing in the sync engine changes:

1. Derive the entity from `SyncableEntity`.
2. Call `builder.ConfigureSyncable()` in its EF configuration — one line that adds the row
   version, tombstone columns, delta index and soft-delete filter together, so none can
   be forgotten.
3. Add a provider deriving from `SyncEntityProviderBase<T>` and register it in
   `AddOfflineSync`. A provider only declares its name, tenant scoping, what a client may
   read, and what a client may write.

A provider's `WritableFields` set is the whole of its write surface. A payload carrying
anything outside it is **rejected**, never ignored: silently dropping an unrecognised
field would tell a device its change was applied when it was discarded, and the device
would then clear that item from its outbox and lose the edit permanently. Field names are
matched case-insensitively. An empty set makes the entity read-only over sync.

Registration order is the order a client receives entities, so register a parent before
the rows that reference it.

### Angular client notes

- **CORS** is configured from `Cors:AllowedOrigins`, preset to `localhost:4200` in
  Development. It **fails closed**: an empty list means no cross-origin access, because a
  wildcard would expose the API to any site the user has open. Credentials are not
  allowed and should not be — authentication is a bearer header, not a cookie.
- `api-supported-versions` and `api-deprecated-versions` are exposed, so the client can
  detect it is calling a deprecated version. Angular's `HttpClient` cannot read a
  response header the server does not list.
- `serverTimeUtc` is on every sync response. Do not trust an offline device's clock for
  ordering or expiry; use the offset against this value.
- `navigator.onLine` only reports that a network interface exists. Use
  `GET /sync/status` to decide whether this API is actually reachable.
- Generate ids client-side as **UUID v7** to match the server's keys — time-ordered, so
  they do not fragment the clustered index.

### Refresh tokens

An access token lives 60–120 minutes, so a device offline all day would return with a
dead token and no way to upload the work queued on it. Login therefore also issues a
**refresh token**, valid for `Jwt:RefreshTokenDays` (30 by default), which bounds how
long a device may stay offline and still recover.

```http
POST /api/v1/auth/login     → accessToken + refreshToken
POST /api/v1/auth/refresh   → a NEW accessToken and a NEW refreshToken
POST /api/v1/auth/logout    → 204, revokes the whole family
```

`refresh` and `logout` are anonymous by necessity: the access token is expired by
definition at the point a client needs to refresh it.

How the token is protected:

- **Stored hashed.** Only the SHA-256 of the token is persisted, so a leaked database
  cannot be replayed against the API. A plain hash is correct here, unlike for a
  password: the token is 256 bits of cryptographic randomness, so there is no dictionary
  to attack, and a deliberately slow hash would be paid on every reconnect for nothing.
- **Rotated on every use.** Each refresh issues a new token and spends the old one, so a
  captured token is useful only until the real client next reconnects.
- **Replay revokes the family.** Presenting an already-rotated token means a copy leaked,
  and the server cannot tell the attacker from the victim — so every token descended from
  that login is revoked and the user must sign in again.
- **Bound to a device.** The token carries the same `deviceId` used for sync, so a token
  lifted from one device will not work from another.
- **Never audited, never synchronised.** `RefreshToken` implements `INonAuditable`, so the
  hash is never copied into the append-only audit trail, and it does not derive from
  `SyncableEntity`, so it can never be replicated to a device.
- **Re-checks the account.** A user disabled or deleted while the device was offline
  cannot be revived by a refresh.

Every failure returns the same message regardless of cause — unknown, expired, revoked or
wrong device — so the endpoint cannot be used to probe which tokens exist. The real reason
goes to the log.

**The client must serialise refreshes.** Because a replayed token revokes the family, an
Angular app with several tabs has to funnel refreshes through one shared call (a single
`shareReplay`-ed observable in an interceptor). Two concurrent refreshes will sign the
user out. That strictness is deliberate: the alternative is a grace window in which a
stolen token keeps working.

Note also that each rotation extends the window, so an actively used device stays signed
in indefinitely. If sessions must die on a fixed schedule, carry an absolute expiry
forward from the family's first token instead.

### Caveat

`rowversion` and `MIN_ACTIVE_ROWVERSION()` are SQL Server features. Moving to PostgreSQL
would mean replacing the change marker with a sequence-backed column — the provider
abstraction is the only place that would change.
