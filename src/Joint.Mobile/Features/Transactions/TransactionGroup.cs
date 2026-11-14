using System.Collections.ObjectModel;

namespace Joint.Mobile.Features.Transactions;

/// <summary>
/// The transactions booked on one day, under a heading for that day; or, with
/// <paramref name="isPending"/>, those the bank has not booked yet, under "Pending".
/// </summary>
public class TransactionGroup(DateOnly date, bool isPending = false) : ObservableCollection<TransactionItem>
{
    public DateOnly Date { get; } = date;

    public bool IsPending { get; } = isPending;

    public string Title { get; } = isPending ? "Pending" : Describe(date);

    private static string Describe(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date == today)
        {
            return "Today";
        }

        if (date == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return date.Year == today.Year ? date.ToString("dddd, d MMMM") : date.ToString("d MMMM yyyy");
    }
}
