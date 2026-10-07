using System.Text.Json.Serialization;

namespace Dugout.Contracts;

public sealed class ExperimentDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "Beginner";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("learningObjectives")]
    public List<string> LearningObjectives { get; set; } = new();

    [JsonPropertyName("setupScript")]
    public string? SetupScript { get; set; }

    [JsonPropertyName("validationScript")]
    public string? ValidationScript { get; set; }

    [JsonPropertyName("baselineScript")]
    public string BaselineScript { get; set; } = string.Empty;

    [JsonPropertyName("optimizedScript")]
    public string OptimizedScript { get; set; } = string.Empty;

    [JsonPropertyName("cleanupScript")]
    public string? CleanupScript { get; set; }

    [JsonIgnore]
    public string ExperimentHash { get; set; } = string.Empty;

    [JsonIgnore]
    public string DefinitionSnapshot { get; set; } = string.Empty;
}

public sealed class BenchmarkOptions
{
    [JsonPropertyName("warmupRuns")]
    public int WarmupRuns { get; set; } = 3;

    [JsonPropertyName("measuredRuns")]
    public int MeasuredRuns { get; set; } = 10;
}

public sealed class BenchmarkRunRecord
{
    [JsonPropertyName("sequenceNumber")]
    public int SequenceNumber { get; set; }

    [JsonPropertyName("durationMs")]
    public decimal DurationMs { get; set; }

    [JsonPropertyName("rowsReturned")]
    public long RowsReturned { get; set; }

    [JsonPropertyName("logicalReads")]
    public long? LogicalReads { get; set; }

    [JsonPropertyName("physicalReads")]
    public long? PhysicalReads { get; set; }

    [JsonPropertyName("scanCount")]
    public long? ScanCount { get; set; }

    [JsonPropertyName("cpuTimeMs")]
    public decimal? CpuTimeMs { get; set; }

    [JsonPropertyName("sqlElapsedTimeMs")]
    public decimal? SqlElapsedTimeMs { get; set; }

    [JsonPropertyName("requestedMemoryKb")]
    public long? RequestedMemoryKb { get; set; }

    [JsonPropertyName("grantedMemoryKb")]
    public long? GrantedMemoryKb { get; set; }

    [JsonPropertyName("usedMemoryKb")]
    public long? UsedMemoryKb { get; set; }

    [JsonPropertyName("tempDbPages")]
    public long? TempDbPages { get; set; }

    [JsonPropertyName("tempDbAllocatedPages")]
    public long? TempDbAllocatedPages { get; set; }

    [JsonPropertyName("worktableLogicalReads")]
    public long? WorktableLogicalReads { get; set; }

    [JsonPropertyName("degreeOfParallelism")]
    public int? DegreeOfParallelism { get; set; }

    [JsonPropertyName("usedParallelPlan")]
    public bool? UsedParallelPlan { get; set; }

    [JsonPropertyName("parallelOperators")]
    public int? ParallelOperators { get; set; }

    [JsonPropertyName("executionOrder")]
    public string ExecutionOrder { get; set; } = string.Empty;
}

public sealed class SqlExecutionMetrics
{
    public long RowsReturned { get; set; }
    public long? LogicalReads { get; set; }
    public long? PhysicalReads { get; set; }
    public long? ScanCount { get; set; }
    public decimal? CpuTimeMs { get; set; }
    public decimal? SqlElapsedTimeMs { get; set; }
    public long? RequestedMemoryKb { get; set; }
    public long? GrantedMemoryKb { get; set; }
    public long? UsedMemoryKb { get; set; }
    public long? TempDbPages { get; set; }
    public long? TempDbAllocatedPages { get; set; }
    public long? WorktableLogicalReads { get; set; }
    public int? DegreeOfParallelism { get; set; }
    public bool? UsedParallelPlan { get; set; }
    public int? ParallelOperators { get; set; }
}

public sealed class ExecutionPlanOperatorOccurrence
{
    [JsonPropertyName("nodeId")]
    public int? NodeId { get; set; }

    [JsonPropertyName("physicalOperator")]
    public string PhysicalOperator { get; set; } = string.Empty;

