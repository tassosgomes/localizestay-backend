using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LocalizeStay.SharedKernel.Security;

/// <summary>
/// Helpers that write a RFC 9457 Problem Details body with the correct
/// <c>application/problem+json</c> content type. <see cref="HttpResponseJsonExtensions.WriteAsJsonAsync"/>
/// rewrites the content type to <c>application/json</c>, which is incompatible with the OpenAPI
/// contract; this serializer keeps the explicit media type on every error path.
/// </summary>
internal static class ProblemDetailsWriter
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes <paramref name="problem"/> into <paramref name="response"/> with <c>application/problem+json</c>.</summary>
    public static async Task WriteAsync(HttpResponse response, ProblemDetails problem, CancellationToken cancellationToken = default)
    {
        response.ContentType = MediaTypeNames.Application.ProblemJson;
        await JsonSerializer.SerializeAsync(response.Body, problem, _serializerOptions, cancellationToken);
    }
}
