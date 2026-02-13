namespace ElasticDemo.Api.Features.Applications;

public record Client
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string NationalId { get; init; }
    public required string ClientId { get; init; }
}

public record Applicant
{
    public required Client Client { get; init; }
    public Client? Spouse { get; init; }
}

public record Application
{
    public required string Id { get; init; }
    public required string Product { get; init; }
    public required string Transaction { get; init; }
    public required string Channel { get; init; }
    public string? Branch { get; init; }
    public required string Status { get; init; }
    public required string User { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public required Applicant MainApplicant { get; init; }
    public List<Applicant> CoApplicants { get; init; } = [];
}

public static class ApplicationIndex
{
    public const string Alias = "applications";
    public const string CurrentVersion = "applications_v2";
    public const string VersionPattern = "applications_v*";
}
