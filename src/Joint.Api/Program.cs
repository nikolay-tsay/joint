using Joint.Api.Common;
using Joint.Api.Data;
using Joint.Api.Features.Accounts;
using Joint.Api.Features.Connections;
using Joint.Api.Features.Health;
using Joint.Api.Features.Households;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Me;
using Joint.Api.Features.Sync;
using Joint.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Railway tells the container which port to listen on through PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

builder.Services.AddDbContext<JointDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSupabaseAuth(builder.Configuration);
builder.Services.AddBackgroundJobs(connectionString);
builder.Services.AddSync(builder.Configuration);
builder.Services.AddHouseholds();
builder.Services.AddApiDocs();

var app = builder.Build();

app.MapApiDocs();

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoint<GetHealth>();
app.MapEndpoint<GetMe>();
app.MapEndpoint<CreateInvitation>();
app.MapEndpoint<AcceptInvitation>();
app.MapEndpoint<LeaveHousehold>();
app.MapEndpoint<ConnectLunchFlow>();
app.MapEndpoint<ConnectTrading212>();
app.MapEndpoint<ConnectFio>();
app.MapEndpoint<ListConnections>();
app.MapEndpoint<DeleteConnection>();
if (!app.Environment.IsProduction())
{
    // Sync on demand is a development aid; see SyncConnection.
    app.MapEndpoint<SyncConnection>();
}

app.MapEndpoint<ListAccounts>();
app.MapEndpoint<UpdateAccount>();
app.MapEndpoint<ListAccountHoldings>();
app.MapEndpoint<ListTransactions>();
app.MapEndpoint<GetInsights>();

app.ScheduleSync();
app.ScheduleInvitationPurge();

app.Run();
