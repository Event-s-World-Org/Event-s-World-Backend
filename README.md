# EventsWorld Backend

**EventsWorld** backend built with **.NET 10** following a **Modular Monolith** architecture, Domain-Driven Design (DDD) principles, and lightweight in-house CQRS abstractions (no MediatR or AutoMapper, per ADR-0003).

---

## Architecture & Solution Structure

The solution (`EventsWorld.slnx`) is organized into three main areas:

```text
├── Directory.Build.props                # net10.0, nullable enabled, TreatWarningsAsErrors=true
├── .editorconfig                        # Code style and Roslyn analyzer rules
├── EventsWorld.slnx                     # Main solution file
├── src/
│   ├── Bootstrapper/
│   │   └── EventsWorld.Api/             # ASP.NET Core host (Composition Root)
│   ├── Shared/
│   │   ├── EventsWorld.Shared.Kernel/   # Entity, AggregateRoot, DomainEvent, Result/Error, Money, CQRS
│   │   └── EventsWorld.Shared.Infrastructure/ # Shared infrastructure contracts (UnitOfWork, Outbox, Audit)
│   └── Modules/
│       └── <Module>/
│           ├── EventsWorld.Modules.<Module>/           # Domain, Application, Infrastructure, Api
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
3. **Minimal visibility:** Types inside `EventsWorld.Modules.<Module>` are `internal` unless they belong to `Api` (such as `Add<Module>Module`) or `*.Contracts`.
4. **Explicit registration:** Every module exposes `Add<Module>Module(IServiceCollection, IConfiguration)` and is registered in `src/Bootstrapper/EventsWorld.Api/Program.cs`.

---

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.x` or later).

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

Available endpoints on startup:
* **Health Check:** `GET /health`
* **OpenAPI Document (Development):** `GET /openapi/v1.json`

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
* **`EventsWorld.ArchitectureTests`**: Enforces module isolation, `Domain` purity, `internal` type visibility, and module registration in `Program.cs` using `NetArchTest.Rules`.
* **`EventsWorld.UnitTests`**: Covers `Shared.Kernel` building blocks (`Result`, `Error`, `Money`, `Entity`, `AggregateRoot`).
* **`EventsWorld.IntegrationTests`**: Boots the full application host in memory via `WebApplicationFactory<Program>` and verifies the `/health` endpoint.
