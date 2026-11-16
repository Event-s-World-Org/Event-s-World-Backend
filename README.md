# EventsWorld Backend

**EventsWorld** backend built with **.NET 10** following a **Modular Monolith** architecture, Domain-Driven Design (DDD) principles, and lightweight in-house CQRS abstractions (no MediatR or AutoMapper, per ADR-0003).

---

## Architecture & Solution Structure

The solution (`EventsWorld.slnx`) is organized into three main areas:

```text
├── Directory.Build.props                # net10.0, nullable enabled, TreatWarningsAsErrors=true
├── Directory.Packages.props             # Central Package Management: every NuGet version, pinned
├── dotnet-tools.json                    # Local tools (dotnet-ef)
├── docker-compose.yml                   # Local PostgreSQL that behaves like Supabase
├── db/                                  # Database bootstrap and local-only scripts
├── .editorconfig                        # Code style and Roslyn analyzer rules
├── EventsWorld.slnx                     # Main solution file
├── src/
│   ├── Bootstrapper/
│   │   └── EventsWorld.Api/             # ASP.NET Core host (Composition Root)
│   ├── Shared/
│   │   ├── EventsWorld.Shared.Kernel/   # Entity, AggregateRoot, DomainEvent, Result/Error, Money, CQRS
│   │   └── EventsWorld.Shared.Infrastructure/ # Persistence (module DbContexts, migrator), UnitOfWork, Outbox, Audit
│   └── Modules/
│       └── <Module>/
│           ├── EventsWorld.Modules.<Module>/           # <Module>Module.cs + Domain, Application, Infrastructure, Api
│           └── EventsWorld.Modules.<Module>.Contracts/ # Public cross-module contracts
└── tests/
    ├── EventsWorld.ArchitectureTests/   # Module boundary enforcement (NetArchTest)
    ├── EventsWorld.UnitTests/           # Domain & Shared Kernel unit tests
    └── EventsWorld.IntegrationTests/    # WebApplicationFactory integration tests
```

### Registered Modules
* `Accounts`
* `Events`
* `Registrations`
* `Payments`
* `Files`
* `Notifications`
* `Audit`
* `Legal`

### Module Boundary Rules
1. **Cross-module isolation:** A module must never reference another module's implementation project; cross-module communication is allowed exclusively through `*.Contracts` projects.
2. **Domain purity:** No `Domain` namespace (nor `Shared.Kernel`) may reference Entity Framework Core, ASP.NET Core, or another module.
3. **Layers:** `Api → Application → Domain ← Infrastructure`. `Domain` depends on no other layer (nor on `Shared.Infrastructure`), and `Application` does not depend on `Infrastructure` or `Api`.
4. **Minimal visibility:** Types inside `EventsWorld.Modules.<Module>` are `internal` unless they belong to `Api` or `*.Contracts`. Exceptions: the module's composition root `<Module>Module` and the EF Core migrations, which are generated as public classes.
5. **Explicit registration:** Every module exposes `Add<Module>Module(IServiceCollection, IConfiguration)` in `<Module>Module.cs`, at the root of the module project, and is registered in `src/Bootstrapper/EventsWorld.Api/Program.cs`.

---

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.x` or later).
* [Docker](https://www.docker.com/) running: the local database and the integration tests use PostgreSQL 17 in a container.
* Local tools: `dotnet tool restore` (installs `dotnet-ef`, pinned in `dotnet-tools.json`).

---

## Build

The solution treats all compiler and analyzer warnings as errors (`TreatWarningsAsErrors = true`). To restore dependencies and build the entire solution:

```bash
dotnet restore EventsWorld.slnx
dotnet build EventsWorld.slnx
```

---

## Run

To run the API host (`EventsWorld.Api`) locally:

```bash
dotnet run --project src/Bootstrapper/EventsWorld.Api/EventsWorld.Api.csproj
```

Available endpoints on startup (`http://localhost:5037`):
* **Liveness:** `GET /health/live` (the process answers)
* **Readiness:** `GET /health/ready` (PostgreSQL answers within 5 s; 503 otherwise)
* **OpenAPI document (Development):** `GET /openapi/v1.json`
* **Scalar UI (Development):** `GET /scalar/v1`
* **API routes:** `/api/v1/public`, `/participant`, `/me`, `/events/{eventId}`, `/admin`

---

## Configuration per Environment

