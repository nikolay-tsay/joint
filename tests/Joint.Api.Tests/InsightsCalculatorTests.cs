using Joint.Api.Features.Insights;

namespace Joint.Api.Tests;

public class InsightsCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Ben = Guid.NewGuid();
    private static readonly Guid AnaAccount = Guid.NewGuid();
    private static readonly Guid AnaSavings = Guid.NewGuid();
    private static readonly Guid BenAccount = Guid.NewGuid();

    [Fact]
    public void Months_carry_income_and_spending_oldest_first()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-10-05", 40000),
                Row(AnaAccount, Ana, "2026-10-09", -1200),
                Row(BenAccount, Ben, "2026-10-20", -300),
                Row(AnaAccount, Ana, "2026-11-02", -800),
            ],
            Today, months: 3);

        Assert.Equal("CZK", insights.Currency);
        Assert.Equal(
            [new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)],
            insights.Months.Select(m => m.Month));
        Assert.Equal((0m, 0m), (insights.Months[0].Income, insights.Months[0].Spending));
        Assert.Equal((40000m, 1500m), (insights.Months[1].Income, insights.Months[1].Spending));
        Assert.Equal((0m, 800m), (insights.Months[2].Income, insights.Months[2].Spending));
    }

    [Fact]
    public void Money_moved_between_own_accounts_is_neither_income_nor_spending()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-11-03", -5000),
                Row(AnaSavings, Ana, "2026-11-04", 5000),
                Row(AnaAccount, Ana, "2026-11-05", -250),
            ],
            Today, months: 2);

        Assert.Equal((0m, 250m), (insights.Months[1].Income, insights.Months[1].Spending));
    }

    [Fact]
    public void Matching_amounts_on_one_account_or_far_apart_are_not_a_transfer()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-11-03", -500),
                Row(AnaAccount, Ana, "2026-11-03", 500),
                Row(AnaAccount, Ana, "2026-11-01", -900),
                Row(BenAccount, Ben, "2026-11-10", 900),
            ],
            Today, months: 2);

        Assert.Equal((1400m, 1400m), (insights.Months[1].Income, insights.Months[1].Spending));
    }

    [Fact]
    public void Running_spending_covers_this_month_to_today_and_all_of_last_month()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-10-01", -100),
                Row(AnaAccount, Ana, "2026-10-31", -50),
                Row(AnaAccount, Ana, "2026-11-02", -200),
                Row(AnaAccount, Ana, "2026-11-02", -30),
                Row(AnaAccount, Ana, "2026-11-13", -70),
            ],
            Today, months: 2);

        Assert.Equal(13, insights.SpendingThisMonth.Count);
        Assert.Equal([0m, 230m, 230m], insights.SpendingThisMonth.Take(3));
        Assert.Equal(300m, insights.SpendingThisMonth[^1]);

        Assert.Equal(31, insights.SpendingLastMonth.Count);
        Assert.Equal(100m, insights.SpendingLastMonth[0]);
        Assert.Equal(150m, insights.SpendingLastMonth[^1]);
    }

    [Fact]
    public void Members_are_ranked_by_what_they_spent_this_month()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-11-02", -200),
                Row(BenAccount, Ben, "2026-11-03", -900),
                Row(BenAccount, Ben, "2026-10-03", -5000),
                Row(AnaAccount, Ana, "2026-11-04", 30000),
            ],
            Today, months: 2);

        Assert.Equal([("Ben", 900m), ("Ana", 200m)], insights.Members.Select(m => (m.DisplayName, m.Spending)));
    }

    [Fact]
    public void Only_the_most_used_currency_is_counted()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-11-02", -200),
                Row(AnaAccount, Ana, "2026-11-03", -300),
                Row(BenAccount, Ben, "2026-11-03", -40, "EUR"),
            ],
            Today, months: 2);

        Assert.Equal("CZK", insights.Currency);
        Assert.Equal(500m, insights.Months[1].Spending);
    }

    [Fact]
    public void A_household_without_transactions_gets_empty_months()
    {
        var insights = InsightsCalculator.Build([], Today, months: 6);

        Assert.Null(insights.Currency);
        Assert.Equal(6, insights.Months.Count);
        Assert.All(insights.Months, m => Assert.Equal((0m, 0m), (m.Income, m.Spending)));
        Assert.Empty(insights.Members);
    }

    private static FlowRow Row(Guid account, Guid owner, string bookedOn, decimal amount, string currency = "CZK") =>
        new(account, owner, owner == Ana ? "Ana" : "Ben", null, DateOnly.Parse(bookedOn), amount, currency);
}
