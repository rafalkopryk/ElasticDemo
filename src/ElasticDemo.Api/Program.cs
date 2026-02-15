using ElasticDemo.Api.Features.Applications;
using ElasticDemo.Api.Features.ApplicationsV2;
using ElasticDemo.Api.Features.Products;
using Microsoft.Extensions.AI;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

builder.AddServiceDefaults();
builder.AddElasticsearchClient("elasticsearch",
configureClientSettings: (seetings => seetings.EnableHttpCompression(false)));
if (builder.Configuration.GetValue<bool>("Embeddings:UseMock"))
{
    builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, MockEmbeddingGenerator>();
}
else
{
    builder.AddOllamaApiClient("ollama-Qwen3-Embedding")
        .AddEmbeddingGenerator();
}

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EmbeddingService>();

// Register handlers
builder.Services.AddScoped<InitializeIndexHandler>();
builder.Services.AddScoped<SeedProductsHandler>();
builder.Services.AddScoped<SearchProductsHandler>();
builder.Services.AddScoped<CreateProductHandler>();
builder.Services.AddScoped<GetProductHandler>();
builder.Services.AddScoped<DeleteProductHandler>();
builder.Services.AddScoped<UpdateProductHandler>();
builder.Services.AddScoped<UpdateProductV2Handler>();
builder.Services.AddScoped<SemanticSearchHandler>();
builder.Services.AddScoped<ArchiveProductsHandler>();
builder.Services.AddScoped<InitializeApplicationIndexHandler>();
builder.Services.AddScoped<SeedApplicationsHandler>();
builder.Services.AddScoped<SearchApplicationsHandler>();
builder.Services.AddScoped<InitializeApplicationV2IndexHandler>();
builder.Services.AddScoped<SeedApplicationsV2Handler>();
builder.Services.AddScoped<SearchApplicationsV2Handler>();
builder.Services.AddScoped<MigrateApplicationsV2Handler>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ElasticDemo API");
    });
}

app.UseHttpsRedirection();

// Map endpoints - resolve handlers from DI
app.MapPost("/api/products/init", async (InitializeIndexHandler handler) =>
    await handler.Handle())
    .WithName("InitializeProductIndex");

app.MapPost("/api/products/seed", async (SeedProductsHandler handler, HttpRequest request) =>
    await handler.Handle(request.Body))
    .WithName("SeedProducts");

app.MapPost("/api/products/search", async (
    SearchProductsHandler handler,
    SearchProductsRequest request) =>
    await handler.Handle(request))
    .WithName("SearchProducts");

app.MapPost("/api/products/semantic-search", async (
    SemanticSearchHandler handler,
    SemanticSearchRequest request) =>
    await handler.Handle(request))
    .WithName("SemanticSearchProducts");

app.MapPost("/api/products/archive", async (ArchiveProductsHandler handler) =>
    await handler.Handle())
    .WithName("ArchiveProducts");

app.MapPost("/api/products", async (CreateProductHandler handler, CreateProductRequest request) =>
    await handler.Handle(request))
    .WithName("CreateProduct");

app.MapGet("/api/products/{id}", async (GetProductHandler handler, string id) =>
    await handler.Handle(id))
    .WithName("GetProduct");

app.MapPut("/api/products/{id}", async (UpdateProductHandler handler, string id, UpdateProductRequest request) =>
    await handler.Handle(id, request))
    .WithName("UpdateProduct");

app.MapPut("/api/products/v2/{id}", async (UpdateProductV2Handler handler, string id, UpdateProductV2Request request) =>
    await handler.Handle(id, request))
    .WithName("UpdateProductV2");

app.MapDelete("/api/products/{id}", async (DeleteProductHandler handler, string id) =>
    await handler.Handle(id))
    .WithName("DeleteProduct");

app.MapPost("/api/applications/init", async (InitializeApplicationIndexHandler handler) =>
    await handler.Handle())
    .WithName("InitializeApplicationIndex");

app.MapPost("/api/applications/seed", async (SeedApplicationsHandler handler, HttpRequest request) =>
    await handler.Handle(request.Body))
    .WithName("SeedApplications");

app.MapPost("/api/applications/search", async (
    SearchApplicationsHandler handler,
    SearchApplicationsRequest request) =>
    await handler.Handle(request))
    .WithName("SearchApplications");

app.MapPost("/api/applications/v2/init", async (InitializeApplicationV2IndexHandler handler) =>
    await handler.Handle())
    .WithName("InitializeApplicationV2Index");

app.MapPost("/api/applications/v2/seed", async (SeedApplicationsV2Handler handler, HttpRequest request) =>
    await handler.Handle(request.Body))
    .WithName("SeedApplicationsV2");

app.MapPost("/api/applications/v2/search", async (
    SearchApplicationsV2Handler handler,
    SearchApplicationsV2Request request) =>
    await handler.Handle(request))
    .WithName("SearchApplicationsV2");

app.MapPost("/api/applications/v2/migrate", async (MigrateApplicationsV2Handler handler) =>
    await handler.Handle())
    .WithName("MigrateApplicationsV2");

app.Run();
