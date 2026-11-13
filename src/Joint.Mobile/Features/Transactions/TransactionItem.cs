using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

/// <summary>
/// One transaction row, already formatted for display. A payment can be tapped open to
/// mark its payee as a subscription or a regular payment, in place.
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

    /// <summary>True while the row shows its choices under the transaction.</summary>
    [ObservableProperty]
    public partial bool IsMenuOpen { get; set; }

    public string? RecurringLabel => RecurringKind is { } kind ? RecurringKinds.Label(kind) : null;

    public bool IsTracked => RecurringKind is not null;

    public bool IsSubscription => RecurringKind == RecurringKinds.Subscription;

    public bool IsBill => RecurringKind == RecurringKinds.Bill;

    /// <summary>Takes over what the backend now says about this row's payee.</summary>
    public void SetRecurring(Guid? recurringId, string? kind)
    {
        RecurringId = recurringId;
        RecurringKind = kind;
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
        item.SetRecurring(transaction.RecurringId, transaction.RecurringKind);
        return item;
    }
}
