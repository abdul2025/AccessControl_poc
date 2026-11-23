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

    // Hardware layout (SIO panels and their points) — changed only through the controller aggregate.

    Task<Result> AddSioAsync(Guid scpId, AddSioDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateSioAsync(Guid scpId, Guid sioId, UpdateSioDto dto, CancellationToken cancellationToken);

    Task<Result> RemoveSioAsync(Guid scpId, Guid sioId, byte[] rowVersion, CancellationToken cancellationToken);

    Task<Result> UpdateInputAsync(Guid scpId, Guid inputId, UpdateInputDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateOutputAsync(Guid scpId, Guid outputId, UpdateOutputDto dto, CancellationToken cancellationToken);

    // Readers and doors.

    Task<Result> AddReaderAsync(Guid scpId, ReaderWriteDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateReaderAsync(Guid scpId, Guid readerId, ReaderWriteDto dto, CancellationToken cancellationToken);

    Task<Result> RemoveReaderAsync(Guid scpId, Guid readerId, byte[] rowVersion, CancellationToken cancellationToken);

    Task<Result> AddDoorAsync(Guid scpId, DoorWriteDto dto, CancellationToken cancellationToken);

    Task<Result> UpdateDoorAsync(Guid scpId, Guid doorId, DoorWriteDto dto, CancellationToken cancellationToken);

    Task<Result> RemoveDoorAsync(Guid scpId, Guid doorId, byte[] rowVersion, CancellationToken cancellationToken);
}
