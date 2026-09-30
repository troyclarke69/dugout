using System.Text.RegularExpressions;
using Dugout.Contracts;

namespace Dugout.Runner;

public static partial class ExperimentValidator
{
    public static bool IsValidExperimentId(string experimentId)
    {
        return ExperimentIdRegex().IsMatch(experimentId);
    }

    public static ValidationResult ValidateDefinition(ExperimentDefinition experiment, string experimentDirectory)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(experiment.Id) || !IsValidExperimentId(experiment.Id))
        {
            errors.Add("Experiment ID must match the EXP### format.");
        }

        if (string.IsNullOrWhiteSpace(experiment.Name))
        {
            errors.Add("Experiment name is required.");
        }

        if (experiment.SchemaVersion != 1)
        {
            errors.Add("Schema version must be 1.");
        }

        if (string.IsNullOrWhiteSpace(experiment.BaselineScript))
        {
            errors.Add("Baseline script is required.");
        }

        if (string.IsNullOrWhiteSpace(experiment.OptimizedScript))
        {
            errors.Add("Optimized script is required.");
        }

        foreach (var relativeScript in new[] { experiment.SetupScript, experiment.ValidationScript, experiment.BaselineScript, experiment.OptimizedScript, experiment.CleanupScript })
        {
            if (string.IsNullOrWhiteSpace(relativeScript))
            {
                continue;
            }

            var scriptPath = Path.Combine(experimentDirectory, relativeScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptPath))
            {
                errors.Add($"Script not found: {relativeScript}");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    [GeneratedRegex("^EXP\\d{3}$", RegexOptions.Compiled)]
    private static partial Regex ExperimentIdRegex();
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);
