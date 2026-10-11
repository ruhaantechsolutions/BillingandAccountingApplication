# R-Biz API — test cases

Manual validation for every feature, with concrete inputs and expected results.
Written 2026-10-10 against commit `7f23128`.

Run these against your LocalDB. The sync delta path in particular has never executed on
a real SQL Server instance, so **section 8 is the highest-value part of this document**.

---

## 0. Setup

```bash
# From the repository root
dotnet ef database update --project src/RBiz.Infrastructure --startup-project src/RBiz.WebAPI
dotnet run --project src/RBiz.WebAPI
```

Development config seeds one tenant and one administrator:

| Field | Value |
| --- | --- |
| Tenant code | `RUHAAN` |
| Email | `admin@ruhaantech.local` |
| Password | `Admin@12345` |

```bash
export BASE=http://localhost:5080/api/v1
```

### Conventions you must know before writing any client

**Enums are numbers on the wire, not strings.** No `JsonStringEnumConverter` is
configured, so you send and receive integers:

| Enum | Values |
| --- | --- |
| `changeType` (sync) | `1` = Upsert, `2` = Delete |
| `status` (push result) | `1` = Applied, `2` = Conflict, `3` = Rejected, `4` = Duplicate |
| `action` (audit log) | `1` = Created, `2` = Updated, `3` = Deleted |

Sending `"changeType": "Upsert"` returns 400. If you would rather use strings, add
`JsonStringEnumConverter` in `Program.cs` — one line, and it changes the contract for
every client, so decide before the Angular app ships.

Other conventions: emails and tenant codes are stored and returned **upper-case**; all
timestamps are UTC; every error is RFC 9457 `ProblemDetails` with a `traceId` that also
appears in the log.

### Getting a token

```bash
TOKEN=$(curl -s -X POST "$BASE/auth/login" -H 'Content-Type: application/json' \
  -d '{"tenantCode":"RUHAAN","email":"admin@ruhaantech.local","password":"Admin@12345","deviceId":"test-device"}' \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['accessToken'])")

AUTH="Authorization: Bearer $TOKEN"
```

---

## 1. JWT authentication

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| AUTH-01 | Valid login | `{"tenantCode":"RUHAAN","email":"admin@ruhaantech.local","password":"Admin@12345"}` | **200**, body has `accessToken`, `refreshToken`, `expiresAtUtc`, `userId`, `tenantId` |
| AUTH-02 | Email case-insensitive | same but `email":"ADMIN@RuhaanTech.Local"` | **200** |
| AUTH-03 | Tenant code case-insensitive | `"tenantCode":"ruhaan"` | **200** |
| AUTH-04 | Wrong password | `"password":"WrongPass1"` | **400**, detail `The tenant code, email address or password is incorrect.` |
| AUTH-05 | Unknown email | `"email":"nobody@x.local"` | **400**, *identical* message to AUTH-04 |
| AUTH-06 | Unknown tenant | `"tenantCode":"NOPE"` | **400**, *identical* message |
| AUTH-07 | Blank fields | `{"tenantCode":"","email":"","password":""}` | **400** |
| AUTH-08 | No token on protected route | `GET $BASE/Users` with no header | **401**, `WWW-Authenticate: Bearer` |
| AUTH-09 | Garbage token | `Authorization: Bearer not.a.token` | **401** |
| AUTH-10 | Tampered signature | take a valid token, change its last character | **401** |

**AUTH-04/05/06 must return byte-identical messages.** Any difference lets an attacker
discover which emails and tenants exist.

```bash
# AUTH-11  Inactive user cannot sign in
# (SQL) UPDATE Users SET IsActive = 0 WHERE Email = 'ADMIN@RUHAANTECH.LOCAL';
# login  -> 400.  Set it back to 1 afterwards.
```

| ID | Scenario | Expected |
| --- | --- | --- |
| AUTH-11 | Login while `IsActive = 0` | **400** |
| AUTH-12 | Login while the tenant is `IsActive = 0` | **400** |
| AUTH-13 | Access token expired | set `Jwt:ExpiryMinutes: 1`, wait 70s, call `$BASE/Users` | **401** (proves `ClockSkew = Zero`) |

---

