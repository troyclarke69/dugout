using Microsoft.Data.SqlClient;

namespace Dugout.Api.Services;

internal static class ApiConnectionStrings
{
    public static string GetBenchmarkConnectionString(IConfiguration configuration)
    {
        return configuration["DUGOUT_CONNECTION"]
            ?? configuration.GetConnectionString("Dugout")
            ?? string.Empty;
    }

    public static string GetResultsConnectionString(IConfiguration configuration)
    {
        var resultsConnectionString = configuration["DUGOUT_RESULTS_CONNECTION"]
            ?? configuration.GetConnectionString("DugoutResults");
        if (!string.IsNullOrWhiteSpace(resultsConnectionString))
        {
            return resultsConnectionString;
        }

        var benchmarkConnectionString = GetBenchmarkConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(benchmarkConnectionString))
        {
            return string.Empty;
        }

        var builder = new SqlConnectionStringBuilder(benchmarkConnectionString)
        {
            InitialCatalog = "DugoutResults"
        };
        return builder.ConnectionString;
    }
}