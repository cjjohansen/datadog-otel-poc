using System;
using System.Diagnostics;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace DatadogOtelPoc;

public class SimpleAzureTelemetryTest : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly ActivitySource _activitySource;
    
    public SimpleAzureTelemetryTest()
    {
        _activitySource = new ActivitySource("simple.azure.test");
        
        // Pure OpenTelemetry setup - exports to Azure Application Insights via OTLP
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("simple.azure.test")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("simple-azure-test", "1.0.0"))
            .AddOtlpExporter(options =>
            {
                // Azure Application Insights OTLP endpoint
                options.Endpoint = new Uri("https://eastus-8.in.applicationinsights.azure.com/v1/traces");
                options.Headers = "x-api-key=5cfb4665-afa2-4c29-a189-cdedc4f5d2e3";
            })
            .Build();
    }

    [Fact]
    public void SimpleAzureConnectivityTest()
    {
        // Create a span - this is the fundamental OpenTelemetry unit
        using var span = _activitySource.StartActivity("simple-azure-test");
        
        // Set span attributes
        span?.SetTag("test.name", "azure-connectivity");
        span?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        
        // Verify span was created with proper trace context
        span.Should().NotBeNull();
        span?.Id.Should().NotBeNull(); // This is the span ID
        span?.TraceId.Should().NotBe(default(ActivityTraceId)); // This is the trace ID
        
        // The span will be automatically exported to Azure when disposed
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _activitySource?.Dispose();
    }
}