## 2. Global error handling

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| ERR-01 | Not found | `GET $BASE/Users/00000000-0000-0000-0000-000000000001` | **404**, title `Not found` |
| ERR-02 | Validation with field errors | `POST $BASE/Users` `{"email":"bad","fullName":"","password":"123"}` | **400**, `errors` object naming `Email`, `FullName`, `Password` |
| ERR-03 | Conflict | create a user, then create the same email again | **409**, title `Request conflicts with existing data` |
| ERR-04 | Unknown route | `GET $BASE/NotAThing` | **404** `ProblemDetails`, not an empty body |
| ERR-05 | Wrong method | `DELETE $BASE/Users` | **405** |
| ERR-06 | Malformed JSON | `POST $BASE/Users` with body `{"email":` | **400** |
| ERR-07 | Every error carries a traceId | any of the above | `traceId` present, and the same value appears in `logs/rbiz-*.log` |
| ERR-08 | No internals leak | stop SQL Server, then `GET $BASE/Users` | **500** with the generic apology only — **no** stack trace, no SQL text, no connection string |

ERR-08 is the one to check carefully. The real exception must appear in the log, never in
the response.

---

## 3. Logging

| ID | Scenario | Expected |
| --- | --- | --- |
| LOG-01 | File sink created | `logs/rbiz-<date>.log` exists after first request |
| LOG-02 | One line per request | a 200 produces exactly one `responded 200 in …ms` line, not three |
| LOG-03 | Level reflects outcome | 2xx → `INF`, 4xx → `WRN`, 5xx → `ERR` |
| LOG-04 | Password never logged | log in with a wrong password; grep the log for it → **no match** |
| LOG-05 | Token never logged | grep the log for an issued `accessToken` / `refreshToken` → **no match** |
| LOG-06 | Config-driven | set `Serilog:MinimumLevel:Default` to `Warning`, restart → INF lines stop |

---

## 4. API versioning

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| VER-01 | v1 resolves | `GET $BASE/sync/status` | **200** |
| VER-02 | Version advertised | any v1 response | header `api-supported-versions: 1.0` |
| VER-03 | Unknown version | `GET http://localhost:5080/api/v9/Users` | **400/404**, not a 500 |
| VER-04 | Missing version segment | `GET http://localhost:5080/api/Users` | **404** — the version is a required route segment |
| VER-05 | **Dynamic discovery** | add `[ApiVersion("2.0")]` to any controller, restart | `/swagger/v2/swagger.json` → **200**, and a v2 entry appears in the Swagger dropdown **with no change to `Program.cs`** |
| VER-06 | Deprecation flagged | add `[ApiVersion("3.0", Deprecated = true)]`, restart | header `api-deprecated-versions: 3.0`; the v3 document description says it is deprecated |

VER-05 is the point of the whole versioning setup — worth doing once to confirm.

---

## 5. Swagger UI

| ID | Scenario | Expected |
| --- | --- | --- |
| SWG-01 | UI loads | `GET /swagger/index.html` → **200** |
| SWG-02 | Document valid | `GET /swagger/v1/swagger.json` → **200**, `openapi: 3.0.4` |
| SWG-03 | All endpoints listed | paths include `/api/v1/Users`, `/api/v1/Tenants`, `/api/v1/AuditLogs`, `/api/v1/Auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/sync/changes`, `/api/v1/sync/push`, `/api/v1/sync/status` |
| SWG-04 | Authorize works | click **Authorize**, paste the token **without** `Bearer `, call `GET /Users` | **200** |
| SWG-05 | Descriptions present | endpoints show the XML summaries, not bare names |
| SWG-06 | Hidden outside Development | run with `ASPNETCORE_ENVIRONMENT=Production` | `/swagger/index.html` → **404** |

---

