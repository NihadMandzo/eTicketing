using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.ReadModels;

public class OrganizationSnapshotProjector : IOrganizationSnapshotProjector
{
    private readonly IOrganizationSnapshotRepository _snapshots;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrganizationSnapshotProjector> _logger;

    public OrganizationSnapshotProjector(
        IOrganizationSnapshotRepository snapshots,
        IUnitOfWork unitOfWork,
        ILogger<OrganizationSnapshotProjector> logger)
    {
        _snapshots = snapshots;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ApplyAsync(OrganizationSnapshotChanged message, CancellationToken ct = default)
    {
        var existing = await _snapshots.GetByIdAsync(message.OrganizationId, ct);

        if (existing is not null && existing.ChangedAt > message.ChangedAt)
        {
            // Identity republishes every organization on a timer, so an old republish overtaking a
            // fresh edit is not hypothetical here — it is the expected traffic pattern.
            _logger.LogInformation(
                "Zastarjeli snapshot za organizaciju {OrganizationId} — preskačem.", message.OrganizationId);
            return;
        }

        if (existing is null)
        {
            await _snapshots.AddAsync(ToEntity(message), ct);
        }
        else
        {
            CopyInto(message, existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid organizationId, CancellationToken ct = default)
    {
        var existing = await _snapshots.GetByIdAsync(organizationId, ct);
        if (existing is null)
            return;

        _snapshots.Remove(existing);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static OrganizationSnapshot ToEntity(OrganizationSnapshotChanged message)
    {
        var snapshot = new OrganizationSnapshot { OrganizationId = message.OrganizationId };
        CopyInto(message, snapshot);
        return snapshot;
    }

    private static void CopyInto(OrganizationSnapshotChanged message, OrganizationSnapshot snapshot)
    {
        snapshot.Name = message.Name;
        snapshot.Address = message.Address;
        snapshot.Email = message.Email;
        snapshot.PhoneNumber = message.PhoneNumber;
        snapshot.SuperAdminEmail = message.SuperAdminEmail;
        snapshot.IsActive = message.IsActive;
        snapshot.ChangedAt = message.ChangedAt;
    }
}
