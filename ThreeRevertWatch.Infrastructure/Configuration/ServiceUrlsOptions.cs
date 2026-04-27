namespace ThreeRevertWatch.Infrastructure.Configuration;

public sealed class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    public string AggregatorBaseUrl { get; set; } = "http://localhost:5084";
    public string GatewayPublicUrl { get; set; } = "http://localhost:8080";
}

