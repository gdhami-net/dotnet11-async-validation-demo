using System.Net;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace SignUpDemo.Tests;

// Two async attributes on one model. First both borrowing the request's scoped AppDb,
// then each opening its own context from IDbContextFactory.
public class SharedDbContextTests(ITestOutputHelper output)
{
    private async Task<HttpResponseMessage> Post(HttpClient client, string path, int i)
    {
        var r = await client.PostAsJsonAsync(path, new { email = $"user{i}@example.com", userName = $"user{i}" });
        var text = await r.Content.ReadAsStringAsync();
        output.WriteLine($"{path}: {(int)r.StatusCode} {text[..Math.Min(500, text.Length)]}");
        return r;
    }

    [Fact]
    public async Task Sharing_the_scoped_DbContext_looks_fine_on_SQLite_as_is()
    {
        // Microsoft.Data.Sqlite finishes its async calls synchronously, so the two checks never overlap.
        using var factory = new SignUpFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/experiments/scoped-db", i)).StatusCode);
    }

    [Fact]
    public async Task Sharing_the_scoped_DbContext_throws_once_queries_take_time()
    {
        using var factory = new SignUpFactory();
        factory.Queries.Delay = TimeSpan.FromMilliseconds(50);
        var client = factory.CreateClient();

        var response = await Post(client, "/experiments/scoped-db", 1);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("A second operation was started on this context instance",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_context_per_check_from_IDbContextFactory_works_when_queries_take_time()
    {
        using var factory = new SignUpFactory();
        factory.Queries.Delay = TimeSpan.FromMilliseconds(50);
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await Post(client, "/signup-with-username", i)).StatusCode);

        // Both checks really ran: one lookup each per request.
        Assert.Equal(10, factory.Queries.Lookups);
    }
}
