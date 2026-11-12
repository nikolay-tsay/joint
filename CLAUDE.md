# joint

Read-only overview of a household's finances across separate bank and investment accounts.
Personal project for one household; not a public product. Wallet by BudgetBakers is the
reference for screens and flows.

## Stack and decisions

- .NET 11, C# 15, runtime async. All set once in `Directory.Build.props`; don't repeat them per project.
- Backend: one ASP.NET Core project (`src/Joint.Api`), vertical slice architecture, minimal APIs, EF Core on Postgres, Hangfire for scheduled sync. Hosted on Railway.
- Auth and database: Supabase. The backend only validates Supabase-issued JWTs. Clients never query the Supabase database directly; all data goes through the backend.
- Mobile: .NET MAUI, Android only, distributed as a sideloaded APK. No iOS, no store publishing.
- A web client is planned later; `src/Joint.Contracts` holds the DTOs shared between backend and clients.
- Providers are read-only: Fio (token API), Trading 212 (read-only API key), Air Bank (via Enable Banking, not yet confirmed to work; fallback is statement import).

## Commands

```bash
dotnet build Joint.slnx
dotnet test tests/Joint.Api.Tests
dotnet run --project src/Joint.Api
dotnet build src/Joint.Mobile
dotnet tool restore
dotnet ef migrations add <Name> --project src/Joint.Api --output-dir Data/Migrations
dotnet ef database update --project src/Joint.Api
```

The backend refuses to start without `ConnectionStrings:Database` and `Supabase:Url`
(user secrets locally, `ConnectionStrings__Database` and `Supabase__Url` as environment
variables). `dotnet ef` needs both set too; dummy values are enough for `migrations add`.

## Backend conventions

- `Features/<Feature>/` holds one file per use case: endpoint, handler and anything only that use case needs. Name files after the use case (`GetMe`, `ConnectFio`).
- An endpoint is a class implementing `IEndpoint` (`Common/IEndpoint.cs`) and is registered with one `app.MapEndpoint<T>()` line in `Program.cs`. No MediatR, no assembly scanning.
- A feature that needs services exposes its own `AddX` extension (see `Features/Sync/SyncSetup.cs`). `Program.cs` stays composition only.
- Shared code lives outside slices only when several features use it: `Domain` (entities), `Data` (DbContext, migrations), `Connectors`, `Common`. Don't add service or repository layers; slices use `JointDbContext` directly.
- Request and response types that a client consumes go in `Joint.Contracts`; everything else stays in the slice.
- EF migrations are the source of truth for the schema (Supabase's declarative schemas and GitHub integration were considered and rejected). `.github/workflows/deploy-database.yml` applies them on pushes that touch `Data/Migrations`; the backend does not migrate at startup.
- Two environments: the `development` branch deploys to development, `main` to production. Work lands on `development` first. Each has its own Supabase project, Railway environment and GitHub environment (secret `DATABASE_CONNECTION_STRING`, variable `SUPABASE_URL`).
- App tables live in the `joint` schema, owned by EF migrations. Hangfire owns the `hangfire` schema. Don't touch Supabase's own schemas.
- Each provider is an `IProviderConnector` returning the normalized `ProviderSnapshot`. Adding a provider must not require changes to other slices.
- Provider credentials are stored encrypted and never returned to clients or logged.

## Things that bite

- The Postgres EF provider is on 10.x because the 11.x provider was still a release candidate when the project started. Check before upgrading; keep `Microsoft.EntityFrameworkCore.Design` on the matching version.
- `Newtonsoft.Json` is pinned in `Joint.Api.csproj` because Hangfire pulls in a vulnerable version. Don't remove the pin.
- Hangfire needs Supabase's direct connection or session pooler. The transaction pooler breaks it.
- Railway is configured in its dashboard (see README), not in the repo. Don't add `railway.json`/`railway.toml`: Railway stops reading them on 2026-12-01. Its replacement, `.railway/railway.ts`, was skipped as overkill for one service.
- Fio allows one request per token every 30 seconds (HTTP 409 otherwise). Respect this in sync and in any manual refresh.
- Trading 212's API is beta; its limits apply per account and can change.
- JWT validation assumes the Supabase project uses asymmetric signing keys (discovered through `Authority`). A legacy shared-secret project needs different setup in `Common/SupabaseAuth.cs`.
- The Android SDK and JDK (21) live in `%LOCALAPPDATA%\Android`. The SDK's command-line tools and emulator are not installed. Android builds fail with "Java SDK directory could not be found" when run inside a sandbox that can't read that folder.
- The mobile app is released by pushing a `v*` tag (`.github/workflows/release-mobile.yml`), which builds a signed APK into a GitHub release. The keystore exists only in repository secrets.

## Not verified yet

- The backend has never been run against a real database.
- The Dockerfile, the Railway deployment and the GitHub workflows (CI, Deploy database, Release mobile) are untested.
- The mobile app builds but has not been run on a device; it still shows the MAUI template page.
- `SyncJob` is a stub until the first connector exists.

Update this section as these get verified, and keep this file current when a decision above changes.
