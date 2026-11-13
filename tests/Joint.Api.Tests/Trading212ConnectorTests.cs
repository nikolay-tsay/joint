using System.Net;
using System.Text;
using Joint.Api.Connectors;
using Joint.Api.Connectors.Trading212;
using Joint.Api.Domain;

namespace Joint.Api.Tests;

public class Trading212ConnectorTests
{
    private const string Summary = """
        {"cash":{"availableToTrade":120.5,"inPies":0,"reservedForOrders":0},
         "currency":"EUR","id":4455667,
         "investments":{"currentValue":2880,"realizedProfitLoss":12,"totalCost":2600,"unrealizedProfitLoss":280},
         "totalValue":3000.5}
        """;

    private const string Positions = """
        [{"averagePricePaid":150.25,"createdAt":"2025-03-01T10:00:00Z","currentPrice":182.4,
          "instrument":{"currency":"USD","isin":"US0378331005","name":"Apple","ticker":"AAPL_US_EQ"},
          "quantity":3.5,"quantityAvailableForTrading":3.5,"quantityInPies":0,
          "walletImpact":{"currency":"EUR","currentValue":590.1,"fxImpact":-4.2,"totalCost":500,"unrealizedProfitLoss":90.1}},
         {"averagePricePaid":80,"currentPrice":76,
          "instrument":{"currency":"EUR","isin":"IE00B4L5Y983","name":"iShares Core MSCI World","ticker":"IWDA_EQ"},
          "quantity":10,
          "walletImpact":{"currency":"EUR","currentValue":760,"fxImpact":null,"totalCost":800,"unrealizedProfitLoss":-40}}]
        """;

    private static readonly string Credential = Trading212Connector.PackCredential(" my-key ", "my-secret");

    [Fact]
    public async Task The_account_is_split_into_investments_main_pot_and_spending_pot()
    {
        // Total 3500 = 2880 invested + (120.5 + 30 + 9.5) investable cash + 460 left over.
        const string summary = """
            {"cash":{"availableToTrade":120.5,"inPies":30,"reservedForOrders":9.5},
             "currency":"EUR","id":4455667,
             "investments":{"currentValue":2880},"totalValue":3500}
            """;
        var handler = new StubHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("summary") ? Json(summary) : Json(Positions));

        var snapshot = await Connector(handler).FetchAsync(Credential, null, CancellationToken.None);

        Assert.Equal(["Investments", "Main pot", "Spending pot"], snapshot.Accounts.Select(a => a.Name));
        Assert.Equal([2880m, 160m, 460m], snapshot.Accounts.Select(a => a.Balance));
        Assert.Equal(
            [AccountKind.Investment, AccountKind.Savings, AccountKind.Current],
            snapshot.Accounts.Select(a => a.Kind));
        Assert.Equal(["4455667", "4455667:cash", "4455667:spending"], snapshot.Accounts.Select(a => a.ExternalId));
        Assert.All(snapshot.Accounts, a => Assert.Equal("Trading 212", a.Institution));

        // Positions belong to the investments part only.
        Assert.Equal(2, snapshot.Accounts[0].Holdings.Count);
        Assert.Empty(snapshot.Accounts[1].Holdings);
        Assert.Empty(snapshot.Accounts[2].Holdings);
    }

    [Fact]
    public async Task No_spending_pot_is_reported_when_the_total_is_fully_accounted_for()
    {
        var handler = new StubHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("summary") ? Json(Summary) : Json("[]"));

        var snapshot = await Connector(handler).FetchAsync(Credential, null, CancellationToken.None);

        // 3000.5 total = 2880 invested + 120.5 cash, nothing left over.
        Assert.Equal(["Investments", "Main pot"], snapshot.Accounts.Select(a => a.Name));
        Assert.Equal([2880m, 120.5m], snapshot.Accounts.Select(a => a.Balance));
    }

    [Fact]
    public async Task Fetch_returns_the_investments_with_their_holdings()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v0/equity/account/summary" => Json(Summary),
            "/api/v0/equity/positions" => Json(Positions),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var snapshot = await Connector(handler).FetchAsync(Credential, null, CancellationToken.None);

        var account = snapshot.Accounts[0];
        Assert.Equal("4455667", account.ExternalId);
        Assert.Equal("Trading 212", account.Institution);
        Assert.Equal(AccountKind.Investment, account.Kind);
        Assert.Equal("EUR", account.Currency);
        Assert.Equal(2880m, account.Balance);
        Assert.Empty(account.Transactions);

        Assert.Equal(2, account.Holdings.Count);
        var apple = account.Holdings[0];
        Assert.Equal("AAPL_US_EQ", apple.Ticker);
        Assert.Equal("Apple", apple.Name);
        Assert.Equal(3.5m, apple.Quantity);
        Assert.Equal(150.25m, apple.AveragePrice);
        Assert.Equal(182.4m, apple.CurrentPrice);
        Assert.Equal("USD", apple.Currency);
        Assert.Equal(590.1m, apple.Value);
        Assert.Equal(90.1m, apple.UnrealizedProfitLoss);
        Assert.Equal(-40m, account.Holdings[1].UnrealizedProfitLoss);
    }

    [Fact]
    public async Task Requests_carry_the_key_and_secret_as_basic_auth()
    {
        var handler = new StubHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("summary") ? Json(Summary) : Json("[]"));

        await Connector(handler).FetchAsync(Credential, null, CancellationToken.None);

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("my-key:my-secret"));
        Assert.All(handler.Authorizations, header => Assert.Equal(expected, header));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_bad_key_or_missing_permission_fails_validation_and_fetch(HttpStatusCode status)
    {
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(status)));

        Assert.False(await connector.ValidateCredentialAsync(Credential, CancellationToken.None));
        await Assert.ThrowsAsync<ProviderAuthenticationException>(
            () => connector.FetchAsync(Credential, null, CancellationToken.None));
    }

    [Fact]
    public async Task Validation_checks_both_permissions()
    {
        // The account call works, but the key was created without the Portfolio permission.
        var connector = Connector(new StubHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("summary")
                ? Json(Summary)
                : new HttpResponseMessage(HttpStatusCode.Forbidden)));

        Assert.False(await connector.ValidateCredentialAsync(Credential, CancellationToken.None));
    }

    [Fact]
    public async Task A_rate_limited_call_waits_and_tries_again()
    {
        var calls = 0;
        var waits = new List<TimeSpan>();
        var handler = new StubHandler(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("summary"))
            {
                return Json("[]");
            }

            calls++;
            if (calls > 1)
            {
                return Json(Summary);
            }

            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddSeconds(3).ToUnixTimeSeconds().ToString());
            return limited;
        });
        var connector = new Trading212Connector(Client(handler))
        {
            Delay = (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            },
        };

        var snapshot = await connector.FetchAsync(Credential, null, CancellationToken.None);

        Assert.NotEmpty(snapshot.Accounts);
        var wait = Assert.Single(waits);
        Assert.InRange(wait, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task A_limit_that_never_clears_is_reported_as_unreachable_not_as_a_bad_key()
    {
        var connector = new Trading212Connector(
            Client(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))))
        {
            Delay = (_, _) => Task.CompletedTask,
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => connector.ValidateCredentialAsync(Credential, CancellationToken.None));
    }

    private static Trading212Connector Connector(HttpMessageHandler handler) =>
        new(Client(handler)) { Delay = (_, _) => Task.CompletedTask };

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri(Trading212Connector.BaseUrl) };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(respond(request));
        }
    }
}
