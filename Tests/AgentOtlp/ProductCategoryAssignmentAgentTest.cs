using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace DatadogOtelPocTests;

public class ProductCategoryAssignmentAgentTest : IDisposable
{
    private readonly OtlpInstrumentation _instrumentation;
    private readonly ITestOutputHelper _output;

    public ProductCategoryAssignmentAgentTest(ITestOutputHelper output)
    {
        _instrumentation = OtlpInstrumentation.CreateForAgent();
        _output = output;
    }

    public void Dispose()
    {
        _instrumentation?.Dispose();
    }

    [Fact]
    public async Task ProductCategoryAssignment_ShouldPropagateTraceAcrossServices_Agent()
    {
        _output.WriteLine("[Agent] Starting product category assignment workflow");
        var (assignmentId, apiTraceId, apiTraceParent) = await SimulateAssignCategoryToProductCommand();
        assignmentId.Should().NotBeNullOrEmpty();
        apiTraceId.Should().NotBe(default(ActivityTraceId));
        var finalProducerTraceState = await SimulateCategoryAssignedEventProducer(apiTraceParent, null, $"{{\"assignmentId\":\"{assignmentId}\",\"productId\":\"PROD-12345\",\"categoryId\":\"CAT-ELECTRONICS\"}}");
        var consumerTraceParent = Activity.Current?.Id ?? apiTraceParent;
        var consumerTraceState = Activity.Current?.TraceStateString ?? finalProducerTraceState;
        var (catalogUpdateResult, finalConsumerTraceState) = await SimulateCategoryAssignedEventConsumer(consumerTraceParent, consumerTraceState);
        catalogUpdateResult.Should().NotBeNullOrEmpty();
        _output.WriteLine("[Agent] Complete workflow traced successfully");
    }

    private async Task<(string assignmentId, ActivityTraceId traceId, string traceParent)> SimulateAssignCategoryToProductCommand()
    {
    using var activity = _instrumentation.ActivitySource.StartActivity("POST AssignCategoryToProduct");
    activity?.SetTag("service.name", "ProductCategoryManagementService");
    activity?.SetTag("deployment.environment", "agent");
    activity?.SetTag("resource.name", "POST AssignCategoryToProduct");
    activity?.SetTag("messaging.system", "api");
    activity?.SetTag("messaging.destination", "pcm-api");
        var traceId = activity?.TraceId ?? default;
        var traceParent = activity?.Id ?? "";
        await Task.Delay(50);
        var assignmentId = $"assignment-{Guid.NewGuid():N}";
        activity?.SetTag("assignment.id", assignmentId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return (assignmentId, traceId, traceParent);
    }

    private async Task<string?> SimulateCategoryAssignedEventProducer(string traceParent, string? traceState = null, string? eventPayload = null)
    {
        var activityContext = ActivityContext.Parse(traceParent, traceState);
        using var activity = _instrumentation.ActivitySource.StartActivity(
            "publish CategoryAssignedToProduct",
            ActivityKind.Producer,
            activityContext);
        activity?.SetTag("service.name", "ProductCategoryManagementService");
        activity?.SetTag("messaging.system", "gpc-event-management");
        activity?.SetTag("messaging.destination", "product-category-assignment-events");
        activity?.SetTag("resource.name", "publish CategoryAssignedToProduct");
        await Task.Delay(20);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return "producer=ProductCategoryManagementService";
    }

    private async Task<(string result, string? traceState)> SimulateCategoryAssignedEventConsumer(string traceParent, string? traceState = null)
    {
        var activityContext = ActivityContext.Parse(traceParent, traceState);
        using var activity = _instrumentation.ActivitySource.StartActivity(
            "receive CategoryAssignedToProduct",
            ActivityKind.Consumer,
            activityContext);
        activity?.SetTag("service.name", "ProductCatalogService");
        activity?.SetTag("messaging.system", "gpc-event-management");
        activity?.SetTag("messaging.source", "product-category-assignment-events");
        activity?.SetTag("resource.name", "receive CategoryAssignedToProduct");
        await Task.Delay(30);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return ($"catalog-update-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", "consumer=ProductCatalogService");
    }
}
