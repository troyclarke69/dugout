using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dugout.Api.Contracts;
using Dugout.Api.Endpoints;
using Dugout.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Dugout.Runner.Tests;

public sealed class ApiEndpointTests
{
    [Fact]
    public async Task ApiRoutesExposeExperimentsRunsHistoryComparisonAnalysisPlansAndOpenApi()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApi = await openApiResponse.Content.ReadAsStringAsync();
        Assert.Contains("/api/experiments", openApi, StringComparison.Ordinal);
        Assert.Contains("/api/runs/{runId}/analysis", openApi, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/experiments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/experiments/EXP001")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/experiments/EXP999")).StatusCode);

        var runResponse = await client.PostAsJsonAsync("/api/experiments/EXP001/run", new RunExperimentRequest());
        Assert.Equal(HttpStatusCode.Created, runResponse.StatusCode);
        Assert.Equal("/api/runs/RUN0001", runResponse.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs?latest=10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs/RUN0001")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/history/EXP001?latest=10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/baseline/EXP001")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/compare/RUN0001/RUN0002")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs/RUN0001/analysis")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs/RUN0001/plans")).StatusCode);
    }

    [Fact]
    public async Task RunEndpointRejectsInvalidOptionsAndMissingExperiments()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var invalidRequest = await client.PostAsJsonAsync("/api/experiments/EXP001/run", new { warmupRuns = -1, measuredRuns = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRequest.StatusCode);

        var missingExperiment = await client.PostAsJsonAsync("/api/experiments/EXP999/run", new RunExperimentRequest());
        Assert.Equal(HttpStatusCode.NotFound, missingExperiment.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi();
        builder.Services.AddValidation();
        builder.Services.AddSingleton<IDugoutApiService, FakeDugoutApiService>();
        var app = builder.Build();
        app.MapOpenApi();
        app.MapExperimentEndpoints();
        app.MapRunEndpoints();
        app.MapHistoricalEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class FakeDugoutApiService : IDugoutApiService
    {
        private static readonly ExperimentResponse Experiment = new(
            "EXP001", "Sample", "Test experiment", "SARGability", "Beginner", ["Date"], ["Compare query plans"]);
        private static readonly RunResponse Run = new(
            "RUN0001", "EXP001", "hash", "Success", 0, DateTimeOffset.Parse("2026-10-01T00:00:00Z"), DateTimeOffset.Parse("2026-10-01T00:01:00Z"),
            EmptyMetrics(), EmptyMetrics(), true, "Validation passed.", null, "SQL Server", "Developer", "test-machine",
            [new AdvisorFindingResponse("TestRule", "Test finding", "Test evidence", "Info")],
            [new AdvisorRecommendationResponse("TestRule", "Test recommendation", 0.9m)]);

        public Task<IReadOnlyList<ExperimentResponse>> ListExperimentsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExperimentResponse>>([Experiment]);
        public Task<ExperimentResponse?> GetExperimentAsync(string experimentId, CancellationToken cancellationToken) => Task.FromResult(experimentId == Experiment.Id ? Experiment : null);
        public Task<RunResponse?> RunExperimentAsync(string experimentId, RunExperimentRequest request, CancellationToken cancellationToken) => Task.FromResult(experimentId == Experiment.Id ? Run : null);
        public Task<IReadOnlyList<RunSummaryResponse>> ListRunsAsync(int latest, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RunSummaryResponse>>([Summary()]);
        public Task<RunResponse?> GetRunAsync(string runId, CancellationToken cancellationToken) => Task.FromResult(runId == Run.RunId ? Run : null);
        public Task<IReadOnlyList<RunSummaryResponse>> GetHistoryAsync(string experimentId, int latest, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RunSummaryResponse>>([Summary()]);
        public Task<BaselineResponse?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken) => Task.FromResult<BaselineResponse?>(new BaselineResponse("EXP001", "RUN0001", Run.StartedUtc, new Dictionary<string, decimal?> { ["Duration (ms)"] = 10m }));
        public Task<RunComparisonResponse?> CompareRunsAsync(string runA, string runB, CancellationToken cancellationToken) => Task.FromResult<RunComparisonResponse?>(new RunComparisonResponse(runA, runB, [], [], null, null));
        public Task<AdvisorAnalysisResponse?> GetAnalysisAsync(string runId, CancellationToken cancellationToken) => Task.FromResult<AdvisorAnalysisResponse?>(runId == Run.RunId ? new AdvisorAnalysisResponse(runId, Run.Findings, Run.Recommendations) : null);
        public Task<ExecutionPlansResponse?> GetPlansAsync(string runId, CancellationToken cancellationToken) => Task.FromResult<ExecutionPlansResponse?>(runId == Run.RunId ? new ExecutionPlansResponse(runId, null, null) : null);

        private static RunSummaryResponse Summary() => new("RUN0001", "EXP001", "Sample", "Success", Run.StartedUtc, Run.CompletedUtc, 10m, 20m, 3m, 4m, "SQL Server", "test-machine", "basehash", "opthash");

        private static MetricSummaryResponse EmptyMetrics() => new(0, null, null, null, null, null, null, null, null, null, null, null, 0, 0);
    }
}
