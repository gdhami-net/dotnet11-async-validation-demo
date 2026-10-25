# dotnet11-async-validation-demo

A sign-up endpoint on .NET 11 RC2 that checks "is this email taken?" with an async
validation attribute, and the tests that show where that check stops being enough.

## What it proves

- `[UniqueEmail]`, an `AsyncValidationAttribute`, runs on a minimal API endpoint after
  `AddValidation()`. A taken email comes back as a normal 400 validation problem:

  ```json
  {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Email":["This email is already registered."]},"traceId":"..."}
  ```
- `[Required]` and `[EmailAddress]` failures stop the database lookup. That held even with
  `[UniqueEmail]` written first in the attribute list.
- Two sign-ups with the same new email, both held until both have passed validation:
  - without the unique index, both are saved (two rows);
  - with the index and no `catch`, the second gets an unhandled `DbUpdateException`, a 500;
  - with the `catch` in `Program.cs`, the second gets the same 400 body as the validation
    error (compared with `traceId` removed).
- Async attributes on one model run at the same time (two 100 ms checks finish in a little
  over 100 ms). Two checks that share the request's scoped `DbContext` throw
  "A second operation was started on this context instance" once each query takes real
  time. Opening a context per check from `IDbContextFactory<AppDb>` fixes it.
- The `IAsyncValidatableObject` version of the check gives the same field error, and only
  runs once the property rules pass.
- An MVC controller with the same model calls the synchronous `IsValid`, which throws.
  Async validation is for minimal APIs (and Blazor), not MVC.

## Run it

```bash
./check.sh          # or: pwsh ./check.ps1
```

If the .NET 11 RC2 SDK isn't the `dotnet` on your PATH, point `DOTNET` at it:

```bash
DOTNET=/path/to/dotnet ./check.sh
```

Everything runs on SQLite files in your temp folder; the tests delete them afterwards.
Nothing else to install.

## Versions

- .NET SDK 11.0.100-rc.2.26504.105 (pinned in `global.json`), runtime 11.0.0-rc.2.26504.105
- Microsoft.EntityFrameworkCore.Sqlite 11.0.0-rc.2.26504.105 (SQLite 3.53.4 through SQLitePCLRaw 3.0.5)
- Microsoft.AspNetCore.Mvc.Testing 11.0.0-rc.2.26504.105
- xUnit 2.9.3, xunit.runner.visualstudio 3.1.0, Microsoft.NET.Test.Sdk 17.14.1
- Run on Windows 11.

## How the tests make things deterministic

- **The race.** The endpoint awaits an `ISignUpGate` right after validation. The app
  registers a no-op gate. `RaceTests` swaps in a gate that holds every request until two
  have arrived, so both have passed `[UniqueEmail]` before either inserts. The test checks
  that two lookups ran before asserting on the rows.
- **Query time.** Microsoft.Data.Sqlite finishes its async calls synchronously, so on SQLite
  as-is two checks on one model never actually overlap (see
  `Sharing_the_scoped_DbContext_looks_fine_on_SQLite_as_is`). `QueryCounter`, an EF command
  interceptor, can await 50 ms before each lookup, which is what a database server across
  a network does to you. That's the setting where the shared context throws.
- **Counting lookups.** The same interceptor counts `SELECT ... FROM "Users"` commands, so
  "the database wasn't asked" is a number, not a guess.

## Edge cases the post leaves out

- **The 500 body.** `WebApplicationFactory` runs the app in Development, so the 500 in the
  race test carries the exception text. In Production it's a bare 500.
- **Other databases.** `IsUniqueViolation()` checks `SqliteException.SqliteExtendedErrorCode == 2067`
  (`SQLITE_CONSTRAINT_UNIQUE`). On SQL Server you'd check `SqlException.Number` for 2601 or
  2627; on PostgreSQL (Npgsql), `PostgresException.SqlState == "23505"`. Not run here.
- **Case.** The `Email` column uses the `NOCASE` collation so the index and the lookup agree
  that `Sara@Example.com` is taken. SQL Server's default collations are already
  case-insensitive; PostgreSQL's aren't (use `citext` or a lower-cased column).
- **One `SaveChanges`, more indexes.** If the table had a second unique index (user name),
  the catch would have to tell them apart by the constraint name in the message. The demo
  maps only Email.
- **Sync path.** `AsyncValidationAttribute.IsValid` is abstract; leaving it out is CS0534.
  The attributes here throw from it, as the docs suggest, which is why the MVC test is a 500.
- **`IValidationMessageFormatter`.** It exists in RC1 and is gone in RC2, replaced by
  `ValidationAttribute.FormatMessage`. The attributes here use `FormatErrorMessage`, which
  didn't change.
- **Blazor.** Not tried here.

## Layout

- `Api/Program.cs`: the endpoints, including the catch that maps the index violation.
- `Api/Validation.cs`: `[UniqueEmail]` and `[UniqueUserName]`.
- `Api/Data.cs`: `AppDb`, the unique index, `IsUniqueViolation()`.
- `Api/Experiments.cs`: models and endpoints that exist only to answer "how does this run?"
  (the overlap probe, attribute order, `IAsyncValidatableObject`, the scoped-context version,
  the MVC controller).
- `Tests/`: one class per question.

MIT licensed.
