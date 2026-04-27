using Microsoft.Extensions.Options;
using Serilog;
using ThreeRevertWatch.Frontend.Components;
using ThreeRevertWatch.Frontend.Configuration;
using ThreeRevertWatch.Frontend.Services;
using ThreeRevertWatch.Infrastructure.Logging;

SerilogExtensions.ConfigureBootstrap("Frontend");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((_, configuration) =>
        configuration.ConfigureThreeRevertWatchLogging(builder.Configuration, "Frontend"));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddOptions<GatewayOptions>()
        .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
        .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "Gateway:BaseUrl is required")
        .ValidateOnStart();

    builder.Services.AddHttpClient<GatewayApiClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
    });
    builder.Services.AddTransient<ConflictRealtimeClient>();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseAntiforgery();
    app.MapHealthChecks("/health");

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Frontend terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

