using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Home;

/// <summary>One account row on the home page, already formatted for display.</summary>
public record AccountItem(Guid Id, string Title, string Subtitle, string Balance)
{
    public static AccountItem From(AccountResponse account) =>
        new(
            account.Id,
            account.Name,
            string.IsNullOrEmpty(account.Institution)
                ? account.OwnerDisplayName
                : $"{account.Institution} · {account.OwnerDisplayName}",
            Money.Format(account.Balance, account.Currency));
}
