namespace Joint.Contracts;

/// <summary>
/// The numbers behind the Insights charts, for the whole household. Amounts are in one
/// currency and never converted; transfers between the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the household's transactions are in; null when there are none.</param>
/// <param name="Months">Oldest first, ending with the current month.</param>
/// <param name="SpendingThisMonth">Running total of spending per day of the current month, up to today.</param>
/// <param name="SpendingLastMonth">The same for every day of the previous month.</param>
/// <param name="Members">Who spent what in the current month, biggest first.</param>
public record InsightsResponse(
    string? Currency,
    List<MonthFlowResponse> Months,
    List<decimal> SpendingThisMonth,
    List<decimal> SpendingLastMonth,
    List<MemberSpendingResponse> Members);
