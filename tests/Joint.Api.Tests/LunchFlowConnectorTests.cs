using System.Net;
using System.Text;
using Joint.Api.Connectors;
using Joint.Api.Connectors.LunchFlow;

namespace Joint.Api.Tests;

public class LunchFlowConnectorTests
{
    private const string Accounts = """
        {"accounts":[
          {"id":7,"connection_id":1,"name":"Běžný účet","institution_name":"Air Bank","provider":"gocardless","currency":"CZK","status":"ACTIVE"},
          {"id":8,"connection_id":1,"name":"Old","institution_name":"Air Bank","currency":"CZK","status":"DISCONNECTED"}
        ],"total":2}
        """;

    private const string Balance = """{"balance":{"amount":15230.55,"currency":"CZK"}}""";

    private const string Transactions = """
        {"transactions":[
          {"id":"t1","accountId":7,"amount":-249.9,"currency":"CZK","date":"2026-11-10","merchant":"Albert","description":"Card payment","isPending":false},
          {"id":"t2","accountId":7,"amount":42000,"currency":"CZK","date":"2026-11-09","merchant":"","description":"Salary","isPending":false},
          {"id":"t3","accountId":7,"amount":-99,"currency":"CZK","date":"2026-11-11","merchant":"Bolt","description":null,"isPending":true}
        ],"total":3}
        """;

    [Fact]
    public async Task Fetch_maps_active_accounts_with_balance_and_booked_transactions()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/accounts" => Json(Accounts),
            "/api/v1/accounts/7/balance" => Json(Balance),
            "/api/v1/accounts/7/transactions" => Json(Transactions),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var snapshot = await Connector(handler).FetchAsync("key", null, CancellationToken.None);

        var account = Assert.Single(snapshot.Accounts);
        Assert.Equal("7", account.ExternalId);
        Assert.Equal("Běžný účet", account.Name);
        Assert.Equal("Air Bank", account.Institution);
        Assert.Equal("CZK", account.Currency);
        Assert.Equal(15230.55m, account.Balance);

        Assert.Equal(["t1", "t2"], account.Transactions.Select(t => t.ExternalId));
        var card = account.Transactions[0];
        Assert.Equal(-249.9m, card.Amount);
        Assert.Equal(new DateOnly(2026, 11, 10), card.BookedOn);
        Assert.Equal("Albert", card.Counterparty);
        Assert.Null(account.Transactions[1].Counterparty);
    }

    [Fact]
    public async Task Fetch_sends_the_key_and_the_since_date()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/accounts" => Json(Accounts),
            "/api/v1/accounts/7/balance" => Json(Balance),
            _ => Json("""{"transactions":[],"total":0}"""),
        });

        await Connector(handler).FetchAsync("my-key", new DateOnly(2026, 10, 1), CancellationToken.None);

        Assert.All(handler.Requests, request => Assert.Equal("my-key", request.ApiKey));
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/api/v1/accounts/7/transactions?from=2026-10-01");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_rejected_key_fails_validation_and_fetch(HttpStatusCode status)
    {
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(status)));

        Assert.False(await connector.ValidateCredentialAsync("bad", CancellationToken.None));
        await Assert.ThrowsAsync<ProviderAuthenticationException>(
            () => connector.FetchAsync("bad", null, CancellationToken.None));
    }

    [Fact]
    public async Task An_accepted_key_passes_validation()
    {
        var connector = Connector(new StubHandler(_ => Json(Accounts)));

        Assert.True(await connector.ValidateCredentialAsync("good", CancellationToken.None));
    }

    [Fact]
    public async Task A_server_error_is_not_mistaken_for_a_bad_key()
    {
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => connector.ValidateCredentialAsync("key", CancellationToken.None));
    }

    private static LunchFlowConnector Connector(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(LunchFlowConnector.BaseUrl) });

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string PathAndQuery, string? ApiKey)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("x-api-key", out var keys);
            Requests.Add((request.RequestUri!.PathAndQuery, keys?.FirstOrDefault()));
            return Task.FromResult(respond(request));
        }
    }
}
