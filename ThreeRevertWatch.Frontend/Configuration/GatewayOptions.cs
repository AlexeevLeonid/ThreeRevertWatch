namespace ThreeRevertWatch.Frontend.Configuration;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string BaseUrl { get; set; } = "http://localhost:5080";
}

