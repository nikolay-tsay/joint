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
- Providers are read-only. Lunch Flow (an aggregator, built) carries Air Bank and can carry other banks; each user pastes the API key of their own Lunch Flow "API destination". Fio (token API) and Trading 212 (read-only API key) are planned as direct connectors. Enable Banking was rejected: it does not serve Czech residents on its own licence.

## Users, households and sharing

- Sign-up and sign-in happen in Supabase Auth, driven by the client. The backend never sees passwords; it validates the access token and keeps its own `User` row whose id is the token's `sub`.
- There is no sign-up endpoint. `CurrentUserService.GetOrCreateAsync` (`Common/Auth`) creates the `User` and a household of one on a person's first authenticated request; clients call `GET /api/me` right after sign-in. Slices that need the caller go through this service.
- Every user belongs to exactly one `Household` (`User.HouseholdId`). A "joint account" is a household with more than one user, and all members see the same data. Scope every data query by the caller's `HouseholdId`, never by user id alone.
- Joining works by invitation (`HouseholdInvitation`, code stored only as a hash): accepting moves the user into the inviter's household (`Features/Households`). Codes are single-use and expire after 48 hours. Leaving puts the user back in a new household of one. A household left empty is deleted; `HouseholdCleanup` handles that for both paths.
- `BankConnection.OwnerUserId` ties a connection to a person, not a household, so connections follow their owner if they change household.
- `Account.Visibility` exists for per-account privacy later; nothing enforces it yet.

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

Connecting a provider and syncing also need `Credentials:EncryptionKey` (32 random bytes,
base64); without it those requests fail, but the backend still starts.

The backend refuses to start without `ConnectionStrings:Database` and `Supabase:Url`
(user secrets locally, `ConnectionStrings__Database` and `Supabase__Url` as environment
variables). `dotnet ef` needs both set too; dummy values are enough for `migrations add`.

## Backend conventions

