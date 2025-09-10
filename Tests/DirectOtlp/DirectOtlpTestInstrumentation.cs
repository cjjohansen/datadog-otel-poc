using System;
using Microsoft.Extensions.Configuration;

namespace DatadogOtelPocTests;

public class DirectOtlpTestInstrumentation : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;

    public DirectOtlpTestInstrumentation()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        // Load OTLP configuration from JSON section
        var otlpConfig = config.GetSection("OtlpTestConfiguration").Get<OtlpConfiguration>() ?? new();
        
        // Load Datadog configuration from JSON section  
        var datadogConfig = config.GetSection("Datadog").Get<DatadogConfiguration>() ?? new();

        // Create instrumentation with both OTLP and Datadog config
        _instrumentation = new OtlpInstrumentation(otlpConfig, datadogConfig);
    }

    public System.Diagnostics.ActivitySource ActivitySource => _instrumentation.ActivitySource;
    public OpenTelemetry.Trace.TracerProvider TracerProvider => _instrumentation.TracerProvider;

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }
}
