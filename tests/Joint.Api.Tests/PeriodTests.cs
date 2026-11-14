using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Insights;
using Joint.Contracts;

namespace Joint.Api.Tests;

public class PeriodTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Ben = Guid.NewGuid();
    private static readonly Guid AnaAccount = Guid.NewGuid();
    private static readonly Guid BenAccount = Guid.NewGuid();

    // Paid on the 15th: one pay period runs to the 14th of the next month.
    private static readonly DatePeriod PayPeriod = new(D("2026-10-15"), D("2026-11-14"));

    [Fact]
    public void Payday_to_payday_steps_by_months_and_any_other_period_by_its_length()
    {
        Assert.Equal(1, PayPeriod.Months);
        Assert.Equal(new DatePeriod(D("2026-09-15"), D("2026-10-14")), PayPeriod.Shift(-1));
        Assert.Equal(new DatePeriod(D("2026-11-15"), D("2026-12-14")), PayPeriod.Shift(1));

        var month = DatePeriod.MonthOf(Today);
        Assert.True(month.IsCalendarMonth);
        Assert.Equal(new DatePeriod(D("2026-10-01"), D("2026-10-31")), month.Shift(-1));
        Assert.Equal(new DatePeriod(D("2026-02-01"), D("2026-02-28")), DatePeriod.MonthOf(D("2026-03-09")).Shift(-1));

        var quarter = new DatePeriod(D("2026-08-15"), D("2026-11-14"));
        Assert.Equal(3, quarter.Months);
        Assert.Equal(new DatePeriod(D("2026-05-15"), D("2026-08-14")), quarter.Shift(-1));

        var tenDays = new DatePeriod(D("2026-11-01"), D("2026-11-10"));
        Assert.Null(tenDays.Months);
        Assert.Equal(new DatePeriod(D("2026-10-22"), D("2026-10-31")), tenDays.Shift(-1));
    }

    [Fact]
    public void A_pay_period_chosen_earlier_moves_on_to_the_one_holding_today()
    {
        var chosenInSummer = new DatePeriod(D("2026-07-15"), D("2026-08-14"));
        var holiday = new DatePeriod(D("2026-07-01"), D("2026-07-10"));

        Assert.Equal(PayPeriod, chosenInSummer.RolledTo(Today));
        Assert.Equal(PayPeriod, PayPeriod.RolledTo(Today));
        Assert.Equal(holiday, holiday.RolledTo(Today));
    }

    [Fact]
    public void Insights_cut_every_chart_by_the_chosen_period()
    {
        var insights = InsightsCalculator.Build(
            [
                Row(AnaAccount, Ana, "2026-09-15", 40000),
                Row(AnaAccount, Ana, "2026-10-14", -700),
                Row(AnaAccount, Ana, "2026-10-15", 41000),
                Row(AnaAccount, Ana, "2026-10-16", -1000),
                Row(BenAccount, Ben, "2026-11-02", -300),
                Row(AnaAccount, Ana, "2026-11-13", -200),
            ],
            Today, months: 3, PayPeriod);

        Assert.Equal((PayPeriod.From, PayPeriod.To), (insights.From!.Value, insights.To!.Value));
        Assert.Equal(
            [(D("2026-08-15"), D("2026-09-14")), (D("2026-09-15"), D("2026-10-14")), (D("2026-10-15"), D("2026-11-14"))],
            insights.Months.Select(m => (m.Month, m.End!.Value)));
        Assert.Equal((40000m, 700m), (insights.Months[1].Income, insights.Months[1].Spending));
        Assert.Equal((41000m, 1500m), (insights.Months[2].Income, insights.Months[2].Spending));

        // Day 1 is the 15th; today, the 13th of November, is day 30. The period before has 30 days.
        Assert.Equal(30, insights.SpendingThisMonth.Count);
        Assert.Equal(1000m, insights.SpendingThisMonth[1]);
        Assert.Equal(1500m, insights.SpendingThisMonth[^1]);
        Assert.Equal(30, insights.SpendingLastMonth.Count);
        Assert.Equal(700m, insights.SpendingLastMonth[^1]);

        Assert.Equal([1200m, 300m], insights.Members.Select(m => m.Spending));
    }

    [Fact]
    public void A_past_period_is_shown_in_full()
    {
        var past = new DatePeriod(D("2026-10-01"), D("2026-10-10"));

        var insights = InsightsCalculator.Build([Row(AnaAccount, Ana, "2026-10-09", -50)], Today, months: 2, past);

        Assert.Equal(10, insights.SpendingThisMonth.Count);
        Assert.Equal(50m, insights.SpendingThisMonth[^1]);
        Assert.Equal(D("2026-09-21"), insights.Months[0].Month);
    }

    [Fact]
    public void Categories_count_only_the_chosen_period()
    {
        var breakdown = CategoryCalculator.Build(
            [
                CategoryRow("2026-10-14", -100, "Albert"),
                CategoryRow("2026-10-15", -250, "Albert"),
                CategoryRow("2026-11-14", -50, "Lidl"),
                CategoryRow("2026-11-15", -999, "Lidl"),
            ],
            [Rule("Groceries", "ALBERT"), Rule("Groceries", "LIDL")],
            PayPeriod);

        Assert.Equal((PayPeriod.From, PayPeriod.To), (breakdown.Month, breakdown.To!.Value));
        Assert.Equal(300m, breakdown.Spending.Single().Amount);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-10-15", "2026-11-14", null)]
    [InlineData("2026-11-13", "2026-12-14", null)]
    [InlineData("2026-10-15", null, "Give both from and to, or neither.")]
    [InlineData("2026-11-14", "2026-11-01", "The period must not end before it starts.")]
    [InlineData("2026-11-14", "2026-12-14", "The period must not start in the future.")]
    [InlineData("2025-01-01", "2026-11-13", "A period can be at most 366 days long.")]
    public void A_period_is_both_dates_or_neither_and_starts_by_today(string? from, string? to, string? expected)
    {
        var period = PeriodQuery.Read(
            from is null ? null : D(from), to is null ? null : D(to), Today, out var error);

        Assert.Equal(expected, error);
        Assert.Equal(expected is null && from is not null, period is not null);
    }

    private static DateOnly D(string date) => DateOnly.Parse(date);

    private static FlowRow Row(Guid account, Guid owner, string bookedOn, decimal amount) =>
        new(account, owner, owner == Ana ? "Ana" : "Ben", null, D(bookedOn), amount, "CZK");

    private static CategoryRow CategoryRow(string bookedOn, decimal amount, string counterparty) =>
        new(AnaAccount, D(bookedOn), amount, "CZK", counterparty, null);

    private static CategoryRule Rule(string category, string matchKey) =>
        new() { Id = Guid.NewGuid(), Category = category, MatchKey = matchKey };
}
