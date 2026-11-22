namespace AccessControl.Application.Common.Paging;

/// <summary>A page request with untrusted values clamped to safe bounds.</summary>
public sealed record PagedRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Largest page whose offset still fits in an <see cref="int"/> at the maximum page size.</summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public PagedRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        Page = Math.Clamp(page, 1, MaxPage);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;
}
