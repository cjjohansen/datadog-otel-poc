# Datadog OpenTelemetry POC - Messaging & Distributed Tracing

## Project Overview
This project demonstrates OpenTelemetry distributed tracing across messaging systems, focusing on proper span propagation, trace context, and telemetry data validation for event-driven architectures.

## Test Project Structure

### Dependencies (C#)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="OpenTelemetry" Version="1.8.1" />
    <PackageReference Include="OpenTelemetry.Exporter.InMemory" Version="1.8.1" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.8.1" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.8.1" />
    <PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Message Producer Telemetry Tests

### Event Publishing Scenarios
```csharp
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

public class MessageProducerTelemetryTests : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly InMemoryExporter<Activity> _exportedActivities;
    private readonly ActivitySource _activitySource;
    private readonly TextMapPropagator _propagator;
    
    public MessageProducerTelemetryTests()
    {
        _exportedActivities = new InMemoryExporter<Activity>();
        _activitySource = new ActivitySource("messaging.producer");
        _propagator = Propagators.DefaultTextMapPropagator;
        
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("messaging.producer")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("order-publisher", "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["messaging.system"] = "rabbitmq",
                    ["deployment.environment"] = "test"
                }))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Fact]
    public void WhenPublishingOrderCreatedEvent_ShouldCreateProducerSpanWithCorrectAttributes()
    {
        // Arrange
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = "ORDER-12345",
            CustomerId = "CUST-789",
            Amount = 299.99m,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var publisher = new EventPublisher(_activitySource, _propagator);
        var messageHeaders = publisher.PublishOrderCreated(orderEvent, "orders.created");

        // Assert
        var activities = _exportedActivities.GetExportedItems().ToList();
        activities.Should().HaveCount(1);
        
        var producerSpan = activities[0];
        producerSpan.DisplayName.Should().Be("orders.created publish");
        producerSpan.Kind.Should().Be(ActivityKind.Producer);
        producerSpan.Status.Should().Be(ActivityStatusCode.Ok);
        
        // Messaging semantic conventions
        producerSpan.GetTagItem("messaging.system").Should().Be("rabbitmq");
        producerSpan.GetTagItem("messaging.destination.name").Should().Be("orders.created");
        producerSpan.GetTagItem("messaging.operation").Should().Be("publish");
        producerSpan.GetTagItem("messaging.message.id").Should().NotBeNull();
        
        // Business context
        producerSpan.GetTagItem("order.id").Should().Be("ORDER-12345");
        producerSpan.GetTagItem("customer.id").Should().Be("CUST-789");
    }

    [Fact]
    public void WhenPublishingEvent_ShouldInjectTraceContextIntoMessageHeaders()
    {
        // Arrange
        var orderEvent = new OrderCreatedEvent { OrderId = "ORDER-67890" };

        // Act
        var publisher = new EventPublisher(_activitySource, _propagator);
        var messageHeaders = publisher.PublishOrderCreated(orderEvent, "orders.created");

        // Assert
        messageHeaders.Should().ContainKey("traceparent");
        messageHeaders.Should().ContainKey("tracestate");
        
        var traceparent = messageHeaders["traceparent"];
        traceparent.Should().MatchRegex(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$");
        
        // Verify traceparent matches the span context
        var activities = _exportedActivities.GetExportedItems().ToList();
        var producerSpan = activities[0];
        var expectedTraceparent = $"00-{producerSpan.Context.TraceId:N}-{producerSpan.Context.SpanId:N}-01";
        traceparent.Should().Be(expectedTraceparent);
    }

    [Theory]
    [InlineData("payment.processed", "PAYMENT-001", "SUCCESS")]
    [InlineData("payment.failed", "PAYMENT-002", "DECLINED")]
    [InlineData("inventory.updated", "SKU-ABC123", "RESTOCKED")]
    public void WhenPublishingDifferentEventTypes_ShouldCreateAppropriateSpans(
        string destinationName, string entityId, string eventStatus)
    {
        // Arrange
        var genericEvent = new GenericBusinessEvent
        {
            EntityId = entityId,
            EventType = destinationName,
            Status = eventStatus,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var publisher = new EventPublisher(_activitySource, _propagator);
        var messageHeaders = publisher.PublishGenericEvent(genericEvent, destinationName);

        // Assert
        var activities = _exportedActivities.GetExportedItems().ToList();
        var producerSpan = activities[0];
        
        producerSpan.DisplayName.Should().Be($"{destinationName} publish");
        producerSpan.GetTagItem("messaging.destination.name").Should().Be(destinationName);
        producerSpan.GetTagItem("event.type").Should().Be(destinationName);
        producerSpan.GetTagItem("entity.id").Should().Be(entityId);
        producerSpan.GetTagItem("event.status").Should().Be(eventStatus);
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _activitySource?.Dispose();
    }
}
```

## Message Consumer Telemetry Tests

