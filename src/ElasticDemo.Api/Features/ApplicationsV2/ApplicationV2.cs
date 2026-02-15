using System.Text.Json.Serialization;
using ElasticDemo.Api.Features.Applications;

namespace ElasticDemo.Api.Features.ApplicationsV2;

public record ApplicationV2Client
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string NationalId { get; init; }
    public required string ClientId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ClientRole Role { get; init; }

    public string? ParentClientId { get; init; }
}

public record ApplicationV2
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
    public List<ApplicationV2Client> Clients { get; init; } = [];
}

public static class ApplicationV2Index
{
    public const string Alias = "applications-v2";
    public const string CurrentVersion = "applications_v3";
}
