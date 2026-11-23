using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites.DTOs;
using AccessControl.Application.Features.Sites.Interfaces;
using AccessControl.Application.Features.Sites.Ports;
using AccessControl.Domain.Common.Results;
using SiteEntity = AccessControl.Domain.Entities.Site;

namespace AccessControl.Application.Features.Sites;

public sealed class SiteService(ISiteRepository repository) : ISiteService
{
    public async Task<Result<PagedResponse<SiteListItemDto>>> ListAsync(
        SiteListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<SiteListItemDto>>.Success(await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<SiteDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var site = await repository.GetByIdAsync(id, cancellationToken);

        return site is null
            ? Result<SiteDto>.Failure(SiteErrors.NotFound, SiteErrors.NotFoundMessage)
            : Result<SiteDto>.Success(site);
    }

    public async Task<Result<Guid>> CreateAsync(CreateSiteDto dto, CancellationToken cancellationToken)
    {
        var created = SiteEntity.Create(dto.Code, dto.Name, dto.Description);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var site = created.Value!;
        var unique = await CheckUniqueAsync(site, excludeId: null, cancellationToken);
        if (unique.IsFailure)
        {
            return Result<Guid>.Failure(unique.ErrorCode!, unique.ErrorMessage!);
        }

        var saved = await repository.AddAsync(site, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(site.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateSiteDto dto, CancellationToken cancellationToken)
    {
        var site = await repository.GetForUpdateAsync(id, cancellationToken);
        if (site is null)
        {
            return Result.Failure(SiteErrors.NotFound, SiteErrors.NotFoundMessage);
        }

        var updated = site.Update(dto.Code, dto.Name, dto.Description);
        if (updated.IsFailure)
        {
            return updated;
        }

        var unique = await CheckUniqueAsync(site, excludeId: id, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        return await repository.UpdateAsync(site, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var site = await repository.GetForUpdateAsync(id, cancellationToken);
        if (site is null)
        {
            return Result.Failure(SiteErrors.NotFound, SiteErrors.NotFoundMessage);
        }

        // Controllers reference their site; deleting it would leave them without a location.
        if (await repository.HasScpsAsync(id, cancellationToken))
        {
            return Result.Failure(SiteErrors.HasScps, SiteErrors.HasScpsMessage);
        }

        return await repository.DeleteAsync(site, rowVersion, cancellationToken);
    }

    /// <summary>Friendly early checks; the filtered unique indexes are still the real guarantee.</summary>
    private async Task<Result> CheckUniqueAsync(SiteEntity site, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.CodeExistsAsync(site.Code, excludeId, cancellationToken))
        {
            return Result.Failure(SiteErrors.DuplicateCode, SiteErrors.DuplicateCodeMessage);
        }

        if (await repository.NameExistsAsync(site.Name, excludeId, cancellationToken))
        {
            return Result.Failure(SiteErrors.DuplicateName, SiteErrors.DuplicateNameMessage);
        }

        return Result.Success();
    }
}
