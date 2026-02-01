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

var ollama = builder.AddOllamaLocal("ollama");

var model = ollama.AddModel("Qwen3-Embedding");

builder.AddProject<Projects.ElasticDemo_Api>("api")
    .WithReference(elasticsearch)
    .WithReference(model)
    .WaitFor(elasticsearch)
    .WaitFor(model);

builder.Build().Run();
