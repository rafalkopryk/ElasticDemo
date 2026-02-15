using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithImageTag("9.2.1")
    .WithEndpoint("http", e => e.Port = 9200)
    .WithEnvironment("xpack.security.enabled", "false")
    .WithDataVolume("elasticsearch-data");

builder.AddContainer("kibana", "kibana", "9.2.1")
    .WithHttpEndpoint(port: 5601, targetPort: 5601)
    .WithEnvironment("ELASTICSEARCH_HOSTS", elasticsearch.GetEndpoint("http"))
    .WaitFor(elasticsearch);

var useMock = builder.Configuration.GetValue<bool>("Embeddings:UseMock");

var api = builder.AddProject<Projects.ElasticDemo_Api>("api")
    .WithReference(elasticsearch)
    .WaitFor(elasticsearch);

if (useMock)
{
    api.WithEnvironment("Embeddings__UseMock", "true");
}
else
{
    var ollama = builder.AddOllamaLocal("ollama");
    var model = ollama.AddModel("Qwen3-Embedding");
    api.WithReference(model).WaitFor(model);
}

var toolsPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools"));

builder.AddK6("k6")
    .WithBindMount(toolsPath, "/scripts", isReadOnly: true)
    .WithEnvironment("K6_BASE_URL", api.GetEndpoint("http"))
    .WithArgs("run", "/scripts/k6-applications-search.js")
    .WaitFor(api);

builder.AddK6("k6-v2")
    .WithBindMount(toolsPath, "/scripts", isReadOnly: true)
    .WithEnvironment("K6_BASE_URL", api.GetEndpoint("http"))
    .WithArgs("run", "/scripts/k6-applications-v2-search.js")
    .WaitFor(api);

builder.Build().Run();
