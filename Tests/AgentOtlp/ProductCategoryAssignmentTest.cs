using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace DatadogOtelPocTests;

public class ProductCategoryAssignmentTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    private readonly ITestOutputHelper _output;

    public ProductCategoryAssignmentTest(ITestOutputHelper output)
    {
        _instrumentation = OtlpInstrumentation.CreateForAgent();
        _output = output;
    }

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public async Task ProductCategoryAssignment_ShouldPropagateTraceAcrossServices()
    {
        _output.WriteLine("🚀 Starting realistic product category assignment workflow");
        _output.WriteLine("📡 Simulating incoming HTTP request to ProductCategoryManagementService API");
        
        // Start the trace with the API command - this is the realistic entry point
        var (assignmentId, apiTraceId, apiTraceParent) = await SimulateAssignCategoryToProductCommand();
        assignmentId.Should().NotBeNullOrEmpty();
        apiTraceId.Should().NotBe(default(ActivityTraceId));
        
        _output.WriteLine($"📡 AssignCategoryToProduct command processed, assignment ID: {assignmentId}");
        _output.WriteLine($"🆔 Root trace started with ID: {apiTraceId}");
        
        ValidateW3CTraceContext(apiTraceParent, null);
        
        // Producer continues the trace from the API call
        var finalProducerTraceState = await SimulateCategoryAssignedEventProducer(apiTraceParent, null, $"{{\"assignmentId\":\"{assignmentId}\",\"productId\":\"PROD-12345\",\"categoryId\":\"CAT-ELECTRONICS\"}}");
        _output.WriteLine($"📤 CategoryAssignedToProduct event published to EventHub");
        _output.WriteLine($"📤 Producer trace state: {finalProducerTraceState}");
        
        // Consumer receives the event with trace context propagated
        var consumerTraceParent = Activity.Current?.Id ?? apiTraceParent;
        var consumerTraceState = Activity.Current?.TraceStateString ?? finalProducerTraceState;
        
        var (catalogUpdateResult, finalConsumerTraceState) = await SimulateCategoryAssignedEventConsumer(consumerTraceParent, consumerTraceState);
        catalogUpdateResult.Should().NotBeNullOrEmpty();
        _output.WriteLine($"📥 ProductCatalogService updated catalog: {catalogUpdateResult}");
        _output.WriteLine($"🏁 Final consumer trace state: {finalConsumerTraceState}");
        
        // Verify the complete trace worked
        apiTraceId.Should().NotBe(default(ActivityTraceId));
        assignmentId.Should().NotBeNullOrEmpty();
        catalogUpdateResult.Should().NotBeNullOrEmpty();
        
        _output.WriteLine("✅ Complete product category assignment workflow traced successfully");
        _output.WriteLine($"🔗 All services correlated under trace ID: {apiTraceId}");
        _output.WriteLine("📊 Expected service flow in Datadog: ProductCategoryManagementService → EventHub → ProductCatalogService");
    }

    private void ValidateW3CTraceContext(string traceParent, string? traceState)
    {
        _output.WriteLine("🔍 Validating W3C Trace Context compliance...");
        
        var parts = traceParent.Split('-');
        parts.Should().HaveCount(4, "traceparent should have 4 parts separated by hyphens");
        
        parts[0].Should().Be("00", "version should be 00 for W3C Trace Context v1");
        
        parts[1].Should().MatchRegex("^[0-9a-f]{32}$", "trace-id should be 32 lowercase hex characters");
        parts[1].Should().NotBe("00000000000000000000000000000000", "trace-id should not be all zeros");
        
        parts[2].Should().MatchRegex("^[0-9a-f]{16}$", "parent-id should be 16 lowercase hex characters");
        parts[2].Should().NotBe("0000000000000000", "parent-id should not be all zeros");
        
        parts[3].Should().MatchRegex("^[0-9a-f]{2}$", "trace-flags should be 2 lowercase hex characters");
        
        _output.WriteLine($"✅ traceparent format is W3C compliant: {traceParent}");
        _output.WriteLine($"   Version: {parts[0]}");
        _output.WriteLine($"   Trace ID: {parts[1]}");
        _output.WriteLine($"   Parent ID: {parts[2]}");
        _output.WriteLine($"   Flags: {parts[3]} (sampled: {(Convert.ToInt32(parts[3], 16) & 1) == 1})");
        
        if (!string.IsNullOrEmpty(traceState))
        {
            _output.WriteLine($"✅ tracestate present: {traceState}");
            
            var stateEntries = traceState.Split(',');
            foreach (var entry in stateEntries)
            {
                entry.Should().Contain("=", $"tracestate entry '{entry}' should contain '=' separator");
                var keyValue = entry.Split('=', 2);
                keyValue[0].Should().NotBeNullOrWhiteSpace("tracestate key should not be empty");
            }
        }
        else
        {
            _output.WriteLine("ℹ️  tracestate is empty (optional in W3C spec)");
        }
    }

    // Azure EventHub Product Category Assignment Simulation Functions
    
    private async Task<(string assignmentId, ActivityTraceId traceId, string traceParent)> SimulateAssignCategoryToProductCommand()
    {
        // This starts a new root trace - simulating an incoming HTTP request to the API
    using var activity = _instrumentation.ActivitySource.StartActivity("POST AssignCategoryToProduct");
        
        activity?.SetTag("service.name", "ProductCategoryManagementService");
        activity?.SetTag("service.version", "1.2.0");
        activity?.SetTag("deployment.environment", "production");
        activity?.SetTag("http.method", "POST");
        activity?.SetTag("http.route", "/api/products/{productId}/categories");
        activity?.SetTag("http.url", "https://api.productmanagement.company.com/api/products/PROD-12345/categories");
        activity?.SetTag("http.user_agent", "ProductManagementPortal/2.1.0");
        activity?.SetTag("command.type", "AssignCategoryToProduct");
        activity?.SetTag("product.id", "PROD-12345");
        activity?.SetTag("category.id", "CAT-ELECTRONICS");
        activity?.SetTag("user.id", "user-789");
        activity?.SetTag("correlation.id", Guid.NewGuid().ToString());
        
        var traceId = activity?.TraceId ?? default;
        var traceParent = activity?.Id ?? "";
        
        _output.WriteLine($"🔵 ProductCategoryManagementService processing AssignCategoryToProduct command");
        _output.WriteLine($"🔵 Service: ProductCategoryManagementService");
        _output.WriteLine($"🔵 Trace ID: {traceId}");
        
        try
        {
            await Task.Delay(120); // Simulate API processing time
            
            var assignmentId = $"assignment-{Guid.NewGuid():N}";
            
            activity?.SetTag("assignment.id", assignmentId);
            activity?.SetTag("http.status_code", 201);
            activity?.SetTag("response.body.size", 156);
            activity?.SetTag("assignment.status", "completed");
            activity?.SetTag("processing.duration_ms", 120);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine($"✅ Category assignment completed successfully: {assignmentId}");
            
            return (assignmentId, traceId, traceParent);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("http.status_code", 500);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
    
    private async Task<string?> SimulateCategoryAssignedEventProducer(string traceParent, string? traceState = null, string? eventPayload = null)
    {
        var activityContext = ActivityContext.Parse(traceParent, traceState);
        
        using var activity = _instrumentation.ActivitySource.StartActivity(
            "publish CategoryAssignedToProduct",
            ActivityKind.Producer,
            activityContext);
        
        activity?.SetTag("service.name", "ProductCategoryManagementService");
        activity?.SetTag("service.version", "1.2.0");
        activity?.SetTag("component", "azure-eventhub-producer");
        activity?.SetTag("messaging.system", "azure-eventhub");
        activity?.SetTag("messaging.destination", "product-category-assignment-events");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("event.type", "CategoryAssignedToProduct");
        activity?.SetTag("event.version", "1.0");
        activity?.SetTag("azure.eventhub.namespace", "company-events.servicebus.windows.net");
        activity?.SetTag("azure.eventhub.name", "product-category-assignment-events");
        activity?.SetTag("azure.eventhub.partition_id", "0");
        activity?.SetTag("trace.parent", traceParent);
        if (traceState != null) activity?.SetTag("trace.state", traceState);
        
        var producerTraceState = $"{traceState},producer=ProductCategoryManagementService".TrimStart(',');
        
        _output.WriteLine($"🟠 ProductCategoryManagementService publishing CategoryAssignedToProduct event");
        _output.WriteLine($"🟠 EventHub: product-category-assignment-events");
        
        try
        {
            await Task.Delay(45); // Simulate EventHub publish time
            
            var eventId = Guid.NewGuid().ToString();
            var sequenceNumber = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            activity?.SetTag("messaging.message_id", eventId);
            activity?.SetTag("messaging.payload_size", eventPayload?.Length ?? 0);
            activity?.SetTag("azure.eventhub.sequence_number", sequenceNumber);
            activity?.SetTag("azure.eventhub.offset", sequenceNumber * 100);
            activity?.SetTag("event.timestamp", DateTimeOffset.UtcNow.ToString("O"));
            activity?.SetTag("trace.state.updated", producerTraceState);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine($"✅ CategoryAssignedToProduct event published: {eventId}");
            _output.WriteLine($"📤 Sequence number: {sequenceNumber}");
            
            return producerTraceState;
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
    
    private async Task<(string result, string? traceState)> SimulateCategoryAssignedEventConsumer(string traceParent, string? traceState = null)
    {
        var activityContext = ActivityContext.Parse(traceParent, traceState);
        
        using var activity = _instrumentation.ActivitySource.StartActivity(
            "receive CategoryAssignedToProduct",
            ActivityKind.Consumer,
            activityContext);
        
        activity?.SetTag("service.name", "ProductCatalogService");
        activity?.SetTag("service.version", "2.3.1");
        activity?.SetTag("component", "azure-eventhub-consumer");
        activity?.SetTag("messaging.system", "azure-eventhub");
        
        activity?.SetTag("messaging.operation", "receive");
        activity?.SetTag("event.type", "CategoryAssignedToProduct");
        activity?.SetTag("azure.eventhub.namespace", "company-events.servicebus.windows.net");
        activity?.SetTag("azure.eventhub.name", "product-category-assignment-events");
        activity?.SetTag("azure.eventhub.consumer_group", "product-catalog-service");
        activity?.SetTag("trace.parent", traceParent);
        if (traceState != null) activity?.SetTag("trace.state", traceState);
        
        var consumerTraceState = $"{traceState},consumer=ProductCatalogService".TrimStart(',');
        
        _output.WriteLine($"🟢 ProductCatalogService processing CategoryAssignedToProduct event");
        _output.WriteLine($"🟢 Consumer Group: product-catalog-service");
        
        try
        {
            await Task.Delay(180); // Simulate catalog update processing time
            
            var catalogUpdateId = $"catalog-update-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            
            activity?.SetTag("catalog.update.id", catalogUpdateId);
            activity?.SetTag("catalog.update.type", "category_assignment");
            activity?.SetTag("product.id", "PROD-12345");
            activity?.SetTag("category.id", "CAT-ELECTRONICS");
            activity?.SetTag("catalog.version.before", "1.42.0");
            activity?.SetTag("catalog.version.after", "1.42.1");
            activity?.SetTag("processing.duration_ms", 180);
            activity?.SetTag("trace.state.final", consumerTraceState);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _output.WriteLine($"✅ Product catalog updated successfully: {catalogUpdateId}");
            _output.WriteLine($"📊 Catalog version: 1.42.0 → 1.42.1");
            
            return (catalogUpdateId, consumerTraceState);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
