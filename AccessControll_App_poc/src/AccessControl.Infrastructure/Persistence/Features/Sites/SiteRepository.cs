using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites;
using AccessControl.Application.Features.Sites.DTOs;
using AccessControl.Application.Features.Sites.Ports;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Enums.Scps;
using AccessControl.Infrastructure.Persistence.Features.ScpProvisioning;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Persistence.Features.Sites;

internal sealed class SiteRepository(AccessControlDbContext dbContext) : ISiteRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<PagedResponse<SiteListItemDto>> ListAsync(
        SiteListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Sites.AsNoTracking();

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where(site => site.Code.Contains(search) || site.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(site => site.Code)
            .ThenBy(site => site.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(site => new SiteListItemDto(
                site.Id,
                site.Code,
                site.Name,
                dbContext.Scps.Count(scp => scp.SiteId == site.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SiteListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<SiteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Sites
            .AsNoTracking()
            .Where(site => site.Id == id)
            .Select(site => new SiteDto(
                site.Id,
                site.Code,
                site.Name,
                site.Description,
                dbContext.Scps
                    .Where(scp => scp.SiteId == site.Id)
                    .OrderBy(scp => scp.ScpNumber)
                    // Sync status is derived from the published version and the Gateway's state (evaluated after the query).
                    .Select(scp => new SiteScpItemDto(
                        scp.Id,
                        scp.Name,
                        scp.ScpNumber,
                        scp.ConnectionMode,
                        ScpSyncStatuses.Derive(
                            scp.PublishedVersion,
                            dbContext.Set<ScpConnectionStateRecord>().Where(state => state.ScpId == scp.Id).Select(state => state.AppliedVersion).FirstOrDefault(),
                            dbContext.Set<ScpConnectionStateRecord>().Where(state => state.ScpId == scp.Id).Select(state => (ScpApplyState?)state.ApplyState).FirstOrDefault())))
                    .ToList(),
                site.CreatedAtUtc,
                site.CreatedBy,
                site.UpdatedAtUtc,
                site.UpdatedBy,
                EF.Property<byte[]>(site, AccessControlDbContext.RowVersionPropertyName)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Site?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Sites.SingleOrDefaultAsync(site => site.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Sites
            .AsNoTracking()
            .AnyAsync(site => site.Code == code && (excludeId == null || site.Id != excludeId), cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.Sites
            .AsNoTracking()
            .AnyAsync(site => site.Name == name && (excludeId == null || site.Id != excludeId), cancellationToken);

    public Task<bool> HasScpsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Scps.AsNoTracking().AnyAsync(scp => scp.SiteId == id, cancellationToken);

    public Task<Result> AddAsync(Site site, CancellationToken cancellationToken)
    {
        dbContext.Sites.Add(site);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync(Site site, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(site, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync(Site site, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion(site, expectedRowVersion);
        dbContext.Sites.Remove(site);   // the interceptor turns this into a soft delete
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion(Site site, byte[] expectedRowVersion) =>
        dbContext.Entry(site).Property(AccessControlDbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(SiteErrors.ConcurrencyConflict, SiteErrors.ConcurrencyConflictMessage);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql
            && sql.Message.Contains(SiteConfiguration.CodeIndexName, StringComparison.Ordinal))
        {
            return Result.Failure(SiteErrors.DuplicateCode, SiteErrors.DuplicateCodeMessage);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql
            && sql.Message.Contains(SiteConfiguration.NameIndexName, StringComparison.Ordinal))
        {
            return Result.Failure(SiteErrors.DuplicateName, SiteErrors.DuplicateNameMessage);
        }
    }
}
