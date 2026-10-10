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
    │   ├── Common/AuditableEntity.cs
    │   ├── Entities/                Tenant, User, AuditLog
    │   └── Exceptions/              Domain, NotFound, Validation, Conflict
    ├── RBiz.Application/
    │   ├── Common/                  paging, claim names
    │   ├── Dtos/
    │   ├── Interfaces/              services + repository contracts
    │   ├── Mapping/
    │   ├── Services/
    │   └── DependencyInjection.cs
    ├── RBiz.Infrastructure/
    │   ├── Authentication/          JwtSettings, JwtTokenGenerator, PasswordHasher
    │   ├── Persistence/
    │   │   ├── AppDbContext.cs      also stamps audit columns + writes the audit trail
    │   │   ├── AppDbContextFactory.cs
    │   │   ├── Configurations/
    │   │   ├── Migrations/
    │   │   └── Repositories/
    │   └── DependencyInjection.cs
    └── RBiz.WebAPI/
        ├── Authentication/CurrentUser.cs
        ├── Controllers/
        ├── Errors/GlobalExceptionHandler.cs
        ├── Extensions/              Serilog, versioning, Swagger, auth, startup
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
