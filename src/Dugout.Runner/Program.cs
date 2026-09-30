using Dugout.Contracts;
using Dugout.Storage;

namespace Dugout.Runner;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            return args[0] switch
            {
                "list" => await ListExperimentsAsync(),
                "run" => await RunExperimentAsync(args, cancellationSource.Token),
                _ => ShowUsageAndReturnFailure()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 3;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static int ShowUsageAndReturnFailure()
    {
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dugout list");
        Console.WriteLine("  dugout run EXP001");
        Console.WriteLine("  dugout run EXP001 --warmups 5 --iterations 25");
    }

    private static async Task<int> ListExperimentsAsync()
    {
        var experimentsRoot = GetExperimentsRoot();
        var experiments = ExperimentRepository.List(experimentsRoot, Console.Error.WriteLine);

        if (experiments.Count == 0)
        {
            Console.WriteLine("No experiments found.");
            return 0;
        }

        foreach (var experiment in experiments)
        {
            Console.WriteLine($"{experiment.Id} - {experiment.Name}");
        }

        return 0;
    }

    private static async Task<int> RunExperimentAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("An experiment ID is required. Example: dugout run EXP001");
            return 1;
        }

        var experimentId = args[1];
        var warmupCount = GetPositiveIntOption(args, "--warmups", 3, "warmups", out var warmupError);
        if (warmupError is not null)
        {
            Console.Error.WriteLine(warmupError);
            return 1;
        }

        var iterationCount = GetPositiveIntOption(args, "--iterations", 10, "iterations", out var iterationError);
        if (iterationError is not null)
        {
            Console.Error.WriteLine(iterationError);
            return 1;
        }

        var experimentsRoot = GetExperimentsRoot();
        var resultsRoot = GetResultsRoot();
        var experimentDirectory = Path.Combine(experimentsRoot, experimentId);
        var experimentFilePath = Path.Combine(experimentDirectory, "experiment.json");

        if (!File.Exists(experimentFilePath))
        {
            Console.Error.WriteLine($"Experiment '{experimentId}' was not found at '{experimentFilePath}'.");
            return 1;
        }

        var connectionString = Environment.GetEnvironmentVariable("DUGOUT_CONNECTION");
        Console.WriteLine("Loading Experiment");
        ExperimentDefinition experiment;
        try
        {
            experiment = ExperimentRepository.Load(experimentFilePath);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unable to load experiment '{experimentId}': {exception.Message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("The DUGOUT_CONNECTION environment variable is required. Set it to a SQL Server connection string.");
            return 1;
        }

        using var executor = new SqlQueryExecutor(connectionString);

        try
        {
            var pipeline = new BenchmarkPipeline(executor);
            var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, warmupCount, iterationCount, cancellationToken, Console.WriteLine);
            var resultFilePath = Path.Combine(resultsRoot, experimentId, $"{result.RunId}.json");
            PrintSummary(result, resultFilePath);
            Console.WriteLine("Benchmark Complete");
            return result.ExitCode;
        }
        finally
        {
            await executor.CloseAsync(CancellationToken.None);
        }
    }

    private static void PrintSummary(BenchmarkResultDocument result, string resultFilePath)
    {
        Console.WriteLine();
        Console.WriteLine("Benchmark Summary");
        Console.WriteLine("Variant     Min        Avg        Median     Max        Stdev");
        Console.WriteLine("---------- --------- --------- --------- --------- ---------");
        Console.WriteLine($"Baseline    {result.Baseline.Statistics.Min:F3}    {result.Baseline.Statistics.Average:F3}    {result.Baseline.Statistics.Median:F3}    {result.Baseline.Statistics.Max:F3}    {result.Baseline.Statistics.StandardDeviation:F3}");
        Console.WriteLine($"Optimized   {result.Optimized.Statistics.Min:F3}    {result.Optimized.Statistics.Average:F3}    {result.Optimized.Statistics.Median:F3}    {result.Optimized.Statistics.Max:F3}    {result.Optimized.Statistics.StandardDeviation:F3}");
        Console.WriteLine($"Improvement: {(result.Improvement.DurationPercent is null ? "n/a" : result.Improvement.DurationPercent.Value.ToString("F3") + "%")}");
        Console.WriteLine($"Validation: {(result.Validation.Passed ? "Passed" : "Failed")}");
        if (result.Status == "Failed" && result.Failure is not null)
        {
            Console.WriteLine($"Failure stage: {result.Failure.Stage}");
            Console.WriteLine($"Failure reason: {result.Failure.Reason}");
        }

        Console.WriteLine($"Result file: {resultFilePath}");
    }

    private static int GetPositiveIntOption(string[] args, string optionName, int defaultValue, string optionLabel, out string? error)
    {
        error = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(args[i + 1], out var value) || value <= 0)
            {
                error = $"The --{optionLabel} value must be a positive integer.";
                return defaultValue;
            }

            return value;
        }

        return defaultValue;
    }

    private static string GetExperimentsRoot()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "experiments");
    }

    private static string GetResultsRoot()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "results");
    }
}

