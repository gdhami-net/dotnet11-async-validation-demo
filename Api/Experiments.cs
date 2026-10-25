using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SignUpDemo;

// Endpoints and models that exist only to answer questions about how validation runs.
// The sign-up itself is in Program.cs.

/// <summary>Counts how many async checks are running at the same moment.</summary>
public sealed class OverlapProbe
{
    private int _inFlight;
    private int _max;

    public int Max => Volatile.Read(ref _max);

    public void Enter()
    {
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while (now > (seen = Volatile.Read(ref _max)))
            Interlocked.CompareExchange(ref _max, now, seen);
    }

    public void Exit() => Interlocked.Decrement(ref _inFlight);
}

/// <summary>Waits 100 ms, like a slow lookup, and always passes.</summary>
public sealed class SlowCheckAttribute : AsyncValidationAttribute
{
    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext context, CancellationToken cancellationToken)
    {
        var probe = context.GetRequiredService<OverlapProbe>();
        probe.Enter();
        try { await Task.Delay(100, cancellationToken); }
        finally { probe.Exit(); }
        return ValidationResult.Success;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        throw new InvalidOperationException("Async only.");
}

public class TwoSlowChecks
{
    [SlowCheck] public string A { get; set; } = "";
    [SlowCheck] public string B { get; set; } = "";
}

// Same rules as SignUp, with the database check written first.
public class SignUpUniqueFirst
{
    [UniqueEmail, Required, EmailAddress]
    public string Email { get; set; } = "";
}

// The same check written as IAsyncValidatableObject instead of an attribute.
public class SignUpAsObject : IAsyncValidatableObject
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = context.GetRequiredService<AppDb>();
        if (await db.Users.AnyAsync(u => u.Email == Email, cancellationToken))
            yield return new ValidationResult("This email is already registered.", [nameof(Email)]);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        throw new InvalidOperationException("Async only.");
}

/// <summary>
/// The first version of the check: it borrows the request's scoped AppDb.
/// Kept to show what happens when two of these run on one model.
/// </summary>
public sealed class UniqueInScopedDbAttribute(string column) : AsyncValidationAttribute
{
    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext context, CancellationToken cancellationToken)
    {
        if (value is not string text || text.Length == 0)
            return ValidationResult.Success;

        var db = context.GetRequiredService<AppDb>();
        var taken = await db.Users.AnyAsync(u => EF.Property<string>(u, column) == text, cancellationToken);

        return taken
            ? new ValidationResult($"This {column} is taken.", [context.MemberName!])
            : ValidationResult.Success;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        throw new InvalidOperationException("Async only.");
}

public class SignUpScopedDb
{
    [Required, EmailAddress, UniqueInScopedDb(nameof(User.Email))]
    public string Email { get; set; } = "";

    [Required, UniqueInScopedDb(nameof(User.UserName))]
    public string UserName { get; set; } = "";
}

[ApiController]
[Route("mvc")]
public class MvcSignUpController(AppDb db) : ControllerBase
{
    [HttpPost("signup")]
    public async Task<IActionResult> SignUp(SignUp request)
    {
        db.Users.Add(new User { Email = request.Email });
        await db.SaveChangesAsync();
        return StatusCode(201);
    }
}

public static class ExperimentEndpoints
{
    public static void MapExperiments(this WebApplication app)
    {
        app.MapPost("/experiments/two-slow-checks", (TwoSlowChecks _) => Results.Ok());
        app.MapPost("/experiments/unique-first", (SignUpUniqueFirst _) => Results.Ok());
        app.MapPost("/experiments/as-object", (SignUpAsObject _) => Results.Ok());
        app.MapPost("/experiments/scoped-db", (SignUpScopedDb _) => Results.Ok());
    }
}
