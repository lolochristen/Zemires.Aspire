using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var env = builder.AddAzureContainerAppEnvironment("env");

//var rg = builder.AddParameter("resource-group", value: "rg-test");
//var lawName = builder.AddParameter("law-name", value: "law-test");
//var appiName = builder.AddParameter("appi-name", value: "appi-test");

var law = builder.AddAzureLogAnalyticsWorkspace("law");
    //.AsExisting(lawName, rg);
var appi = builder.AddAzureApplicationInsights("appi", law);
    //.AsExisting(appiName, rg);

var collector = builder.AddOpenTelemetryCollector("otel-collector")
    .WithOtlpExporter()
    .WithEnvironment((ctx) =>
    {
        ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_API_KEY"] = builder.Configuration["AppHost:OtlpApiKey"];
    })
    .WithConfig("./config.yaml")
    .WithReference(appi);

var n8n = builder.AddN8n("n8n")
    .WithDataBindMount("./.n8n_data")
    .WithInstanceOwner("admin@dev.local", "Admin", "Local")
    .WithOtlpExporter(OtlpProtocol.HttpProtobuf)
    .WithEnvironment(ctx =>
    {
        // Extension Method .WithOpenTelemetryCollectorRouting(collector) cannot be used as it throws error on azure prepare step.
        var endpoint = collector.Resource.GetEndpoint("http");
        if (!ctx.EnvironmentVariables.TryAdd("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint))
        {
            ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
        }

        ctx.EnvironmentVariables["N8N_OTEL_ENABLED"] = "true";
        ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_OTLP_ENDPOINT"] = ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"];
        ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_OTLP_HEADERS"] = ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"];
        ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_SERVICE_NAME"] = ctx.EnvironmentVariables["OTEL_SERVICE_NAME"];
    })
    .WaitFor(collector);

builder.Build().Run();
