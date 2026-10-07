using Dugout.Contracts;
using Microsoft.Data.SqlClient;
using System.Data.SqlTypes;
using System.Globalization;

namespace Dugout.Runner;

public interface IExecutionPlanCollector
{
    Task<ExecutionPlanArtifact> CaptureAsync(string sql, CancellationToken cancellationToken = default);
}

public sealed class SqlExecutionPlanCollector : IExecutionPlanCollector
{
    private readonly SqlConnection _connection;

    public SqlExecutionPlanCollector(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<ExecutionPlanArtifact> CaptureAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using (var enableCommand = new SqlCommand("SET SHOWPLAN_XML ON;", _connection))
        {
            await enableCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await using var planCommand = new SqlCommand(sql, _connection)
            {
                CommandTimeout = 60
            };
            await using var reader = await planCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.FieldCount == 0 || reader.IsDBNull(0))
            {
                throw new InvalidOperationException("SQL Server did not return an execution plan.");
            }

            var planValue = reader.GetValue(0);
            var planXml = planValue switch
            {
                string value => value,
                SqlXml value => value.Value,
                SqlString value => value.Value,
                _ => Convert.ToString(planValue, CultureInfo.InvariantCulture)
            };
            if (string.IsNullOrWhiteSpace(planXml))
            {
                throw new InvalidOperationException("SQL Server returned an empty execution plan.");
            }

            return ExecutionPlanParser.Parse(planXml);
        }
        finally
        {
            if (_connection.State == System.Data.ConnectionState.Open)
            {
                await using var disableCommand = new SqlCommand("SET SHOWPLAN_XML OFF;", _connection);
                await disableCommand.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
    }
}