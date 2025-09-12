using System;
using System.Diagnostics;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace DatadogOtelPocTests;

public class SimpleAzureTelemetryTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    
    public SimpleAzureTelemetryTest()
    {
        _instrumentation = OtlpInstrumentation.CreateForAgent();
    }

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public void SimpleAzureConnectivityTest()
    {
        // Create a span - this is the fundamental OpenTelemetry unit
        using var span = _instrumentation.ActivitySource.StartActivity("simple-azure-test");
        
        // Set span attributes
        span?.SetTag("test.name", "azure-connectivity");
        span?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        span?.SetTag("azure.environment", "test");
        span?.SetTag("azure.deployment", "local-agent");
        
        // Verify span was created with proper trace context
        span.Should().NotBeNull();
        span?.Id.Should().NotBeNull(); // This is the span ID
        span?.TraceId.Should().NotBe(default(ActivityTraceId)); // This is the trace ID
        
        // The span will be automatically exported to Azure via local agent when disposed
    }
}
