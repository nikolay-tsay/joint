namespace Joint.Contracts;

/// <summary>
/// The numbers behind the Insights charts, for the whole household, over a chosen period (the
/// current calendar month unless another is asked for). Amounts are in one currency and never
/// converted; transfers between the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the household's transactions are in; null when there are none.</param>
/// <param name="Months">Periods like the chosen one, oldest first, ending with it.</param>
/// <param name="SpendingThisMonth">Running total of spending per day of the chosen period, up to today.</param>
/// <param name="SpendingLastMonth">The same for every day of the period before it.</param>
/// <param name="Members">Who spent what in the chosen period, biggest first.</param>
/// <param name="From">The chosen period's first day.</param>
/// <param name="To">The chosen period's last day.</param>
public record InsightsResponse(
    string? Currency,
    List<MonthFlowResponse> Months,
    List<decimal> SpendingThisMonth,
    List<decimal> SpendingLastMonth,
    List<MemberSpendingResponse> Members,
    DateOnly? From = null,
    DateOnly? To = null);
