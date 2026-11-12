# joint

A read-only overview of a household's finances across separate bank and investment
accounts, for couples and families who don't want a joint account.

## Layout

| Path | What it is |
|---|---|
| `src/Joint.Api` | ASP.NET Core backend: REST API, EF Core data access, Hangfire sync jobs |
| `src/Joint.Contracts` | DTOs shared by the backend and the clients |
| `src/Joint.Mobile` | .NET MAUI app, Android only |
| `tests/Joint.Api.Tests` | Backend tests |

Everything targets .NET 11 with C# 15 and runtime async, set once in `Directory.Build.props`.

The backend is a single project organized as vertical slices:

| Folder | What goes there |
|---|---|
| `Features/<Feature>` | One file per use case, holding its endpoint, handler and anything only it needs |
| `Domain` | Entities shared by several slices |
| `Data` | The EF Core context and migrations |
| `Connectors` | The provider connector interface and its implementations |
| `Common` | Cross-cutting plumbing such as auth and the endpoint interface |

A new endpoint is a class implementing `IEndpoint` in its feature folder, plus one
`app.MapEndpoint<T>()` line in `Program.cs`.

## Backend

The backend needs two settings. Keep them out of the repo with user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "<Supabase Postgres connection string>" --project src/Joint.Api
```

```bash
dotnet user-secrets set "Supabase:Url" "https://<project-ref>.supabase.co" --project src/Joint.Api
```

Use Supabase's direct connection or session pooler, not the transaction pooler: Hangfire
needs session-level features.

Apply the database schema, then run:

```bash
dotnet tool restore
```

```bash
dotnet ef database update --project src/Joint.Api
```

```bash
dotnet run --project src/Joint.Api
```

Outside production the API serves a Scalar UI at `/scalar` for trying endpoints by hand
(OpenAPI document at `/openapi/v1.json`). For endpoints that need sign-in, paste a Supabase
access token into its Bearer authentication field.

App tables live in the `joint` schema; Hangfire creates its own `hangfire` schema on first start.
Sign-in is handled by Supabase Auth; the API validates the tokens it issues.

### Deploying to Railway

One Railway project with two environments, `production` and `development`, each running one
service built from this repo. Railway's `railway.json` is being retired, so the service is
configured in the dashboard. Set these per environment:

| Setting | Value |
|---|---|
| Variable `RAILWAY_DOCKERFILE_PATH` | `src/Joint.Api/Dockerfile` (root directory stays the repo root) |
| Variable `ConnectionStrings__Database` | That environment's Supabase session pooler string, Npgsql format |
| Variable `Supabase__Url` | `https://<project-ref>.supabase.co` |
| Variable `Credentials__EncryptionKey` | 32 random bytes, base64 (`openssl rand -base64 32`). Different per environment; never change it once connections exist |
| Variable `ASPNETCORE_ENVIRONMENT` | `Development` in the development environment only, to expose `/scalar` there |
| Watch paths | `src/Joint.Api/**`, `src/Joint.Contracts/**`, `Directory.Build.props`, `global.json` |
| Healthcheck path | `/health` |
| Serverless / app sleeping | Off, or scheduled sync stops running |

The backend listens on the `PORT` Railway provides. Railway deploys on every push to the
branch the environment tracks; database migrations are deployed separately (below).

### Connecting bank accounts

Bank data comes through [Lunch Flow](https://www.lunchflow.app/). Each person:

1. Creates a Lunch Flow account and connects their bank there.
2. Adds a destination of type "REST API", switches on the accounts to share under
   "Account Access", and copies the API key.
3. Pastes the key into the app under "Connect Lunch Flow".

The key is stored encrypted with `Credentials:EncryptionKey`. Locally, set it with:

```bash
dotnet user-secrets set "Credentials:EncryptionKey" "<output of: openssl rand -base64 32>" --project src/Joint.Api
```

Without `openssl`, PowerShell can generate the key:

```powershell
$b = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

Accounts sync right after connecting and then every six hours; Lunch Flow itself refreshes
about once a day.

### Deploying migrations

There are two environments, each with its own Supabase project and Railway environment:
the `development` branch deploys to development and `main` deploys to production.

The `Deploy database` workflow runs `dotnet ef database update`. A push that changes
`src/Joint.Api/Data/Migrations` migrates the database of the branch's environment. A manual
run migrates the one environment you pick.

Create two GitHub environments named `development` and `production`, and give each:

- Secret `DATABASE_CONNECTION_STRING`: that project's Supabase session pooler connection
  string in Npgsql format. GitHub runners have no IPv6, so the direct connection won't work.
- Variable `SUPABASE_URL`: `https://<project-ref>.supabase.co`.

To approve production migrations by hand, add a required reviewer to the `production`
environment in GitHub.

## Mobile

Building needs the `maui-android` workload plus the Android SDK and a JDK:

```bash
dotnet workload install maui-android
```

```bash
dotnet build src/Joint.Mobile
```

### Sign-in setup

The app signs in with Google through Supabase Auth. Per Supabase project:

1. In Google Cloud, create an OAuth client of type "Web application" and add
   `https://<project-ref>.supabase.co/auth/v1/callback` as an authorized redirect URI.
2. In Supabase, enable the Google provider under Authentication and paste the client id
   and secret.
3. In Supabase's URL configuration, add `joint://auth-callback` to Redirect URLs.
4. Fill in `src/Joint.Mobile/Config/AppConfig.cs`: the Supabase URL, the publishable key
   and the backend URL. Debug builds use the development values, Release builds production.

### Releasing the APK

Pushing a tag such as `v0.1.0` runs the `Release mobile` workflow, which builds a signed APK
and attaches it to a GitHub release as `joint-0.1.0.apk`. The tag sets the version shown in
the app.

One-time setup: create a release keystore and keep it and its passwords somewhere safe.
Updates only install over the existing app when signed with the same key.

```bash
keytool -genkeypair -v -keystore joint-release.keystore -alias joint -keyalg RSA -keysize 2048 -validity 10000
```

Then add four repository secrets: `ANDROID_KEYSTORE_BASE64` (the keystore file,
base64-encoded), `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` and `ANDROID_KEY_PASSWORD`.
Never commit the keystore.

## Tests

```bash
dotnet test tests/Joint.Api.Tests
```
