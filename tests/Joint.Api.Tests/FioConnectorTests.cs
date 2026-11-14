using System.Net;
using System.Text;
using Joint.Api.Connectors;
using Joint.Api.Connectors.Fio;
using Joint.Api.Domain;

namespace Joint.Api.Tests;

public class FioConnectorTests
{
    // Three movements: an incoming transfer with a named sender and a text date, a card
    // payment with a numeric date, and one without an id that must be skipped.
    private const string Statement = """
        {"accountStatement":{"info":{"accountId":"2400222222","bankId":"2010","currency":"CZK",
          "iban":"CZ7920100000002400222222","bic":"FIOBCZPPXXX","openingBalance":195.00,"closingBalance":1250.75,
          "dateStart":"2026-11-01+0100","dateEnd":"2026-11-13+0100","idFrom":1,"idTo":3,"idLastDownload":null},
         "transactionList":{"transaction":[
          {"column22":{"value":26000000001,"name":"ID pohybu","id":22},
           "column0":{"value":"2026-11-10+0100","name":"Datum","id":0},
           "column1":{"value":42000.00,"name":"Objem","id":1},
           "column14":{"value":"CZK","name":"Měna","id":14},
           "column2":{"value":"2900233333","name":"Protiúčet","id":2},
           "column10":{"value":"Novák, Pavel","name":"Název protiúčtu","id":10},
           "column3":{"value":"2010","name":"Kód banky","id":3},
           "column16":{"value":"Salary","name":"Zpráva pro příjemce","id":16},
           "column8":{"value":"Příjem převodem uvnitř banky","name":"Typ","id":8}},
          {"column22":{"value":26000000002,"name":"ID pohybu","id":22},
           "column0":{"value":1340661600000,"name":"Datum","id":0},
           "column1":{"value":-249.90,"name":"Objem","id":1},
           "column14":{"value":"CZK","name":"Měna","id":14},
           "column2":null,"column10":null,"column3":null,"column16":null,
           "column7":{"value":"Nákup: ALBERT 0123, Praha","name":"Uživatelská identifikace","id":7},
           "column25":{"value":" ","name":"Komentář","id":25},
           "column8":{"value":"Platba kartou","name":"Typ","id":8}},
          {"column22":null,
           "column0":{"value":"2026-11-11+0100","name":"Datum","id":0},
           "column1":{"value":-1.00,"name":"Objem","id":1}}
         ]}}}
        """;

    private const string EmptyStatement = """
        {"accountStatement":{"info":{"accountId":"2400222222","bankId":"2010","currency":"CZK",
          "iban":"CZ7920100000002400222222","closingBalance":10.00},"transactionList":{"transaction":[]}}}
        """;

    [Fact]
    public async Task Fetch_maps_the_account_its_balance_and_its_movements()
    {
        var snapshot = await Connector(new StubHandler(_ => Json(Statement)))
            .FetchAsync("token", null, CancellationToken.None);

        var account = Assert.Single(snapshot.Accounts);
        Assert.Equal("CZ7920100000002400222222", account.ExternalId);
        Assert.Equal("2400222222/2010", account.Name);
        Assert.Equal("Fio banka", account.Institution);
        Assert.Equal(AccountKind.Current, account.Kind);
        Assert.Equal("CZK", account.Currency);
        Assert.Equal(1250.75m, account.Balance);

        Assert.Equal(["26000000001", "26000000002"], account.Transactions.Select(t => t.ExternalId));

        var salary = account.Transactions[0];
        Assert.Equal(new DateOnly(2026, 11, 10), salary.BookedOn);
        Assert.Equal(42000m, salary.Amount);
        Assert.Equal("Novák, Pavel", salary.Counterparty);
        Assert.Equal("Salary", salary.Description);

        var card = account.Transactions[1];
        Assert.Equal(new DateOnly(2012, 6, 26), card.BookedOn);
        Assert.Equal(-249.90m, card.Amount);
        Assert.Null(card.Counterparty);
        Assert.Equal("Nákup: ALBERT 0123, Praha", card.Description);
    }

