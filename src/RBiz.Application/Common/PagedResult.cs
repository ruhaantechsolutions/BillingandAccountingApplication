namespace RBiz.Application.Common;

/// <summary>
/// One page of results plus the total count, so a client can render paging controls.
/// Used for list endpoints to guarantee a query can never return an unbounded number
/// of rows - which matters most for the audit trail.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Page number and size for a list request, clamped to a sane range so a caller cannot
/// ask for a million rows in one go.
/// </summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public PageRequest(int? page = null, int? pageSize = null)
    {
        Page = page is > 0 ? page.Value : 1;
        PageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Number of rows to skip for this page.</summary>
    public int Skip => (Page - 1) * PageSize;
}
