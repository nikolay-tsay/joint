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
- Providers are read-only. Lunch Flow (an aggregator, built) carries Air Bank and can carry other banks; each user pastes the API key of their own Lunch Flow "API destination". Trading 212 (built) is a direct connector taking an API key and secret with the Account data and Portfolio permissions; it yields up to three accounts and no transactions: Investments (with holdings), Main pot (investable cash) and Spending pot. The API has no spending-pot field, so that one is the account total minus the other two and appears only when something is left over. Fio (built) is a direct connector taking the read-only token of one account; one request returns its balance and movements. Enable Banking was rejected: it does not serve Czech residents on its own licence.

## Users, households and sharing

- Sign-up and sign-in happen in Supabase Auth, driven by the client. The backend never sees passwords; it validates the access token and keeps its own `User` row whose id is the token's `sub`.
- There is no sign-up endpoint. `CurrentUserService.GetOrCreateAsync` (`Common/Auth`) creates the `User` and a household of one on a person's first authenticated request; clients call `GET /api/me` right after sign-in. Slices that need the caller go through this service.
- `User.AvatarUrl` is the Google profile picture link from the token's `user_metadata`, refreshed when a token carries a different one. Only the link is stored; clients load the image from Google. Avatars in the app are an initial with the picture laid over it, so a missing picture needs no special case.
- Every user belongs to exactly one `Household` (`User.HouseholdId`). A "joint account" is a household with more than one user, and all members see the same data. Scope every data query by the caller's `HouseholdId`, never by user id alone.
- Joining works by invitation (`HouseholdInvitation`, code stored only as a hash): accepting moves the user into the inviter's household (`Features/Households`). Codes are single-use and expire after 48 hours. Leaving puts the user back in a new household of one. A household left empty is deleted; `HouseholdCleanup` handles that for both paths.
- `BankConnection.OwnerUserId` ties a connection to a person, not a household, so connections follow their owner if they change household.
- `Account.Visibility` exists for per-account privacy later; nothing enforces it yet.
- `Account.Kind` (Current, Savings, Investment) is the household's choice, set through `PATCH /api/accounts/{id}` by any member. Sync sets it only when an account is first created and must never overwrite it afterwards. The app shows these as Everyday, Savings and Investments tiles on Home.

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
- A "connect" endpoint only gathers its provider's credential and calls `ConnectionLinker.LinkAsync` (`Features/Connections`), which validates it with the connector, stores it encrypted and enqueues the first sync. A provider with a multi-part credential packs it into one string (see `Trading212Connector.PackCredential`).
- Holdings carry both the instrument's price currency and a value in the account's currency; totals and profit/loss always use the latter. `GET /api/accounts/{id}/holdings` serves them and the app lists them on the account's screen.
- `POST /api/connections/{id}/sync` starts a sync on demand. It is a development aid: mapped only outside Production (it has no throttle, and providers allow few reads a day), and the app's "Sync now" button exists only in Debug builds. Give it a rate limit before ever exposing it to production.
- `GET /api/insights` (`Features/Insights`) serves every chart on the Insights tab in one response; the arithmetic is in `InsightsCalculator`, a pure function over the period's transactions. It works in the household's most-used currency only, and drops internal transfers by guessing: the same amount leaving one account and arriving in another within two days. Money sent to Trading 212 has no matching arrival (that connector imports no transactions) and so counts as spending. Months are cut in UTC.
- Subscriptions and regular payments (`Features/Recurring`) are marked by hand, never detected. `POST /api/recurring` takes a transaction, a kind (`Subscription` or `Bill`, shown as "Regular payment") and an optional match text, and stores a `RecurringPayment`: the payee's name and the match text (`MatchKey`, upper case). Every outgoing transaction whose counterparty or description contains that text counts towards it, past and future; nothing is stored on transactions. Exact matching was the first design and failed on real data: Spotify puts a new payment id after its name each month. `PaymentKey` suggests the text (the counterparty, or the merchant part of the description, minus words that look like references: four or more characters with a digit) and `ListTransactions` sends that suggestion with every payment as `RecurringMatch`; the app shows it in an editable field so the user can correct it. The text must be at least three characters and appear in the payment being marked. `RecurringMatcher` picks the longest matching text when several fit. Matching runs in memory (`GetRecurring` over a year of outgoing transactions, `ListTransactions` over the page it returns). Marking a payment that already counts towards something edits that tracked payment. A `RecurringPayment` belongs to the user who marked it and is visible to their household, like a connection; marking or removing acts on the payee for the whole household.
- The budget (`Features/Budget`) is one amount per household per calendar month for everything it spends: `Household.MonthlyBudget`, null when unset, in the household's most-used currency (no currency is stored). It lives on the household, so it is lost when a person's household of one is deleted on joining another. `GET /api/budget` returns the month's figures whether or not a budget is set; `PUT /api/budget` sets or removes it. `BudgetCalculator` takes "spent" from `InsightsCalculator` so the Budget and Insights screens agree, and projects the month as fixed costs in full plus everything else at the pace so far. Fixed costs are the tracked subscriptions and regular payments: paid this month, plus any paid last month and not yet this month. Per-category budgets were left out because there are no categories.
- Read household data through `Data/HouseholdQueries.cs` (`ConnectionsOf`, `AccountsOf`, `RecurringOf`); that is the single place deciding what a household sees.
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
- Five tabs, declared in `AppShell.xaml` in the order the user chose: Budget (the monthly budget), Insights (charts), Home (total balance and accounts), Transactions (whole household, by day), Profile (identity, household, connections, sign out). Home is in the middle of the bar and is still where the app opens, because sign-in and launch navigate to `Routes.Home`, not to the first tab. Five is the most an Android tab bar shows; a sixth area has to live inside an existing tab. Household and connection management belongs in Profile, never on Home. Tapping an account on Home pushes `TransactionsPage` filtered to it.
- Page view models derive from `Common/PageViewModel` (refresh flag, error line, shared handling of lost sessions and network failures). A page's load goes through `ReloadAsync` and its RefreshView binds `IsRefreshing`; never bind a RefreshView to a general busy flag, because it runs its command whenever the flag turns on and loads twice. Fetch before clearing a collection.
- The Android status bar color is `colorPrimaryDark` in `Platforms/Android/Resources/values` and `values-night`, kept equal to the page background; `MainActivity` applies it and sets the icon contrast. Rows shown in lists are small pre-formatted records (`AccountItem`, `TransactionItem`, `ConnectionItem`) built from the contract types; amounts go through `Common/Money`.
- No system dialogs: never call `DisplayAlertAsync`, `DisplayPromptAsync` or `DisplayActionSheetAsync`. Input, confirmation and choices happen in place: a card swaps its buttons for a small panel (see the household card in `ProfilePage`), a row expands to confirm a destructive action, a choice is a row of chips or a dropdown that opens inside its card (see `TransactionsPage`). The system share sheet and the sign-in browser are the only exceptions.
- Profile's "Connect" button lists the available connectors (`Features/Profile/ProviderOption.cs`) and then opens the chosen one's own panel. A new connector adds an entry there, a `ConnectStep` value and a panel; the button itself never names a provider.
- A panel that opens a text field also manages the keyboard: `ProfilePage.xaml.cs` focuses the field when its panel opens and hides the keyboard when it closes.
- The splash is the gradient logo badge on the page background, because Android only allows one flat color behind it; `StartPage` repeats the same image so the hand-over from the system splash is seamless. `Resources/Splash/splash.svg` and `Resources/Images/logo.svg` must stay identical.
- Insights has two views, Overview and Recurring, inside the one Insights tab, switched by an underlined tab strip at the top of the page or by dragging sideways. The views are the two items of a `CarouselView` (`InsightsView` records, laid out by `InsightsViewTemplateSelector`), each with its own RefreshView and ScrollView and bound to the one view model through `Model.`. A `SwipeGestureRecognizer` on the scrolling content was tried first and barely registered on Android; don't go back to it. a new group of charts becomes another view there, not another tab in the tab bar. A row in Transactions opens in place when tapped: its title and detail, cut to one line when closed, wrap to show in full, and a payment also offers Subscription, Regular payment and Stop tracking; after a change the rows already on screen are re-read and updated where they stand.
- Insights (`Features/Insights`) draws its charts by hand: `BarChart` and `LineChart` derive from `Chart` (an `IDrawable`) and are shown in a `GraphicsView`, sharing colors, grid and axis format through `ChartStyle`. No charting package; add a chart by building another drawable in `InsightsViewModel`. Touching a chart picks the month or day under the finger (`Chart.SelectAt`, called from `InsightsPage.OnChartTouched`): the chart marks it and its `Selected` callback, set where the chart is built, writes that period's exact amounts into the caption under the chart. The pick stays until another is made or the page reloads.
- The template's `Resources/Styles/Styles.xaml` gives every `BoxView` a `BackgroundColor`, which shows as a square behind a rounded `Color` fill. A BoxView used as a dot, line or divider must set `BackgroundColor="Transparent"`. A chart reads theme colors when it is built, so it follows a light/dark switch only after the next reload.
- The Transactions tab filters by account type and opens on Everyday; opened from an account it shows that account with a dropdown for its type instead.
- Look and feel: colors live in `Resources/Styles/Colors.xaml` (light/dark pairs, used with `AppThemeBinding`), app styles in `Resources/Styles/Theme.xaml` (`Card`, `HeroCard`, `Field`, `SectionTitle`, `RowTitle`, `Caption`, `ErrorText`, `PrimaryButton`, `SecondaryButton`, `QuietButton`, `DangerButton`, `DangerOutlineButton`). Use those instead of inline colors and sizes. The brand is the purple-to-orange `BrandGradient` brush (hero card, primary buttons, profile avatar, app icon), with purple as the solid accent; teal was tried and rejected. Tab icons are hand-written SVGs in `Resources/Images`.
- The app lock (`Features/Lock`) is optional and local to the device: a four-digit PIN kept only as a salted PBKDF2 hash in SecureStorage, with fingerprint or face as the quicker way in. `LockScreen` shows `LockPage` modally over the Shell at launch (from `StartPage`) and every time the app returns from the background (from `App`); a one-minute grace period was tried and removed at the user's request. While the lock is on, the app's card in the recent-apps screen is blank (`IScreenPrivacy`, `Platforms/Android/AndroidScreenPrivacy.cs`): `setRecentsScreenshotEnabled(false)` on Android 13+, `FLAG_SECURE` (which also blocks screenshots) below. Five wrong PINs sign the user out; signing out for any reason removes the lock (`AuthService.Clear`), which is also the recovery for a forgotten PIN. It is turned on from the Security card in Profile; changing the PIN or turning the lock off asks for the current PIN, counted against the same five attempts. Biometrics use Android's platform `BiometricPrompt` (`Platforms/Android/AndroidBiometrics.cs`, Android 11+), not the AndroidX package; that prompt is a system dialog and an accepted exception to the no-dialogs rule. The lock is a gate on the UI, not encryption: the session token is protected by SecureStorage either way.
- Sign-in is Google through Supabase Auth using the browser and PKCE (`Features/Auth/AuthService.cs`), talking to Supabase's HTTP API directly; there is no Supabase SDK dependency. The session lives in SecureStorage.
- All backend calls go through `Api/ApiClient.cs`, which attaches a fresh access token. A `SignedOutException` means send the user to the login page.
- Environment values (Supabase URL, publishable key, backend URL) are constants in `Config/AppConfig.cs`, switched by build configuration. They are public values; never put a secret there.
- The sign-in redirect is `joint://auth-callback`; changing it means updating `AppConfig`, `AuthCallbackActivity` and Supabase's Redirect URLs together.