## 6. Tenants and Users (CRUD, validation, paging)

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| TEN-01 | List | `GET $BASE/Tenants` | **200**, `items`, `page`, `pageSize`, `totalCount`, `totalPages` |
| TEN-02 | Create | `{"name":"Second Business","code":"second"}` | **201**, `Location` header, `code` returned as **`SECOND`** |
| TEN-03 | Duplicate code | repeat TEN-02 | **409** |
| TEN-04 | Blank name | `{"name":"","code":"X"}` | **400** |
| TEN-05 | Update | `PUT $BASE/Tenants/{id}` `{"name":"Renamed","isActive":true}` | **200** |
| TEN-06 | Unknown id | `PUT $BASE/Tenants/{random guid}` | **404** |
| USR-01 | Create | `{"email":"clerk@ruhaantech.local","fullName":"Clerk","password":"Clerk@12345"}` | **201**, email returned upper-case, **no password field in the response** |
| USR-02 | Duplicate email | repeat USR-01 | **409** |
| USR-03 | Short password | `"password":"short"` | **400**, `Password` error mentions 8 characters |
| USR-04 | Email without `@` | `"email":"nope"` | **400** |
| USR-05 | Update | `PUT $BASE/Users/{id}` `{"fullName":"Senior Clerk","isActive":false}` | **200** |
| PAGE-01 | Defaults | `GET $BASE/Users` | `pageSize` = **20**, `page` = **1** |
| PAGE-02 | Capped | `GET $BASE/Users?pageSize=5000` | `pageSize` = **100** |
| PAGE-03 | Negative ignored | `GET $BASE/Users?page=-5&pageSize=-1` | `page` = 1, `pageSize` = 20 |
| PAGE-04 | Second page | `GET $BASE/Users?page=2&pageSize=1` | different item from page 1, `totalCount` unchanged |

**USR-01 check the response has no `passwordHash` field at all.**

---

## 7. Audit trail

| ID | Scenario | Expected |
| --- | --- | --- |
| AUD-01 | Create is audited | after USR-01, `GET $BASE/AuditLogs?pageSize=5` → newest entry `entityName: "User"`, `action: 1` |
| AUD-02 | `createdBy` attributed | that entry's `userId` = the signed-in admin's id |
| AUD-03 | Update records only changed columns | after USR-05, newest entry `action: 2`, `oldValues`/`newValues` contain **only** `FullName`, `IsActive`, `ModifiedAtUtc`, `ModifiedBy` — not the untouched columns |
| AUD-04 | **Password hash redacted** | no audit row anywhere contains `PasswordHash` |
| AUD-05 | **Refresh tokens never audited** | log in several times, then `SELECT COUNT(*) FROM AuditLogs WHERE EntityName LIKE '%RefreshToken%'` → **0** |
| AUD-06 | No write path | there is no POST/PUT/DELETE on `/AuditLogs` — confirm in Swagger |
| AUD-07 | Newest first | entries descend by `timestampUtc` |
| AUD-08 | Tenant-scoped | an audit list never shows another tenant's rows (see SEC-02) |

AUD-04 and AUD-05 are security assertions. Run them as SQL, not just through the API:

```sql
SELECT COUNT(*) FROM AuditLogs
WHERE ISNULL(OldValues,'') + ISNULL(NewValues,'') LIKE '%TokenHash%'
   OR ISNULL(OldValues,'') + ISNULL(NewValues,'') LIKE '%PasswordHash%';
-- must be 0
```

---

## 8. Offline sync — pull

The cursor is opaque. Store what the server returns; never construct one.

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| PULL-01 | Status probe | `GET $BASE/sync/status` | **200**, `syncableEntities: ["tenants","users"]`, `serverTimeUtc`, limits |
| PULL-02 | First full download | `GET $BASE/sync/changes` (no cursor) | **200**, every tenant and user, each entity with its own `nextCursor`, `hasMore: false` |
| PULL-03 | Cursor advances | repeat PULL-02 passing the returned cursor | **200**, `changes: []` — nothing changed |
| PULL-04 | Only the delta | `PUT` a user's name, then pull with the stored cursor | exactly **one** change, the edited user |
| PULL-05 | Narrow by entity | `GET $BASE/sync/changes?entities=users` | only the `users` change set |
| PULL-06 | Paging | `GET $BASE/sync/changes?entities=users&limit=1` with 2+ users | 1 change, `hasMore: true`; follow `nextCursor` until `hasMore: false`, with **no row repeated or skipped** |
| PULL-07 | Limit capped | `?limit=99999` | at most **500** rows |
| PULL-08 | Malformed cursor | `?cursor=total-garbage` | **400** — *not* a silent full resync |
| PULL-09 | Unknown entity | `?entities=invoices` | **400**, message naming `tenants, users` |
| PULL-10 | **No password hash** | inspect a `users` payload | fields are `id, tenantId, email, fullName, isActive, createdAtUtc, modifiedAtUtc` — **no `passwordHash`** |
| PULL-11 | Server clock | any pull | `serverTimeUtc` present (never trust the device clock) |
| PULL-12 | Injection attempt | `?cursor=';DROP TABLE Users;--` | **400**, and `Users` still exists |

