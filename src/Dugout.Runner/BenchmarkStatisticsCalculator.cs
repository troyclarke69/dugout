namespace Dugout.Runner;

public static class BenchmarkStatisticsCalculator
{
    public static decimal CalculateMedian(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return 0m;
        }

        if (ordered.Length % 2 == 1)
        {
            return ordered[ordered.Length / 2];
        }

        var left = ordered[(ordered.Length / 2) - 1];
        var right = ordered[ordered.Length / 2];
        return (left + right) / 2m;
    }

    public static decimal CalculateImprovement(decimal baselineMedian, decimal optimizedMedian)
    {
        if (baselineMedian == 0m)
        {
            return 0m;
        }

        return ((baselineMedian - optimizedMedian) / baselineMedian) * 100m;
    }

    public static decimal CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count < 2)
        {
            return 0m;
        }

        var average = list.Average();
        var variance = list.Sum(value => (value - average) * (value - average)) / (list.Count - 1);
        return Convert.ToDecimal(Math.Sqrt((double)variance));
    }

    public static (decimal Min, decimal Max, decimal Average, decimal Median, decimal StandardDeviation) CalculateStatistics(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return (0m, 0m, 0m, 0m, 0m);
        }

        var min = list.Min();
        var max = list.Max();
        var average = list.Average();
        var median = CalculateMedian(list);
        var stdDev = CalculateStandardDeviation(list);

        return (min, max, average, median, stdDev);
    }
}