Set these in each environment (environment variables use `__`, e.g. `Cors__AllowedOrigins__0`):

| Key | Default | Set it to |
|---|---|---|
| `Cors:AllowedOrigins` | none (`http://localhost:4200` in development) | The frontend origins of the environment. Without them, browsers cannot call the API. |
| `ForwardedHeaders:KnownProxies` / `ForwardedHeaders:KnownNetworks` | none | The IPs (or CIDR ranges) of the hosting proxy. Without them, every client shares the proxy's IP and therefore the same rate limit. |
| `RateLimiting:AnonymousPermitLimit` / `RateLimiting:AnonymousWindowSeconds` | 60 requests / 60 s | The per-IP limit on `/api/v1/public` and `/api/v1/participant`. Exceeding it returns 429 with `Retry-After`. |
| `Serilog:MinimumLevel` | `Information` (`Warning` for ASP.NET Core and EF Core) | The log levels. |

---

## Adding Endpoints to a Module

1. Implement `IApiEndpoints` (`Shared.Infrastructure/Api`) and map the routes into the groups it receives (`Public`, `Participant`, `Me`, `Event`, `Admin`, or `V1` for routes outside them):

   ```csharp
   internal sealed class EventsEndpoints : IApiEndpoints
   {
       public void MapEndpoints(ApiGroups groups)
       {
           groups.Me.MapGet("/events", GetMyEvents);
           groups.V1.MapPost("/events", CreateEvent).WithRequestValidation<CreateEventRequest>();
       }
   }
   ```

2. Register it in the module's `Add<Module>Module`: `services.AddSingleton<IApiEndpoints, EventsEndpoints>();`.
3. Validate the request shape with a FluentValidation `AbstractValidator<T>` in the module and `.WithRequestValidation<T>()` on the endpoint. Validators are registered automatically.
4. Return business errors with `result.ToProblem()` or `error.ToProblem()`. The response is a ProblemDetails with the error `code`, the HTTP status of its `ErrorType` and a `correlationId`.
5. Mark properties that hold personal data with `[SensitiveData]` when their name does not reveal it (names containing `Email`, `Token`, `Password`, `Phone`, `FullName`, `Document`… are already redacted from logs).

---

## Database (PostgreSQL on Supabase)

Decisions: ADR-0002 and `Docs/architecture/database.md`. Each module owns a schema (`accounts`, `events`, `registrations`, `payments`, `files`, `notifications`, `audit`, `legal`), with its own `DbContext` and its own `__EFMigrationsHistory` table inside that schema. The `public` schema stays empty.

| Role | Used by | Can |
|---|---|---|
| `ew_migrator` | The `migrate` command | Owns the schemas, runs DDL (migrations) |
| `ew_app` | The API | Read and write data (DML) in the module schemas. No DDL |
| `anon`, `authenticated` | Supabase Data API | Nothing on our schemas |

### Connection strings

Never stored in the repository or in `appsettings*.json`. Locally use user secrets; elsewhere use environment variables (`ConnectionStrings__Database`, `ConnectionStrings__Migrations`).

| Name | Role | Supabase connection |
|---|---|---|
| `ConnectionStrings:Database` | `ew_app` | Pooler, **transaction mode** (port 6543). The API adds `No Reset On Close=true` and `Max Auto Prepare=0` itself |
| `ConnectionStrings:Migrations` | `ew_migrator` | **Direct or session mode** (port 5432) |

### Local database

```bash
cp .env.example .env            # choose your local passwords
docker compose up -d            # PostgreSQL 17 + Supabase emulation + roles (first start only)
dotnet tool restore

dotnet user-secrets --project src/Bootstrapper/EventsWorld.Api set "ConnectionStrings:Migrations" "Host=localhost;Port=5432;Database=eventsworld;Username=ew_migrator;Password=<EW_MIGRATOR_PASSWORD>"
dotnet user-secrets --project src/Bootstrapper/EventsWorld.Api set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=eventsworld;Username=ew_app;Password=<EW_APP_PASSWORD>"

dotnet run --project src/Bootstrapper/EventsWorld.Api -- migrate
```

The init scripts only run on an empty volume. To start over: `docker compose down -v`.

### Migrations

