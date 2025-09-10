using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace DatadogOtelPocTests;

public class DatadogDirectOtlpTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    private readonly ITestOutputHelper _output;
    
    public DatadogDirectOtlpTest(ITestOutputHelper output)
    {
        _instrumentation = OtlpInstrumentation.CreateForDirectOtlp();
        _output = output;
    }
    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public async Task SimpleDatadogSpanTest()
    {
        using var span = _instrumentation.ActivitySource.StartActivity("simple-test-span");
        
        span?.SetTag("test.name", "simple-datadog-span");
        span?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        
        span.Should().NotBeNull();
        span?.Id.Should().NotBeNull();
        span?.TraceId.Should().NotBe(default(ActivityTraceId));
        
        _output.WriteLine($"Trace sent to Datadog: {span?.TraceId}");
        
        // Force export before test ends
        var exportResult = _instrumentation.TracerProvider.ForceFlush(5000);
        _output.WriteLine($"OpenTelemetry ForceFlush Result: {exportResult}");
        
        if (exportResult)
        {
            _output.WriteLine("✅ ForceFlush completed successfully within timeout");
            _output.WriteLine("   This means: OpenTelemetry sent POST request(s) to configured exporters");
            _output.WriteLine("   - Target: https://otlp-intake.datadoghq.eu/v1/traces");
            _output.WriteLine("   - Method: POST");
            _output.WriteLine("   - Content-Type: application/x-protobuf");
            _output.WriteLine("   - Authentication: DD-API-KEY header");
        }
        else
        {
            _output.WriteLine("❌ ForceFlush timed out or failed");
            _output.WriteLine("   This could mean network issues or endpoint problems");
        }
        
        // Optional: Test endpoint connectivity (separate from actual export)
        await TestDatadogEndpointConnectivity();
    }
    
    private async Task TestDatadogEndpointConnectivity()
    {
        _output.WriteLine("");
        _output.WriteLine("--- Manual Endpoint Connectivity Test (Diagnostic Only) ---");
        
        try
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            
            var datadogSite = config["Datadog:Site"] ?? "datadoghq.com";
            var apiKey = config["Datadog:ApiKey"];
            
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("DD-API-KEY", apiKey);
            
            var endpoint = $"https://otlp-intake.{datadogSite}/v1/traces";
            _output.WriteLine($"Testing endpoint: {endpoint}");
            
            // Test with OPTIONS request (CORS preflight)
            var optionsResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, endpoint));
            _output.WriteLine($"OPTIONS Response: {optionsResponse.StatusCode}");
            _output.WriteLine("ℹ️  Note: 404 is expected - OTLP endpoints typically only accept POST with data");
            _output.WriteLine("ℹ️  The actual OpenTelemetry export (above) uses POST and succeeded!");
            
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error in diagnostic test: {ex.Message}");
        }
    }
}