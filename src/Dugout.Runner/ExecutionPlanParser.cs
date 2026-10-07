using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Dugout.Contracts;

namespace Dugout.Runner;

public static class ExecutionPlanParser
{
    public static SqlExecutionMetrics ParseRuntimeMetrics(string planXml)
    {
        var document = XDocument.Parse(planXml);
        var queryPlan = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "QueryPlan");
        var grant = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "MemoryGrantInfo");
        var parallelOperators = document.Descendants()
            .Count(element => element.Name.LocalName == "RelOp" && (string?)element.Attribute("PhysicalOp") == "Parallelism");
        var degreeOfParallelism = ParseInt((string?)queryPlan?.Attribute("DegreeOfParallelism"));

        return new SqlExecutionMetrics
        {
            RequestedMemoryKb = ParseLong((string?)grant?.Attribute("RequestedMemory")),
            GrantedMemoryKb = ParseLong((string?)grant?.Attribute("GrantedMemory")),
            UsedMemoryKb = ParseLong((string?)grant?.Attribute("MaxUsedMemory")),
            DegreeOfParallelism = degreeOfParallelism,
            ParallelOperators = parallelOperators,
            UsedParallelPlan = degreeOfParallelism is null && parallelOperators == 0
                ? null
                : degreeOfParallelism > 1 || parallelOperators > 0
        };
    }

    public static ExecutionPlanArtifact Parse(string planXml)
    {
        var document = XDocument.Parse(planXml);
        var statement = document.Descendants().FirstOrDefault(element =>
            element.Name.LocalName is "StmtSimple" or "StmtCond");
        var operators = new Dictionary<string, int>(StringComparer.Ordinal);
        var occurrences = new List<ExecutionPlanOperatorOccurrence>();

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "RelOp"))
        {
            var physicalOperator = (string?)element.Attribute("PhysicalOp");
            if (string.IsNullOrWhiteSpace(physicalOperator))
            {
                continue;
            }

            operators.TryGetValue(physicalOperator, out var count);
            operators[physicalOperator] = count + 1;
            occurrences.Add(new ExecutionPlanOperatorOccurrence
            {
                NodeId = ParseInt((string?)element.Attribute("NodeId")),
                PhysicalOperator = physicalOperator,
                LogicalOperator = (string?)element.Attribute("LogicalOp"),
                EstimatedRows = ParseDecimal((string?)element.Attribute("EstimateRows")),
                EstimatedSubtreeCost = ParseDecimal((string?)element.Attribute("EstimatedTotalSubtreeCost"))
            });
        }

        var rootOperator = occurrences.FirstOrDefault();
        var normalizedXml = document.ToString(SaveOptions.DisableFormatting);
        var sqlServerPlanHash = (string?)statement?.Attribute("QueryPlanHash");
        var hashInput = string.IsNullOrWhiteSpace(sqlServerPlanHash)
            ? normalizedXml
            : $"SQL Server QueryPlanHash:{sqlServerPlanHash.Trim().ToUpperInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));

        return new ExecutionPlanArtifact
        {
            PlanHash = hash,
            PlanXml = planXml,
            EstimatedCost = ParseDecimal((string?)statement?.Attribute("StatementSubTreeCost")),
            EstimatedRows = rootOperator?.EstimatedRows,
            EstimatedSubtreeCost = rootOperator?.EstimatedSubtreeCost,
            Operators = operators,
            OperatorOccurrences = occurrences
        };
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

public static class ExecutionPlanComparer
{
    public static ExecutionPlanComparison Compare(ExecutionPlanArtifact baseline, ExecutionPlanArtifact optimized)
    {
        var operatorNames = baseline.Operators.Keys
            .Union(optimized.Operators.Keys, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        var comparison = new ExecutionPlanComparison
        {
            PlanHashesMatch = string.Equals(baseline.PlanHash, optimized.PlanHash, StringComparison.Ordinal)
        };

        foreach (var operatorName in operatorNames)
        {
            baseline.Operators.TryGetValue(operatorName, out var baselineCount);
            optimized.Operators.TryGetValue(operatorName, out var optimizedCount);
            if (baselineCount != optimizedCount)
            {
                comparison.OperatorChanges.Add(new ExecutionPlanOperatorChange
                {
                    PhysicalOperator = operatorName,
                    BaselineCount = baselineCount,
                    OptimizedCount = optimizedCount
                });
            }
        }

        return comparison;
    }
}