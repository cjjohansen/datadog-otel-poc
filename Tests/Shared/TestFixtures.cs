using Microsoft.Extensions.Configuration;

namespace DatadogOtelPocTests;

public static class TestFixtures
{
	public static IConfigurationRoot Configuration { get; }
	public static DatadogConfiguration Datadog { get; }
	public static OtlpConfiguration Otlp { get; }

	static TestFixtures()
	{
		Configuration = new ConfigurationBuilder()
			.SetBasePath(System.IO.Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
			.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
			.AddEnvironmentVariables()
			.Build();

		Datadog = new DatadogConfiguration();
		Configuration.GetSection("Datadog").Bind(Datadog);

		Otlp = new OtlpConfiguration();
		Configuration.GetSection("OpenTelemetry").Bind(Otlp);
	}
}