PULL-06 is the one that would expose a broken cursor. Walk the whole set with `limit=1`
and confirm you end up with exactly the number of users that exist.

---

## 9. Offline sync — push

```bash
# Get a user's current row version first; it is the baseRowVersion you must send.
curl -s -H "$AUTH" "$BASE/sync/changes?entities=users" | python3 -m json.tool
```

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| PUSH-01 | Valid update | `{"deviceId":"dev-A","items":[{"operationId":"<new guid>","entity":"users","id":"<user id>","changeType":1,"baseRowVersion":"<current>","payload":{"fullName":"Pushed Name"}}]}` | **200**, `status: 1` (Applied); next pull shows the new name |
| PUSH-02 | **Idempotency** | resend PUSH-01 **unchanged** | **200**, `status: 4` (Duplicate) — and the name is *not* applied twice |
| PUSH-03 | **Conflict** | send a stale `baseRowVersion` (the value from before PUSH-01) | **200**, `status: 2` (Conflict), `serverRow` carries the current row, nothing overwritten |
| PUSH-04 | Missing base version on existing row | `"baseRowVersion": null` for a row that exists | **200**, `status: 2` (Conflict) |
| PUSH-05 | Base version for a row that does not exist | random `id` + non-null `baseRowVersion` | **200**, `status: 2` (Conflict) |
| PUSH-06 | **Delete → tombstone** | `changeType: 2` with the current `baseRowVersion` | **200**, `status: 1`; next pull returns that id with `changeType: 2` and `payload: null` |
| PUSH-07 | Repeat delete | resend PUSH-06 with a new `operationId` | **200**, `status: 1`, message `Already deleted on the server.` |
| PUSH-08 | Deleted row hidden from REST | `GET $BASE/Users/{deleted id}` | **404** — the query filter hides tombstones |
| PUSH-09 | Create over sync refused | `changeType: 1`, random new `id`, `baseRowVersion: null` | **200**, `status: 3` (Rejected), message points at `POST /api/v1/Users` |
| PUSH-10 | Tenants read-only | `"entity":"tenants"`, `changeType: 1` | **200**, `status: 3`, `'tenants' is read-only over sync...` |
| PUSH-11 | Unknown entity | `"entity":"invoices"` | **200**, `status: 3` — a per-item rejection, **not** a 400 for the batch |
| PUSH-12 | Blank field rejected | `"payload":{"fullName":"   "}` | **200**, `status: 3`, `fullName cannot be empty.` |
| PUSH-12a | **Unknown field rejected, not ignored** | `"payload":{"email":"x@y.z"}` | **200**, `status: 3`, message names `email` and lists the writable fields |
| PUSH-12b | Several unknown fields | `"payload":{"email":"x@y.z","passwordHash":"p","tenantId":"..."}` | **200**, `status: 3`, all three named |
| PUSH-12c | Valid + unknown mixed | `"payload":{"fullName":"Valid","email":"x@y.z"}` | **200**, `status: 3` — the valid part is **not** partially applied |
| PUSH-12d | Field name casing | `"payload":{"FullName":"Works"}` | **200**, `status: 1` — field names are case-insensitive |
| PUSH-13 | Bad boolean | `"payload":{"isActive":"maybe"}` | **200**, `status: 3` |
| PUSH-14 | Missing operationId | omit it / send all-zero guid | **200**, `status: 3` |
| PUSH-15 | Empty batch | `"items":[]` | **400** |
| PUSH-16 | Missing deviceId | `"deviceId":""` | **400** |
| PUSH-17 | Batch too large | 201 items | **400**, message naming the 200 limit |
| PUSH-18 | Duplicate operationId in one batch | two items sharing an `operationId` | **400**, naming the repeated id |
| PUSH-19 | Partial failure does not fail the batch | one valid item + one with a bad entity | **200**, `appliedCount: 1`, `rejectedCount: 1` |
| PUSH-20 | String enum rejected | `"changeType":"Upsert"` | **400** — enums are numbers |

Note the deliberate split: a malformed **batch** is a 400, while a bad **item** inside a
valid batch is a per-item rejection with HTTP 200. That is what lets a device drain the
rest of its outbox when one row is bad.

---

