using System.Collections.ObjectModel;

namespace Joint.Mobile.Features.Transactions;

/// <summary>The transactions booked on one day, under a heading for that day.</summary>
public class TransactionGroup(DateOnly date) : ObservableCollection<TransactionItem>
{
    public DateOnly Date { get; } = date;

    public string Title { get; } = Describe(date);

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
