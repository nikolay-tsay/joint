using System.Security.Claims;
using System.Security.Cryptography;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Joint.Api.Common.Auth;
using Joint.Api.Common.Security;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Connections;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class ConnectionLinkerTests
{
    private static readonly CredentialProtector Protector = new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task Connecting_again_replaces_the_credential_when_it_covers_everything_at_the_provider()
    {
        await using var db = NewDb();
        var ana = await UserAsync(db, "ana@example.com");
        var jobs = new RecordingJobClient();
        var linker = new ConnectionLinker(db, Protector, jobs);
        var lunchFlow = new FakeConnector(ProviderKind.LunchFlow);

        await linker.LinkAsync(ana, lunchFlow, "first", "rejected", CancellationToken.None);
        await linker.LinkAsync(ana, lunchFlow, "second", "rejected", CancellationToken.None);

        var connection = Assert.Single(db.BankConnections);
        Assert.Equal("second", Protector.Unprotect(connection.EncryptedCredential));
        Assert.Null(connection.AccountKey);
        Assert.Equal(2, jobs.Jobs.Count);
    }

    [Fact]
    public async Task A_token_for_another_account_adds_a_connection_instead_of_replacing_the_first()
    {
        await using var db = NewDb();
        var ana = await UserAsync(db, "ana@example.com");
        var linker = new ConnectionLinker(db, Protector, new RecordingJobClient());
        var fio = new FakeConnector(ProviderKind.Fio) { AccountOf = token => token.Split(':')[0] };

        await linker.LinkAsync(ana, fio, "everyday:t1", "rejected", CancellationToken.None);
        await linker.LinkAsync(ana, fio, "savings:t1", "rejected", CancellationToken.None);

        Assert.Equal(["everyday", "savings"], db.BankConnections.Select(c => c.AccountKey!).Order().ToList());
    }

    [Fact]
    public async Task A_new_token_for_an_account_already_connected_replaces_its_token_only()
    {
        await using var db = NewDb();
        var ana = await UserAsync(db, "ana@example.com");
        var linker = new ConnectionLinker(db, Protector, new RecordingJobClient());
        var fio = new FakeConnector(ProviderKind.Fio) { AccountOf = token => token.Split(':')[0] };
        await linker.LinkAsync(ana, fio, "everyday:t1", "rejected", CancellationToken.None);
        await linker.LinkAsync(ana, fio, "savings:t1", "rejected", CancellationToken.None);

        var result = await linker.LinkAsync(ana, fio, "savings:t2", "rejected", CancellationToken.None);

        Assert.IsType<Ok<ConnectionResponse>>(result.Result);
        Assert.Equal(2, db.BankConnections.Count());
        Assert.Equal("everyday:t1", Protector.Unprotect(db.BankConnections.Single(c => c.AccountKey == "everyday").EncryptedCredential));
        Assert.Equal("savings:t2", Protector.Unprotect(db.BankConnections.Single(c => c.AccountKey == "savings").EncryptedCredential));
    }

    [Fact]
    public async Task A_connection_made_before_accounts_were_recorded_is_recognised_by_the_account_it_synced()
    {
        await using var db = NewDb();
        var ana = await UserAsync(db, "ana@example.com");
        var old = new BankConnection
        {
            Id = Guid.NewGuid(), OwnerUserId = ana.Id, Provider = ProviderKind.Fio, EncryptedCredential = Protector.Protect("old"),
        };
        db.BankConnections.Add(old);
        db.Accounts.Add(new Account { Id = Guid.NewGuid(), ConnectionId = old.Id, ExternalId = "everyday", Name = "1/2010", Currency = "CZK" });
        await db.SaveChangesAsync();
        var linker = new ConnectionLinker(db, Protector, new RecordingJobClient());
        var fio = new FakeConnector(ProviderKind.Fio) { AccountOf = token => token.Split(':')[0] };

        // The other account is new; the same account is the old connection getting a fresh token.
        await linker.LinkAsync(ana, fio, "savings:t1", "rejected", CancellationToken.None);
        await linker.LinkAsync(ana, fio, "everyday:t2", "rejected", CancellationToken.None);

        Assert.Equal(2, db.BankConnections.Count());
        var renewed = db.BankConnections.Single(c => c.Id == old.Id);
        Assert.Equal(("everyday", "everyday:t2"), (renewed.AccountKey, Protector.Unprotect(renewed.EncryptedCredential)));
    }

    [Fact]
    public async Task Each_connection_is_listed_with_the_accounts_it_brought()
    {
        await using var db = NewDb();
        var principal = Principal("ana@example.com");
        var ana = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var linker = new ConnectionLinker(db, Protector, new RecordingJobClient());
        var fio = new FakeConnector(ProviderKind.Fio) { AccountOf = token => token.Split(':')[0] };
        await linker.LinkAsync(ana, fio, "everyday:t1", "rejected", CancellationToken.None);
        await linker.LinkAsync(ana, fio, "savings:t1", "rejected", CancellationToken.None);
        var synced = db.BankConnections.Single(c => c.AccountKey == "savings");
        db.Accounts.Add(new Account { Id = Guid.NewGuid(), ConnectionId = synced.Id, ExternalId = "savings", Name = "2/2010", Currency = "CZK" });
        await db.SaveChangesAsync();

        var listed = await ListConnections.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);

        var rows = Assert.IsType<Ok<List<ConnectionResponse>>>(listed.Result).Value!;
        Assert.Equal(["2/2010"], rows.Single(r => r.Id == synced.Id).AccountNames);
        Assert.Empty(rows.Single(r => r.Id != synced.Id).AccountNames!);
    }

    private static async Task<User> UserAsync(JointDbContext db, string email) =>
        (await new CurrentUserService(db).GetOrCreateAsync(Principal(email), CancellationToken.None))!;

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
