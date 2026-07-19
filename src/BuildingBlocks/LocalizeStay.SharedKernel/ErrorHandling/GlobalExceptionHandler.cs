using System.Net;
using System.Net.Mime;
using System.Text.Json;
using FluentValidation;
using LocalizeStay.SharedKernel.Correlation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.SharedKernel.ErrorHandling;

/// <summary>
/// Single point of translation from exceptions to RFC 9457 Problem Details. Never exposes stack
/// traces, secrets or infrastructure details — only the stable fields required by the OpenAPI
/// contract: <c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>instance</c>, <c>code</c>,
/// <c>traceId</c>, plus optional <c>errors</c> and <c>metadata</c> (architecture baseline: Erros e
/// versionamento; RFC 9457 baseline).
/// </summary>
public sealed class GlobalExceptionHandler(
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(exception, httpContext);

        LogException(exception, problemDetails);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = MediaTypeNames.Application.ProblemJson;
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, _serializerOptions, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(Exception exception, HttpContext httpContext)
    {
        var (status, title, errorCode, type, detail) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = type,
            Detail = string.IsNullOrWhiteSpace(detail) ? exception.Message : detail,
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["code"] = errorCode;
        // AsyncLocal-backed values do not survive the upstream exception propagation, so prefer the
        // correlation id mirrored into HttpContext.Items by CorrelationIdMiddleware when available.
        problemDetails.Extensions["traceId"] = ResolveTraceId(httpContext);
        problemDetails.Extensions["metadata"] = BuildMetadata(exception);
        problemDetails.Extensions["errors"] = BuildErrors(exception);

        return problemDetails;
    }

    private string ResolveTraceId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var stored) && stored is string mirrored)
        {
            return mirrored;
        }

        if (!string.IsNullOrWhiteSpace(httpContext.TraceIdentifier))
        {
            return httpContext.TraceIdentifier;
        }

        return correlationIdAccessor.CorrelationId;
    }

    private static IReadOnlyList<ValidationErrorOutput> BuildErrors(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return validationException.Errors
                .Select(error => new ValidationErrorOutput(
                    error.PropertyName,
                    error.ErrorCode,
                    error.ErrorMessage))
                .ToList();
        }

        return [];
    }

    private static IReadOnlyDictionary<string, object> BuildMetadata(Exception exception)
    {
        if (exception is ConflictException conflict && conflict.ConflictingResourceId is not null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["conflictingResourceId"] = conflict.ConflictingResourceId.Value,
            };
        }

        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static (HttpStatusCode Status, string Title, string ErrorCode, string Type, string? Detail) MapException(Exception exception) => exception switch
    {
        NotFoundException notFound => (
            HttpStatusCode.NotFound,
            "Recurso não encontrado",
            notFound.ErrorCode,
            "https://api.localizestay.com/problems/not-found",
            notFound.Message),
        ConflictException conflict => (
            HttpStatusCode.Conflict,
            "Conflito com unicidade, versão ou estado concorrente do recurso",
            conflict.ErrorCode,
            "https://api.localizestay.com/problems/conflict",
            conflict.Message),
        BusinessRuleViolationException rule => (
            HttpStatusCode.UnprocessableEntity,
            "Requisição válida que viola uma regra de negócio",
            rule.ErrorCode,
            "https://api.localizestay.com/problems/business-rule",
            rule.Message),
        ExternalDependencyException external => (
            HttpStatusCode.BadGateway,
            "Uma dependência externa está indisponível.",
            external.ErrorCode,
            "https://api.localizestay.com/problems/external-dependency",
            "Uma dependência externa está indisponível."),
        ValidationException => (
            HttpStatusCode.BadRequest,
            "Requisição malformada ou parâmetro sintaticamente inválido",
            "BAD_REQUEST",
            "https://api.localizestay.com/problems/bad-request",
            "Um ou mais campos não passaram na validação."),
        DomainException domain => (
            HttpStatusCode.BadRequest,
            "Erro de domínio",
            domain.ErrorCode,
            "https://api.localizestay.com/problems/domain",
            domain.Message),
        _ => (
            HttpStatusCode.InternalServerError,
            "Erro interno",
            "INTERNAL_ERROR",
            "https://api.localizestay.com/problems/internal-error",
            "Ocorreu uma falha inesperada. Tente novamente mais tarde."),
    };

    private void LogException(Exception exception, ProblemDetails problemDetails)
    {
        var logLevel = problemDetails.Status >= 500 ? LogLevel.Error : LogLevel.Warning;
        logger.Log(
            logLevel,
            exception,
            "Request failed with {ErrorCode} ({StatusCode}). TraceId: {TraceId}.",
            problemDetails.Extensions["code"],
            problemDetails.Status,
            correlationIdAccessor.CorrelationId);
    }

    private sealed record ValidationErrorOutput(string? Field, string Code, string Message);
}
