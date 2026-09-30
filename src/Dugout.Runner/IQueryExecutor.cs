using Dugout.Contracts;

namespace Dugout.Runner;

public interface IQueryExecutor : IDisposable
{
    Task OpenAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task<(bool IsValid, string? Message)> ValidateDatasetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);
    Task<long> ExecuteQueryAndCountAsync(string sql, CancellationToken cancellationToken = default);
    Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default);
    Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default);
    Task<EnvironmentSummary> GetEnvironmentAsync(string runnerVersion, CancellationToken cancellationToken = default);
}
