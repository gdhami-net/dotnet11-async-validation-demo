using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace SignUpDemo.Tests;

public class HowValidationRunsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Two_async_attributes_on_one_model_run_at_the_same_time()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();
        var probe = factory.Services.GetRequiredService<OverlapProbe>();

        var started = DateTime.UtcNow;
        var response = await client.PostAsJsonAsync("/experiments/two-slow-checks", new { a = "x", b = "y" });
        var took = DateTime.UtcNow - started;
        output.WriteLine($"{(int)response.StatusCode}, two 100 ms checks took {took.TotalMilliseconds:F0} ms, max at once: {probe.Max}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, probe.Max);
    }

    [Fact]
    public async Task Attribute_order_does_not_matter_EmailAddress_still_blocks_the_lookup()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/experiments/unique-first", new { email = "not-an-email" });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)response.StatusCode} {body}");
        output.WriteLine($"lookups: {factory.Queries.Lookups}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Queries.Lookups);
    }

    [Fact]
    public async Task IAsyncValidatableObject_gives_the_same_field_error()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/signup", new { email = "sara@example.com" });
        var response = await client.PostAsJsonAsync("/experiments/as-object", new { email = "sara@example.com" });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.Equal("This email is already registered.", errors.GetProperty("Email")[0].GetString());
    }

    [Fact]
    public async Task IAsyncValidatableObject_waits_for_the_property_rules()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/experiments/as-object", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Queries.Lookups);
    }

    [Fact]
    public async Task An_MVC_controller_calls_the_sync_IsValid()
    {
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/mvc/signup", new { email = "sara@example.com" });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)response.StatusCode} {body[..Math.Min(700, body.Length)]}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("UniqueEmail only runs on the async path.", body);
    }
}
