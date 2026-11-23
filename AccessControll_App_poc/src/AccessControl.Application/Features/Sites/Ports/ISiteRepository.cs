using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites.DTOs;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Features.Sites.Ports;

public interface ISiteRepository
{
    Task<PagedResponse<SiteListItemDto>> ListAsync(SiteListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<SiteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked entity for a write in the same operation.</summary>
    Task<Site?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>True when the site still has controllers that are not deleted.</summary>
    Task<bool> HasScpsAsync(Guid id, CancellationToken cancellationToken);

    Task<Result> AddAsync(Site site, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when the record changed since it was read.</summary>
    Task<Result> UpdateAsync(Site site, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes; fails with a concurrency error when the record changed since it was read.</summary>
    Task<Result> DeleteAsync(Site site, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
