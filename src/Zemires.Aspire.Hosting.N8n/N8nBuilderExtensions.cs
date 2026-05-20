using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.N8n;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding N8n resources to the application model.
/// </summary>
public static class N8nBuilderExtensions
{
    private const int N8nPort = 5678;

    /// <summary>
    /// Adds an n8n container resource to the application model.
    /// The default image is <inheritdoc cref="N8nContainerImageTags.Image"/> and the tag is <inheritdoc cref="N8nContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <param name="encryptionKey">The parameter used to provide the master key for the N8n. If <see langword="null"/> a random master key will be generated.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var N8n = builder.AddN8n("n8n");
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<N8nResource> AddN8n(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? encryptionKey = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var encryptionKeyParameter = encryptionKey?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-encryption-key");

        var N8n = new N8nResource(name, encryptionKeyParameter);

        var n8nBuilder = builder.AddResource(N8n)
            .WithImage(N8nContainerImageTags.Image, N8nContainerImageTags.Tag)
            .WithImageRegistry(N8nContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: N8nPort, port: port, name: N8nResource.PrimaryEndpointName, env: "N8N_PORT")
            .WithHttpHealthCheck("/healthz", 200, N8nResource.PrimaryEndpointName)
            .WithIconName("BranchFork", IconVariant.Regular)
            .WithEnvironment("OFFLOAD_MANUAL_EXECUTIONS_TO_WORKERS", "true")
            .WithEnvironment("N8N_ENCRYPTION_KEY", encryptionKeyParameter)
            .WithEnvironment("WEBHOOK_URL", N8n.GetEndpoint(N8nResource.PrimaryEndpointName, builder.ExecutionContext.IsPublishMode ? KnownNetworkIdentifiers.PublicInternet : KnownNetworkIdentifiers.LocalhostNetwork));


#pragma warning disable ASPIRECERTIFICATES001
        n8nBuilder.WithHttpsCertificateConfiguration(ctx =>
        {
            ctx.EnvironmentVariables["N8N_PROTOCOL"] = "https";
            ctx.EnvironmentVariables["N8N_SSL_KEY"] = ctx.KeyPath;
            ctx.EnvironmentVariables["N8N_SSL_CERT"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificatePath;
            return Task.CompletedTask;
        });
#pragma warning restore ASPIRECERTIFICATES001

        if (builder.ExecutionContext.IsRunMode)
        {
#pragma warning disable ASPIRECERTIFICATES001
            builder.Eventing.Subscribe<BeforeStartEvent>((@event, cancellationToken) =>
            {
                var developerCertificateService = @event.Services.GetRequiredService<IDeveloperCertificateService>();

                bool addHttps = false;
                if (!n8nBuilder.Resource.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var annotation))
                {
                    if (developerCertificateService.UseForHttps)
                    {
                        addHttps = true;
                    }
                }
                else if (annotation.UseDeveloperCertificate.GetValueOrDefault(developerCertificateService.UseForHttps) || annotation.Certificate is not null)
                {
                    addHttps = true;
                }

                if (addHttps)
                {
                    // If a TLS certificate is configured, override the endpoint to use HTTPS instead of HTTP
                    n8nBuilder.WithEndpoint(N8nResource.PrimaryEndpointName, ep => ep.UriScheme = "https");
                }

                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001
        }

        return n8nBuilder;
    }

    /// <summary>
    /// Adds a named volume for the data folder to a N8n container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var N8n = builder.AddN8n("N8n")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(N8n);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<N8nResource> WithDataVolume(this IResourceBuilder<N8nResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/home/node/.n8n");
    }

    /// <summary>
    /// Configures the N8n resource to use a PostgreSQL database.
    /// This method reads connection properties from the provided <paramref name="database"/>
    /// (which must implement <see cref="IResourceWithConnectionString"/>) and sets the
    /// environment variables required by the n8n image to connect to a Postgres backend.
    /// It also creates a reference relationship and waits for the database resource.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="database">A resource builder for the PostgreSQL database. Must expose connection string information.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided <paramref name="database"/> does not expose connection string information.</exception>
    public static IResourceBuilder<N8nResource> WithPostgresDatabase(this IResourceBuilder<N8nResource> builder, IResourceBuilder<IResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        if (database.Resource is IResourceWithConnectionString resourceWithConnection)
        {
            return builder.WithEnvironment("DB_TYPE", "postgresdb")
                .WithEnvironment("DB_POSTGRESDB_DATABASE", $"{resourceWithConnection.GetConnectionProperty("DatabaseName")}")
                .WithEnvironment("DB_POSTGRESDB_HOST", $"{resourceWithConnection.GetConnectionProperty("Host")}")
                .WithEnvironment("DB_POSTGRESDB_PORT", $"{resourceWithConnection.GetConnectionProperty("Port")}")
                .WithEnvironment("DB_POSTGRESDB_USER", $"{resourceWithConnection.GetConnectionProperty("Username")}")
                .WithEnvironment("DB_POSTGRESDB_PASSWORD", $"{resourceWithConnection.GetConnectionProperty("Password")}")
                .WithReferenceRelationship(database)
                .WaitFor(database);
        }
        else
        {
            throw new ArgumentException($"The provided resource '{database.Resource.Name}' does not contain connection string information and cannot be used as a database for N8n.", nameof(database));
        }
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a n8n container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var n8n = builder.AddN8n("n8n")
    /// .WithDataBindMount("./data/N8n/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(n8n);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<N8nResource> WithDataBindMount(this IResourceBuilder<N8nResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/home/node/.n8n");
    }

    /// <summary>
    /// Configures the N8n resource to run in queue mode using a Redis instance.
    /// This sets the necessary environment variables (host, port, password and TLS) from
    /// the provided <paramref name="redis"/> resource and creates a parent/reference
    /// relationship so containers start in the correct order.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="redis">A resource builder for the Redis instance. Must expose connection string information.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided <paramref name="redis"/> does not expose connection string information.</exception>
    public static IResourceBuilder<N8nResource> WithQueueMode(this IResourceBuilder<N8nResource> builder, IResourceBuilder<IResource> redis)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        if (redis.Resource is IResourceWithConnectionString resourceWithConnection)
        {
            return builder.WithEnvironment("EXECUTIONS_MODE", "queue")
                .WithEnvironment("QUEUE_BULL_REDIS_HOST", $"{resourceWithConnection.GetConnectionProperty("Host")}")
                .WithEnvironment("QUEUE_BULL_REDIS_PORT", $"{resourceWithConnection.GetConnectionProperty("Port")}")
                .WithEnvironment("QUEUE_BULL_REDIS_PASSWORD", $"{resourceWithConnection.GetConnectionProperty("Password")}")
                .WithEnvironment("QUEUE_BULL_REDIS_TLS", "true")
                .WithReferenceRelationship(redis)
                .WaitFor(redis);
        }
        else
        {
            throw new ArgumentException($"The provided resource '{redis.Resource.Name}' does not contain connection string information and cannot be used as a redis for N8n.", nameof(redis));
        }
    }

