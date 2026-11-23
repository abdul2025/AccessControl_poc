using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Application.Features.Scps.Ports;
using AccessControl.Domain.Catalog;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Persistence.Features.Scps;

internal sealed class ScpRepository(AccessControlDbContext dbContext) : IScpRepository
{
    public const int MaxSiteOptions = 1000;
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<PagedResponse<ScpListItemDto>> ListAsync(
        ScpListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Scps.AsNoTracking();

        if (filter.SiteId is { } siteId)
        {
            query = query.Where(scp => scp.SiteId == siteId);
        }

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where(scp => scp.Name.Contains(search)
                || (scp.IpAddress != null && scp.IpAddress.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Join(dbContext.Sites, scp => scp.SiteId, site => site.Id, (scp, site) => new { scp, site.Code })
            .OrderBy(row => row.scp.ScpNumber)
            .ThenBy(row => row.scp.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(row => new ScpListItemDto(
                row.scp.Id,
                row.scp.Name,
                row.scp.ScpNumber,
                row.Code,
                row.scp.ConnectionMode,
                row.scp.SerialNumber,
                row.scp.IpAddress,
                row.scp.SyncStatus))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ScpListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<ScpDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await dbContext.Scps
            .AsNoTracking()
            .Where(scp => scp.Id == id)
            .Join(dbContext.Sites, scp => scp.SiteId, site => site.Id, (scp, site) => new
            {
                Scp = scp,
                SiteCode = site.Code,
                SiteName = site.Name,
                Panels = scp.SioPanels
                    .OrderBy(panel => panel.Number)
                    .Select(panel => new { panel.Id, panel.Number, panel.Model, panel.Port, panel.Address, panel.Name })
                    .ToList(),
                RowVersion = EF.Property<byte[]>(scp, AccessControlDbContext.RowVersionPropertyName),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var scp = row.Scp;

        // Capacities come from the model catalog, which lives in code, so they are added after the query.
        var panels = row.Panels
            .Select(panel =>
            {
                var spec = HardwareCatalog.For(panel.Model);
                return new SioPanelDto(panel.Id, panel.Number, panel.Model, panel.Port, panel.Address, panel.Name,
                    spec.Inputs, spec.Outputs, spec.ReaderPorts);
            })
            .ToList();

        return new ScpDto(
            scp.Id,
            scp.SiteId,
            row.SiteCode,
            row.SiteName,
            scp.Name,
            scp.ScpNumber,
            scp.Model,
            scp.ConnectionMode,
            scp.SerialNumber,
            scp.IpAddress,
            scp.Port,
            scp.Description,
            new ScpAllocationDto(
                scp.Allocation.Transactions,
                scp.Allocation.Cards,
                scp.Allocation.AccessLevelsPerCard,
                scp.Allocation.GmtOffsetSeconds),
            scp.SyncStatus,
            scp.PublishedVersion,
            scp.AppliedVersion,
            scp.CanChangeScpNumber,
            panels,
            scp.CreatedAtUtc,
            scp.CreatedBy,
            scp.UpdatedAtUtc,
            scp.UpdatedBy,
            row.RowVersion);
    }

    public Task<Scp?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Scps
            .Include(scp => scp.SioPanels)
            .SingleOrDefaultAsync(scp => scp.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SiteOptionDto>> ListSiteOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Sites
            .AsNoTracking()
            .OrderBy(site => site.Code)
            .ThenBy(site => site.Id)
            .Take(MaxSiteOptions)
            .Select(site => new SiteOptionDto(site.Id, site.Code, site.Name))
            .ToListAsync(cancellationToken);

    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) =>
        dbContext.Sites.AsNoTracking().AnyAsync(site => site.Id == siteId, cancellationToken);

    public Task<bool> ScpNumberExistsAsync(short scpNumber, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Scps
            .AsNoTracking()
            .AnyAsync(scp => scp.ScpNumber == scpNumber && (excludeId == null || scp.Id != excludeId), cancellationToken);

    public Task<bool> SerialNumberExistsAsync(int serialNumber, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Scps
            .AsNoTracking()
            .AnyAsync(scp => scp.SerialNumber == serialNumber && (excludeId == null || scp.Id != excludeId), cancellationToken);

    public Task<bool> NameExistsInSiteAsync(Guid siteId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Scps
            .AsNoTracking()
            .AnyAsync(scp => scp.SiteId == siteId && scp.Name == name && (excludeId == null || scp.Id != excludeId), cancellationToken);

    public async Task<short?> GetNextFreeScpNumberAsync(CancellationToken cancellationToken)
    {
        var used = await dbContext.Scps
            .AsNoTracking()
            .OrderBy(scp => scp.ScpNumber)
            .Select(scp => scp.ScpNumber)
            .ToListAsync(cancellationToken);

        var candidate = Scp.MinScpNumber;
        foreach (var number in used)
        {
            if (number > candidate)
            {
                break;
            }

            if (number == candidate)
            {
                candidate++;
            }
        }

        return candidate <= Scp.MaxScpNumber ? candidate : null;
    }

    public Task<Result> AddAsync(Scp scp, CancellationToken cancellationToken)
    {
        dbContext.Scps.Add(scp);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(Scp scp, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(scp, expectedRowVersion);

        // Child changes do not touch the root's columns, so EF would not check (or bump) its row version.
        // Marking the root modified makes every change concurrency-checked against the version the caller read.
        dbContext.Entry(scp).State = EntityState.Modified;
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(Scp scp, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(scp, expectedRowVersion);
        dbContext.Scps.Remove(scp);   // the interceptor turns the controller into a soft delete

        // Removing the root cascades "Deleted" to its loaded SIO rows immediately. The controller is only
        // soft-deleted, so its SIO rows stay as they are (history and a possible restore keep their hardware layout).
        foreach (var panel in dbContext.ChangeTracker.Entries<SioPanel>().Where(entry => entry.State == EntityState.Deleted))
        {
            panel.State = EntityState.Unchanged;
        }

        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(Scp scp, byte[] expectedRowVersion) =>
        dbContext.Entry(scp).Property(AccessControlDbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ScpErrors.ConcurrencyConflict, ScpErrors.ConcurrencyConflictMessage);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            // Map the violated index to the business error; anything else is unexpected and rethrown.
            if (sql.Message.Contains(ScpConfiguration.ScpNumberIndexName, StringComparison.Ordinal))
            {
                return Result.Failure(ScpErrors.DuplicateNumber, ScpErrors.DuplicateNumberMessage);
            }

            if (sql.Message.Contains(ScpConfiguration.SerialNumberIndexName, StringComparison.Ordinal))
            {
                return Result.Failure(ScpErrors.DuplicateSerial, ScpErrors.DuplicateSerialMessage);
            }

            if (sql.Message.Contains(ScpConfiguration.SiteNameIndexName, StringComparison.Ordinal))
            {
                return Result.Failure(ScpErrors.DuplicateName, ScpErrors.DuplicateNameMessage);
            }

            throw;
        }
    }
}