## Things that bite

- The Postgres EF provider is on 10.x because the 11.x provider was still a release candidate when the project started. Check before upgrading; keep `Microsoft.EntityFrameworkCore.Design` on the matching version.
- `Newtonsoft.Json` is pinned in `Joint.Api.csproj` because Hangfire pulls in a vulnerable version. Don't remove the pin.
- Hangfire needs Supabase's direct connection or session pooler. The transaction pooler breaks it.
- Railway is configured in its dashboard (see README), not in the repo. Don't add `railway.json`/`railway.toml`: Railway stops reading them on 2026-12-01. Its replacement, `.railway/railway.ts`, was skipped as overkill for one service.
- Fio allows one request per token every 30 seconds (HTTP 409 otherwise). `FioConnector` makes one request per sync and waits 31 seconds on a 409, at most twice; the first sync always hits this because it follows the connect-time check. Respect the limit in any manual refresh.
- Fio puts the token in the URL. Its HTTP client is registered with `RemoveAllLoggers()` and the connector never calls `EnsureSuccessStatusCode`, so the token cannot reach logs or exception text. Keep both.
- Fio answers 500, not 401, for an unknown or expired token, and 422 for anything older than 90 days unless the user has just unlocked history in internet banking (a ten-minute window). The connector treats 500 as a rejected token and never asks for more than 89 days.
- A user has one connection per provider (`ConnectionLinker`), and a Fio token is one account, so a person with two Fio accounts can connect only one for now.
- Trading 212's API is beta (v0); its limits apply per account and are tight (account summary: one call per five seconds, positions: one per second). `Trading212Connector` waits out a 429 using `x-ratelimit-reset` and retries, which matters because the first sync runs seconds after the connect-time validation. It only works for Invest and Stocks ISA accounts.
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
- The Fio connector is written from Fio's specification (version of 16 October 2025) and tested only against canned responses. Unconfirmed against the real service: the JSON date format (text and epoch are both handled), that `closingBalance` for a period ending today is the current balance, which columns carry the merchant on card payments, and the 30-second retry. "Today" is taken in UTC, so movements booked between midnight in Prague and midnight UTC arrive with the next sync.
- The Trading 212 connector is written from its OpenAPI spec and tested only against canned responses. Confirmed by the user on 2026-11-13: it connects, syncs, and `totalValue` is the whole account including both pots. Unconfirmed: that the three-way split matches the app's Investments, Main pot and Spending pot figures (the spending pot is derived, not reported), and how the rate-limit retry behaves. A spending pot that drops to exactly zero keeps its last balance, because an absent account is left untouched by sync. Its trades, dividends and deposits are not imported.
- The app lock compiles but has never run on a device. To check: the lock page appears at launch and on every return to the app (including after the share sheet, and that the biometric prompt itself does not re-trigger it), the recent-apps card is blank while the lock is on, the biometric prompt opens and unlocks, the PIN pad layout and the delete glyph, the five-attempt sign-out, and that pages underneath do not flash before the lock page covers them on resume.
- Recurring payments are unit-tested but have not run on a device or against real bank data. Confirmed by the user on 2026-11-13: marking works, and Spotify descriptions carry a changing id with digits. Unconfirmed: how good `PaymentKey`'s suggestion is at each bank (the Fio card-payment format is taken from its documentation), how the editable match field behaves inside the transaction list with the keyboard up, and that a transaction row grows and shrinks cleanly in the list when tapped.
- The Budget tab is unit-tested on the backend but has never run on a device: layout, the progress bar on the gradient card, the tab icon and the amount field need a look, and the projection has not been compared with a real month.
- The Insights tab compiles but has never run on a device: chart proportions, label fit, dark mode and the tab icon need a look, and the transfer guess has not been compared with real data.
- The redesigned app (tabs, theme, Transactions and Profile screens) compiles but was written without seeing it run; layout, dark mode and the tab icons need a look on a device.

Update this section as these get verified, and keep this file current when a decision above changes.
