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
                    .Select(panel => new
                    {
                        panel.Id,
                        panel.Number,
                        panel.Model,
                        panel.Port,
                        panel.Address,
                        panel.Name,
                        Inputs = panel.Inputs
                            .OrderBy(point => point.Number)
                            .Select(point => new InputPointDto(
                                point.Id, point.Number, point.Name, point.Contact, point.Supervised, point.Monitored, point.Dedicated))
                            .ToList(),
                        Outputs = panel.Outputs
                            .OrderBy(point => point.Number)
                            .Select(point => new OutputPointDto(point.Id, point.Number, point.Name, point.PulseSeconds))
                            .ToList(),
                    })
                    .ToList(),
                Readers = scp.Readers
                    .Select(reader => new
                    {
                        reader.Id,
                        reader.SioPanelId,
                        reader.Number,
                        reader.Name,
                        reader.Model,
                        reader.Interface,
                        reader.OsdpAddress,
                        reader.OsdpBaudRate,
                        reader.SecureChannel,
                        reader.HasKeypad,
                    })
                    .ToList(),
                Doors = scp.Doors.OrderBy(door => door.Number).ToList(),
                RowVersion = EF.Property<byte[]>(scp, AccessControlDbContext.RowVersionPropertyName),
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var scp = row.Scp;

        // Reader capacity comes from the model catalog, which lives in code, so it is added after the query.
        var panels = row.Panels
            .Select(panel => new SioPanelDto(
                panel.Id,
                panel.Number,
                panel.Model,
                panel.Port,
                panel.Address,
                panel.Name,
                HardwareCatalog.For(panel.Model).ReaderPorts,
                panel.Inputs,
                panel.Outputs))
            .ToList();

        // Display names for readers and door points are resolved in memory from the rows already loaded.
        var sioNumbers = panels.ToDictionary(panel => panel.Id, panel => panel.Number);
        var readers = row.Readers
            .Select(reader => new ReaderDto(
                reader.Id,
                reader.SioPanelId,
                sioNumbers.GetValueOrDefault(reader.SioPanelId),
                reader.Number,
                reader.Name,
                reader.Model,
                reader.Interface,
                reader.OsdpAddress,
                reader.OsdpBaudRate,
                reader.SecureChannel,
                reader.HasKeypad))
            .OrderBy(reader => reader.SioNumber)
            .ThenBy(reader => reader.Number)
            .ToList();

        var readerNames = readers.ToDictionary(reader => reader.Id, reader => reader.Name);
        var inputNames = panels.SelectMany(panel => panel.Inputs.Select(point => (point.Id, Name: $"SIO {panel.Number} · {point.Name}")))
            .ToDictionary(pair => pair.Id, pair => pair.Name);
        var outputNames = panels.SelectMany(panel => panel.Outputs.Select(point => (point.Id, Name: $"SIO {panel.Number} · {point.Name}")))
            .ToDictionary(pair => pair.Id, pair => pair.Name);

        string? NameOf(Dictionary<Guid, string> names, Guid? id) => id is { } value ? names.GetValueOrDefault(value) : null;

        var doors = row.Doors
            .Select(door => new DoorDto(
                door.Id,
                door.Number,
                door.Name,
                door.EntryReaderId,
                readerNames.GetValueOrDefault(door.EntryReaderId) ?? "?",
                door.ExitReaderId,
                NameOf(readerNames, door.ExitReaderId),
                door.LockOutputId,
                outputNames.GetValueOrDefault(door.LockOutputId) ?? "?",
                door.DoorContactInputId,
                NameOf(inputNames, door.DoorContactInputId),
                door.ExitButtonInputId,
                NameOf(inputNames, door.ExitButtonInputId),
                door.StrikeMinSeconds,
                door.StrikeMaxSeconds,
                door.HeldOpenSeconds,
                door.AccessMode))
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
            Scp.NextFreeSioNumber(panels.Select(panel => panel.Number)),
            readers,
            doors,
            Scp.NextFreeDoorNumber(doors.Select(door => door.Number)),
            scp.CreatedAtUtc,
            scp.CreatedBy,
            scp.UpdatedAtUtc,
            scp.UpdatedBy,
            row.RowVersion);
    }

    /// <summary>Loads the whole aggregate; split queries avoid multiplying rows across SIOs × inputs × outputs.</summary>
    public Task<Scp?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Scps
            .Include(scp => scp.SioPanels).ThenInclude(panel => panel.Inputs)
            .Include(scp => scp.SioPanels).ThenInclude(panel => panel.Outputs)
            .Include(scp => scp.Readers)
            .Include(scp => scp.Doors)
            .AsSplitQuery()
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

        // Removing the root cascades "Deleted" to its loaded children immediately. The controller is only
        // soft-deleted, so its hardware layout rows stay as they are (history and a possible restore keep them).
        foreach (var child in dbContext.ChangeTracker.Entries()
                     .Where(entry => entry.Entity is SioPanel or InputPoint or OutputPoint or Reader or Door
                                     && entry.State == EntityState.Deleted))
        {
            child.State = EntityState.Unchanged;
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
