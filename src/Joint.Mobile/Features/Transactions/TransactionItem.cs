using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

/// <summary>
/// One transaction row, already formatted for display. Tapping it opens it in place: the
/// texts that were cut to one line are shown in full, and a payment also offers to mark
/// its payee as a subscription or a regular payment.
/// </summary>
public partial class TransactionItem(
    Guid id, string title, string subtitle, string ownerInitial, string? ownerAvatarUrl, string amount, bool isIncome)
    : ObservableObject
{
    public Guid Id { get; } = id;

    public string Title { get; } = title;

    /// <summary>Who made it, then the bank's detail and the account where useful.</summary>
    public string Subtitle { get; } = subtitle;

    /// <summary>First letter of the person who made it, for the avatar.</summary>
    public string OwnerInitial { get; } = ownerInitial;

    /// <summary>Their profile picture; the initial shows when there is none.</summary>
    public string? OwnerAvatarUrl { get; } = ownerAvatarUrl;

    public string Amount { get; } = amount;

    public bool IsIncome { get; } = isIncome;

    /// <summary>Only money going out can be a subscription or a regular payment.</summary>
    public bool CanTrack => !IsIncome;

    /// <summary>The tracked payment this one counts towards, if any.</summary>
    public Guid? RecurringId { get; private set; }

    /// <summary>Subscription or Bill when the payee is tracked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecurringLabel), nameof(IsTracked), nameof(IsSubscription), nameof(IsBill))]
    public partial string? RecurringKind { get; set; }

    /// <summary>
    /// The text a payment must contain to be counted with this one. It starts as the
    /// backend's suggestion, or what is already tracked, and the user may edit it in the open row.
    /// </summary>
    [ObservableProperty]
    public partial string? MatchText { get; set; }

    /// <summary>True when this payment was taken out of the budget by hand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BudgetChoice))]
    public partial bool IsExcludedFromBudget { get; set; }

    /// <summary>What the chip in the open row offers: excluding, or putting back.</summary>
    public string BudgetChoice => IsExcludedFromBudget ? "Count in budget again" : "Exclude from budget";

    /// <summary>True while the row is open: full texts, and the choices for a payment.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTrackMenuOpen))]
    public partial bool IsMenuOpen { get; set; }

    /// <summary>Whether the open row offers the tracking choices.</summary>
    public bool IsTrackMenuOpen => IsMenuOpen && CanTrack;

    public string? RecurringLabel => RecurringKind is { } kind ? RecurringKinds.Label(kind) : null;

    public bool IsTracked => RecurringKind is not null;

    public bool IsSubscription => RecurringKind == RecurringKinds.Subscription;

    public bool IsBill => RecurringKind == RecurringKinds.Bill;

    /// <summary>Takes over what the backend now says about this row's payee.</summary>
    public void SetRecurring(Guid? recurringId, string? kind, string? matchText)
    {
        RecurringId = recurringId;
        RecurringKind = kind;
        MatchText = matchText;
    }

    /// <param name="showAccount">Name the account when the list mixes several.</param>
    public static TransactionItem From(TransactionResponse transaction, bool showAccount)
    {
        // Lead with who the money went to or came from; fall back to the bank's description.
        var title = transaction.Counterparty ?? transaction.Description ?? "Transaction";
        var detail = transaction.Counterparty is null ? null : transaction.Description;

        string?[] parts = [transaction.OwnerDisplayName, detail, showAccount ? transaction.AccountName : null];
        var owner = transaction.OwnerDisplayName;

        var item = new TransactionItem(
            transaction.Id,
            title,
            string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part))),
            owner.Length > 0 ? owner[..1].ToUpperInvariant() : "?",
            transaction.OwnerAvatarUrl,
            Money.FormatSigned(transaction.Amount, transaction.Currency),
            transaction.Amount > 0);
        item.SetRecurring(transaction.RecurringId, transaction.RecurringKind, transaction.RecurringMatch);
        item.IsExcludedFromBudget = transaction.ExcludedFromBudget;
        return item;
    }
}