    [JsonPropertyName("logicalOperator")]
    public string? LogicalOperator { get; set; }

    [JsonPropertyName("estimatedRows")]
    public decimal? EstimatedRows { get; set; }

    [JsonPropertyName("estimatedSubtreeCost")]
    public decimal? EstimatedSubtreeCost { get; set; }
}

public sealed class ExecutionPlanArtifact
{
    [JsonPropertyName("planHash")]
    public string PlanHash { get; set; } = string.Empty;

    [JsonPropertyName("planXml")]
    public string PlanXml { get; set; } = string.Empty;

    [JsonPropertyName("estimatedCost")]
    public decimal? EstimatedCost { get; set; }

    [JsonPropertyName("estimatedRows")]
    public decimal? EstimatedRows { get; set; }

    [JsonPropertyName("estimatedSubtreeCost")]
    public decimal? EstimatedSubtreeCost { get; set; }

    [JsonPropertyName("operators")]
    public Dictionary<string, int> Operators { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("operatorOccurrences")]
    public List<ExecutionPlanOperatorOccurrence> OperatorOccurrences { get; set; } = new();
}

public sealed class ExecutionPlanComparison
{
    [JsonPropertyName("planHashesMatch")]
    public bool PlanHashesMatch { get; set; }

    [JsonPropertyName("operatorChanges")]
    public List<ExecutionPlanOperatorChange> OperatorChanges { get; set; } = new();
}

public sealed class ExecutionPlanOperatorChange
{
    [JsonPropertyName("physicalOperator")]
    public string PhysicalOperator { get; set; } = string.Empty;

    [JsonPropertyName("baselineCount")]
    public int BaselineCount { get; set; }

    [JsonPropertyName("optimizedCount")]
    public int OptimizedCount { get; set; }
}

public sealed class BenchmarkStatistics
{
    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; set; }

    [JsonPropertyName("min")]
    public decimal Min { get; set; }

    [JsonPropertyName("max")]
    public decimal Max { get; set; }

    [JsonPropertyName("average")]
    public decimal Average { get; set; }

    [JsonPropertyName("median")]
    public decimal Median { get; set; }

    [JsonPropertyName("standardDeviation")]
    public decimal StandardDeviation { get; set; }
}

public sealed class BenchmarkRunsSummary
{
    [JsonPropertyName("runs")]
    public List<BenchmarkRunRecord> Runs { get; set; } = new();

    [JsonPropertyName("statistics")]
    public BenchmarkStatistics Statistics { get; set; } = new();

    [JsonPropertyName("logicalReadsStatistics")]
    public BenchmarkStatistics LogicalReadsStatistics { get; set; } = new();

    [JsonPropertyName("physicalReadsStatistics")]
    public BenchmarkStatistics PhysicalReadsStatistics { get; set; } = new();

    [JsonPropertyName("scanCountStatistics")]
    public BenchmarkStatistics ScanCountStatistics { get; set; } = new();

    [JsonPropertyName("cpuTimeMsStatistics")]
    public BenchmarkStatistics CpuTimeMsStatistics { get; set; } = new();

    [JsonPropertyName("sqlElapsedTimeMsStatistics")]
    public BenchmarkStatistics SqlElapsedTimeMsStatistics { get; set; } = new();

    [JsonPropertyName("requestedMemoryKbStatistics")]
    public BenchmarkStatistics RequestedMemoryKbStatistics { get; set; } = new();

    [JsonPropertyName("grantedMemoryKbStatistics")]
    public BenchmarkStatistics GrantedMemoryKbStatistics { get; set; } = new();

    [JsonPropertyName("usedMemoryKbStatistics")]
    public BenchmarkStatistics UsedMemoryKbStatistics { get; set; } = new();

    [JsonPropertyName("tempDbPagesStatistics")]
    public BenchmarkStatistics TempDbPagesStatistics { get; set; } = new();

    [JsonPropertyName("tempDbAllocatedPagesStatistics")]
    public BenchmarkStatistics TempDbAllocatedPagesStatistics { get; set; } = new();

