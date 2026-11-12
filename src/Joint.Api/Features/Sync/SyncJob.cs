using Joint.Api.Data;
using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Sync;

/// <summary>Recurring job that syncs every connection, one after another.</summary>
public class SyncJob(JointDbContext db, ConnectionSyncer syncer, ILogger<SyncJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Failing connections are retried too: most failures are temporary.
        var connectionIds = await db.BankConnections
            .Where(c => c.Status != ConnectionStatus.ConsentExpired)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var connectionId in connectionIds)
        {
            await syncer.SyncAsync(connectionId, cancellationToken);
        }

        logger.LogInformation("Sync run finished for {Count} connections.", connectionIds.Count);
    }
}
