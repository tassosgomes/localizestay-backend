using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.ErrorHandling;

public static class ExceptionHandlingServiceCollectionExtensions
{
    /// <summary>Registers the global exception handler and RFC 9457 Problem Details generation.</summary>
    public static IServiceCollection AddLocalizeStayProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
