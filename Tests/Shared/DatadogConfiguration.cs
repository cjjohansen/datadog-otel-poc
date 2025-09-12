namespace DatadogOtelPocTests;

public record DatadogConfiguration
{
    public string ApiKey { get; init; } = string.Empty;
    public string Site { get; init; } = string.Empty;
}
