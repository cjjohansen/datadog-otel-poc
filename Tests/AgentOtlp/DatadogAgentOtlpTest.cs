using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace DatadogOtelPocTests;

public class DatadogAgentOtlpTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    private readonly ITestOutputHelper _output;
    
    public DatadogAgentOtlpTest(ITestOutputHelper output)
    {
        _instrumentation = OtlpInstrumentation.CreateForAgent();
        _output = output;
        _output.WriteLine("Using Datadog Agent for OTLP export");
    }

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public async Task SimpleAgentSpanTest()
    {
        // First, check if Datadog Agent is running
        await CheckDatadogAgentStatus();
        
        using var span = _instrumentation.ActivitySource.StartActivity("simple-agent-test-span");
        
        span?.SetTag("test.name", "simple-agent-span");
        span?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        span?.SetTag("agent.type", "datadog-agent");
        
        span.Should().NotBeNull();
        span?.Id.Should().NotBeNull();
        span?.TraceId.Should().NotBe(default(ActivityTraceId));
        
        _output.WriteLine($"Trace sent to Datadog Agent: {span?.TraceId}");
        
        // Force export before test ends
        var exportResult = _instrumentation.TracerProvider.ForceFlush(5000);
        _output.WriteLine($"OpenTelemetry ForceFlush Result: {exportResult}");
        
        if (exportResult)
        {
            _output.WriteLine("✅ ForceFlush completed successfully");
            _output.WriteLine("   Trace sent to: http://localhost:4318/v1/traces");
            _output.WriteLine("   The Datadog Agent should forward this to Datadog cloud");
        }
        else
        {
            _output.WriteLine("❌ ForceFlush failed - check if Datadog Agent is running");
        }
    }
    
    private async Task CheckDatadogAgentStatus()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            // Check both the GUI port (5002) and the API port (8126)
            var endpointsToTry = new[]
            {
                "http://localhost:5002/",           // Agent GUI home
                "http://localhost:5002/status",     // Agent GUI status  
                "http://localhost:8126/status",     // Agent API status
                "http://localhost:8126/v1/status",  // Agent API v1 status
                "http://localhost:8126/health",     // Agent API health
                "http://localhost:4318/v1/traces"       // OTLP endpoint (4318)
            };
            
            _output.WriteLine($"Checking Datadog Agent (GUI on :5002, OTLP on :4318, API on :8126)");
            
            bool agentFound = false;
            foreach (var endpoint in endpointsToTry)
            {
                try
                {
                    _output.WriteLine($"Trying: {endpoint}");
                    var response = await client.GetAsync(endpoint);
                    _output.WriteLine($"  Response: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        agentFound = true;
                        _output.WriteLine($"✅ Datadog Agent responding on {endpoint}!");
                        
                        // Try to read some response content for GUI endpoints
                        if (endpoint.Contains("5002"))
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            if (content.Contains("Datadog") || content.Contains("Agent"))
                            {
                                _output.WriteLine("  Confirmed: This is the Datadog Agent GUI");
                            }
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed && endpoint.Contains("4318"))
                    {
                        _output.WriteLine("  ✅ OTLP endpoint is ready (GET not allowed, but POST will work)");
                        agentFound = true;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Error: {ex.Message}");
                }
            }
            
            if (agentFound)
            {
                _output.WriteLine("✅ Datadog Agent is running and accessible!");
            }
            else
            {
                _output.WriteLine("❌ Cannot reach Datadog Agent on any endpoints");
                _output.WriteLine("💡 Make sure Datadog Agent is running");
                _output.WriteLine("💡 GUI should be on http://localhost:5002");
                _output.WriteLine("💡 OTLP should be on http://localhost:4318");
            }
            
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error checking Datadog Agent: {ex.Message}");
        }
    }
}