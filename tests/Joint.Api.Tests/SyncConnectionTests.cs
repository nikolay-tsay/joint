using System.Security.Claims;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Connections;
using Joint.Api.Features.Sync;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class SyncConnectionTests
{
    [Fact]
    public async Task A_household_member_can_start_a_sync_of_a_connection()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var connection = await ConnectAsync(db, ana);
        var jobs = new RecordingJobClient();

        var result = await SyncConnection.HandleAsync(
            connection.Id, ana, new CurrentUserService(db), jobs, db, CancellationToken.None);

        Assert.IsType<Accepted>(result.Result);
        var job = Assert.Single(jobs.Jobs);
        Assert.Equal(typeof(ConnectionSyncer), job.Type);
        Assert.Equal(connection.Id, job.Args[0]);
    }

    [Fact]
    public async Task Someone_outside_the_household_cannot_start_a_sync()
    {
        await using var db = NewDb();
        var connection = await ConnectAsync(db, Principal("ana@example.com"));
        var jobs = new RecordingJobClient();

        var result = await SyncConnection.HandleAsync(
            connection.Id, Principal("ben@example.com"), new CurrentUserService(db), jobs, db, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
        Assert.Empty(jobs.Jobs);
    }

    private static async Task<BankConnection> ConnectAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var user = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Provider = ProviderKind.LunchFlow,
            EncryptedCredential = "x",
        };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection;
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));

    /// <summary>Stands in for Hangfire and remembers what was queued.</summary>
    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = [];

        public string Create(Job job, IState state)
        {
            Jobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}
