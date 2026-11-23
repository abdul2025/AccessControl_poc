using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Common.Results;

namespace AccessControl.Application.Features.Scps.Interfaces;

public interface IScpService
{
    Task<Result<PagedResponse<ScpListItemDto>>> ListAsync(ScpListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<Result<ScpDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<SiteOptionDto>>> ListSiteOptionsAsync(CancellationToken cancellationToken);

    /// <summary>The lowest SCP number not used by a live controller, suggested on the create form.</summary>
    Task<Result<short>> GetNextFreeScpNumberAsync(CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(CreateScpDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, UpdateScpDto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
