var builder = DistributedApplication.CreateBuilder(args);

var entraIdApp = builder.AddBicepTemplate("entraid-app", "appregistration.bicep")
    .WithParameter("cloudEnvironment", "AzureCloud")
    .WithParameter("clientAppName", "n8n-test-app")
    .WithParameter("clientAppDisplayName", "n8n SSO Test Application")
    .WithParameter("webAppEndpoint", "https://wedontknowyet.com");

var n8n = builder.AddN8n("n8n", port: 5678)
    .WithDataBindMount("./.n8n_data")
    .WithOtlpExporter()
    .WithInstanceOwner("admin@dev.com", "Admin", "Dev")
    .WithLicenseKey("YOUR-ENTERPRISE-KEY");

n8n.WithEnvironment("N8N_SSO_MANAGED_BY_ENV", "true")
    .WithEnvironment("N8N_SSO_OIDC_LOGIN_ENABLED", "true")
    .WithEnvironment("N8N_SSO_OIDC_CLIENT_ID", entraIdApp.GetOutput("clientAppId"))
    .WithEnvironment("N8N_SSO_OIDC_CLIENT_SECRET", entraIdApp.GetOutput("clientSecret"))
    .WithEnvironment("N8N_SSO_OIDC_DISCOVERY_ENDPOINT", "https://login.microsoftonline.com/02f3babb-3706-4bcc-b909-60bad09450bd/v2.0/.well-known/openid-configuration");

builder.Build().Run();
