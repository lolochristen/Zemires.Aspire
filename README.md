# Zemires.Aspire

.NET Aspire Extensions for n8n.

## Overview

This repository contains libraries, an OpenAPI-generated n8n client, an example AppHost, playground projects, and tests to demonstrate Aspire integration patterns.

- Libraries: `src/*`
- Example AppHost: `examples/Zemires.Aspire.Hosting.N8n.AppHost`
- Tests: `tests/*`

## Prerequisites

- .NET SDK 10 (targets net10.0)
- Git
- Kiota (only needed to regenerate the n8n client)

## Build

Restore and build the full solution:

    dotnet restore
    dotnet build Zemires.Aspire.slnx

## Run the example AppHost

The `examples/Zemires.Aspire.Hosting.N8n.AppHost` project demonstrates registering an n8n resource with an Aspire AppHost.

Run the example AppHost locally:

    dotnet run --project examples\Zemires.Aspire.Hosting.N8n.AppHost\Zemires.Aspire.Hosting.N8n.AppHost.csproj

Example AppHost entry (examples/Zemires.Aspire.Hosting.N8n.AppHost/AppHost.cs):

    var builder = DistributedApplication.CreateBuilder(args);

    var n8n = builder.AddN8n("n8n");

    builder.Build().Run();

This registers a resource named "n8n" with the distributed application host. The host will manage that resource and expose per-resource HTTP clients in tests and when running the AppHost.

## Advanced Scenario

Setup n8n using workers communicating via Postgres database and Redis queue including OopenTelemetry support:

    var n8n = builder.AddN8n("n8n", port: 5678)
        .WithDataBindMount("./.n8n_data")
        .WithPostgresDatabase(db)
        .WithQueueMode(redis)
        .WithTimeZone("Europe/Zurich")
        .WithOtlpExporter();

    var worker = n8n.AddWorker("n8n-worker", port: 5679)
        .WithPostgresDatabase(db)
        .WithQueueMode(redis)
        .WithTimeZone("Europe/Zurich")
        .WithOtlpExporter();


## Contributing

Follow the existing project layout when adding new projects: `src/` for libraries, `examples/` for runnable hosts, `playground/` for experiments, `tests/` for tests. Keep project references relative.

## License & Support

See LICENSE if present. Open issues for questions or feature requests.
