using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites.DTOs;
using AccessControl.Domain.Common.Results;

namespace AccessControl.Application.Features.Sites.Interfaces;

public interface ISiteService
{
    Task<Result<PagedResponse<SiteListItemDto>>> ListAsync(SiteListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<Result<SiteDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateSiteDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateSiteDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
