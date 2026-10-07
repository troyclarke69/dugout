using Dugout.Api.Contracts;

namespace Dugout.Api.Services;

public interface IDugoutApiService
{
    Task<IReadOnlyList<ExperimentResponse>> ListExperimentsAsync(CancellationToken cancellationToken);
    Task<ExperimentResponse?> GetExperimentAsync(string experimentId, CancellationToken cancellationToken);
    Task<RunResponse?> RunExperimentAsync(string experimentId, RunExperimentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RunSummaryResponse>> ListRunsAsync(int latest, CancellationToken cancellationToken);
    Task<RunResponse?> GetRunAsync(string runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RunSummaryResponse>> GetHistoryAsync(string experimentId, int latest, CancellationToken cancellationToken);
    Task<BaselineResponse?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken);
    Task<RunComparisonResponse?> CompareRunsAsync(string runA, string runB, CancellationToken cancellationToken);
    Task<AdvisorAnalysisResponse?> GetAnalysisAsync(string runId, CancellationToken cancellationToken);
    Task<ExecutionPlansResponse?> GetPlansAsync(string runId, CancellationToken cancellationToken);
}
