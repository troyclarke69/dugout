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

    [JsonPropertyName("executionOrder")]
    public string ExecutionOrder { get; set; } = string.Empty;
}

public sealed class BenchmarkStatistics
{
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
    public int SchemaVersion { get; set; } = 1;

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

    [JsonPropertyName("validation")]
    public ValidationSummary Validation { get; set; } = new();

    [JsonPropertyName("failure")]
    public FailureSummary? Failure { get; set; }

    [JsonPropertyName("improvement")]
    public ImprovementSummary Improvement { get; set; } = new();
}
