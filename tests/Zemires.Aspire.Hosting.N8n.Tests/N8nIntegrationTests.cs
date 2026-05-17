namespace Zemires.Aspire.Hosting.N8n.Tests;

public class N8nIntegrationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task N8n_Starts_And_HealthReady_Ok()
    {
        var n8nName = "n8n";

        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Zemires_Aspire_Hosting_N8n_AppHost>(TestContext.Current.CancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => { clientBuilder.AddStandardResilienceHandler(); });

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var httpClient = app.CreateHttpClient(n8nName);
        await resourceNotificationService.WaitForResourceAsync(n8nName, KnownResourceStates.Running, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var response = await httpClient.GetAsync("/healthz", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

}