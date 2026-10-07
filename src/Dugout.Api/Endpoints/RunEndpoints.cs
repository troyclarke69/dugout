using Dugout.Api.Contracts;
using Dugout.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dugout.Api.Endpoints;

public static class RunEndpoints
{
    public static WebApplication MapRunEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/runs").WithTags("Runs");
        group.MapGet("/", async Task<Ok<IReadOnlyList<RunSummaryResponse>>> (
            int? latest,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ListRunsAsync(latest ?? 20, cancellationToken)))
            .WithName("ListRuns")
            .WithSummary("List benchmark runs")
            .WithDescription("Returns the most recent stored benchmark runs.");

        group.MapGet("/{runId}", async Task<Results<Ok<RunResponse>, NotFound>> (
            string runId,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var run = await service.GetRunAsync(runId, cancellationToken);
            return run is null ? TypedResults.NotFound() : TypedResults.Ok(run);
        })
            .WithName("GetRun")
            .WithSummary("Get a benchmark run")
            .WithDescription("Returns run status, metrics, validation, environment, and advisor summaries.")
            .Produces<RunResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{runId}/analysis", async Task<Results<Ok<AdvisorAnalysisResponse>, NotFound>> (
            string runId,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var analysis = await service.GetAnalysisAsync(runId, cancellationToken);
            return analysis is null ? TypedResults.NotFound() : TypedResults.Ok(analysis);
        })
            .WithName("GetRunAnalysis")
            .WithSummary("Get advisor analysis for a run")
            .WithDescription("Returns evidence-backed findings and deterministic recommendations.")
            .Produces<AdvisorAnalysisResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{runId}/plans", async Task<Results<Ok<ExecutionPlansResponse>, NotFound>> (
            string runId,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var plans = await service.GetPlansAsync(runId, cancellationToken);
            return plans is null ? TypedResults.NotFound() : TypedResults.Ok(plans);
        })
            .WithName("GetRunPlans")
            .WithSummary("Get execution plans for a run")
            .WithDescription("Returns plan hashes, estimates, operator counts, and raw Showplan XML for both variants.")
            .Produces<ExecutionPlansResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
