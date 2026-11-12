using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

/// <summary>One transaction row, already formatted for display.</summary>
/// <param name="OwnerInitial">First letter of the person who made it, for the avatar.</param>
/// <param name="Subtitle">Who made it, then the bank's detail and the account where useful.</param>
/// <param name="OwnerAvatarUrl">Their profile picture; the initial shows when there is none.</param>
public record TransactionItem(
    string Title, string Subtitle, string OwnerInitial, string? OwnerAvatarUrl, string Amount, bool IsIncome)
{
    /// <param name="showAccount">Name the account when the list mixes several.</param>
    public static TransactionItem From(TransactionResponse transaction, bool showAccount)
    {
        // Lead with who the money went to or came from; fall back to the bank's description.
        var title = transaction.Counterparty ?? transaction.Description ?? "Transaction";
        var detail = transaction.Counterparty is null ? null : transaction.Description;

        string?[] parts = [transaction.OwnerDisplayName, detail, showAccount ? transaction.AccountName : null];
        var owner = transaction.OwnerDisplayName;

        return new TransactionItem(
            title,
            string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part))),
            owner.Length > 0 ? owner[..1].ToUpperInvariant() : "?",
            transaction.OwnerAvatarUrl,
            Money.FormatSigned(transaction.Amount, transaction.Currency),
            transaction.Amount > 0);
    }
}
