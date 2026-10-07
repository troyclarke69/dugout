using System.ComponentModel.DataAnnotations;

namespace Dugout.Api.Contracts;

/// <summary>Experiment metadata exposed by the public API.</summary>
public sealed record ExperimentResponse(
    string Id,
    string Name,
    string? Description,
    string Category,
    string Difficulty,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> LearningObjectives);

/// <summary>Options for running a benchmark experiment.</summary>
public sealed record RunExperimentRequest
{
    /// <summary>Number of untimed warmup executions for each variant.</summary>
    [Range(0, 1000)]
    public int WarmupRuns { get; init; } = 3;

    /// <summary>Number of measured executions for each variant.</summary>
    [Range(1, 1000)]
    public int MeasuredRuns { get; init; } = 10;
}

/// <summary>Performance metrics summarized for one benchmark variant.</summary>
public sealed record MetricSummaryResponse(
    int SampleCount,
    decimal? DurationMedianMs,
    decimal? LogicalReadsMedian,
    decimal? PhysicalReadsMedian,
    decimal? CpuTimeMedianMs,
    decimal? SqlElapsedTimeMedianMs,
    decimal? RequestedMemoryMedianKb,
    decimal? GrantedMemoryMedianKb,
    decimal? UsedMemoryMedianKb,
    decimal? TempDbPagesMedian,
    decimal? WorktableLogicalReadsMedian,
    decimal? DegreeOfParallelismMedian,
    int ParallelPlanCount,
    int ParallelPlanSampleCount);

/// <summary>Summary information for a stored benchmark run.</summary>
public sealed record RunSummaryResponse(
    string RunId,
    string ExperimentId,
    string ExperimentName,
    string Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    decimal? OptimizedDurationMedianMs,
    decimal? OptimizedLogicalReadsMedian,
    decimal? OptimizedCpuTimeMedianMs,
    decimal? OptimizedSqlElapsedTimeMedianMs,
    string SqlServerVersion,
    string MachineName,
    string? BaselinePlanHash,
    string? OptimizedPlanHash);

/// <summary>Detailed metrics and advisor analysis for a benchmark run.</summary>
public sealed record RunResponse(
    string RunId,
    string ExperimentId,
    string ExperimentHash,
    string Status,
    int ExitCode,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    MetricSummaryResponse Baseline,
    MetricSummaryResponse Optimized,
    bool ValidationPassed,
    string ValidationDetails,
    string? FailureReason,
    string SqlServerVersion,
    string SqlServerEdition,
    string MachineName,
    IReadOnlyList<AdvisorFindingResponse> Findings,
    IReadOnlyList<AdvisorRecommendationResponse> Recommendations);

/// <summary>A finding and its measured supporting evidence.</summary>
public sealed record AdvisorFindingResponse(string Rule, string Finding, string Evidence, string Severity);

/// <summary>A deterministic recommendation with its rule and confidence.</summary>
public sealed record AdvisorRecommendationResponse(string Rule, string Recommendation, decimal Confidence);

/// <summary>Performance baseline stored for an experiment.</summary>
public sealed record BaselineResponse(
    string ExperimentId,
    string RunId,
    DateTimeOffset EstablishedUtc,
    IReadOnlyDictionary<string, decimal?> Metrics);

/// <summary>One metric comparison between two benchmark runs.</summary>
public sealed record MetricComparisonResponse(
    string Metric,
    decimal PreviousMedian,
    decimal CurrentMedian,
    decimal? ImprovementPercent);

/// <summary>A physical operator count change between two plans.</summary>
public sealed record OperatorChangeResponse(string PhysicalOperator, int PreviousCount, int CurrentCount);

/// <summary>Plan hash and operator changes for one benchmark variant.</summary>
public sealed record PlanComparisonResponse(bool PlanHashesMatch, IReadOnlyList<OperatorChangeResponse> OperatorChanges);

/// <summary>Metric and plan differences between two benchmark runs.</summary>
public sealed record RunComparisonResponse(
    string PreviousRunId,
    string CurrentRunId,
    IReadOnlyList<MetricComparisonResponse> BaselineMetrics,
    IReadOnlyList<MetricComparisonResponse> OptimizedMetrics,
    PlanComparisonResponse? BaselinePlan,
    PlanComparisonResponse? OptimizedPlan);

/// <summary>Advisor findings and recommendations attached to a benchmark run.</summary>
public sealed record AdvisorAnalysisResponse(
    string RunId,
    IReadOnlyList<AdvisorFindingResponse> Findings,
    IReadOnlyList<AdvisorRecommendationResponse> Recommendations);

/// <summary>An execution plan and its parsed operator metadata.</summary>
public sealed record ExecutionPlanResponse(
    string PlanHash,
    decimal? EstimatedCost,
    decimal? EstimatedRows,
    decimal? EstimatedSubtreeCost,
    IReadOnlyDictionary<string, int> Operators,
    string PlanXml);

/// <summary>Execution plans captured for a benchmark run.</summary>
public sealed record ExecutionPlansResponse(
    string RunId,
    ExecutionPlanResponse? Baseline,
    ExecutionPlanResponse? Optimized);
