using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;

namespace eTicketing.Identity.Business.Organizations;

public class OrganizationSnapshotPublisher : IOrganizationSnapshotPublisher
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationSnapshotPublisher(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task PublishAsync(Organization organization, CancellationToken ct = default)
    {
        var superAdminEmail = await _userRepository.GetEmailByOrganizationAndRoleAsync(
            organization.Id, RoleType.OrganizationSuperAdmin, ct);

        await PublishAsync(organization, superAdminEmail, ct);
    }

    public Task PublishAsync(Organization organization, string? superAdminEmail, CancellationToken ct = default) =>
        _eventPublisher.PublishAsync(
            EventNames.OrganizationSnapshotChanged,
            new OrganizationSnapshotChanged(
                organization.Id,
                organization.Name,
                organization.Address,
                organization.Email,
                organization.PhoneNumber,
                superAdminEmail,
                organization.IsActive,
                DateTime.UtcNow),
            ct);

    public Task PublishDeletedAsync(Guid organizationId, CancellationToken ct = default) =>
        _eventPublisher.PublishAsync(
            EventNames.OrganizationDeleted,
            new OrganizationDeleted(organizationId, DateTime.UtcNow),
            ct);

    public async Task<int> RepublishAllAsync(CancellationToken ct = default)
    {
        var rows = await _organizationRepository.GetSnapshotRowsAsync(ct);
        if (rows.Count == 0)
            return 0;

        // One timestamp for the whole sweep. Per-row clock reads would differ by microseconds for
        // no benefit, and a single instant makes a republish trivially identifiable in the outbox.
        var publishedAt = DateTime.UtcNow;

        foreach (var row in rows)
        {
            await _eventPublisher.PublishAsync(
                EventNames.OrganizationSnapshotChanged,
                new OrganizationSnapshotChanged(
                    row.Id, row.Name, row.Address, row.Email, row.PhoneNumber,
                    row.SuperAdminEmail, row.IsActive, publishedAt),
                ct);
        }

        // Unlike every other method here this one has no caller transaction to join, so it commits
        // its own outbox rows — without this the sweep would add rows nothing ever persists and
        // report a number of events it did not actually publish.
        await _unitOfWork.SaveChangesAsync(ct);

        return rows.Count;
    }
}
