using ElasticDemo.Api.Features.Products;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddElasticsearchClient("elasticsearch");
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

// Register handlers
builder.Services.AddScoped<InitializeIndexHandler>();
builder.Services.AddScoped<SeedProductsHandler>();
builder.Services.AddScoped<SearchProductsHandler>();
builder.Services.AddScoped<CreateProductHandler>();
builder.Services.AddScoped<GetProductHandler>();
builder.Services.AddScoped<DeleteProductHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map endpoints - resolve handlers from DI
app.MapPost("/api/products/init", async (InitializeIndexHandler handler) =>
    await handler.Handle())
    .WithName("InitializeProductIndex");

app.MapPost("/api/products/seed", async (SeedProductsHandler handler) =>
    await handler.Handle())
    .WithName("SeedProducts");

app.MapPost("/api/products/search", async (
    SearchProductsHandler handler,
    SearchProductsRequest request) =>
    await handler.Handle(request))
    .WithName("SearchProducts");

app.MapPost("/api/products", async (CreateProductHandler handler, CreateProductRequest request) =>
    await handler.Handle(request))
    .WithName("CreateProduct");

app.MapGet("/api/products/{id}", async (GetProductHandler handler, string id) =>
    await handler.Handle(id))
    .WithName("GetProduct");

app.MapDelete("/api/products/{id}", async (DeleteProductHandler handler, string id) =>
    await handler.Handle(id))
    .WithName("DeleteProduct");

app.Run();
