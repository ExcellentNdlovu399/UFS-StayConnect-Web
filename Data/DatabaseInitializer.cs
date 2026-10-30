using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace UFSStayConnect.Data
{
    public static class DatabaseInitializer
    {
        private const string DatabaseName = "UFSSTAYCONNECTDB";

        public static async Task InitializeAsync(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            var connectionString =
                configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The DefaultConnection connection string is not configured.");
            }

            var connectionBuilder =
                new SqlConnectionStringBuilder(connectionString);

            // Connect to master to check/create the database.
            connectionBuilder.InitialCatalog = "master";

            await using var masterConnection =
                new SqlConnection(connectionBuilder.ConnectionString);

            await masterConnection.OpenAsync();

            const string databaseExistsSql = """
                SELECT COUNT(*)
                FROM sys.databases
                WHERE name = @DatabaseName;
                """;

            await using var databaseExistsCommand =
                new SqlCommand(
                    databaseExistsSql,
                    masterConnection);

            databaseExistsCommand.Parameters.AddWithValue(
                "@DatabaseName",
                DatabaseName);

            bool databaseExists =
                Convert.ToInt32(
                    await databaseExistsCommand.ExecuteScalarAsync()) > 0;

            // Create the database if it does not exist.
            if (!databaseExists)
            {
                const string createDatabaseSql = """
                    CREATE DATABASE [UFSSTAYCONNECTDB];
                    """;

                await using var createDatabaseCommand =
                    new SqlCommand(
                        createDatabaseSql,
                        masterConnection);

                await createDatabaseCommand.ExecuteNonQueryAsync();
            }

            // Connect to UFSSTAYCONNECTDB.
            var databaseConnectionBuilder =
                new SqlConnectionStringBuilder(connectionString);

            databaseConnectionBuilder.InitialCatalog =
                DatabaseName;

            await using var databaseConnection =
                new SqlConnection(
                    databaseConnectionBuilder.ConnectionString);

            await databaseConnection.OpenAsync();

            // Check whether the database already contains tables.
            const string tableCountSql = """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE';
                """;

            await using var tableCountCommand =
                new SqlCommand(
                    tableCountSql,
                    databaseConnection);

            int tableCount =
                Convert.ToInt32(
                    await tableCountCommand.ExecuteScalarAsync());

            // Database is already initialized.
            if (tableCount > 0)
            {
                return;
            }

            // Locate the deployment script.
            var scriptPath = Path.Combine(
                environment.ContentRootPath,
                "Database",
                "UFSSTAYCONNECTDB.sql");

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException(
                    "The UFSSTAYCONNECT database script could not be found.",
                    scriptPath);
            }

            // Read the deployment script.
            var script =
                await File.ReadAllTextAsync(scriptPath);

            // Split SSMS batches.
            var batches = Regex.Split(
                script,
                @"^\s*GO\s*(?:--.*)?$",
                RegexOptions.Multiline |
                RegexOptions.IgnoreCase);

            foreach (var batch in batches)
            {
                var sql = batch.Trim();

                if (string.IsNullOrWhiteSpace(sql))
                {
                    continue;
                }

                // USE statements are not required because the
                // connection is already connected to UFSSTAYCONNECTDB.
                if (Regex.IsMatch(
                    sql,
                    @"^\s*USE\s+\[?UFSSTAYCONNECTDB\]?\s*;?",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                await using var command =
                    new SqlCommand(
                        sql,
                        databaseConnection);

                command.CommandTimeout = 120;

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}