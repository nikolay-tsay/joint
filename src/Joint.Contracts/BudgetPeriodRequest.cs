namespace Joint.Contracts;

/// <summary>
/// The days a budget covers. Both left out: calendar months. Otherwise the first and last day
/// of one period, which then repeats: by whole months when it runs from a day to the day before
/// that date a month or more later (payday to payday), otherwise by its own length.
/// </summary>
public record BudgetPeriodRequest(DateOnly? From = null, DateOnly? To = null);