* **Apply:** `dotnet run --project src/Bootstrapper/EventsWorld.Api -- migrate` (or `dotnet EventsWorld.Api.dll migrate` from the published API). It applies every module's pending migrations as `ew_migrator` and exits. The deployment pipeline runs it before starting the new version.
* **Create** a migration for a module (example: Events):

  ```bash
  dotnet ef migrations add <Name> --context EventsDbContext \
    --project src/Modules/Events/EventsWorld.Modules.Events \
    --startup-project src/Bootstrapper/EventsWorld.Api \
    --output-dir Infrastructure/Persistence/Migrations
  ```

* Grants, triggers, extensions and other things EF Core cannot express go in the migration as raw SQL. Every module's first migration calls `SetUpModuleSchema(...)`, which creates the schema, gives `ew_app` data access to future tables and revokes everything from `anon` and `authenticated`.

### Setting up a Supabase project (once per environment)

1. Create the project in **São Paulo (sa-east-1)**.
2. In the SQL Editor (as `postgres`), run `db/bootstrap/roles-and-privileges.sql`, then set the passwords from the secrets manager:
   `ALTER ROLE ew_migrator PASSWORD '...';` and `ALTER ROLE ew_app PASSWORD '...';`
3. In **Settings → Data API**, do **not** add any of our schemas to "Exposed schemas".
4. Run `migrate` with the direct/session connection string of `ew_migrator`.
5. Give the API the pooler (transaction mode) connection string of `ew_app`.

Never run `db/local/*` on Supabase: those scripts only emulate Supabase locally.

### Deployment checklist (every environment, every release)

ADR-0002 requires a manual check besides the integration tests, because exposing a schema through the Data API by mistake would publish data. After `migrate`, run `db/checks/verify-database.sql` in the Supabase SQL Editor (or with `psql`). It returns a single table and **every row must show `ok = true`**: schemas owned by `ew_migrator`, one history table per module, no access for `anon`/`authenticated`, data access but no DDL for `ew_app`, an empty `public` schema and both extensions installed.

The SQL Editor only shows the result of the last statement, which is why the checks are a single query.

Also confirm in **Integrations → Data API** that the Data API is off or that none of our schemas is listed in *Exposed schemas*.

---

## Test

To run the full test suite (Architecture, Unit, and Integration tests):

```bash
dotnet test EventsWorld.slnx
```

If you have already built the solution:

```bash
dotnet test EventsWorld.slnx --no-build
```

### Test Suites
* **`EventsWorld.ArchitectureTests`**: Enforces module isolation, `Domain` purity, the layer dependency rule, `internal` type visibility, and module registration in `Program.cs` using `NetArchTest.Rules`.
* **`EventsWorld.UnitTests`**: Covers `Shared.Kernel` building blocks (`Result`, `Error`, `Money`, `Entity`, `AggregateRoot`), log masking and the error-to-status mapping.
* **`EventsWorld.IntegrationTests`**: Boots the full application host in memory (`ApiFactory`, which adds test-only endpoints under `/api/v1/public/test`) and checks:
  * the request pipeline: ProblemDetails, validation, rate limiting, CORS, security headers, correlation IDs, OpenAPI and Scalar, and that the real Serilog output never contains emails, tokens or personal data;
  * the database setup against a real PostgreSQL 17 started with Testcontainers: schemas, history tables, extensions, the `anon`/`authenticated` lockout, `ew_app` without DDL, the pooler settings and `/health/ready`. **Docker must be running.**

---

## Continuous Integration

`.github/workflows/ci.yml` runs on every pull request to `main`, on every push to `main`, and on demand (*Actions → CI → Run workflow*). The job **Build, format and test** fails on:

* a build warning (`-warnaserror`);
* a formatting difference (`dotnet format --verify-no-changes`);
* a failing unit, architecture or integration test. Integration tests use a real PostgreSQL container on the runner's Docker engine.

Test results appear as annotations and in the job summary. On failure, the `test-results` artifact (TRX files and hang dumps) is kept for 7 days. The job is limited to 10 minutes.

Before pushing, run the same checks locally:

```bash
dotnet build EventsWorld.slnx -warnaserror
dotnet format EventsWorld.slnx --verify-no-changes
dotnet test EventsWorld.slnx        # Docker must be running
```

To fix formatting automatically, run `dotnet format EventsWorld.slnx`. Text files use LF line endings (`.gitattributes`); in a clone made before that, the same command also converts the old CRLF files, without changes for Git.

> **Required check:** a repository admin must mark **Build, format and test** as a required status check in the branch protection rule for `main` (*Settings → Branches*), so a pull request cannot be merged until it passes.
