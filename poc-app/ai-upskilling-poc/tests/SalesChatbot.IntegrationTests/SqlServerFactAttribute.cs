using Microsoft.Data.SqlClient;

namespace SalesChatbot.IntegrationTests;

/// <summary>
/// xUnit fact attribute that skips the test when SQL Server / LocalDB is unavailable.
/// This ensures CI passes on machines without a local SQL Server instance.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> IsAvailable = new(CheckAvailability);

    public SqlServerFactAttribute()
    {
        if (!IsAvailable.Value)
        {
            Skip = "SQL Server is not available on this machine.";
        }
    }

    private static bool CheckAvailability()
    {
        const string connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;Connect Timeout=3;";

        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