    /// <summary>
    /// Adds a worker instance for the given N8n resource.
    /// Worker instances run the n8n process in "worker" mode and are configured to
    /// share the same encryption key and webhook configuration as the primary N8n resource.
    /// The worker is created as a child of the main N8n resource so lifecycle and ordering
    /// are handled automatically.
    /// </summary>
    /// <param name="n8nBuilder">The primary N8n resource builder to attach the worker to.</param>
    /// <param name="name">The name to use for the worker resource.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A new <see cref="IResourceBuilder{N8nResource}"/> for the worker instance.</returns>
    public static IResourceBuilder<N8nWorkerResource> AddWorker(this IResourceBuilder<N8nResource> n8nBuilder, string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(n8nBuilder);

        // worker does not support https
        var worker = new N8nWorkerResource(n8nBuilder.Resource.Name + "-" + name, n8nBuilder.Resource);

        var workerBuilder = n8nBuilder.ApplicationBuilder.AddResource(worker)
            .WithImage(N8nContainerImageTags.Image, N8nContainerImageTags.Tag)
            .WithImageRegistry(N8nContainerImageTags.Registry)
            .WithArgs("worker")
            .WithIconName("SettingsCogMultiple", IconVariant.Filled)
            .WithHttpEndpoint(targetPort: N8nPort, port: port, name: N8nResource.PrimaryEndpointName, env: "N8N_PORT")
            .WithHttpHealthCheck("/healthz", 200, N8nResource.PrimaryEndpointName)
            .WithEnvironment("N8N_ENCRYPTION_KEY", n8nBuilder.Resource.EncryptionKeyParameter)
            .WithEnvironment("WEBHOOK_URL", n8nBuilder.GetEndpoint(N8nResource.PrimaryEndpointName, n8nBuilder.ApplicationBuilder.ExecutionContext.IsPublishMode ? KnownNetworkIdentifiers.PublicInternet : KnownNetworkIdentifiers.LocalhostNetwork))
            .WithEnvironment("QUEUE_HEALTH_CHECK_ACTIVE", "true")
            .WithParentRelationship(n8nBuilder);

        if (n8nBuilder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            // to accept dev certificate of redis in run mode
#pragma warning disable ASPIRECERTIFICATES001
            workerBuilder.WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificatePath; 
                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001
        }

        return workerBuilder;
    }

    /// <summary>
    /// Sets the timezone for the N8n container by configuring the standard
    /// environment variables used by the image (GENERIC_TIMEZONE and TZ).
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="timeZone">The timezone identifier (for example "UTC" or "America/Los_Angeles").</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    public static IResourceBuilder<N8nResource> WithTimeZone(this IResourceBuilder<N8nResource> builder, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithEnvironment("GENERIC_TIMEZONE", timeZone)
            .WithEnvironment("TZ", timeZone);
    }

    /// <summary>
    /// Configures the resource builder to enable OpenTelemetry Protocol (OTLP) exporting for N8n resources using the
    /// HTTP/Protobuf protocol.
    /// </summary>
    /// <remarks>This method sets environment variables required for OTLP integration with N8n, including
    /// enabling OTEL support and mapping standard OTEL environment variables to N8n-specific variables. Use this method
    /// to ensure that telemetry data is exported from N8n resources in a compatible format.</remarks>
    /// <param name="builder">The resource builder to configure for OTLP exporting. Cannot be null.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    public static IResourceBuilder<N8nResource> WithOtlpExporter(this IResourceBuilder<N8nResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithOtlpExporter(OtlpProtocol.HttpProtobuf)
            .WithEnvironment(ctx =>
            {
                ctx.EnvironmentVariables["N8N_OTEL_ENABLED"] = "true";
                ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_OTLP_ENDPOINT"] = ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"];
                ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_OTLP_HEADERS"] = ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"];
                ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_SERVICE_NAME"] = ctx.EnvironmentVariables["OTEL_SERVICE_NAME"];
            });
    }
}