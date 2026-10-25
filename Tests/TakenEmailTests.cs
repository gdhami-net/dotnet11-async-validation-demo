using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace SignUpDemo.Tests;

public class TakenEmailTests(ITestOutputHelper output)
{
    [Fact]
    public async Task A_taken_email_gets_a_400_with_the_field_error()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/signup", new { email = "sara@example.com" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/signup", new { email = "sara@example.com" });
        var body = await second.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)second.StatusCode} {second.Content.Headers.ContentType}");
        output.WriteLine(body);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.Equal("This email is already registered.", errors.GetProperty("Email")[0].GetString());
        Assert.Equal(1, await factory.CountUsersAsync("sara@example.com"));
    }

    [Fact]
    public async Task The_check_ignores_case_like_the_index()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/signup", new { email = "sara@example.com" });
        var second = await client.PostAsJsonAsync("/signup", new { email = "Sara@Example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Theory]
    [InlineData("", "The Email field is required.")]
    [InlineData("not-an-email", "The Email field is not a valid e-mail address.")]
    public async Task Required_and_EmailAddress_fail_without_asking_the_database(string email, string message)
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/signup", new { email });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine(body);
        output.WriteLine($"database checks: {factory.Queries.Lookups}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors").GetProperty("Email");
        Assert.Equal(message, errors[0].GetString());
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal(0, factory.Queries.Lookups);
    }
}