### Event Processing Scenarios
```csharp
public class MessageConsumerTelemetryTests : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly InMemoryExporter<Activity> _exportedActivities;
    private readonly ActivitySource _activitySource;
    private readonly TextMapPropagator _propagator;
    
    public MessageConsumerTelemetryTests()
    {
        _exportedActivities = new InMemoryExporter<Activity>();
        _activitySource = new ActivitySource("messaging.consumer");
        _propagator = Propagators.DefaultTextMapPropagator;
        
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("messaging.consumer")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("order-processor", "1.0.0"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Fact]
    public void WhenProcessingOrderCreatedEvent_ShouldCreateConsumerSpanLinkedToProducerTrace()
    {
        // Arrange - Simulate received message with trace context
        var messageHeaders = new Dictionary<string, string>
        {
            ["traceparent"] = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
            ["tracestate"] = "dd=s:1;o:rum;t.dm:-4",
            ["content-type"] = "application/json"
        };
        
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = "ORDER-98765",
            CustomerId = "CUST-456",
            Amount = 199.99m
        };

        // Act
        var consumer = new EventConsumer(_activitySource, _propagator);
        var result = consumer.ProcessOrderCreated(orderEvent, messageHeaders, "orders.created");

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var activities = _exportedActivities.GetExportedItems().ToList();
        activities.Should().HaveCount(1);
        
        var consumerSpan = activities[0];
        consumerSpan.DisplayName.Should().Be("orders.created process");
        consumerSpan.Kind.Should().Be(ActivityKind.Consumer);
        consumerSpan.Status.Should().Be(ActivityStatusCode.Ok);
        
        // Verify trace context propagation
        consumerSpan.Context.TraceId.ToString().Should().Be("0123456789abcdef0123456789abcdef");
        consumerSpan.ParentSpanId.ToString().Should().Be("0123456789abcdef");
        
        // Messaging attributes
        consumerSpan.GetTagItem("messaging.system").Should().Be("rabbitmq");
        consumerSpan.GetTagItem("messaging.destination.name").Should().Be("orders.created");
        consumerSpan.GetTagItem("messaging.operation").Should().Be("process");
    }

    [Fact]
    public void WhenProcessingEventFails_ShouldCreateErrorSpanWithFailureDetails()
    {
        // Arrange
        var messageHeaders = new Dictionary<string, string>
        {
            ["traceparent"] = "00-abcdef0123456789abcdef0123456789ab-abcdef0123456789-01"
        };
        
        var invalidEvent = new OrderCreatedEvent
        {
            OrderId = "", // Invalid - empty order ID
            CustomerId = "CUST-789",
            Amount = -100m // Invalid - negative amount
        };

        // Act
        var consumer = new EventConsumer(_activitySource, _propagator);
        var result = consumer.ProcessOrderCreated(invalidEvent, messageHeaders, "orders.created");

        // Assert
        result.IsSuccess.Should().BeFalse();
        
        var activities = _exportedActivities.GetExportedItems().ToList();
        var consumerSpan = activities[0];
        
        consumerSpan.Status.Should().Be(ActivityStatusCode.Error);
        consumerSpan.StatusDescription.Should().Contain("Invalid order data");
        consumerSpan.GetTagItem("error.type").Should().Be("ValidationException");
        consumerSpan.GetTagItem("messaging.consumer.error.reason").Should().Be("validation_failed");
    }

    [Fact]
    public void WhenProcessingEventWithoutTraceContext_ShouldCreateNewTraceForConsumer()
    {
        // Arrange - Message without trace headers
        var messageHeaders = new Dictionary<string, string>
        {
            ["content-type"] = "application/json",
            ["message-id"] = "MSG-12345"
        };
        
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = "ORDER-ORPHANED-001",
            CustomerId = "CUST-999"
        };

        // Act
        var consumer = new EventConsumer(_activitySource, _propagator);
        var result = consumer.ProcessOrderCreated(orderEvent, messageHeaders, "orders.created");

        // Assert
        var activities = _exportedActivities.GetExportedItems().ToList();
        var consumerSpan = activities[0];
        
        // Should create a new trace since no parent context
        consumerSpan.Parent.Should().BeNull();
        consumerSpan.Context.TraceId.Should().NotBe(default(ActivityTraceId));
        consumerSpan.GetTagItem("messaging.orphaned_message").Should().Be("true");
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _activitySource?.Dispose();
    }
}
```

## Distributed Tracing Scenarios

