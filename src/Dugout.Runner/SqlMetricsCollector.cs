using System.Globalization;
using System.Text.RegularExpressions;
using Dugout.Contracts;
using Microsoft.Data.SqlClient;

namespace Dugout.Runner;

public interface ISqlMetricsCollector
{
    Task<SqlExecutionMetrics> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}

public sealed class SqlMetricsCollector : ISqlMetricsCollector
{
    private static readonly Regex IoPattern = new(
        @"Table\s+'([^']+)'\.\s*Scan count\s+([\d,]+),\s*logical reads\s+([\d,]+),\s*physical reads\s+([\d,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TimePattern = new(
        @"SQL Server Execution Times:\s*CPU time\s*=\s*([\d,]+)\s*ms,\s*elapsed time\s*=\s*([\d,]+)\s*ms",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly SqlConnection _connection;

    public SqlMetricsCollector(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<SqlExecutionMetrics> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var tempDbBefore = await ReadTempDbUsageAsync(cancellationToken);
        var messages = new List<string>();
        SqlInfoMessageEventHandler handler = (_, eventArgs) =>
        {
            foreach (SqlError error in eventArgs.Errors)
            {
                messages.Add(error.Message);
            }
        };
        _connection.InfoMessage += handler;
        var completed = false;
        SqlExecutionMetrics? metricsFromPlan = null;

        try
        {
            var commandText = $"SET STATISTICS IO ON;{Environment.NewLine}SET STATISTICS TIME ON;{Environment.NewLine}SET STATISTICS XML ON;{Environment.NewLine}{sql}{Environment.NewLine}SET STATISTICS XML OFF;{Environment.NewLine}SET STATISTICS IO OFF;{Environment.NewLine}SET STATISTICS TIME OFF;";
            await using var command = new SqlCommand(commandText, _connection)
            {
                CommandTimeout = 60
            };

            long rowsReturned = 0;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    rowsReturned++;
                }

                while (await reader.NextResultAsync(cancellationToken))
                {
                    var isShowplan = reader.FieldCount == 1
                        && reader.GetName(0).Contains("Showplan", StringComparison.OrdinalIgnoreCase);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        if (isShowplan && !reader.IsDBNull(0))
                        {
                            var planXml = ReadXmlValue(reader.GetValue(0));
                            var runtimeMetrics = ExecutionPlanParser.ParseRuntimeMetrics(planXml);
                            metricsFromPlan = runtimeMetrics;
                        }
                    }
                }
            }

            completed = true;
            var metrics = Parse(messages);
            if (metricsFromPlan is not null)
            {
                metrics.RequestedMemoryKb = metricsFromPlan.RequestedMemoryKb;
                metrics.GrantedMemoryKb = metricsFromPlan.GrantedMemoryKb;
                metrics.UsedMemoryKb = metricsFromPlan.UsedMemoryKb;
                metrics.DegreeOfParallelism = metricsFromPlan.DegreeOfParallelism;
                metrics.UsedParallelPlan = metricsFromPlan.UsedParallelPlan;
                metrics.ParallelOperators = metricsFromPlan.ParallelOperators;
            }

            var tempDbAfter = await ReadTempDbUsageAsync(cancellationToken);
            if (tempDbBefore is not null && tempDbAfter is not null)
            {
                var allocatedPages = Math.Max(0, tempDbAfter.Value.AllocatedPages - tempDbBefore.Value.AllocatedPages);
                var deallocatedPages = Math.Max(0, tempDbAfter.Value.DeallocatedPages - tempDbBefore.Value.DeallocatedPages);
                metrics.TempDbAllocatedPages = allocatedPages;
                metrics.TempDbPages = Math.Max(0, allocatedPages - deallocatedPages);
            }

            metrics.RowsReturned = rowsReturned;
            return metrics;
        }
        finally
        {
            _connection.InfoMessage -= handler;
            if (!completed && _connection.State == System.Data.ConnectionState.Open)
            {
                try
                {
                    await using var resetCommand = new SqlCommand("SET STATISTICS XML OFF; SET STATISTICS IO OFF; SET STATISTICS TIME OFF;", _connection);
                    await resetCommand.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }

    public static SqlExecutionMetrics Parse(IEnumerable<string> messages)
    {
        long scanCount = 0;
        long logicalReads = 0;
        long physicalReads = 0;
        decimal cpuTimeMs = 0;
        decimal elapsedTimeMs = 0;
        long worktableLogicalReads = 0;
        var foundIo = false;
        var foundTime = false;

        foreach (var message in messages)
        {
            foreach (Match match in IoPattern.Matches(message))
            {
                scanCount += ParseInteger(match.Groups[2].Value);
                logicalReads += ParseInteger(match.Groups[3].Value);
                physicalReads += ParseInteger(match.Groups[4].Value);
                if (match.Groups[1].Value.Contains("Worktable", StringComparison.OrdinalIgnoreCase)
                    || match.Groups[1].Value.Contains("Workfile", StringComparison.OrdinalIgnoreCase))
                {
                    worktableLogicalReads += ParseInteger(match.Groups[3].Value);
                }

                foundIo = true;
            }

            foreach (Match match in TimePattern.Matches(message))
            {
                cpuTimeMs += ParseInteger(match.Groups[1].Value);
                elapsedTimeMs += ParseInteger(match.Groups[2].Value);
                foundTime = true;
            }
        }

        return new SqlExecutionMetrics
        {
            ScanCount = foundIo ? scanCount : null,
            LogicalReads = foundIo ? logicalReads : null,
            PhysicalReads = foundIo ? physicalReads : null,
            CpuTimeMs = foundTime ? cpuTimeMs : null,
            SqlElapsedTimeMs = foundTime ? elapsedTimeMs : null,
            WorktableLogicalReads = foundIo ? worktableLogicalReads : null
        };
    }

    private async Task<(long AllocatedPages, long DeallocatedPages)?> ReadTempDbUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new SqlCommand("""
                SELECT COALESCE(SUM(user_objects_alloc_page_count + internal_objects_alloc_page_count), 0),
                       COALESCE(SUM(user_objects_dealloc_page_count + internal_objects_dealloc_page_count), 0)
                FROM sys.dm_db_session_space_usage
                WHERE session_id = @@SPID;
                """, _connection) { CommandTimeout = 15 };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return (reader.GetInt64(0), reader.GetInt64(1));
        }
        catch (SqlException)
        {
            return null;
        }
    }

    private static string ReadXmlValue(object value)
    {
        return value switch
        {
            string text => text,
            System.Data.SqlTypes.SqlXml xml => xml.Value,
            System.Data.SqlTypes.SqlString text => text.Value,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static long ParseInteger(string value)
    {
        return long.Parse(value.Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
    }
}