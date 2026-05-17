var builder = DistributedApplication.CreateBuilder(args);

var n8n = builder.AddN8n("n8n");

builder.Build().Run();
