namespace EquipmentManagementBackend.Infrastructure;

public sealed class BackupExpiryWorker(IBackupsService backups) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        backups.RunExpiryAsync(stoppingToken);
}
