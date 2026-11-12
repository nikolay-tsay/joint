using Joint.Contracts;

namespace Joint.Mobile.Features.Home;

/// <summary>One account row on the home page, already formatted for display.</summary>
public record AccountItem(string Title, string Subtitle, string Balance)
{
    public static AccountItem From(AccountResponse account) =>
        new(
            account.Name,
            string.IsNullOrEmpty(account.Institution)
                ? account.OwnerDisplayName
                : $"{account.Institution} · {account.OwnerDisplayName}",
            $"{account.Balance:N2} {account.Currency}");
}
