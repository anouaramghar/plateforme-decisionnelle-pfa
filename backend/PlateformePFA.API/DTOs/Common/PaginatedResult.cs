namespace PlateformePFA.API.DTOs.Common;

/// <summary>
/// Standard wrapper for paginated list endpoints. Lets the frontend render
/// "Page X of Y" without an extra count round-trip.
/// </summary>
public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
