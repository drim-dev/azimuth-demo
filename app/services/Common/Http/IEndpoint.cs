using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Http;

/// <summary>A slice's own route registration, so routes live beside the logic they reach.</summary>
public interface IEndpoint
{
    void MapEndpoint(WebApplication app);
}

public static class EndpointRegistration
{
    /// <summary>Maps every endpoint the assembly declares, so adding a slice registers nothing centrally.</summary>
    public static void MapEndpoints<TAssemblyMarker>(this WebApplication app)
    {
        var endpoints = typeof(TAssemblyMarker).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpoint>();

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(app);
        }
    }
}
