namespace Core.Models;

public static class PaginationHandler
{
    public static Pagination GetDefaultPagination() => new() { };
}

public class Pagination
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public string OrderBy { get; set; } = "Id";
    public string GroupBy { get; set; } = "Id";
    public bool Ascending { get; set; } = true;
}