## 10. Refresh tokens

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| RT-01 | Login issues both | `POST $BASE/auth/login` with `deviceId` | `refreshToken` + `refreshTokenExpiresAtUtc` ≈ 30 days out |
| RT-02 | **Rotation** | `POST $BASE/auth/refresh` `{"refreshToken":"<rt1>","deviceId":"test-device"}` | **200**, new `accessToken` **and a different `refreshToken`** |
| RT-03 | Refreshed token works | call `GET $BASE/Users` with the new access token | **200** |
| RT-04 | **Replay detection** | resend `rt1` (already rotated) | **400** |
| RT-05 | **Family revoked** | now try `rt2`, which was legitimate | **400** — the replay killed the whole chain, by design |
| RT-06 | Recovery | log in again | **200**, a fresh independent family |
| RT-07 | Device binding | refresh a device-B token sending `"deviceId":"device-STOLEN"` | **400** |
| RT-08 | Correct device | same token, correct `deviceId` | **200** |
| RT-09 | Logout | `POST $BASE/auth/logout` `{"refreshToken":"<current>"}` | **204** |
| RT-10 | Reuse after logout | refresh with that token | **400** |
| RT-11 | Logout idempotent | repeat RT-09 | **204** |
| RT-12 | Unknown token | `{"refreshToken":"made-up-value"}` | **400**, message identical to RT-04 |
| RT-13 | Blank token | `{"refreshToken":""}` | **400** |
| RT-14 | Anonymous access | call refresh with **no** `Authorization` header | **200** — must work, the access token is expired by definition |
| RT-15 | Disabled user cannot refresh | set `IsActive = 0`, then refresh | **400** |
| RT-16 | Expired token | SQL below, then refresh | **400** |

```sql
-- RT-16  force expiry
UPDATE RefreshTokens SET ExpiresAtUtc = DATEADD(day, -1, SYSUTCDATETIME())
WHERE RevokedAtUtc IS NULL;
```

```sql
-- RT-17  stored state after the sequence above
SELECT DeviceId, RevokedReason, COUNT(*) FROM RefreshTokens GROUP BY DeviceId, RevokedReason;
-- RevokedReason: NULL = active, 1 = Rotated, 2 = SignedOut, 3 = ReuseDetected
-- RT-18  the plain token is never stored: every hash is 64 hex characters
SELECT TOP 5 TokenHash, LEN(TokenHash) FROM RefreshTokens;
```

**RT-05 is expected behaviour, not a bug.** A replay means a copy leaked and the server
cannot tell attacker from victim, so the whole family dies. This is why the Angular
client **must serialise refreshes** through one shared call — two tabs refreshing at once
will sign the user out.

---

## 11. Multi-tenancy isolation

Create a second tenant with its own user. Copying the existing `PasswordHash` means the
second admin's password is also `Admin@12345`.

```sql
DECLARE @t UNIQUEIDENTIFIER = NEWID(), @u UNIQUEIDENTIFIER = NEWID();

INSERT INTO Tenants (Id, Name, Code, IsActive, CreatedAtUtc, IsDeleted)
VALUES (@t, 'Second Business', 'SECOND', 1, SYSUTCDATETIME(), 0);

INSERT INTO Users (Id, TenantId, Email, FullName, PasswordHash, IsActive, CreatedAtUtc, IsDeleted)
SELECT @u, @t, 'ADMIN@SECOND.LOCAL', 'Second Admin', PasswordHash, 1, SYSUTCDATETIME(), 0
FROM Users WHERE Email = 'ADMIN@RUHAANTECH.LOCAL';

SELECT @t AS TenantId, @u AS UserId;
```

| ID | Scenario | Expected |
| --- | --- | --- |
| SEC-01 | Second tenant can log in | `{"tenantCode":"SECOND","email":"admin@second.local","password":"Admin@12345"}` → **200** |
| SEC-02 | `GET /Users` is scoped | each token sees only its own tenant's users |
| SEC-03 | Cross-tenant read | tenant A's token requesting tenant B's user id → **404** (not 403 — the row is simply not in scope) |
| SEC-04 | Cross-tenant sync pull | tenant A's pull never returns a tenant B row |
| SEC-05 | Cross-tenant sync push | tenant A pushes an update for tenant B's user id → **`status: 3`**, treated as a non-existent row |
| SEC-06 | `tenantId` in payload ignored | include `"tenantId":"<other tenant>"` in a push payload → ignored; the row keeps its own tenant |
| SEC-07 | Audit scoped | each tenant's `/AuditLogs` shows only its own entries |
| SEC-08 | Token without `tenant_id` | mint a JWT omitting the claim → **401** |

