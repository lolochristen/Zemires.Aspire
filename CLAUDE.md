# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

.NET Aspire integration libraries for [n8n](https://n8n.io/) workflow automation. Targets `net10.0`.

## Commands

```shell
dotnet restore
dotnet build Zemires.Aspire.slnx
dotnet test
dotnet test tests\Zemires.Aspire.Hosting.N8n.Tests\Zemires.Aspire.Hosting.N8n.Tests.csproj
dotnet test tests\Zemires.Aspire.Hosting.N8n.Tests\Zemires.Aspire.Hosting.N8n.Tests.csproj --filter "FullyQualifiedName~TestMethod"
dotnet run --project examples\Zemires.Aspire.Hosting.N8n.AppHost\Zemires.Aspire.Hosting.N8n.AppHost.csproj
dotnet test --collect:"XPlat Code Coverage"
```

To regenerate the n8n API client (from `src\Zemires.N8n.Api`):

```shell
kiota generate -d .\n8n-api-1.yaml -l csharp -n Zemires.N8n.Api -c N8nClient -o .
```

## Architecture

**`src/Zemires.Aspire.Hosting.N8n`** — Aspire hosting extension. Extension methods in `N8nBuilderExtensions.cs` follow the `IDistributedApplicationBuilder` fluent API pattern. `N8nResource` extends `ContainerResource` and implements `IResourceWithConnectionString` (connection string = `{scheme}://{host}:{port}`). `N8nWorkerResource` extends `N8nResource` and implements `IResourceWithParent<N8nResource>` — workers share the parent's encryption key and are created via `AddWorker()`.

**`src/Zemires.Aspire.N8n`** — Client-side DI integration. `AspireN8nExtensions` adds `AddN8nClient()` / `AddKeyedN8nClient()` to `IHostApplicationBuilder`. Reads config from `Aspire:n8n:Client`, falls back to `ConnectionStrings:{name}`. Registers `N8nClient` as a singleton and wires up an `N8nHealthCheck` unless `DisableHealthChecks` is set.

**`src/Zemires.N8n.Api`** — Kiota-generated OpenAPI client for n8n. Do not hand-edit these files; regenerate from `n8n-api-1.yaml`. `N8nAuthenticationProvider.cs` is the only hand-written file in this project.

**`tests/`** — Integration tests use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Zemires_Aspire_Hosting_N8n_AppHost>()` to boot the example AppHost in-process, then call `app.CreateHttpClient(resourceName)` and wait for `KnownResourceStates.Running` via `ResourceNotificationService`.

## Key conventions

- Extension methods live in `namespace Aspire.Hosting` (hosting) or `namespace Microsoft.Extensions.Hosting` (client), not in the library's own namespace — this keeps intellisense discoverability consistent with the Aspire ecosystem.
- `WithPostgresDatabase` and `WithQueueMode` extract connection properties (Host, Port, Password, etc.) from `IResourceWithConnectionString` and map them to n8n environment variables.
- `WithOtlpExporter` calls `builder.WithOtlpExporter(OtlpProtocol.HttpProtobuf)` then re-maps the standard `OTEL_*` env vars to the `N8N_OTEL_*` equivalents.
- HTTPS is handled via `WithHttpsCertificateConfiguration` (pragma `ASPIRECERTIFICATES001` suppressed intentionally) and only applied in run mode; workers do not get HTTPS but do accept the dev CA cert via `NODE_EXTRA_CA_CERTS`.
- AppHost projects use `Sdk="Aspire.AppHost.Sdk"` in the csproj. Preserve this when editing.
- Test csproj files use xUnit v3 (`xunit.v3`) with explicit `<Using>` elements and `IsTestProject` metadata.
- No CI workflow exists in `.github/workflows/` — use the commands above locally.
