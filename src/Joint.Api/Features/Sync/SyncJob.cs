using Joint.Api.Data;
using Joint.Api.Connectors;
using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Sync;

/// <summary>
/// Recurring Hangfire job that walks every active connection. Fetching and storing
/// provider data is added together with the first connector; for now it only reports
/// which connections have no connector yet.
/// </summary>
public class SyncJob(JointDbContext db, ConnectorRegistry connectors, ILogger<SyncJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var connections = await db.BankConnections
            .Where(c => c.Status == ConnectionStatus.Active || c.Status == ConnectionStatus.Failing)
            .ToListAsync(cancellationToken);

        foreach (var connection in connections)
        {
            if (!connectors.TryGet(connection.Provider, out _))
            {
                connection.Status = ConnectionStatus.Failing;
                connection.LastError = $"No connector is available for {connection.Provider}.";
                logger.LogWarning("Connection {ConnectionId} skipped: {Error}", connection.Id, connection.LastError);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sync run finished for {Count} connections.", connections.Count);
    }
}
