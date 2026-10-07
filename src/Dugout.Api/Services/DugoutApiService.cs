using Dugout.Api.Contracts;
using Dugout.Api.Middleware;
using Dugout.Contracts;
using Dugout.Runner;
using Dugout.Storage;

namespace Dugout.Api.Services;

public sealed class DugoutApiService : IDugoutApiService
{
    private readonly IBenchmarkRepository _repository;
    private readonly string _connectionString;
    private readonly string _resultsConnectionString;
    private readonly string _experimentsRoot;
    private readonly string _resultsRoot;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _repositoryInitialized;

    public DugoutApiService(IBenchmarkRepository repository, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _repository = repository;
        _connectionString = ApiConnectionStrings.GetBenchmarkConnectionString(configuration);
        _resultsConnectionString = ApiConnectionStrings.GetResultsConnectionString(configuration);
        var workspaceRoot = FindWorkspaceRoot(environment.ContentRootPath);
        _experimentsRoot = configuration["DUGOUT_EXPERIMENTS_ROOT"]
            ?? Path.Combine(workspaceRoot, "experiments");
        _resultsRoot = configuration["DUGOUT_RESULTS_ROOT"]
            ?? Path.Combine(workspaceRoot, "results");
    }

    public Task<IReadOnlyList<ExperimentResponse>> ListExperimentsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ExperimentResponse> experiments = ExperimentRepository.List(_experimentsRoot)
            .Select(MapExperiment)
            .ToList();
        return Task.FromResult(experiments);
    }

    public Task<ExperimentResponse?> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var experiment = LoadExperiment(experimentId);
        return Task.FromResult(experiment is null ? null : MapExperiment(experiment));
    }

    public async Task<RunResponse?> RunExperimentAsync(string experimentId, RunExperimentRequest request, CancellationToken cancellationToken)
    {
        ValidateExperimentId(experimentId);
        if (request.WarmupRuns is < 0 or > 1000 || request.MeasuredRuns is < 1 or > 1000)
        {
            throw new ArgumentException("WarmupRuns must be between 0 and 1000 and MeasuredRuns must be between 1 and 1000.");
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new DugoutApiUnavailableException("Benchmark database connection is not configured.");
        }

        var experiment = LoadExperiment(experimentId);
        if (experiment is null)
        {
            return null;
        }

        using var executor = new SqlQueryExecutor(_connectionString);
        try
        {
            var result = await new BenchmarkPipeline(executor, _repository).RunAsync(
                experiment,
                Path.Combine(_experimentsRoot, experimentId),
                _resultsRoot,
                request.WarmupRuns,
                request.MeasuredRuns,
                cancellationToken);
            return MapRun(result);
        }
        finally
        {
            await executor.CloseAsync(CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<RunSummaryResponse>> ListRunsAsync(int latest, CancellationToken cancellationToken)
    {
        ValidateLatest(latest);
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var history = await _repository.GetHistoryAsync(latest, cancellationToken: cancellationToken);
        return history.Select(MapRunSummary).ToList();
    }

    public async Task<RunResponse?> GetRunAsync(string runId, CancellationToken cancellationToken)
    {
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var result = await _repository.GetRunAsync(runId, cancellationToken);
        return result is null ? null : MapRun(result);
    }

    public async Task<IReadOnlyList<RunSummaryResponse>> GetHistoryAsync(string experimentId, int latest, CancellationToken cancellationToken)
    {
        ValidateExperimentId(experimentId);
        ValidateLatest(latest);
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var history = await _repository.GetHistoryAsync(latest, experimentId, cancellationToken: cancellationToken);
        return history.Select(MapRunSummary).ToList();
    }

    public async Task<BaselineResponse?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken)
    {
        ValidateExperimentId(experimentId);
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var baseline = await _repository.GetBaselineAsync(experimentId, cancellationToken);
        return baseline is null
            ? null
            : new BaselineResponse(baseline.ExperimentId, baseline.RunId, baseline.EstablishedUtc, baseline.Metrics);
    }

    public async Task<RunComparisonResponse?> CompareRunsAsync(string runA, string runB, CancellationToken cancellationToken)
    {
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var previous = await _repository.GetRunAsync(runA, cancellationToken);
        var current = await _repository.GetRunAsync(runB, cancellationToken);
        if (previous is null || current is null)
        {
            return null;
        }

        if (!string.Equals(previous.ExperimentId, current.ExperimentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Runs from different experiments cannot be compared.");
        }

        var comparison = BenchmarkRunComparer.Compare(previous, current);
        return new RunComparisonResponse(
            comparison.PreviousRunId,
            comparison.CurrentRunId,
            comparison.BaselineMetrics.Select(MapMetricComparison).ToList(),
            comparison.OptimizedMetrics.Select(MapMetricComparison).ToList(),
            MapPlanComparison(comparison.BaselinePlanComparison),
            MapPlanComparison(comparison.OptimizedPlanComparison));
    }

    public async Task<AdvisorAnalysisResponse?> GetAnalysisAsync(string runId, CancellationToken cancellationToken)
    {
        await EnsureRepositoryInitializedAsync(cancellationToken);
        var result = await _repository.GetRunAsync(runId, cancellationToken);
        return result is null
            ? null
            : new AdvisorAnalysisResponse(
                result.RunId,
                result.AdvisorFindings.Select(item => new AdvisorFindingResponse(item.Rule, item.Finding, item.Evidence, item.Severity)).ToList(),
                result.AdvisorRecommendations.Select(item => new AdvisorRecommendationResponse(item.Rule, item.Recommendation, item.Confidence)).ToList());
    }

    public async Task<ExecutionPlansResponse?> GetPlansAsync(string runId, CancellationToken cancellationToken)
    {
        await EnsureRepositoryInitializedAsync(cancellationToken);
        if (await _repository.GetRunAsync(runId, cancellationToken) is null)
        {
            return null;
        }

        var baseline = await _repository.GetExecutionPlanAsync(runId, "Baseline", cancellationToken);
        var optimized = await _repository.GetExecutionPlanAsync(runId, "Optimized", cancellationToken);
        return new ExecutionPlansResponse(runId, MapPlan(baseline), MapPlan(optimized));
    }

    private async Task EnsureRepositoryInitializedAsync(CancellationToken cancellationToken)
    {
        if (_repositoryInitialized)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_resultsConnectionString))
        {
            throw new DugoutApiUnavailableException("Dugout results database connection is not configured.");
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_repositoryInitialized)
            {
                await _repository.InitializeAsync(cancellationToken);
                _repositoryInitialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private ExperimentDefinition? LoadExperiment(string experimentId)
    {
        ValidateExperimentId(experimentId);
        var experimentPath = Path.Combine(_experimentsRoot, experimentId, "experiment.json");
        return File.Exists(experimentPath) ? ExperimentRepository.Load(experimentPath) : null;
    }

    private static ExperimentResponse MapExperiment(ExperimentDefinition experiment)
    {
        return new ExperimentResponse(
            experiment.Id,
            experiment.Name,
            experiment.Description,
            experiment.Category,
            experiment.Difficulty,
            experiment.Tags,
            experiment.LearningObjectives);
    }

    private static RunSummaryResponse MapRunSummary(BenchmarkHistoryRecord run)
    {
        return new RunSummaryResponse(
            run.RunId,
            run.ExperimentId,
            run.ExperimentName,
            run.Status,
            run.StartedUtc,
            run.CompletedUtc,
            run.OptimizedDurationMedianMs,
            run.OptimizedLogicalReadsMedian,
            run.OptimizedCpuTimeMedianMs,
            run.OptimizedSqlElapsedTimeMedianMs,
            run.SqlServerVersion,
            run.MachineName,
            run.BaselinePlanHash,
            run.OptimizedPlanHash);
    }

    private static RunResponse MapRun(BenchmarkResultDocument result)
    {
        return new RunResponse(
            result.RunId,
            result.ExperimentId,
            result.ExperimentHash,
            result.Status,
            result.ExitCode,
            result.StartedUtc,
            result.CompletedUtc,
            MapMetricSummary(result.Baseline),
            MapMetricSummary(result.Optimized),
            result.Validation.Passed,
            result.Validation.Details,
            result.Failure?.Reason,
            result.Environment.SqlServerVersion,
            result.Environment.SqlServerEdition,
            result.Environment.MachineName,
            result.AdvisorFindings.Select(item => new AdvisorFindingResponse(item.Rule, item.Finding, item.Evidence, item.Severity)).ToList(),
            result.AdvisorRecommendations.Select(item => new AdvisorRecommendationResponse(item.Rule, item.Recommendation, item.Confidence)).ToList());
    }

    private static MetricSummaryResponse MapMetricSummary(BenchmarkRunsSummary summary)
    {
        return new MetricSummaryResponse(
            summary.Statistics.SampleCount,
            GetMedian(summary.Statistics),
            GetMedian(summary.LogicalReadsStatistics),
            GetMedian(summary.PhysicalReadsStatistics),
            GetMedian(summary.CpuTimeMsStatistics),
            GetMedian(summary.SqlElapsedTimeMsStatistics),
            GetMedian(summary.RequestedMemoryKbStatistics),
            GetMedian(summary.GrantedMemoryKbStatistics),
            GetMedian(summary.UsedMemoryKbStatistics),
            GetMedian(summary.TempDbPagesStatistics),
            GetMedian(summary.WorktableLogicalReadsStatistics),
            GetMedian(summary.DegreeOfParallelismStatistics),
            summary.Runs.Count(item => item.UsedParallelPlan == true),
            summary.Runs.Count(item => item.UsedParallelPlan.HasValue));
    }

    private static decimal? GetMedian(BenchmarkStatistics statistics) => statistics.SampleCount > 0 ? statistics.Median : null;

    private static MetricComparisonResponse MapMetricComparison(BenchmarkMetricComparison metric)
    {
        return new MetricComparisonResponse(metric.Metric, metric.PreviousMedian, metric.CurrentMedian, metric.ImprovementPercent);
    }

    private static PlanComparisonResponse? MapPlanComparison(ExecutionPlanComparison? comparison)
    {
        return comparison is null
            ? null
            : new PlanComparisonResponse(
                comparison.PlanHashesMatch,
                comparison.OperatorChanges.Select(item => new OperatorChangeResponse(item.PhysicalOperator, item.BaselineCount, item.OptimizedCount)).ToList());
    }

    private static ExecutionPlanResponse? MapPlan(ExecutionPlanArtifact? plan)
    {
        return plan is null
            ? null
            : new ExecutionPlanResponse(plan.PlanHash, plan.EstimatedCost, plan.EstimatedRows, plan.EstimatedSubtreeCost, plan.Operators, plan.PlanXml);
    }

    private static void ValidateExperimentId(string experimentId)
    {
        if (!ExperimentValidator.IsValidExperimentId(experimentId))
        {
            throw new ArgumentException("Experiment IDs must use the EXP### format.", nameof(experimentId));
        }
    }

    private static void ValidateLatest(int latest)
    {
        if (latest is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(latest), "The latest value must be between 1 and 500.");
        }
    }

    private static string FindWorkspaceRoot(string contentRoot)
    {
        for (var directory = new DirectoryInfo(contentRoot); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "experiments")))
            {
                return directory.FullName;
            }
        }

        return contentRoot;
    }
}
