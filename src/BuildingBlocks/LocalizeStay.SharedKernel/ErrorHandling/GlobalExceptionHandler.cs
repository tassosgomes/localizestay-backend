using System.Net;
using FluentValidation;
using LocalizeStay.SharedKernel.Correlation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.SharedKernel.ErrorHandling;

/// <summary>
/// Single point of translation from exceptions to RFC 9457 Problem Details. Never exposes stack
/// traces, secrets or infrastructure details — only a stable error code, a safe title and the
/// correlation id needed to look the failure up in logs and traces (architecture baseline: Erros e
/// versionamento).
/// </summary>
public sealed class GlobalExceptionHandler(
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(exception);
        LogException(exception, problemDetails);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(Exception exception)
    {
        var (status, title, errorCode) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = $"https://docs.localizestay.com/errors/{errorCode}",
        };

        problemDetails.Extensions["correlationId"] = correlationIdAccessor.CorrelationId;
        problemDetails.Extensions["errorCode"] = errorCode;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        }

        return problemDetails;
    }

    private static (HttpStatusCode Status, string Title, string ErrorCode) MapException(Exception exception) => exception switch
    {
        NotFoundException notFound => (HttpStatusCode.NotFound, notFound.Message, notFound.ErrorCode),
        ConflictException conflict => (HttpStatusCode.Conflict, conflict.Message, conflict.ErrorCode),
        BusinessRuleViolationException rule => (HttpStatusCode.UnprocessableEntity, rule.Message, rule.ErrorCode),
        ExternalDependencyException external => (HttpStatusCode.BadGateway, "An external dependency is unavailable.", external.ErrorCode),
        ValidationException => (HttpStatusCode.BadRequest, "One or more validation errors occurred.", "validation_failed"),
        DomainException domain => (HttpStatusCode.BadRequest, domain.Message, domain.ErrorCode),
        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", "internal_server_error"),
    };

    private void LogException(Exception exception, ProblemDetails problemDetails)
    {
        var logLevel = problemDetails.Status >= 500 ? LogLevel.Error : LogLevel.Warning;
        logger.Log(
            logLevel,
            exception,
            "Request failed with {ErrorCode} ({StatusCode}). CorrelationId: {CorrelationId}.",
            problemDetails.Extensions["errorCode"],
            problemDetails.Status,
            correlationIdAccessor.CorrelationId);
    }
}
