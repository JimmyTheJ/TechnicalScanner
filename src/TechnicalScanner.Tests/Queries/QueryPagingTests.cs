using TechnicalScanner.Application.Models.Queries;

namespace TechnicalScanner.Tests.Queries;

public class QueryPagingTests
{
    [Theory]
    [InlineData(0, 0, 1, 50)]
    [InlineData(-1, 500, 1, 200)]
    [InlineData(2, 25, 2, 25)]
    public void Normalize_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (normalizedPage, normalizedPageSize, skip) = QueryPaging.Normalize(page, pageSize);

        Assert.Equal(expectedPage, normalizedPage);
        Assert.Equal(expectedPageSize, normalizedPageSize);
        Assert.Equal((expectedPage - 1) * expectedPageSize, skip);
    }

    [Fact]
    public void PagedResult_CalculatesTotalPages()
    {
        var result = new PagedResult<string>(["a", "b"], 1, 2, 5);

        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }
}
