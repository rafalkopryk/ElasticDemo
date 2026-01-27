# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ElasticDemo is a .NET Aspire-based distributed application demonstrating Elasticsearch integration with ASP.NET Core. It provides a REST API for managing and searching a product catalog.

**Stack:** .NET 10.0, C#, Elasticsearch 8.x+, .NET Aspire orchestration, OpenTelemetry

## Build & Run Commands

```bash
# Build the solution
dotnet build ElasticDemo.slnx

# Run the application (starts Aspire orchestrator + Elasticsearch container + API)
aspire run

# Run just the API (requires Elasticsearch running separately)
dotnet run --project src/ElasticDemo.Api
```

After startup, initialize the Elasticsearch index and seed sample data:
- `POST /api/products/init` - Create the products index
- `POST /api/products/seed` - Load sample data

## Testing with .http Files

Use `src/ElasticDemo.Api/products.http` to test the API workflow:

1. Run init and seed requests to set up data
2. Test search with various filters (query, category, price range)
3. Test CRUD operations (create, get, delete)

The file uses `@baseUrl` variable - modify for HTTPS if needed.

## Architecture

Three-project structure following .NET Aspire patterns:

1. **ElasticDemo.AppHost** - Aspire orchestration host
   - Manages Elasticsearch container with persistent volume
   - Coordinates service startup and health checks
   - Entry: `src/ElasticDemo.AppHost/AppHost.cs`

2. **ElasticDemo.Api** - Web API service
   - Minimal APIs pattern with feature-based folder structure
   - Product handlers in `src/ElasticDemo.Api/Features/Products/`
   - Endpoints registered in `src/ElasticDemo.Api/Program.cs`

3. **ElasticDemo.ServiceDefaults** - Shared configuration
   - OpenTelemetry setup (traces, metrics, logs)
   - HTTP resilience policies (retries, circuit breakers)
   - Health check endpoints (`/health`, `/alive`)

## Key Files

- [AppHost.cs](src/ElasticDemo.AppHost/AppHost.cs)
- [Program.cs](src/ElasticDemo.Api/Program.cs)
- [Products feature](src/ElasticDemo.Api/Features/Products/)
- [products.http](src/ElasticDemo.Api/products.http)

## Handler Conventions

- **Feature folders**: Organize handlers by feature in `Features/{FeatureName}/` directories
- **Primary constructors**: Use C# primary constructors for dependency injection
- **DTOs in same file**: Place Request/Response records in the same file as the handler, but at namespace level (not nested inside the class)
- **Naming**: `{Action}Handler` for classes, `{Action}{Feature}Request/Response` for DTOs

Example:
```csharp
public record CreateProductRequest(string Name, decimal Price);

public class CreateProductHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(CreateProductRequest request) { ... }
}
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/products/init` | Initialize Elasticsearch index |
| POST | `/api/products/seed` | Seed with sample products |
| GET | `/api/products/search` | Full-text search with filters |
| POST | `/api/products` | Create product |
| GET | `/api/products/{id}` | Get product by ID |
| DELETE | `/api/products/{id}` | Delete product |

Search supports: query text (fuzzy matching on name/description), category filter, price range, pagination (from/size).

## Product Model

```csharp
Product {
    Id (string), Name (string), Description (string),
    Category (string), Price (decimal), Tags (List<string>),
    InStock (bool), CreatedAt (DateTimeOffset),
    Variants (List<ProductVariant>)
}

ProductVariant {
    Sku (string), Size (string?), Color (string?),
    PriceAdjustment (decimal), Stock (int)
}
```

Elasticsearch index: `products`

Approximately 30% of sample products include 2-5 variants with different colors, sizes (optional), price adjustments, and stock levels.

## Development URLs

- HTTP: `http://localhost:5275`
- HTTPS: `https://localhost:7232`
- OpenAPI spec: `/openapi/v1.json`

## Coding Conventions

- **Immutable types** - Prefer `record` with `init` properties over mutable `class` with `set`
- **DateTimeOffset** - Use `DateTimeOffset` instead of `DateTime`
- **TimeProvider** - Use `TimeProvider` instead of `DateTime.Now`/`DateTime.UtcNow`/`DateTimeOffset.UtcNow` for testability
- **Do not read `sample-products.json`** - This is a large generated file; do not read it for context

## Working Style

- **Limit exploration** - Don't over-explore or spawn multiple agents for simple tasks. Read relevant files directly instead of extensive codebase searches.
- **Be concise** - Avoid lengthy research sessions. If a task is straightforward, just do it.
- **Ask early** - If there are significant trade-offs or limitations, ask the user before deep-diving.
- **File-based tools** - Tool scripts in `tools/` should use .NET 10 file-based programs (single `.cs` file without a `.csproj`). This keeps utility scripts simple and portable.

## Regenerating Sample Products

The sample products JSON file can be regenerated using the ProductGenerator tool:

```bash
# From repository root
dotnet run tools/ProductGenerator/generate-products.cs

# Or specify custom output path
dotnet run tools/ProductGenerator/generate-products.cs -- /path/to/output.json

# On Unix, can also run directly (after chmod +x)
./tools/ProductGenerator/generate-products.cs
```

This generates 1000 products with the following distribution:
- Electronics: 250 (laptops, monitors, keyboards, mice, etc.)
- Audio: 150 (headphones, speakers, earbuds, microphones)
- Accessories: 200 (cables, hubs, adapters, stands)
- Storage: 100 (SSDs, HDDs, USB drives, memory cards)
- Furniture: 150 (desks, chairs, shelves, mats)
- Gaming: 150 (controllers, gaming chairs, VR, accessories)

Approximately 30% of products include 2-5 variants with different colors, sizes, price adjustments, and stock levels.

Output: `src/ElasticDemo.Api/Features/Products/sample-products.json`

## Debugging with Aspire MCP Server

Use the Aspire MCP server tools to inspect running applications:

- **`mcp__aspire__list_resources`** - List all resources (API, Elasticsearch, etc.) with their status, endpoints, and health
- **`mcp__aspire__list_console_logs`** - View console output for a specific resource (useful for startup errors)
- **`mcp__aspire__list_structured_logs`** - Query structured logs with filtering by resource
- **`mcp__aspire__list_traces`** - List distributed traces across resources
- **`mcp__aspire__list_trace_structured_logs`** - Get logs for a specific trace ID
- **`mcp__aspire__execute_resource_command`** - Start/stop/restart resources

When debugging issues:
1. Check resource status with `list_resources`
2. If a resource isn't running, check `list_console_logs` for startup errors
3. For request issues, use `list_traces` to find the trace, then `list_trace_structured_logs` for details
