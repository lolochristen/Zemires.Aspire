var builder = DistributedApplication.CreateBuilder(args);

var ownerPassword = builder.AddParameter("owner-password", true);
var licenseKey = builder.AddParameter("license-key", true);
var adminApiKey = builder.AddParameter("admin-api-key", true); // value needs to be created manually in n8n

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var db = postgres.AddDatabase("n8n-db");

var redis = builder.AddRedis("redis")
    .WithDataVolume();

//var ollama = builder.AddOllama("ollama"):
var ollama = builder.AddOllamaLocal("ollama");
var model_gemma4 = ollama.AddModel("gemma3");

var n8n = builder.AddN8n("n8n", port: 5678)
    .WithDataBindMount("./.n8n_data")
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithTimeZone("Europe/Zurich")
    .WithInstanceOwner("admin@dev.local", "Admin", "Local", ownerPassword)
    .WithLicenseKey(licenseKey)
    .WithOtlpExporter()
    .WithReference(ollama)
    .WithEnvironment("CREDENTIALS_OVERWRITE_DATA", $"{{\"ollamaApi\":{{\"baseUrl\":\"{ollama.Resource.PrimaryEndpoint}\"}} }}")
    .WithEnvironment("CREDENTIALS_OVERWRITE_PERSISTENCE", "true")
    .WithCommunityPackages("n8n-nodes-openapi-node@0.1.4");

var worker = n8n.AddWorker("worker", port: 5679)
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithTimeZone("Europe/Zurich")
    .WithOtlpExporter()
    .WithEnvironment("CREDENTIALS_OVERWRITE_DATA", $"{{\"ollamaApi\":{{\"baseUrl\":\"{ollama.Resource.PrimaryEndpoint}\"}} }}")
    .WithCommunityPackages("n8n-nodes-openapi-node@0.1.4");

var api = builder.AddProject<Projects.WorkflowApp_ApiService>("apiservice")
    .WithReference(n8n)
    .WaitFor(n8n)
    .WithEnvironment("Aspire__n8n__Client__ApiKey", adminApiKey);

worker.WithReference(api);

builder.Build().Run();