    [JsonPropertyName("worktableLogicalReadsStatistics")]
    public BenchmarkStatistics WorktableLogicalReadsStatistics { get; set; } = new();

    [JsonPropertyName("degreeOfParallelismStatistics")]
    public BenchmarkStatistics DegreeOfParallelismStatistics { get; set; } = new();

    [JsonPropertyName("parallelOperatorsStatistics")]
    public BenchmarkStatistics ParallelOperatorsStatistics { get; set; } = new();
}

public sealed class ValidationSummary
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}

public sealed class FailureSummary
{
    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class ImprovementSummary
{
    [JsonPropertyName("durationPercent")]
    public decimal? DurationPercent { get; set; }

    [JsonPropertyName("logicalReadsPercent")]
    public decimal? LogicalReadsPercent { get; set; }

    [JsonPropertyName("cpuTimePercent")]
    public decimal? CpuTimePercent { get; set; }

    [JsonPropertyName("sqlElapsedTimePercent")]
    public decimal? SqlElapsedTimePercent { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class EnvironmentSummary
{
    [JsonPropertyName("sqlServerVersion")]
    public string SqlServerVersion { get; set; } = string.Empty;

    [JsonPropertyName("sqlServerEdition")]
    public string SqlServerEdition { get; set; } = string.Empty;

    [JsonPropertyName("maxDop")]
    public int? MaxDop { get; set; }

    [JsonPropertyName("costThresholdForParallelism")]
    public int? CostThresholdForParallelism { get; set; }

    [JsonPropertyName("maxServerMemoryMb")]
    public int? MaxServerMemoryMb { get; set; }

    [JsonPropertyName("runnerVersion")]
    public string RunnerVersion { get; set; } = string.Empty;

    [JsonPropertyName("dotnetVersion")]
    public string DotnetVersion { get; set; } = string.Empty;

    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;
}

public sealed class BenchmarkResultDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 4;

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("experimentId")]
    public string ExperimentId { get; set; } = string.Empty;

    [JsonPropertyName("experimentHash")]
    public string ExperimentHash { get; set; } = string.Empty;

    [JsonPropertyName("startedUtc")]
    public DateTimeOffset StartedUtc { get; set; }

    [JsonPropertyName("completedUtc")]
    public DateTimeOffset CompletedUtc { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; set; }

    [JsonPropertyName("options")]
    public BenchmarkOptions Options { get; set; } = new();

    [JsonPropertyName("environment")]
    public EnvironmentSummary Environment { get; set; } = new();

    [JsonPropertyName("baseline")]
    public BenchmarkRunsSummary Baseline { get; set; } = new();

    [JsonPropertyName("optimized")]
    public BenchmarkRunsSummary Optimized { get; set; } = new();

    [JsonPropertyName("baselinePlan")]
    public ExecutionPlanArtifact? BaselinePlan { get; set; }

    [JsonPropertyName("optimizedPlan")]
    public ExecutionPlanArtifact? OptimizedPlan { get; set; }

    [JsonPropertyName("planComparison")]
    public ExecutionPlanComparison? PlanComparison { get; set; }

    [JsonPropertyName("validation")]
    public ValidationSummary Validation { get; set; } = new();

    [JsonPropertyName("failure")]
    public FailureSummary? Failure { get; set; }

    [JsonPropertyName("improvement")]
    public ImprovementSummary Improvement { get; set; } = new();

    [JsonPropertyName("advisorFindings")]
    public List<AdvisorFinding> AdvisorFindings { get; set; } = new();

    [JsonPropertyName("advisorRecommendations")]
    public List<AdvisorRecommendation> AdvisorRecommendations { get; set; } = new();
}

public sealed class BenchmarkHistoryRecord
{
    public string RunId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public string ExperimentHash { get; set; } = string.Empty;
    public string ExperimentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
    public string SqlServerVersion { get; set; } = string.Empty;
    public string SqlServerEdition { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public decimal? BaselineDurationMedianMs { get; set; }
    public decimal? OptimizedDurationMedianMs { get; set; }
    public decimal? BaselineLogicalReadsMedian { get; set; }
    public decimal? OptimizedLogicalReadsMedian { get; set; }
    public decimal? OptimizedCpuTimeMedianMs { get; set; }
    public decimal? OptimizedSqlElapsedTimeMedianMs { get; set; }
    public string? BaselinePlanHash { get; set; }
    public string? OptimizedPlanHash { get; set; }
}

public sealed class BenchmarkResourceRecord
{
    public string RunId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public long? RequestedMemoryKb { get; set; }
    public long? GrantedMemoryKb { get; set; }
    public long? UsedMemoryKb { get; set; }
    public long? TempDbPages { get; set; }
    public long? TempDbAllocatedPages { get; set; }
    public long? WorktableLogicalReads { get; set; }
    public int? DegreeOfParallelism { get; set; }
    public bool? UsedParallelPlan { get; set; }
    public int? ParallelOperators { get; set; }
}

public sealed class ExperimentVersionRecord
{
    public string ExperimentId { get; set; } = string.Empty;
    public string ExperimentHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public string DefinitionSnapshot { get; set; } = string.Empty;
}

public sealed class BenchmarkMetricComparison
{
    public string Metric { get; set; } = string.Empty;
    public decimal PreviousMedian { get; set; }
    public decimal CurrentMedian { get; set; }
    public decimal? ImprovementPercent { get; set; }
}

public sealed class BenchmarkRunComparison
{
    public string PreviousRunId { get; set; } = string.Empty;
    public string CurrentRunId { get; set; } = string.Empty;
    public List<BenchmarkMetricComparison> BaselineMetrics { get; set; } = new();
    public List<BenchmarkMetricComparison> OptimizedMetrics { get; set; } = new();
    public ExecutionPlanComparison? BaselinePlanComparison { get; set; }
    public ExecutionPlanComparison? OptimizedPlanComparison { get; set; }
}

public sealed class RegressionThresholds
{
    public decimal MinorPercent { get; set; } = 10m;
    public decimal ModeratePercent { get; set; } = 25m;
    public decimal SeverePercent { get; set; } = 50m;