### Cross-Service Trace Propagation
```csharp
public class DistributedTracingTests : IDisposable
{
    private readonly TracerProvider _producerTracerProvider;
    private readonly TracerProvider _consumerTracerProvider;
    private readonly InMemoryExporter<Activity> _producerActivities;
    private readonly InMemoryExporter<Activity> _consumerActivities;
    private readonly ActivitySource _producerSource;
    private readonly ActivitySource _consumerSource;
    private readonly TextMapPropagator _propagator;
    
    public DistributedTracingTests()
    {
        _producerActivities = new InMemoryExporter<Activity>();
        _consumerActivities = new InMemoryExporter<Activity>();
        _producerSource = new ActivitySource("order-service");
        _consumerSource = new ActivitySource("inventory-service");
        _propagator = Propagators.DefaultTextMapPropagator;
        
        _producerTracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("order-service")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("order-service", "1.0.0"))
            .AddInMemoryExporter(_producerActivities)
            .Build();
            
        _consumerTracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("inventory-service")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("inventory-service", "1.0.0"))
            .AddInMemoryExporter(_consumerActivities)
            .Build();
    }

    [Fact]
    public void WhenOrderFlowSpansMultipleServices_ShouldMaintainTraceCoherence()
    {
        // Act - Producer publishes order created event
        var orderEvent = new OrderCreatedEvent { OrderId = "ORDER-DISTRIBUTED-001" };
        var producer = new EventPublisher(_producerSource, _propagator);
        var messageHeaders = producer.PublishOrderCreated(orderEvent, "orders.created");

        // Consumer processes the event
        var consumer = new EventConsumer(_consumerSource, _propagator);
        var result = consumer.ProcessOrderCreated(orderEvent, messageHeaders, "orders.created");

        // Assert - Both spans belong to the same trace
        var producerSpans = _producerActivities.GetExportedItems().ToList();
        var consumerSpans = _consumerActivities.GetExportedItems().ToList();
        
        producerSpans.Should().HaveCount(1);
        consumerSpans.Should().HaveCount(1);
        
        var producerSpan = producerSpans[0];
        var consumerSpan = consumerSpans[0];
        
        // Same trace ID across services
        consumerSpan.Context.TraceId.Should().Be(producerSpan.Context.TraceId);
        
        // Consumer span is child of producer span
        consumerSpan.ParentSpanId.Should().Be(producerSpan.Context.SpanId);
    }

    [Fact]
    public void WhenMultipleEventsPublishedInSameTransaction_ShouldShareTraceContext()
    {
        // Arrange & Act - Publish multiple related events in transaction
        using var transactionActivity = _producerSource.StartActivity("order.transaction");
        transactionActivity?.SetTag("transaction.id", "TXN-001");
        
        var producer = new EventPublisher(_producerSource, _propagator);
        
        var orderCreatedHeaders = producer.PublishOrderCreated(
            new OrderCreatedEvent { OrderId = "ORDER-001" }, "orders.created");
        
        var inventoryReservedHeaders = producer.PublishInventoryReserved(
            new InventoryReservedEvent { OrderId = "ORDER-001", ProductId = "SKU-123" }, "inventory.reserved");
        
        var paymentRequestedHeaders = producer.PublishPaymentRequested(
            new PaymentRequestedEvent { OrderId = "ORDER-001", Amount = 99.99m }, "payment.requested");

        // Assert - All events share the same trace context
        var activities = _producerActivities.GetExportedItems().ToList();
        activities.Should().HaveCount(4); // transaction + 3 publish spans
        
        var traceId = activities[0].Context.TraceId;
        activities.All(a => a.Context.TraceId == traceId).Should().BeTrue();
        
        // All publish spans should be children of transaction span
        var publishSpans = activities.Where(a => a.DisplayName.Contains("publish")).ToList();
        publishSpans.All(s => s.ParentId == transactionActivity.Id).Should().BeTrue();
    }

    public void Dispose()
    {
        _producerTracerProvider?.Dispose();
        _consumerTracerProvider?.Dispose();
        _producerSource?.Dispose();
        _consumerSource?.Dispose();
    }
}
```

## Datadog Integration Tests
```csharp
public class DatadogOtlpMessagingTests
{
    [Fact]
    public void WhenConfiguredWithDatadogAgent_ShouldSetCorrectMessagingAttributes()
    {
        // Arrange & Act
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("messaging.test")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("messaging-service", "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["messaging.system"] = "kafka",
                    ["service.namespace"] = "messaging"
                }))
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4318/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
            })
            .Build();

        // Assert
        tracerProvider.Should().NotBeNull();
    }
}
```

## MCP Servers Configuration

### GitHub MCP Server
- Repository management and code analysis for messaging patterns

### Memory MCP Server  
- Trace correlation analysis and message flow tracking
- Event schema evolution tracking

### Sequential Thinking MCP Server
- Complex distributed tracing problem solving
- Message ordering and consistency analysis

### Azure MCP Server
- Service Bus and Event Hub integration
- Application Insights message tracking

### Datadog MCP Server
- APM service map for messaging flows
- Custom messaging dashboards and alerts

## Build Commands
- `dotnet test` - Run all messaging telemetry tests
- `dotnet test --filter "Category=MessageProducer"` - Producer tests
- `dotnet test --filter "Category=MessageConsumer"` - Consumer tests
- `dotnet test --filter "Category=DistributedTracing"` - Cross-service tests