SEC-05 and SEC-06 matter most: the API has no way to express a cross-tenant write, because
the tenant always comes from the validated token and never from the body.

---

## 12. Concurrency (409)

| ID | Scenario | Expected |
| --- | --- | --- |
| CON-01 | Lost update detected | load a user in two sessions; `PUT` from both | second → **409**, title `Record was changed by someone else` |
| CON-02 | Message is user-facing | that 409's `detail` tells the user to reload and retry — no EF or SQL jargon |

```sql
-- CON-01 the deterministic way: change the row behind the API between read and write
UPDATE Users SET FullName = 'Changed By Someone Else' WHERE Email = 'ADMIN@RUHAANTECH.LOCAL';
```

---

## 13. CORS (Angular)

| ID | Scenario | Input | Expected |
| --- | --- | --- | --- |
| CORS-01 | Angular dev origin allowed | preflight `OPTIONS $BASE/sync/push` with `Origin: http://localhost:4200` | **204**, `Access-Control-Allow-Origin: http://localhost:4200` |
| CORS-02 | Other origin refused | `Origin: https://evil.example` | **no** `Access-Control-Allow-Origin` header |
| CORS-03 | Version headers readable | any CORS response | `Access-Control-Expose-Headers` includes `api-supported-versions` |
| CORS-04 | Preflight cached | CORS-01 | `Access-Control-Max-Age: 600` |
| CORS-05 | Fails closed | set `Cors:AllowedOrigins` to `[]`, restart | no origin gets CORS headers |
| CORS-06 | No credentials | any CORS response | `Access-Control-Allow-Credentials` **absent** — auth is a bearer header, not a cookie |

```bash
curl -i -X OPTIONS "$BASE/sync/push" \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: authorization,content-type"
```

---

## 14. Migrations

| ID | Scenario | Expected |
| --- | --- | --- |
| MIG-01 | Clean apply | `dotnet ef database update` on an empty database | `Tenants`, `Users`, `AuditLogs`, `SyncOperations`, `RefreshTokens` created |
| MIG-02 | Idempotent | run it again | no error, nothing changed |
| MIG-03 | `rowversion` populated | `SELECT Id, RowVersion FROM Users` | non-null, 8 bytes, and it **changes** after an update |
| MIG-04 | Ceiling function | `SELECT MIN_ACTIVE_ROWVERSION()` | returns a value (this is what PULL-02 depends on) |
| MIG-05 | Unique indexes hold | insert a duplicate tenant `Code` by SQL | rejected |
| MIG-06 | Applied list | `dotnet ef migrations list` | `InitialCreate`, `AddSyncSupport`, `AddRefreshTokens` all applied |

MIG-03 and MIG-04 are the two assumptions the entire sync design rests on and the two I
could not verify without SQL Server. Check them first.

---

## Known behaviours — not defects

Confirmed by reading the code; listing them so they are not filed as bugs.

1. **No REST `DELETE` exists** for users or tenants. The only delete path is a sync push
   with `changeType: 2`. Add REST delete endpoints if the UI needs them.
2. **Users cannot be created over sync** (a password would be required) and **tenants are
   entirely read-only over sync**. Both are online-only operations.
3. **A replay signs out the whole family** (RT-05). Deliberate.
4. **Each rotation extends the refresh window**, so an active device never has to re-login.
   Change only if you want fixed-length sessions.
5. **Enums are integers** on the wire.
6. **Tombstones and `SyncOperations` grow without bound** — no purge job yet.
7. **`push` returns no new `rowVersion`** for an applied item; the database assigns it
   during the batch commit. The client picks it up on the next pull.

---

## Priority if you are short on time

1. MIG-03, MIG-04 — the assumptions sync is built on.
2. PULL-02, PULL-06, PUSH-01, PUSH-02, PUSH-03, PUSH-06 — the sync core, never run on real SQL Server.
3. RT-02, RT-04, RT-05 — rotation and reuse detection.
4. AUD-04, AUD-05, PULL-10, SEC-05 — the four security assertions.
5. ERR-08 — confirm nothing leaks on failure.
