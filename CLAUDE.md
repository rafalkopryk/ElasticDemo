# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ElasticDemo is a .NET Aspire-based distributed application demonstrating Elasticsearch integration with ASP.NET Core. It provides a REST API for managing and searching a product catalog and an application (loan/financial) registry.

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

Use `src/ElasticDemo.Api/products.http` and `src/ElasticDemo.Api/applications.http` to test API workflows:

**Products:** init, seed, search (text/semantic), CRUD, archive
**Applications:** init, seed (octet-stream), search (by product/channel/status/date/client across roles)

The files use `@baseUrl` variable - modify for HTTPS if needed.

## Architecture

Three-project structure following .NET Aspire patterns:

1. **ElasticDemo.AppHost** ([AppHost.cs](src/ElasticDemo.AppHost/AppHost.cs)) - Aspire orchestration host
   - Manages Elasticsearch container (security disabled, no auth) with persistent volume
   - Provisions Kibana container on port 5601 for index inspection
   - Provisions Ollama container with `Qwen3-Embedding-8B` embedding model (4096 dimensions)
   - Coordinates service startup and health checks

2. **ElasticDemo.Api** ([Program.cs](src/ElasticDemo.Api/Program.cs), [Products feature](src/ElasticDemo.Api/Features/Products/), [Applications feature](src/ElasticDemo.Api/Features/Applications/)) - Web API service
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
| POST | `/api/products/seed` | Seed products from raw JSON body stream |
| POST | `/api/products/search` | Full-text search with filters |
| POST | `/api/products/semantic-search` | Vector/semantic search using embeddings |
| POST | `/api/products/archive` | Move products older than 1 year to archive indices |
| POST | `/api/products` | Create product |
| GET | `/api/products/{id}` | Get product by ID |
| DELETE | `/api/products/{id}` | Delete product |
| POST | `/api/applications/init` | Initialize applications Elasticsearch index |
| POST | `/api/applications/seed` | Seed applications from raw JSON body stream |
| POST | `/api/applications/search` | Search applications with filters |

**Products search** supports: query text (fuzzy matching on name/description), category filter, price range, date range, pagination (from/size).

**Semantic search** supports: natural language queries (kNN on embeddings), category filter, price range, date range, k (results), numCandidates.

**Archive** moves products with `CreatedAt` older than 1 year from the active index to yearly `products-archive-{year}` indices using ES Reindex + DeleteByQuery.

**Applications search** supports: product, transaction, channel, status, date range, client lookup by firstName/lastName/nationalId/clientId/email across roles (MainClient, Spouse, CoApplicant), pagination (size), sort order (asc/desc by createdAt). Spouse role searches across both mainApplicant.spouse and coApplicants.spouse.

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

Elasticsearch indices use an active/archive partitioning strategy:
- **Active index** (`products`): all CRUD operations target this index only
- **Archive indices** (`products-archive-{year}`): read-only via search
- **Search optimization**: `ProductIndex.IndicesForSearch()` computes target indices at the application level — skips archives when the requested date range falls entirely within the last year

Approximately 30% of sample products include 2-5 variants with different colors, sizes (optional), price adjustments, and stock levels.

## Application Model

```csharp
Application {
    Id (string), Product (string), Transaction (string),
    Channel (string), Branch (string?), Status (string),
    User (string), CreatedAt (DateTimeOffset), UpdatedAt (DateTimeOffset),
    MainApplicant (Applicant),
    CoApplicants (List<Applicant>) // nested mapping
}

Applicant {
    Client (Client), Spouse (Client?)
}

Client {
    Email (string), FirstName (string), LastName (string),
    NationalId (string), ClientId (string)
}
```

Elasticsearch index: `applications` (single active index, keyword fields with lowercase normalizer for client name/email fields, nested mapping for CoApplicants). Each Applicant contains a Client sub-object and an optional Spouse sub-object.

The seed endpoint accepts a raw JSON body stream (`application/octet-stream`) and streams it into Elasticsearch in batches of 10,000 using `JsonSerializer.DeserializeAsyncEnumerable`.

- **Do not read `applications.json`** — This is a large generated file; do not read it for context
- **Do not commit `applications.json`** — This file is regenerated; exclude it from commits

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

## Regenerating Sample Applications

```bash
dotnet run tools/ApplicationGenerator/generate-applications.cs
```

Output: `src/ElasticDemo.Api/applications.json` (default 1000 applications with Polish client names)

Supports `--count N` and `--indented` flags.

## Claude Code Skills

Available slash commands for common tasks:

| Skill | Description | Example |
|-------|-------------|---------|
| `/run` | Start Aspire application in background | `/run` |
| `/stop` | Stop the running Aspire application | `/stop` |
| `/add-product` | Add a product to the index | `/add-product iPhone 17 Pro, Electronics, $1199` |
| `/semantic-search` | Vector similarity search | `/semantic-search lightweight running shoes under $150` |
| `/seed-reset` | Reset and reseed indices (products/applications/all) | `/seed-reset applications` |
| `/generate-products` | Regenerate sample-products.json | `/generate-products 500` |
| `/generate-applications` | Regenerate applications.json | `/generate-applications 10000` |

## Debugging with Aspire MCP Server

When debugging issues with the Aspire MCP server tools:
1. Check resource status with `list_resources`
2. If a resource isn't running, check `list_console_logs` for startup errors
3. For request issues, use `list_traces` to find the trace, then `list_trace_structured_logs` for details
