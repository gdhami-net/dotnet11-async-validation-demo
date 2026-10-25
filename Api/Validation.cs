using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SignUpDemo;

public sealed class UniqueEmailAttribute : AsyncValidationAttribute
{
    public UniqueEmailAttribute() => ErrorMessage = "This email is already registered.";

    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext context, CancellationToken cancellationToken)
    {
        if (value is not string email || email.Length == 0)
            return ValidationResult.Success; // [Required] reports this one

        // Its own short-lived context: other async checks on the model run at the same time.
        var dbFactory = context.GetRequiredService<IDbContextFactory<AppDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taken = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);

        return taken
            ? new ValidationResult(FormatErrorMessage(context.DisplayName), [context.MemberName!])
            : ValidationResult.Success;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        throw new InvalidOperationException("UniqueEmail only runs on the async path.");
}

public sealed class UniqueUserNameAttribute : AsyncValidationAttribute
{
    public UniqueUserNameAttribute() => ErrorMessage = "This user name is taken.";

    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext context, CancellationToken cancellationToken)
    {
        if (value is not string name || name.Length == 0)
            return ValidationResult.Success;

        var dbFactory = context.GetRequiredService<IDbContextFactory<AppDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taken = await db.Users.AnyAsync(u => u.UserName == name, cancellationToken);

        return taken
            ? new ValidationResult(FormatErrorMessage(context.DisplayName), [context.MemberName!])
            : ValidationResult.Success;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        throw new InvalidOperationException("UniqueUserName only runs on the async path.");
}
