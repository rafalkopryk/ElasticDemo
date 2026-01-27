var builder = DistributedApplication.CreateBuilder(args);

var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithEndpoint("http", e => e.Port = 9200)
    .WithEnvironment("xpack.security.enabled", "false")
    .WithDataVolume("elasticsearch-data");

builder.AddContainer("kibana", "kibana", "8.17.3")
    .WithHttpEndpoint(port: 5601, targetPort: 5601)
    .WithEnvironment("ELASTICSEARCH_HOSTS", elasticsearch.GetEndpoint("http"))
    .WaitFor(elasticsearch);

builder.AddProject<Projects.ElasticDemo_Api>("api")
    .WithReference(elasticsearch)
    .WaitFor(elasticsearch);

builder.Build().Run();