- `Features/<Feature>/` holds one file per use case: endpoint, handler and anything only that use case needs. Name files after the use case (`GetMe`, `ConnectFio`).
- An endpoint is a class implementing `IEndpoint` (`Common/IEndpoint.cs`) and is registered with one `app.MapEndpoint<T>()` line in `Program.cs`. No MediatR, no assembly scanning.
- Scalar UI is served at `/scalar` in every environment except Production (`Common/ApiDocs.cs`). New endpoints show up there automatically; give them a name and typed results so the document is useful.
- A feature that needs services exposes its own `AddX` extension (see `Features/Sync/SyncSetup.cs`). `Program.cs` stays composition only.
- Shared code lives outside slices only when several features use it: `Domain` (entities), `Data` (DbContext, migrations), `Connectors`, `Common`. Don't add service or repository layers; slices use `JointDbContext` directly.
- One type per file, including entities and enums in `Domain`. Never group several entities in one file.
- EF mapping goes in `Data/Configurations/<Entity>Configuration.cs` (`IEntityTypeConfiguration<T>`), picked up automatically. Don't configure entities inside `JointDbContext`; it only declares the `DbSet`s and the schema.
- Request and response types that a client consumes go in `Joint.Contracts`; everything else stays in the slice.
- EF migrations are the source of truth for the schema (Supabase's declarative schemas and GitHub integration were considered and rejected). `.github/workflows/deploy-database.yml` applies them on pushes that touch `Data/Migrations`; the backend does not migrate at startup.
- Two environments: the `development` branch deploys to development, `main` to production. Work lands on `development` first. Each has its own Supabase project, Railway environment and GitHub environment (secret `DATABASE_CONNECTION_STRING`, variable `SUPABASE_URL`).
- App tables live in the `joint` schema, owned by EF migrations. Hangfire owns the `hangfire` schema. Don't touch Supabase's own schemas.
- Each provider is an `IProviderConnector` (`Connectors/<Provider>/`) returning the normalized `ProviderSnapshot`, registered in `SyncSetup`. Adding a provider must not require changes to other slices; `ConnectionSyncer` stores whatever a connector returns.
- Sync rules (`Features/Sync/ConnectionSyncer.cs`): accounts match on the provider's id, transactions are deduplicated on `ExternalId` and only booked ones are stored, holdings are replaced. A failed sync saves nothing and records a user-safe message in `BankConnection.LastError`. Connecting enqueues an immediate sync; the recurring job runs every six hours.
- Read household data through `Data/HouseholdQueries.cs` (`ConnectionsOf`, `AccountsOf`); that is the single place deciding what a household sees.
- Provider credentials are encrypted with `CredentialProtector` (AES-GCM, key from `Credentials:EncryptionKey`) before storage and never returned to clients or logged. Changing that key makes every stored credential unreadable.

## Data retention

The database must stay small (Supabase bills by size). Before adding a table or column, decide when its rows die.

- Store only what a screen shows. Never store raw provider responses, request logs or audit trails in the database.
- Transient rows get a purge. Invitations are deleted daily once expired or accepted (`PurgeInvitationsJob`); follow that pattern for anything else short-lived.
- Holdings are current state: replace them on each sync, don't append.
- Transactions are the only table that grows without bound, and are kept in full because history is the point of the app. If a history table is ever added (balances, portfolio value), keep at most one row per account per day.
- A feature that needs a recurring job registers and schedules it itself (`AddX` / `ScheduleX` next to the job); Hangfire setup lives in `Common/BackgroundJobs.cs`. Hangfire prunes its own finished-job records after a day; don't extend that.

## Mobile conventions

- Organized by feature like the backend: `Features/<Feature>/` holds the page, its view model and feature-only services. MVVM with CommunityToolkit.Mvvm; pages and view models are registered in `MauiProgram.cs` and resolved by Shell.
- Three tabs, declared in `AppShell.xaml`: Home (total balance and accounts), Transactions (whole household, by day), Profile (identity, household, connections, sign out). Household and connection management belongs in Profile, never on Home. Tapping an account on Home pushes `TransactionsPage` filtered to it.
- Page view models derive from `Common/PageViewModel` (busy flag, error line, shared handling of lost sessions and network failures). Rows shown in lists are small pre-formatted records (`AccountItem`, `TransactionItem`, `ConnectionItem`) built from the contract types; amounts go through `Common/Money`.
- Look and feel: colors live in `Resources/Styles/Colors.xaml` (light/dark pairs, used with `AppThemeBinding`), app styles in `Resources/Styles/Theme.xaml` (`Card`, `HeroCard`, `SectionTitle`, `RowTitle`, `Caption`, `PrimaryButton`, `SecondaryButton`, `DangerButton`). Use those instead of inline colors and sizes. Tab icons are hand-written SVGs in `Resources/Images`.
- Sign-in is Google through Supabase Auth using the browser and PKCE (`Features/Auth/AuthService.cs`), talking to Supabase's HTTP API directly; there is no Supabase SDK dependency. The session lives in SecureStorage.
- All backend calls go through `Api/ApiClient.cs`, which attaches a fresh access token. A `SignedOutException` means send the user to the login page.
- Environment values (Supabase URL, publishable key, backend URL) are constants in `Config/AppConfig.cs`, switched by build configuration. They are public values; never put a secret there.
- The sign-in redirect is `joint://auth-callback`; changing it means updating `AppConfig`, `AuthCallbackActivity` and Supabase's Redirect URLs together.

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

Verified on 2026-11-12 in the development environment: Google sign-in on a phone, the
backend on Railway against Supabase, the Deploy database workflow, and user creation
through `/api/me`.

- The production environment has not been deployed or signed in to.
- Token refresh after the access token expires, and session restore after an app restart, have not been observed.
- Household invite, join and leave are unit-tested but have not been run against the real database or with two real accounts.
- The Release mobile workflow is untested (no keystore secrets yet).
- The Lunch Flow connector is written from its API reference and tested only against canned responses. Unconfirmed against the real service: that transaction ids are stable between fetches, that negative amounts mean money out, whether long histories are paginated, and its rate limits.
- Lunch Flow holdings (brokerage accounts) are not read yet.
- The redesigned app (tabs, theme, Transactions and Profile screens) compiles but was written without seeing it run; layout, dark mode and the tab icons need a look on a device.

Update this section as these get verified, and keep this file current when a decision above changes.
