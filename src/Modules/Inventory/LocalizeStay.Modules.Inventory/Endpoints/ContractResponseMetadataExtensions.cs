using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalizeStay.Modules.Inventory.Endpoints;

internal static class ContractResponseMetadataExtensions
{
    public static RouteHandlerBuilder WithContractResponses<TResponse>(this RouteHandlerBuilder builder, int successStatusCode, params int[] problemStatusCodes)
    {
        builder.Produces<TResponse>(successStatusCode, "application/json");

        foreach (var statusCode in problemStatusCodes)
        {
            builder.ProducesProblem(statusCode, "application/problem+json");
        }

        return builder;
    }
}
