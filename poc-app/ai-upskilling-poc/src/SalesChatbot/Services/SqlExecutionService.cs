using System.Data;
using Microsoft.EntityFrameworkCore;
using SalesChatbot.Data;
using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;
using SalesChatbot.Services.Validation;

namespace SalesChatbot.Services;

public sealed class SqlExecutionService(SalesDbContext dbContext) : ISqlExecutionService
{
    public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (!SqlSafetyValidator.IsValidSelect(sql, out var reason))
        {
            throw new InvalidOperationException(reason ?? "Invalid SQL.");
        }

        var limitedSql = SqlSafetyValidator.EnforceRowLimit(sql);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = limitedSql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return new QueryResult
        {
            ColumnNames = columnNames,
            Rows = rows
        };
    }
}
