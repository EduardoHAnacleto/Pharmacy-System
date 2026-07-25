using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PharmacyWorkerAPI.Infrastructure
{
    /// <summary>
    /// Turns any unhandled exception into a ProblemDetails response.
    /// </summary>
    /// <remarks>
    /// Every controller action used to be unguarded, so a failed file write or a
    /// concurrency conflict surfaced as a bare 500 — with a full stack trace
    /// attached, because the deployment also ran in the Development environment.
    /// The detail text here never includes exception messages: the log gets those,
    /// the client gets a trace id to quote.
    /// </remarks>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // A cancelled request is the client hanging up, not a server fault.
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                return false;
            }

            var traceId = httpContext.TraceIdentifier;

            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path} (traceId {TraceId}).",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado. Tente novamente.",
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            };

            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = problem.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

            return true;
        }
    }
}
