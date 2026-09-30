using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dugout.Contracts;

namespace Dugout.Storage;

public static class ExperimentRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static IReadOnlyList<ExperimentDefinition> List(string experimentsRoot, Action<string>? warning = null)
    {
        if (!Directory.Exists(experimentsRoot))
        {
            return Array.Empty<ExperimentDefinition>();
        }

        var reportWarning = warning ?? Console.Error.WriteLine;
        var experiments = new List<ExperimentDefinition>();
        foreach (var directory in Directory.EnumerateDirectories(experimentsRoot, "EXP*", SearchOption.TopDirectoryOnly))
        {
            var experimentPath = Path.Combine(directory, "experiment.json");
            if (!File.Exists(experimentPath))
            {
                continue;
            }

            try
            {
                var experiment = Load(experimentPath);
                if (!string.IsNullOrWhiteSpace(experiment.Id))
                {
                    experiments.Add(experiment);
                }
            }
            catch (Exception exception)
            {
                reportWarning($"Warning: Skipping unreadable experiment '{experimentPath}': {exception.Message}");
            }
        }

        return experiments;
    }

    public static ExperimentDefinition Load(string experimentPath)
    {
        var experimentDirectory = Path.GetDirectoryName(experimentPath) ?? throw new InvalidOperationException("Experiment directory was not found.");
        var json = NormalizeLineEndings(File.ReadAllText(experimentPath));
        var experiment = JsonSerializer.Deserialize<ExperimentDefinition>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"Experiment file '{experimentPath}' is invalid.");

        experiment.ExperimentHash = ComputeExperimentHash(experimentPath, experimentDirectory, experiment);
        return experiment;
    }

    public static string Hash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(NormalizeLineEndings(content));
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    public static string ComputeExperimentHash(string experimentPath, string experimentDirectory, ExperimentDefinition experiment)
    {
        var builder = new StringBuilder();
        builder.AppendLine(NormalizeLineEndings(File.ReadAllText(experimentPath)));

        var scriptPaths = new[]
        {
            experiment.SetupScript,
            experiment.ValidationScript,
            experiment.BaselineScript,
            experiment.OptimizedScript,
            experiment.CleanupScript
        };

        foreach (var script in scriptPaths)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            var scriptPath = Path.Combine(experimentDirectory, script.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(scriptPath))
            {
                builder.AppendLine(NormalizeLineEndings(File.ReadAllText(scriptPath)));
            }
        }

        return Hash(builder.ToString());
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}

public static class ResultStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task SaveAsync(string resultsRoot, BenchmarkResultDocument result)
    {
        var directory = Path.Combine(resultsRoot, result.ExperimentId);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{result.RunId}.json");
        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
}
