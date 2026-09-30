using System.Data;
using System.Reflection;
using Dugout.Contracts;
using Microsoft.Data.SqlClient;

namespace Dugout.Runner;

public sealed class SqlQueryExecutor : IQueryExecutor
{
    private readonly string _connectionString;
    private readonly SqlConnection _connection;

    public SqlQueryExecutor(string connectionString)
    {
        _connectionString = connectionString;
        _connection = new SqlConnection(connectionString);
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State == ConnectionState.Open)
        {
            return;
        }

        await _connection.OpenAsync(cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync();
        }
    }

    public async Task<(bool IsValid, string? Message)> ValidateDatasetAsync(CancellationToken cancellationToken = default)
    {
        var databaseId = await ExecuteScalarAsync("SELECT DB_ID('SalesLab');", cancellationToken);
        if (databaseId is null || databaseId is DBNull || Convert.ToInt32(databaseId) == 0)
        {
            return (false, "The SalesLab database does not exist.");
        }

        var tableCount = Convert.ToInt32(await ExecuteScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name = 'Orders';", cancellationToken));
        if (tableCount == 0)
        {
            return (false, "The Orders table was not found in SalesLab.");
        }

        var rowCount = Convert.ToInt64(await ExecuteScalarAsync("SELECT COUNT(*) FROM dbo.Orders;", cancellationToken));
        if (rowCount != 1_000_000)
        {
            return (false, $"The Orders table contains {rowCount} rows; the expected count is 1,000,000.");
        }

        return (true, "Database validated.");
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var command = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 60
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<string>();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < columns.Count; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columns[i]] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    public async Task<long> ExecuteQueryAndCountAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var command = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 60
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        long rowCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rowCount++;
        }

        return rowCount;
    }

    public async Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var command = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 60
        };

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DBNull ? null : value;
    }

    public async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var command = new SqlCommand(sql, _connection)
        {
            CommandTimeout = 60
        };

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EnvironmentSummary> GetEnvironmentAsync(string runnerVersion, CancellationToken cancellationToken = default)
    {
        var sqlServerVersion = await ExecuteScalarAsync("SELECT @@VERSION;", cancellationToken) as string ?? string.Empty;
        var edition = await ExecuteScalarAsync("SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(255));", cancellationToken) as string ?? string.Empty;
        var maxDop = await ExecuteScalarAsync("SELECT @@MAXDOP;", cancellationToken);
        var costThreshold = await ExecuteScalarAsync("SELECT value_in_use FROM sys.configurations WHERE name = 'cost threshold for parallelism';", cancellationToken);
        var maxServerMemory = await ExecuteScalarAsync("SELECT value_in_use FROM sys.configurations WHERE name = 'max server memory (MB)';", cancellationToken);

        return new EnvironmentSummary
        {
            SqlServerVersion = sqlServerVersion,
            SqlServerEdition = edition,
            MaxDop = maxDop is null ? null : Convert.ToInt32(maxDop),
            CostThresholdForParallelism = costThreshold is null ? null : Convert.ToInt32(costThreshold),
            MaxServerMemoryMb = maxServerMemory is null ? null : Convert.ToInt32(maxServerMemory),
            RunnerVersion = runnerVersion,
            DotnetVersion = Environment.Version.ToString(),
            MachineName = Environment.MachineName
        };
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
