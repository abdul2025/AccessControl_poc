using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Features.Scps.Ports;

public interface IScpRepository
{
    Task<PagedResponse<ScpListItemDto>> ListAsync(ScpListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<ScpDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked controller with its SIO panels for a write in the same operation.</summary>
    Task<Scp?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the whole controller aggregate without tracking, to compare the draft with a published snapshot.</summary>
    Task<Scp?> GetAggregateForReadAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the controller (with its new published version number) and the immutable version row in one transaction;
    /// fails with a concurrency error when the controller changed since the caller read it.
    /// </summary>
    Task<Result> PublishAsync(Scp scp, ScpConfigVersion version, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <returns>The compiled commands of the given version (latest when <c>null</c>), or <c>null</c> when it does not exist.</returns>
    Task<ScpCommandListDto?> GetCommandsAsync(Guid scpId, int? version, CancellationToken cancellationToken);

    Task<IReadOnlyList<SiteOptionDto>> ListSiteOptionsAsync(CancellationToken cancellationToken);

    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken);

    Task<bool> ScpNumberExistsAsync(short scpNumber, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> SerialNumberExistsAsync(int serialNumber, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> NameExistsInSiteAsync(Guid siteId, string name, Guid? excludeId, CancellationToken cancellationToken);

    /// <returns>The lowest free SCP number, or <c>null</c> when every number in the range is used.</returns>
    Task<short?> GetNextFreeScpNumberAsync(CancellationToken cancellationToken);

    Task<Result> AddAsync(Scp scp, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when the record changed since it was read.</summary>
    Task<Result> UpdateAsync(Scp scp, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the controller (its SIO rows are kept); fails with a concurrency error when it changed.</summary>
    Task<Result> DeleteAsync(Scp scp, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
