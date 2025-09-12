# Datadog OpenTelemetry POC

This project demonstrates OpenTelemetry integration with Datadog using both direct OTLP and Datadog Agent approaches.

## Security

🔒 **API keys are never stored in source code or configuration files!**

- All sensitive values are loaded from environment variables
- `.env` files are excluded from version control
- Use `.env.example` as a template for your local setup

## Configuration

### Environment Variables

For security, API keys are not stored in configuration files. Instead, set the following environment variables:

**Required for Direct OTLP tests:**
```bash
DD_API_KEY=your_datadog_api_key_here
```

**Optional:**
```bash
DD_SITE=datadoghq.eu  # or datadoghq.com (defaults to datadoghq.eu)
```

### Setting Environment Variables

**Windows (PowerShell):**
```powershell
$env:DD_API_KEY = "your_api_key_here"
```

**Windows (Command Prompt):**
```cmd
set DD_API_KEY=your_api_key_here
```

**Linux/macOS:**
```bash
export DD_API_KEY=your_api_key_here
```

**Using .env file (recommended for development):**
1. Copy `.env.template` to `.env`
2. Fill in your actual API key in `.env`
3. The application will automatically load from `.env`

## Project Structure

### DirectOtlp Tests (2 tests)
- **Service Name**: `xunit-datadog-direct-test`
- **Target**: `https://otlp-intake.datadoghq.eu/v1/traces`
- **Purpose**: Tests direct OTLP submission to Datadog (bypassing agent)
- **Tests**: 
  - `SimpleDirectOtlpTest.DirectOtlpConnectivity_ShouldCreateAndExportSpan`
  - `DatadogDirectOtlpTest.SimpleDatadogSpanTest`

### AgentOtlp Tests (5 tests)
- **Service Name**: `datadog-agent-grpc-test`
- **Target**: `http://localhost:4317` (local Datadog Agent)
- **Purpose**: Tests OTLP submission through local Datadog Agent
- **Tests**: 
  - `DatadogAgentGrpcOtlpTest.DatabaseOperation_ShouldCreateSpan`
  - `DatadogAgentGrpcOtlpTest.MyTest_ShouldPass`
  - `DatadogAgentGrpcOtlpTest.HttpRequest_ShouldCreateSpan`
  - `DatadogAgentGrpcOtlpTest.DistributedTracing_ShouldPropagateTraceContext` 🆕
  - `DatadogAgentOtlpTest.SimpleAgentSpanTest`
- **Features**:
  - 🔗 **Distributed Tracing**: Simulates trace context propagation across services
  - 📡 **HTTP API Calls**: With trace parent/state propagation
  - 📨 **Message Producer/Consumer**: RabbitMQ-style messaging with trace context
  - 🔄 **End-to-End Scenarios**: Complete distributed trace workflows

### AzureAgentOtlp Tests (1 test)
- **Service Name**: `azure-datadog-agent-test`
- **Target**: `http://localhost:4317` (local Datadog Agent)
- **Purpose**: Tests Azure-specific connectivity through local Datadog Agent

## Running Tests

```bash
dotnet test DatadogOtelPoc.sln
```

Make sure you have:
1. Set the `DD_API_KEY` environment variable (for DirectOtlp tests)
2. Running Datadog Agent on localhost:4317 (for AgentOtlp tests)
