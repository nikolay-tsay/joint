using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Contracts;

namespace Joint.Mobile.Features.Profile;

/// <summary>One provider connection on the profile page, already formatted for display.</summary>
public partial class ConnectionItem(Guid id, string title, string status, bool isMine, bool hasProblem)
    : ObservableObject
{
    public Guid Id { get; } = id;

    public string Title { get; } = title;

    public string Status { get; } = status;

    /// <summary>The last sync failed; the status line says why.</summary>
    public bool HasProblem { get; } = hasProblem;

    /// <summary>True while the row is asking "really remove?" in place.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartRemove))]
    public partial bool IsConfirmingRemove { get; set; }

    /// <summary>Whether to offer "Sync now", a development aid that exists only in Debug builds.</summary>
#if DEBUG
    public bool CanSyncNow => true;
#else
    public bool CanSyncNow => false;
#endif

    /// <summary>Only the owner can remove a connection, and not while already being asked.</summary>
    public bool CanStartRemove => isMine && !IsConfirmingRemove;

    public static ConnectionItem From(ConnectionResponse connection)
    {
        var provider = ProviderOption.DisplayName(connection.Provider);
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
