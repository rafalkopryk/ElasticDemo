namespace ElasticDemo.Api.Features.Products;

public static class ProductIndex
{
    public const string Active = "products";
    public const string ArchivePattern = "products-archive-*";
    public const string ArchiveTemplateName = "products-archive-template";

    public static string ArchiveForYear(int year) => $"products-archive-{year}";

    /// <summary>
    /// Returns the minimal set of indices needed for a date range query.
    /// - No date range or range spans past the cutoff → active + archives
    /// - Range entirely within last year → active only
    /// - Range entirely older than 1yr → archives only
    /// </summary>
    public static string[] IndicesForSearch(
        DateTimeOffset? from, DateTimeOffset? to, TimeProvider timeProvider)
    {
        var cutoff = timeProvider.GetUtcNow().AddYears(-1);

        bool needsActive = !to.HasValue || to.Value >= cutoff;
        bool needsArchive = !from.HasValue || from.Value < cutoff;

        return (needsActive, needsArchive) switch
        {
            (true, true) => [Active, ArchivePattern],
            (true, false) => [Active],
            (false, true) => [ArchivePattern],
            (false, false) => [Active], // fallback
        };
    }
}
