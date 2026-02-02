# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ElasticDemo is a .NET Aspire-based distributed application demonstrating Elasticsearch integration with ASP.NET Core. It provides a REST API for managing and searching a product catalog.

**Stack:** .NET 10.0, C#, Elasticsearch 8.x+, Ollama (Qwen3-Embedding-8B embeddings), .NET Aspire orchestration, OpenTelemetry

## Build & Run Commands

```bash
# Build the solution
dotnet build ElasticDemo.slnx

# Run the application (starts Aspire orchestrator + Elasticsearch container + API)
aspire run

# Run just the API (requires Elasticsearch running separately)
dotnet run --project src/ElasticDemo.Api
```

## Testing with .http Files

Use `src/ElasticDemo.Api/products.http` to test the API workflow:

1. Run init and seed requests to set up data
2. Test search with various filters (query, category, price range)
3. Test CRUD operations (create, get, delete)

The file uses `@baseUrl` variable - modify for HTTPS if needed.

## Architecture

Three-project structure following .NET Aspire patterns:

1. **ElasticDemo.AppHost** ([AppHost.cs](src/ElasticDemo.AppHost/AppHost.cs)) - Aspire orchestration host
   - Manages Elasticsearch container (security disabled, no auth) with persistent volume
   - Provisions Kibana container on port 5601 for index inspection
   - Provisions Ollama container with `Qwen3-Embedding-8B` embedding model (4096 dimensions)
   - Coordinates service startup and health checks

2. **ElasticDemo.Api** ([Program.cs](src/ElasticDemo.Api/Program.cs), [Products feature](src/ElasticDemo.Api/Features/Products/)) - Web API service
   - Minimal APIs pattern with feature-based folder structure

3. **ElasticDemo.ServiceDefaults** - Shared configuration
   - OpenTelemetry, HTTP resilience policies, health checks (`/health`, `/alive`)

## Handler Conventions

- **Feature folders**: Organize handlers by feature in `Features/{FeatureName}/` directories
- **Primary constructors**: Use C# primary constructors for dependency injection
- **DTOs in same file**: Place Request/Response records in the same file as the handler, but at namespace level (not nested inside the class)
- **Naming**: `{Action}Handler` for classes, `{Action}{Feature}Request/Response` for DTOs
- **Registration**: Handlers are registered as `AddScoped<T>()` in Program.cs and resolved inline in `MapPost`/`MapGet`/`MapDelete` lambdas — no MediatR or endpoint extension classes

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
| POST | `/api/products/search` | Full-text search with filters |
| POST | `/api/products/semantic-search` | Vector/semantic search using embeddings |
| POST | `/api/products` | Create product |
| GET | `/api/products/{id}` | Get product by ID |
| DELETE | `/api/products/{id}` | Delete product |

Search supports: query text (fuzzy matching on name/description), category filter, price range, pagination (from/size).

Semantic search supports: natural language queries (kNN on embeddings), category filter, price range, k (results), numCandidates.

## Product Model

```csharp
Product {
    Id (string), Name (string), Description (string),
    Category (string), Price (decimal), Tags (List<string>),
    InStock (bool), CreatedAt (DateTimeOffset),
    Variants (List<ProductVariant>),
    Embedding (float[]?) // 4096-dim vector from Qwen3-Embedding-8B
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
- Elasticsearch: `http://localhost:9200`
- Kibana: `http://localhost:5601`
- OpenAPI spec: `/openapi/v1.json`

## Coding Conventions

- **Latest NuGet versions** - Always use the latest stable version of NuGet packages when adding new dependencies
- **Immutable types** - Prefer `record` with `init` properties over mutable `class` with `set`
- **DateTimeOffset** - Use `DateTimeOffset` instead of `DateTime`
- **TimeProvider** - Use `TimeProvider` instead of `DateTime.Now`/`DateTime.UtcNow`/`DateTimeOffset.UtcNow` for testability
- **Do not read `sample-products.json`** - This is a large generated file; do not read it for context
- **Do not commit `sample-products.json`** - This file is regenerated; exclude it from commits

## Working Style

- **Limit exploration** - Don't over-explore or spawn multiple agents for simple tasks. Read relevant files directly instead of extensive codebase searches.
- **Be concise** - Avoid lengthy research sessions. If a task is straightforward, just do it.
- **Ask early** - If there are significant trade-offs or limitations, ask the user before deep-diving.
- **File-based tools** - Tool scripts in `tools/` should use .NET 10 file-based programs (single `.cs` file without a `.csproj`). This keeps utility scripts simple and portable.

## Regenerating Sample Products

```bash
dotnet run tools/ProductGenerator/generate-products.cs
```

Output: `src/ElasticDemo.Api/Features/Products/sample-products.json` (1000 products across 6 categories)

## Claude Code Skills

Available slash commands for common tasks:

| Skill | Description | Example |
|-------|-------------|---------|
| `/run` | Start Aspire application in background | `/run` |
| `/stop` | Stop the running Aspire application | `/stop` |
| `/add-product` | Add a product to the index | `/add-product iPhone 17 Pro, Electronics, $1199` |
| `/semantic-search` | Vector similarity search | `/semantic-search lightweight running shoes under $150` |
| `/seed-reset` | Reset and reseed the products index | `/seed-reset` |
| `/generate-products` | Regenerate sample-products.json | `/generate-products 500` |

## Debugging with Aspire MCP Server

When debugging issues with the Aspire MCP server tools:
1. Check resource status with `list_resources`
2. If a resource isn't running, check `list_console_logs` for startup errors
3. For request issues, use `list_traces` to find the trace, then `list_trace_structured_logs` for details
