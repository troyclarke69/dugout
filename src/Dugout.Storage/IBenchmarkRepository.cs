using Dugout.Contracts;

namespace Dugout.Storage;

public interface IBenchmarkRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task StoreBenchmarkRunAsync(
        ExperimentDefinition experiment,
        BenchmarkResultDocument result,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BenchmarkHistoryRecord>> GetHistoryAsync(
        int latest = 20,
        string? experimentId = null,
        string? sqlServerVersion = null,
        string? machineName = null,
        CancellationToken cancellationToken = default);

    Task<BenchmarkResultDocument?> GetRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<BenchmarkResultDocument?> GetLatestSuccessfulRunAsync(string experimentId, CancellationToken cancellationToken = default);

    Task SetBaselineAsync(BenchmarkBaselineRecord baseline, CancellationToken cancellationToken = default);

    Task<BenchmarkBaselineRecord?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken = default);

    Task<BenchmarkBaselineRecord?> GetLatestBaselineAsync(CancellationToken cancellationToken = default);

    Task<Guid> StoreComparisonAsync(BenchmarkComparison comparison, CancellationToken cancellationToken = default);

    Task<BenchmarkComparison?> GetComparisonAsync(Guid comparisonId, CancellationToken cancellationToken = default);

    Task<ExecutionPlanArtifact?> GetExecutionPlanAsync(
        string runId,
        string variant,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BenchmarkResourceRecord>> GetResourceTrendsAsync(
        string experimentId,
        int latest = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExperimentVersionRecord>> GetExperimentVersionsAsync(
        string experimentId,
        CancellationToken cancellationToken = default);
}