using System.Text.Json;
using Amazon.Runtime.CredentialManagement;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace MajakServer.Infrastructure;

internal static class ParameterStoreJsonConfiguration
{
    private const string DevelopmentPath = "/config/application_development/majak4";
    private const string AlphaPath = "/config/application_alpha/majak4";
    private const string ProductionPath = "/config/application_production/majak4";

    internal static async Task AddEnvironmentJsonAsync(WebApplicationBuilder builder)
    {
        var environmentName = builder.Environment.EnvironmentName;
        var parameterPath = environmentName switch
        {
            var name when string.Equals(name, Environments.Development, StringComparison.OrdinalIgnoreCase) => DevelopmentPath,
            var name when string.Equals(name, Environments.Production, StringComparison.OrdinalIgnoreCase) => ProductionPath,
            _ => AlphaPath,
        };
        var regionName = builder.Configuration["AWS:Region"]
            ?? Environment.GetEnvironmentVariable("AWS_REGION")
            ?? "ap-northeast-1";
        var region = Amazon.RegionEndpoint.GetBySystemName(regionName);

        using var client = CreateClient(builder.Environment, builder.Configuration, region);
        GetParameterResponse response;
        try
        {
            response = await client.GetParameterAsync(new GetParameterRequest
            {
                Name = parameterPath,
                WithDecryption = true,
            });
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to load application configuration from Parameter Store path '{parameterPath}'.",
                exception);
        }

        var json = response.Parameter?.Value;
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"Parameter Store path '{parameterPath}' returned an empty value.");

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The root value must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Parameter Store path '{parameterPath}' does not contain valid application JSON.",
                exception);
        }

        builder.Configuration.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
        builder.Configuration["AWS:ParameterStore:ResolvedPath"] = parameterPath;
    }

    private static IAmazonSimpleSystemsManagement CreateClient(
        IHostEnvironment environment,
        IConfiguration configuration,
        Amazon.RegionEndpoint region)
    {
        if (environment.IsDevelopment())
        {
            var profileName = configuration["AWS:ProfileName"]?.Trim();
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                var chain = new CredentialProfileStoreChain();
                if (chain.TryGetAWSCredentials(profileName, out var credentials))
                    return new AmazonSimpleSystemsManagementClient(credentials, region);
            }
        }

        return new AmazonSimpleSystemsManagementClient(region);
    }
}