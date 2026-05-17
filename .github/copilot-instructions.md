# Copilot instructions — Zemires.Aspire

Purpose: help future Copilot CLI sessions (and other automated assistants) make correct, targeted changes to this repository.

---

## Build, test, and lint commands

- Restore packages: `dotnet restore`
- Build solution: `dotnet build Zemires.Aspire.slnx`
- Run all tests: `dotnet test`
- Run a specific test project: `dotnet test tests\Zemires.Aspire.Hosting.N8n.Tests\Zemires.Aspire.Hosting.N8n.Tests.csproj`
- Run a single test (example):
  `dotnet test tests\Zemires.Aspire.Hosting.N8n.Tests\Zemires.Aspire.Hosting.N8n.Tests.csproj --filter "FullyQualifiedName~Namespace.ClassName.TestMethod"`
  (Partial name matches with `~` are supported.)
- Run an example AppHost: `dotnet run --project examples\Zemires.Aspire.Hosting.N8n.AppHost\Zemires.Aspire.Hosting.N8n.AppHost.csproj`
- Collect code coverage when running tests (coverlet):
  `dotnet test --collect:"XPlat Code Coverage"`
- Regenerate the N8n API client (project-local):
  From `src\Zemires.N8n.Api` run the command in that README: `kiota generate -d .\\n8n-api-1.yaml -l csharp -n Zemires.N8n.Api -c N8nClient -o .`

Notes: there is no repository CI workflow file in .github/workflows — CI commands should mirror the commands above.

---

## High-level architecture

- Solution: `Zemires.Aspire.slnx` ties together:
  - `src/Zemires.Aspire.Hosting.N8n` — core hosting integration library for Aspire and n8n.
  - `src/Zemires.Aspire.N8n` — supporting library code (helpers/adapters).
  - `src/Zemires.N8n.Api` — OpenAPI-generated client (Kiota) for n8n; a generator command is recorded in its README.
  - `examples/Zemires.Aspire.Hosting.N8n.AppHost` — AppHost sample using the Aspire.AppHost.Sdk.
  - `playground/WorkflowApp/*` — experimental sample apps (ApiService, AppHost, ServiceDefaults).
  - `tests/*` — integration/unit tests that reference example apphost and libraries.

- SDKs and framework:
  - Projects target `net10.0`.
  - App hosts use `Aspire.AppHost.Sdk` (examples reference version 13.3.1).
  - API services use `Microsoft.NET.Sdk.Web` where appropriate.

- Testing:
  - Tests use xUnit v3 (`xunit.v3`) and `Microsoft.NET.Test.Sdk`.
  - `Aspire.Hosting.Testing` is used for host integration testing in tests.
  - Coverlet collector is included for coverage data.

---

## Key conventions and repo-specific patterns

- Project layout: `src/` for libraries, `examples/` for runnable sample hosts, `playground/` for experiments, `tests/` for test projects. Follow this layout when adding new code.
- Tests reference example AppHost and library projects via relative `ProjectReference` paths. Keep references relative to the repo root to match existing structure.
- Generated API client: `src/Zemires.N8n.Api` is Kiota-generated. Keep the generator command and the source OpenAPI YAML next to that project; check the project README when regenerating.
- AppHost projects use the `Aspire.AppHost.Sdk` SDK (note the custom Sdk token in *.csproj). Preserve Sdk-specific msbuild properties when editing AppHost projects.
- Test csproj files include explicit `<Using>` elements and `IsTestProject` metadata — prefer adding test projects that mirror these settings so test discovery and shared usings behave consistently.
- Target framework is `net10.0`; new projects should default to that unless intentionally different.

---

## Where to look next

- `src/Zemires.N8n.Api/README.md` for the Kiota generation command.
- `tests/Zemires.Aspire.Hosting.N8n.Tests` to see how integration tests bootstrap AppHost and use `Aspire.Hosting.Testing`.

---

If this file needs to incorporate any existing assistant/agent configs (CLAUDE.md, AGENTS.md, .cursorrules, etc.), add them under a new "Assistant integration" section and reference those files.
