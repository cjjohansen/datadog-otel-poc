using OpenTelemetry.Exporter;

namespace DatadogOtelPocTests;

public class OtlpConfiguration
{
    public string ActivitySourceName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string ServiceVersion { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string ServiceNamespace { get; init; } = string.Empty;
    
    // OTLP Configuration
    public string Endpoint { get; init; } = string.Empty;
    public OtlpExportProtocol Protocol { get; init; } = OtlpExportProtocol.Grpc;
    public string? Headers { get; init; } = null;
    public int TimeoutMilliseconds { get; init; } = 10000;
    
    // Test Framework Metadata
    public string TestFramework { get; init; } = string.Empty;
}
