using Joint.Contracts;

namespace Joint.Mobile.Features.Profile;

/// <summary>One provider connection on the profile page, already formatted for display.</summary>
/// <param name="IsMine">Only the owner can remove a connection.</param>
/// <param name="HasProblem">The last sync failed; the status line says why.</param>
public record ConnectionItem(Guid Id, string Title, string Status, bool IsMine, bool HasProblem)
{
    public static ConnectionItem From(ConnectionResponse connection)
    {
        var provider = connection.Provider == "LunchFlow" ? "Lunch Flow" : connection.Provider;
        var status = connection.Status switch
        {
            "Active" when connection.LastSyncedAt is { } synced => $"Synced {synced.ToLocalTime():g}",
            "Pending" => "First sync in progress",
            "Failing" => connection.LastError ?? "The last sync failed",
            "ConsentExpired" => "Access has expired. Reconnect to continue.",
            var other => other,
        };

        return new ConnectionItem(
            connection.Id,
            $"{provider} · {connection.OwnerDisplayName}",
            status,
            connection.IsMine,
            connection.Status is "Failing" or "ConsentExpired");
    }
}
