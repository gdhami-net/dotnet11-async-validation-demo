using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SignUpDemo.Tests;

/// <summary>A fresh SQLite file per factory, a query counter, and an optional gate.</summary>
public sealed class SignUpFactory : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"signup-{Guid.NewGuid():N}.db");

    public QueryCounter Queries { get; } = new();
    public ISignUpGate Gate { get; init; } = new NoGate();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_path}");
        builder.ConfigureServices(services =>
        {
            services.ConfigureDbContext<AppDb>(o => o.AddInterceptors(Queries));
            services.RemoveAll<ISignUpGate>();
            services.AddSingleton(Gate);
        });
    }

    public AppDb OpenDb() =>
        new(new DbContextOptionsBuilder<AppDb>().UseSqlite($"Data Source={_path}").Options);

    /// <summary>Drops the unique index, to see what the check alone does.</summary>
    public async Task DropUniqueIndexAsync()
    {
        _ = Server; // makes sure the app ran EnsureCreated
        await using var db = OpenDb();
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Users_Email");
    }

    public async Task<int> CountUsersAsync(string email)
    {
        await using var db = OpenDb();
        return await db.Users.CountAsync(u => u.Email == email);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            try { File.Delete(f); } catch (IOException) { }
    }
}

/// <summary>Holds every request until <paramref name="count"/> of them have passed validation.</summary>
public sealed class BarrierGate(int count) : ISignUpGate
{
    private int _arrived;
    private readonly TaskCompletionSource _allIn = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task WaitAsync(CancellationToken ct)
    {
        if (Interlocked.Increment(ref _arrived) == count) _allIn.SetResult();
        await _allIn.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }
}
