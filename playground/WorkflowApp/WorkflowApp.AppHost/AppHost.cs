var builder = DistributedApplication.CreateBuilder(args);

var adminApiKey = builder.AddParameter("AdminApiKey", true);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var db = postgres.AddDatabase("n8n-db");

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var n8n = builder.AddN8n("n8n", port: 5678)
    .WithDataBindMount("./.n8n_data")
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithTimeZone("Europe/Zurich");

var worker = n8n.AddWorker("n8n-worker", port: 5679)
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithTimeZone("Europe/Zurich");

builder.AddProject<Projects.WorkflowApp_ApiService>("apiservice")
    .WithReference(n8n)
    .WaitFor(n8n)
    .WithEnvironment("Aspire__n8n__Client__ApiKey", adminApiKey);

builder.Build().Run();
