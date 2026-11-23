using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Application.Features.Scps.Interfaces;
using AccessControl.Application.Features.Scps.Ports;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Entities;
using ScpEntity = AccessControl.Domain.Entities.Scp;

namespace AccessControl.Application.Features.Scps;

public sealed class ScpService(IScpRepository repository) : IScpService
{
    public async Task<Result<PagedResponse<ScpListItemDto>>> ListAsync(
        ScpListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<ScpListItemDto>>.Success(await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<ScpDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var scp = await repository.GetByIdAsync(id, cancellationToken);

        return scp is null
            ? Result<ScpDto>.Failure(ScpErrors.NotFound, ScpErrors.NotFoundMessage)
            : Result<ScpDto>.Success(scp);
    }

    public async Task<Result<IReadOnlyList<SiteOptionDto>>> ListSiteOptionsAsync(CancellationToken cancellationToken) =>
        Result<IReadOnlyList<SiteOptionDto>>.Success(await repository.ListSiteOptionsAsync(cancellationToken));

    public async Task<Result<short>> GetNextFreeScpNumberAsync(CancellationToken cancellationToken) =>
        Result<short>.Success(await repository.GetNextFreeScpNumberAsync(cancellationToken) ?? ScpEntity.MinScpNumber);

    public async Task<Result<Guid>> CreateAsync(CreateScpDto dto, CancellationToken cancellationToken)
    {
        var created = ScpEntity.Create(
            dto.SiteId,
            dto.Name,
            dto.ScpNumber,
            dto.Model,
            dto.ConnectionMode,
            dto.SerialNumber,
            dto.IpAddress,
            dto.Port,
            dto.Description,
            ScpAllocation.GmtOffsetSecondsFromUtcHours(dto.UtcOffsetHours));

        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var scp = created.Value!;
        var checkedRules = await CheckDatabaseRulesAsync(scp, excludeId: null, cancellationToken);
        if (checkedRules.IsFailure)
        {
            return Result<Guid>.Failure(checkedRules.ErrorCode!, checkedRules.ErrorMessage!);
        }

        var saved = await repository.AddAsync(scp, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success(scp.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateScpDto dto, CancellationToken cancellationToken)
    {
        var scp = await repository.GetForUpdateAsync(id, cancellationToken);
        if (scp is null)
        {
            return Result.Failure(ScpErrors.NotFound, ScpErrors.NotFoundMessage);
        }

        var updated = scp.Update(
            dto.SiteId,
            dto.Name,
            dto.ScpNumber,
            dto.ConnectionMode,
            dto.SerialNumber,
            dto.IpAddress,
            dto.Port,
            dto.Description);

        if (updated.IsFailure)
        {
            return updated;
        }

        var checkedRules = await CheckDatabaseRulesAsync(scp, excludeId: id, cancellationToken);
        if (checkedRules.IsFailure)
        {
            return checkedRules;
        }

        return await repository.UpdateAsync(scp, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var scp = await repository.GetForUpdateAsync(id, cancellationToken);
        if (scp is null)
        {
            return Result.Failure(ScpErrors.NotFound, ScpErrors.NotFoundMessage);
        }

        // Step 4 adds a confirmation rule for published controllers; nothing is published yet.
        return await repository.DeleteAsync(scp, rowVersion, cancellationToken);
    }

    public Task<Result> AddSioAsync(Guid scpId, AddSioDto dto, CancellationToken cancellationToken) =>
        ChangeHardwareAsync(scpId, dto.RowVersion, scp => scp.AddSio(dto.Number, dto.Model, dto.Port, dto.Address, dto.Name), cancellationToken);

    public Task<Result> UpdateSioAsync(Guid scpId, Guid sioId, UpdateSioDto dto, CancellationToken cancellationToken) =>
        ChangeHardwareAsync(scpId, dto.RowVersion, scp => scp.RenameSio(sioId, dto.Name), cancellationToken);

    public Task<Result> RemoveSioAsync(Guid scpId, Guid sioId, byte[] rowVersion, CancellationToken cancellationToken) =>
        ChangeHardwareAsync(scpId, rowVersion, scp => scp.RemoveSio(sioId), cancellationToken);

    public Task<Result> UpdateInputAsync(Guid scpId, Guid inputId, UpdateInputDto dto, CancellationToken cancellationToken) =>
        ChangeHardwareAsync(
            scpId,
            dto.RowVersion,
            scp => scp.UpdateInput(inputId, dto.Name, dto.Contact, dto.Supervised, dto.Monitored),
            cancellationToken);

    public Task<Result> UpdateOutputAsync(Guid scpId, Guid outputId, UpdateOutputDto dto, CancellationToken cancellationToken) =>
        ChangeHardwareAsync(scpId, dto.RowVersion, scp => scp.UpdateOutput(outputId, dto.Name, dto.PulseSeconds), cancellationToken);

    /// <summary>
    /// Load the whole controller aggregate, let the domain apply the change, then save against the row version the
    /// caller read — every hardware-layout change is concurrency-checked on the controller.
    /// </summary>
    private async Task<Result> ChangeHardwareAsync(
        Guid scpId,
        byte[] rowVersion,
        Func<ScpEntity, Result> change,
        CancellationToken cancellationToken)
    {
        var scp = await repository.GetForUpdateAsync(scpId, cancellationToken);
        if (scp is null)
        {
            return Result.Failure(ScpErrors.NotFound, ScpErrors.NotFoundMessage);
        }

        var changed = change(scp);
        if (changed.IsFailure)
        {
            return changed;
        }

        return await repository.UpdateAsync(scp, rowVersion, cancellationToken);
    }

    /// <summary>
    /// Rules that need the database. Friendly early checks; the filtered unique indexes are the real guarantee.
    /// </summary>
    private async Task<Result> CheckDatabaseRulesAsync(ScpEntity scp, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (!await repository.SiteExistsAsync(scp.SiteId, cancellationToken))
        {
            return Result.Failure(ScpErrors.SiteNotFound, ScpErrors.SiteNotFoundMessage);
        }

        if (await repository.ScpNumberExistsAsync(scp.ScpNumber, excludeId, cancellationToken))
        {
            return Result.Failure(ScpErrors.DuplicateNumber, ScpErrors.DuplicateNumberMessage);
        }

        if (scp.SerialNumber is { } serialNumber
            && await repository.SerialNumberExistsAsync(serialNumber, excludeId, cancellationToken))
        {
            return Result.Failure(ScpErrors.DuplicateSerial, ScpErrors.DuplicateSerialMessage);
        }

        if (await repository.NameExistsInSiteAsync(scp.SiteId, scp.Name, excludeId, cancellationToken))
        {
            return Result.Failure(ScpErrors.DuplicateName, ScpErrors.DuplicateNameMessage);
        }

        return Result.Success();
    }
}
