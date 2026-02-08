namespace ElasticDemo.Api.Features.Applications;

public record Client
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string NationalId { get; init; }
    public required string ClientId { get; init; }
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
    public required Client MainClient { get; init; }
    public Client? Spouse { get; init; }
    public List<Client> CoApplicants { get; init; } = [];
}

public static class ApplicationIndex
{
    public const string Active = "applications";
}
