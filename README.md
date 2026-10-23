# ApexPost Counter Platform

ASP.NET Core MVC counter platform for ApexPost Logistics. This repository is the Week 2 replacement for the Week 1 TechNova RMS console application.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or a compatible editor
- Git

## Build and test

```powershell
dotnet --info
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Run the web application:

```powershell
dotnet run --project src/ApexPost.Web
```

> The initial scaffold is architectural plumbing only. Business features, seed data, authentication, middleware, audit persistence and integration tests are added in subsequent feature branches. Do not report test/build success until these commands have actually run in your environment.

## Configuration and secrets

Never commit API keys or passwords. Configure development-only secrets with user-secrets after initializing the Web project:

```powershell
dotnet user-secrets init --project src/ApexPost.Web
dotnet user-secrets set "Integration:ApiKey" "<local-development-key>" --project src/ApexPost.Web
```

Use configuration files only for non-secret settings. Do not commit real credentials.

## Architecture

- `ApexPost.Domain`: business entities, value objects, aggregates, domain rules and domain events. No framework or infrastructure references.
- `ApexPost.Application`: use cases, DTOs, repository/service abstractions and orchestration. References Domain.
- `ApexPost.Infrastructure`: JSON repositories, audit store, notifications and external-service simulations. Implements Application abstractions.
- `ApexPost.Web`: MVC controllers, Razor views, authentication, middleware and filters. Composition root references Application and Infrastructure.
- `tests/`: domain/application unit tests and web integration tests.

See [DESIGN.md](DESIGN.md) and [docs/adr](docs/adr/).

## Git workflow

Create short-lived feature branches from `main`, make small commits, run build/tests, and open a pull request. Merge only reviewed, passing work. Keep secrets and local runtime data out of source control.

## Status

Architecture scaffold; feature implementation is tracked through feature branches and the project rubric checklist.
