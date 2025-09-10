using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DatadogOtelPocTests;

public class OtlpInstrumentation : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly ActivitySource _activitySource;

    /// <summary>
    /// Create instrumentation with explicit configuration objects
    /// </summary>
    public OtlpInstrumentation(OtlpConfiguration otlpConfig, DatadogConfiguration? datadogConfig = null)
    {
        _activitySource = new ActivitySource(otlpConfig.ActivitySourceName);
        
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(otlpConfig.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(otlpConfig.ServiceName, otlpConfig.ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["test.framework"] = otlpConfig.TestFramework,
                    ["environment"] = otlpConfig.Environment,
                    ["service.namespace"] = otlpConfig.ServiceNamespace
                }))
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpConfig.Endpoint);
                options.Protocol = otlpConfig.Protocol;
                options.TimeoutMilliseconds = otlpConfig.TimeoutMilliseconds;
                
                // Configure headers based on whether we have Datadog config or custom headers
                if (datadogConfig != null && !string.IsNullOrEmpty(datadogConfig.ApiKey))
                {
                    options.Headers = $"DD-API-KEY={datadogConfig.ApiKey}";
                }
                else if (!string.IsNullOrEmpty(otlpConfig.Headers))
                {
                    options.Headers = otlpConfig.Headers;
                }
            })
            .AddConsoleExporter();

        _tracerProvider = builder.Build();
    }

    /// <summary>
    /// Create instrumentation with auto-loaded configuration (for xUnit IClassFixture)
    /// </summary>
    public static OtlpInstrumentation CreateForAgent()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var otlpConfig = config.GetSection("OtlpConfiguration").Get<OtlpConfiguration>() ?? new();
        return new OtlpInstrumentation(otlpConfig);
    }

    /// <summary>
    /// Create instrumentation with auto-loaded configuration for direct Datadog (for xUnit IClassFixture)
    /// </summary>
    public static OtlpInstrumentation CreateForDirectOtlp()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var otlpConfig = config.GetSection("OtlpConfiguration").Get<OtlpConfiguration>() ?? new();
        var datadogConfig = config.GetSection("DatadogConfiguration").Get<DatadogConfiguration>() ?? new();
        
        return new OtlpInstrumentation(otlpConfig, datadogConfig);
    }

    public ActivitySource ActivitySource => _activitySource;
    public TracerProvider TracerProvider => _tracerProvider;

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _activitySource?.Dispose();
    }
}
