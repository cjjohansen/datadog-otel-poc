using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace DatadogOtelPocTests;

public class DatadogAgentGrpcOtlpTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    private readonly ITestOutputHelper _output;

    public DatadogAgentGrpcOtlpTest(ITestOutputHelper output)
    {
        _instrumentation = OtlpInstrumentation.CreateForAgent();
        _output = output;
    }

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public async Task MyTest_ShouldPass()
    {
        using var activity = _instrumentation.ActivitySource.StartActivity("MyTest_ShouldPass");
        
        // Add test metadata
        activity?.SetTag("test.name", "MyTest_ShouldPass");
        activity?.SetTag("test.suite", "MyTestSuite");
        activity?.SetTag("test.type", "unit");
        activity?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        
        _output.WriteLine($"Starting test trace: {activity?.TraceId}");
        
        try
        {
            // Simulate some test logic
            await SomeMethodToTest();
            
            activity?.SetTag("test.result", "passed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine("✅ Test passed successfully");
            
            // Verify activity was created
            activity.Should().NotBeNull();
            activity?.Id.Should().NotBeNull();
            activity?.TraceId.Should().NotBe(default(ActivityTraceId));
        }
        catch (Exception ex)
        {
            activity?.SetTag("test.result", "failed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _output.WriteLine($"❌ Test failed: {ex.Message}");
            throw;
        }
        finally
        {
            // NOTE: _output.WriteLine() is just for test console output - it does NOT send data to Datadog!
            // What sends data to Datadog is:
            // 1. The Activity creation and tagging above
            // 2. The OTLP exporter configured in TestInstrumentation
            // 3. The ForceFlush() call below to ensure data is transmitted
            
            _output.WriteLine($"Test trace completed: {activity?.TraceId}");
            _output.WriteLine($"Trace sent to Datadog Agent gRPC endpoint: http://localhost:4317");
            
            // Force export before test ends - this ensures telemetry data is sent to Datadog
            var exportResult = _instrumentation.TracerProvider.ForceFlush(5000);
            _output.WriteLine($"OpenTelemetry ForceFlush Result: {exportResult}");
            
            if (exportResult)
            {
                _output.WriteLine("✅ ForceFlush completed successfully - traces sent to Datadog Agent");
            }
            else
            {
                _output.WriteLine("❌ ForceFlush timed out or failed");
            }
        }
    }

    [Fact]
    public async Task DatabaseOperation_ShouldCreateSpan()
    {
        using var activity = _instrumentation.ActivitySource.StartActivity("DatabaseOperation_ShouldCreateSpan");
        
        // Add test metadata
        activity?.SetTag("test.name", "DatabaseOperation_ShouldCreateSpan");
        activity?.SetTag("test.suite", "MyTestSuite");
        activity?.SetTag("test.type", "integration");
        activity?.SetTag("db.operation", "select");
        activity?.SetTag("db.table", "users");
        
        _output.WriteLine($"Starting database operation trace: {activity?.TraceId}");
        
        try
        {
            // Simulate database operation
            await SimulateDatabaseOperation();
            
            activity?.SetTag("test.result", "passed");
            activity?.SetTag("db.rows_affected", 5);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine("✅ Database operation completed successfully");
            
            // Verify activity was created
            activity.Should().NotBeNull();
            activity?.Id.Should().NotBeNull();
            activity?.TraceId.Should().NotBe(default(ActivityTraceId));
        }
        catch (Exception ex)
        {
            activity?.SetTag("test.result", "failed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _output.WriteLine($"❌ Database operation failed: {ex.Message}");
            throw;
        }
        finally
        {
            _output.WriteLine($"Database operation trace completed: {activity?.TraceId}");
        }
    }

    [Fact]
    public async Task HttpRequest_ShouldCreateSpan()
    {
        using var activity = _instrumentation.ActivitySource.StartActivity("HttpRequest_ShouldCreateSpan");
        
        // Add test metadata
        activity?.SetTag("test.name", "HttpRequest_ShouldCreateSpan");
        activity?.SetTag("test.suite", "MyTestSuite");
        activity?.SetTag("test.type", "integration");
        activity?.SetTag("http.method", "GET");
        activity?.SetTag("http.url", "https://api.example.com/users");
        
        _output.WriteLine($"Starting HTTP request trace: {activity?.TraceId}");
        
        try
        {
            // Simulate HTTP request
            await SimulateHttpRequest();
            
            activity?.SetTag("test.result", "passed");
            activity?.SetTag("http.status_code", 200);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine("✅ HTTP request completed successfully");
            
            // Verify activity was created
            activity.Should().NotBeNull();
            activity?.Id.Should().NotBeNull();
            activity?.TraceId.Should().NotBe(default(ActivityTraceId));
        }
        catch (Exception ex)
        {
            activity?.SetTag("test.result", "failed");
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _output.WriteLine($"❌ HTTP request failed: {ex.Message}");
            throw;
        }
        finally
        {
            _output.WriteLine($"HTTP request trace completed: {activity?.TraceId}");
        }
    }

    private async Task SomeMethodToTest()
    {
        // Simulate some work with a nested span
        using var nestedActivity = _instrumentation.ActivitySource.StartActivity("NestedOperation");
        nestedActivity?.SetTag("operation.type", "business_logic");
        
        await Task.Delay(100); // Simulate some processing time
        
        nestedActivity?.SetTag("operation.duration_ms", 100);
        nestedActivity?.SetStatus(ActivityStatusCode.Ok);
    }

    private async Task SimulateDatabaseOperation()
    {
        // Simulate database work with a nested span
        using var dbActivity = _instrumentation.ActivitySource.StartActivity("Database.Query");
        dbActivity?.SetTag("db.system", "postgresql");
        dbActivity?.SetTag("db.statement", "SELECT * FROM users WHERE active = true");
        
        await Task.Delay(50); // Simulate database query time
        
        dbActivity?.SetTag("db.duration_ms", 50);
        dbActivity?.SetStatus(ActivityStatusCode.Ok);
    }

    private async Task SimulateHttpRequest()
    {
        // Simulate HTTP request with a nested span
        using var httpActivity = _instrumentation.ActivitySource.StartActivity("Http.Request");
        httpActivity?.SetTag("http.client", "HttpClient");
        httpActivity?.SetTag("http.user_agent", "DatadogOtelPoc/1.0");
        
        await Task.Delay(200); // Simulate HTTP request time
        
        httpActivity?.SetTag("http.duration_ms", 200);
        httpActivity?.SetStatus(ActivityStatusCode.Ok);
    }
}
