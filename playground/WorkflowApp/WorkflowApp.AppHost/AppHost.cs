var builder = DistributedApplication.CreateBuilder(args);

var adminApiKey = builder.AddParameter("AdminApiKey", true);

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
    .WithOtlpExporter()
    .WithReference(ollama)
    .WithEnvironment("CREDENTIALS_OVERWRITE_DATA", $"{{\"ollamaApi\":{{\"baseUrl\":\"{ollama.Resource.PrimaryEndpoint}\"}} }}")
    .WithEnvironment("CREDENTIALS_OVERWRITE_PERSISTENCE", "true");

var worker = n8n.AddWorker("worker", port: 5679)
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithTimeZone("Europe/Zurich")
    .WithOtlpExporter()
    .WithEnvironment("CREDENTIALS_OVERWRITE_DATA", $"{{\"ollamaApi\":{{\"baseUrl\":\"{ollama.Resource.PrimaryEndpoint}\"}} }}");

builder.AddProject<Projects.WorkflowApp_ApiService>("apiservice")
    .WithReference(n8n)
    .WaitFor(n8n)
    .WithEnvironment("Aspire__n8n__Client__ApiKey", adminApiKey);

builder.Build().Run();
