using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

/// <summary>One transaction row, already formatted for display.</summary>
public record TransactionItem(string Title, string? Subtitle, string Amount, bool IsIncome)
{
    /// <param name="showAccount">Name the account when the list mixes several.</param>
    public static TransactionItem From(TransactionResponse transaction, bool showAccount)
    {
        // Lead with who the money went to or came from; fall back to the bank's description.
        var title = transaction.Counterparty ?? transaction.Description ?? "Transaction";
        var detail = transaction.Counterparty is null ? null : transaction.Description;
        var subtitle = (detail, showAccount) switch
        {
            (null, true) => transaction.AccountName,
            (not null, true) => $"{detail} · {transaction.AccountName}",
            _ => detail,
        };

        return new TransactionItem(
            title,
            subtitle,
            Money.FormatSigned(transaction.Amount, transaction.Currency),
            transaction.Amount > 0);
    }
}
