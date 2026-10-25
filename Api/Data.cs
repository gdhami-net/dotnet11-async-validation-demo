using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SignUpDemo;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? UserName { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<User>();

        // NOCASE makes both the index and the AnyAsync check ignore case on SQLite.
        user.Property(u => u.Email).UseCollation("NOCASE");

        // The guarantee. Without it, two sign-ups at the same moment both get in.
        user.HasIndex(u => u.Email).IsUnique();
    }
}

public static class DbUpdateExceptionExtensions
{
    // SQLITE_CONSTRAINT_UNIQUE. On SQL Server, check for error numbers 2601 and 2627 instead.
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };
}
