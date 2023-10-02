// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Core;
using Azure.Core.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Aspire.Azure.Common;

internal abstract class AzureComponent<TSettings, TClient, TClientOptions>
    where TSettings : class, new()
    where TClient : class
    where TClientOptions : class
{
    protected virtual string[] ActivitySourceNames => new[] { $"{typeof(TClient).Namespace}.{typeof(TClient).Name}" };

    // There would be no need for Get* methods if TSettings had a common base type or if it was implementing a shared interface.
    // TSettings is a public type and we don't have a shared package yet, but we may reconsider the approach in near future.
    protected abstract bool GetHealthCheckEnabled(TSettings settings);

    protected abstract bool GetTracingEnabled(TSettings settings);

    protected abstract TokenCredential? GetTokenCredential(TSettings settings);

    protected abstract void Validate(TSettings settings, string configurationSectionName);

    protected abstract IAzureClientBuilder<TClient, TClientOptions> AddClient<TBuilder>(TBuilder azureFactoryBuilder, TSettings settings)
        where TBuilder : IAzureClientFactoryBuilder, IAzureClientFactoryBuilderWithCredential;

    protected abstract IHealthCheck CreateHealthCheck(TClient client, TSettings settings);

    protected virtual void LoadCustomSettings(TSettings settings, IConfiguration rootConfiguration, string configurationSectionName) { }

    internal static string GetKeyedConfigurationSectionName(string key, string defaultConfigSectionName)
        => $"{defaultConfigSectionName}:{key}";

    internal void AddClient(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<TSettings>? configureSettings,
        Action<IAzureClientBuilder<TClient, TClientOptions>>? configureClientBuilder,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configSection = builder.Configuration.GetSection(configurationSectionName);

        var settings = new TSettings();
        configSection.Bind(settings);

        LoadCustomSettings(settings, builder.Configuration, configurationSectionName);

        configureSettings?.Invoke(settings);

        Validate(settings, configurationSectionName);

        if (!string.IsNullOrEmpty(name))
        {
            // When named client registration is used (.WithName), Microsoft.Extensions.Azure
            // TRIES to register a factory for given client type and later
            // a call to serviceProvider.GetService<TClient> throws InvalidOperationException:
            // "Unable to find client registration with type 'SecretClient' and name 'Default'."
            // It's not desired, as Microsoft.Extensions.DependencyInjection keyed services
            // factory methods just return null in such cases.
            // To align the behavior across the Components, a null factory is registered up-front.
            builder.Services.AddSingleton<TClient>(static _ => null!);
        }

        builder.Services.AddAzureClients(azureFactoryBuilder =>
        {
            var secretClientBuilder = AddClient(azureFactoryBuilder, settings);

            if (GetTokenCredential(settings) is { } credential)
            {
                secretClientBuilder.WithCredential(credential);
            }

            secretClientBuilder.ConfigureOptions(configSection.GetSection("ClientOptions"));

            configureClientBuilder?.Invoke(secretClientBuilder);

            if (!string.IsNullOrEmpty(name))
            {
                // Set the name for the client registration.
                secretClientBuilder.WithName(name);

                // To resolve named clients IAzureClientFactory{TClient}.CreateClient needs to be used.
                builder.Services.AddKeyedSingleton(name,
                    static (serviceProvider, serviceKey) => serviceProvider.GetRequiredService<IAzureClientFactory<TClient>>().CreateClient((string)serviceKey!));
            }
        });

        if (GetHealthCheckEnabled(settings))
        {
            string namePrefix = $"Azure_{typeof(TClient).Name}";

            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                   name is null ? namePrefix : $"{namePrefix}_{name}",
                   serviceProvider =>
                   {
                       // From https://devblogs.microsoft.com/azure-sdk/lifetime-management-and-thread-safety-guarantees-of-azure-sdk-net-clients/:
                       // "The main rule of Azure SDK client lifetime management is: treat clients as singletons".
                       // So it's fine to root the client via the health check.
                       TClient client = name is null
                            ? serviceProvider.GetRequiredService<TClient>()
                            : serviceProvider.GetRequiredKeyedService<TClient>(name);

                       return CreateHealthCheck(client, settings);
                   },
                   failureStatus: default,
                   tags: default,
                   timeout: default));
        }

        if (GetTracingEnabled(settings))
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder => traceBuilder.AddSource(ActivitySourceNames));
        }
    }
}