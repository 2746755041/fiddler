using Microsoft.Extensions.DependencyInjection;

namespace FiddlerCapture.Engine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFiddlerCaptureEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICapturedSessionStore, InMemoryCapturedSessionStore>();
        services.AddSingleton<FiddlerCaptureEngine>();
        services.AddSingleton<IFiddlerCaptureEngine>(sp => sp.GetRequiredService<FiddlerCaptureEngine>());
        services.AddHostedService(sp => sp.GetRequiredService<FiddlerCaptureEngine>());

        return services;
    }
}
