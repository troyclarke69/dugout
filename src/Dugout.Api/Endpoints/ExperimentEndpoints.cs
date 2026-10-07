using Dugout.Api.Contracts;
using Dugout.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dugout.Api.Endpoints;

public static class ExperimentEndpoints
{
    public static WebApplication MapExperimentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/experiments").WithTags("Experiments");
        group.MapGet("/", async Task<Ok<IReadOnlyList<ExperimentResponse>>> (
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ListExperimentsAsync(cancellationToken)))
            .WithName("ListExperiments")
            .WithSummary("List benchmark experiments")
            .WithDescription("Returns all readable experiment definitions available to the runner.");

        group.MapGet("/{experimentId}", async Task<Results<Ok<ExperimentResponse>, NotFound>> (
            string experimentId,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var experiment = await service.GetExperimentAsync(experimentId, cancellationToken);
            return experiment is null ? TypedResults.NotFound() : TypedResults.Ok(experiment);
        })
            .WithName("GetExperiment")
            .WithSummary("Get an experiment")
            .WithDescription("Returns experiment metadata by ID.")
            .Produces<ExperimentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{experimentId}/run", async Task<Results<Created<RunResponse>, NotFound, ValidationProblem>> (
            string experimentId,
            RunExperimentRequest request,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = new Dictionary<string, string[]>();
            if (request.WarmupRuns is < 0 or > 1000)
            {
                validationErrors[nameof(request.WarmupRuns)] = ["WarmupRuns must be between 0 and 1000."];
            }

            if (request.MeasuredRuns is < 1 or > 1000)
            {
                validationErrors[nameof(request.MeasuredRuns)] = ["MeasuredRuns must be between 1 and 1000."];
            }

            if (validationErrors.Count > 0)
            {
                return TypedResults.ValidationProblem(validationErrors);
            }

            var run = await service.RunExperimentAsync(experimentId, request, cancellationToken);
            return run is null
                ? TypedResults.NotFound()
                : TypedResults.Created($"/api/runs/{run.RunId}", run);
        })
            .WithName("RunExperiment")
            .WithSummary("Run a benchmark experiment")
            .WithDescription("Executes a validated benchmark and returns the created run resource.")
            .Produces<RunResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return app;
    }
}
