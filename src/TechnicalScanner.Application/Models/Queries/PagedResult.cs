namespace TechnicalScanner.Application.Models.Queries;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public static class QueryPaging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize, int Skip) Normalize(int page, int pageSize, int maxPageSize = MaxPageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, maxPageSize);
        return (normalizedPage, normalizedPageSize, (normalizedPage - 1) * normalizedPageSize);
    }
}
