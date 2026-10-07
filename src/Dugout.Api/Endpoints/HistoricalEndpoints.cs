using Dugout.Api.Contracts;
using Dugout.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dugout.Api.Endpoints;

public static class HistoricalEndpoints
{
    public static WebApplication MapHistoricalEndpoints(this WebApplication app)
    {
        var history = app.MapGroup("/api/history").WithTags("History");
        history.MapGet("/{experimentId}", async Task<Ok<IReadOnlyList<RunSummaryResponse>>> (
            string experimentId,
            int? latest,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetHistoryAsync(experimentId, latest ?? 20, cancellationToken)))
            .WithName("GetExperimentHistory")
            .WithSummary("Get benchmark history")
            .WithDescription("Returns recent benchmark runs for an experiment, including available trend metrics.");

        var baselines = app.MapGroup("/api/baseline").WithTags("Baselines");
        baselines.MapGet("/{experimentId}", async Task<Results<Ok<BaselineResponse>, NotFound>> (
            string experimentId,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var baseline = await service.GetBaselineAsync(experimentId, cancellationToken);
            return baseline is null ? TypedResults.NotFound() : TypedResults.Ok(baseline);
        })
            .WithName("GetExperimentBaseline")
            .WithSummary("Get an experiment baseline")
            .WithDescription("Returns the stored baseline run and its metric values.")
            .Produces<BaselineResponse>()
            .Produces(StatusCodes.Status404NotFound);

        var comparisons = app.MapGroup("/api/compare").WithTags("Comparisons");
        comparisons.MapGet("/{runA}/{runB}", async Task<Results<Ok<RunComparisonResponse>, NotFound>> (
            string runA,
            string runB,
            IDugoutApiService service,
            CancellationToken cancellationToken) =>
        {
            var comparison = await service.CompareRunsAsync(runA, runB, cancellationToken);
            return comparison is null ? TypedResults.NotFound() : TypedResults.Ok(comparison);
        })
            .WithName("CompareRuns")
            .WithSummary("Compare two benchmark runs")
            .WithDescription("Compares baseline and optimized metrics and execution plans for two runs of the same experiment.")
            .Produces<RunComparisonResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