    [Fact]
    public async Task Fetch_asks_for_the_period_since_the_last_sync_in_one_request()
    {
        var handler = new StubHandler(_ => Json(EmptyStatement));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddDays(-10);

        await Connector(handler).FetchAsync("my-token", since, CancellationToken.None);

        var path = Assert.Single(handler.Paths);
        Assert.StartsWith($"/v1/rest/periods/my-token/{since:yyyy-MM-dd}/", path);
        Assert.EndsWith("/transactions.json", path);
    }

    [Fact]
    public async Task Fetch_never_reaches_back_past_what_Fio_serves_without_unlocking()
    {
        var handler = new StubHandler(_ => Json(EmptyStatement));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Connector(handler).FetchAsync("t", null, CancellationToken.None);
        await Connector(handler).FetchAsync("t", today.AddDays(-400), CancellationToken.None);

        Assert.All(handler.Paths, path =>
        {
            var from = DateOnly.ParseExact(path.Split('/')[5], "yyyy-MM-dd");
            Assert.InRange(today.DayNumber - from.DayNumber, 88, 90);
        });
    }

    [Fact]
    public async Task A_request_made_too_soon_is_waited_out_and_repeated()
    {
        var calls = 0;
        var waits = new List<TimeSpan>();
        var connector = Connector(
            new StubHandler(_ => ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.Conflict) : Json(EmptyStatement)),
            waits);

        var snapshot = await connector.FetchAsync("token", null, CancellationToken.None);

        Assert.Single(snapshot.Accounts);
        Assert.Equal(2, calls);
        Assert.True(Assert.Single(waits) >= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Waiting_for_the_rate_limit_gives_up_eventually()
    {
        var waits = new List<TimeSpan>();
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)), waits);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => connector.FetchAsync("token", null, CancellationToken.None));
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public async Task An_unknown_token_fails_validation_and_fetch()
    {
        // Fio reports a missing or expired token as 500.
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.False(await connector.ValidateCredentialAsync("bad", CancellationToken.None));
        await Assert.ThrowsAsync<ProviderAuthenticationException>(
            () => connector.FetchAsync("bad", null, CancellationToken.None));
    }

    [Fact]
    public async Task An_accepted_token_passes_validation_with_a_request_for_today_only()
    {
        var handler = new StubHandler(_ => Json(EmptyStatement));

        Assert.True(await Connector(handler).ValidateCredentialAsync("good", CancellationToken.None));

        var parts = Assert.Single(handler.Paths).Split('/');
        Assert.Equal(parts[5], parts[6]);
    }

    [Fact]
    public async Task The_check_names_the_account_a_token_is_for_as_the_sync_will()
    {
        var connector = Connector(new StubHandler(_ => Json(EmptyStatement)));

        var check = await connector.CheckCredentialAsync("good", CancellationToken.None);
        var synced = (await connector.FetchAsync("good", null, CancellationToken.None)).Accounts.Single();

        Assert.True(check.Accepted);
        Assert.Equal("CZ7920100000002400222222", check.AccountKey);
        Assert.Equal(synced.ExternalId, check.AccountKey);
    }

    [Fact]
    public async Task An_outage_is_not_mistaken_for_a_bad_token_and_does_not_leak_it()
    {
        var connector = Connector(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => connector.ValidateCredentialAsync("secret-token", CancellationToken.None));
        Assert.DoesNotContain("secret-token", exception.ToString());
    }

    private static FioConnector Connector(HttpMessageHandler handler, List<TimeSpan>? waits = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(FioConnector.BaseUrl) })
        {
            Delay = (wait, _) =>
            {
                waits?.Add(wait);
                return Task.CompletedTask;
            },
        };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(respond(request));
        }
    }
}