    public void Validate()
    {
        if (MinorPercent <= 0 || ModeratePercent <= MinorPercent || SeverePercent <= ModeratePercent)
        {
            throw new ArgumentException("Regression thresholds must be positive and strictly increasing.");
        }
    }
}

public sealed class BenchmarkBaselineRecord
{
    public string ExperimentId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset EstablishedUtc { get; set; }
    public Dictionary<string, decimal?> Metrics { get; set; } = new(StringComparer.Ordinal);
}

public sealed class BenchmarkComparisonMetric
{
    public string Metric { get; set; } = string.Empty;
    public decimal? ReferenceValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? ChangePercent { get; set; }
    public string Classification { get; set; } = "Healthy";
}

public sealed class BenchmarkComparison
{
    public string ExperimentId { get; set; } = string.Empty;
    public string RunA { get; set; } = string.Empty;
    public string RunB { get; set; } = string.Empty;
    public DateTimeOffset ComparedUtc { get; set; }
    public List<BenchmarkComparisonMetric> Metrics { get; set; } = new();
    public bool RegressionDetected { get; set; }
    public string Health { get; set; } = "Healthy";
}

public sealed class BenchmarkTrend
{
    public string Metric { get; set; } = string.Empty;
    public string Direction { get; set; } = "Stable";
    public int SampleCount { get; set; }
    public decimal? FirstValue { get; set; }
    public decimal? LatestValue { get; set; }
    public decimal? ChangePercent { get; set; }
}

public sealed class BenchmarkTrendReport
{
    public string ExperimentId { get; set; } = string.Empty;
    public List<BenchmarkTrend> Trends { get; set; } = new();
}

public sealed class AdvisorFinding
{
    public string Rule { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
}

public sealed class AdvisorRecommendation
{
    public string Recommendation { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}

public sealed class BenchmarkAdvisorAnalysis
{
    public List<AdvisorFinding> Findings { get; set; } = new();
    public List<AdvisorRecommendation> Recommendations { get; set; } = new();
}
