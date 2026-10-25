using Microsoft.EntityFrameworkCore;
using SignUpDemo;

var builder = WebApplication.CreateBuilder(args);

// The factory also registers AppDb itself as a scoped service for the endpoints.
builder.Services.AddDbContextFactory<AppDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=signup.db"));
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<ISignUpGate, NoGate>();
builder.Services.AddSingleton<OverlapProbe>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();

app.MapPost("/signup", async (SignUp request, AppDb db, ISignUpGate gate, CancellationToken ct) =>
{
    await gate.WaitAsync(ct);

    var user = new User { Email = request.Email };
    db.Users.Add(user);
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (ex.IsUniqueViolation())
    {
        // Lost the race: answer exactly like the validation error does.
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Email"] = ["This email is already registered."]
        });
    }
    return Results.Created($"/users/{user.Id}", new { user.Id, user.Email });
});

// The same endpoint without the catch, to see what losing the race looks like on its own.
app.MapPost("/signup-unmapped", async (SignUp request, AppDb db, ISignUpGate gate, CancellationToken ct) =>
{
    await gate.WaitAsync(ct);

    var user = new User { Email = request.Email };
    db.Users.Add(user);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/users/{user.Id}", new { user.Id, user.Email });
});

app.MapPost("/signup-with-username", async (SignUpWithUserName request, AppDb db, CancellationToken ct) =>
{
    var user = new User { Email = request.Email, UserName = request.UserName };
    db.Users.Add(user);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/users/{user.Id}", new { user.Id, user.Email });
});

app.MapControllers();
app.MapExperiments();

app.Run();

/// <summary>
/// A pause between validation and the insert. Production uses <see cref="NoGate"/>;
/// the race test swaps in a gate that holds both requests until both have passed validation.
/// </summary>
public interface ISignUpGate
{
    Task WaitAsync(CancellationToken ct);
}

public sealed class NoGate : ISignUpGate
{
    public Task WaitAsync(CancellationToken ct) => Task.CompletedTask;
}

public partial class Program;
