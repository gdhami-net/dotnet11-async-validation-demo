using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SignUpDemo.Tests;

/// <summary>
/// Counts the lookups on the Users table (the uniqueness checks), and can make each
/// query wait before it runs. SQLite answers async calls synchronously; the wait makes
/// a query take time the way a database server on the network does.
/// </summary>
public sealed class QueryCounter : DbCommandInterceptor
{
    private int _lookups;

    public int Lookups => Volatile.Read(ref _lookups);
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("FROM \"Users\""))
        {
            Interlocked.Increment(ref _lookups);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        }
        return result;
    }
}
