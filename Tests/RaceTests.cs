using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace SignUpDemo.Tests;

public class RaceTests(ITestOutputHelper output)
{
    private static Task<HttpResponseMessage[]> TwoAtOnce(HttpClient client, string path, string email) =>
        Task.WhenAll(
            client.PostAsJsonAsync(path, new { email }),
            client.PostAsJsonAsync(path, new { email }));

    [Fact]
    public async Task Without_the_unique_index_both_sign_ups_get_in()
    {
        using var factory = new SignUpFactory { Gate = new BarrierGate(2) };
        await factory.DropUniqueIndexAsync();
        var client = factory.CreateClient();

        var responses = await TwoAtOnce(client, "/signup", "omar@example.com");

        output.WriteLine(string.Join(", ", responses.Select(r => (int)r.StatusCode)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(2, await factory.CountUsersAsync("omar@example.com"));
    }

    [Fact]
    public async Task With_the_index_but_no_catch_the_loser_gets_a_500()
    {
        using var factory = new SignUpFactory { Gate = new BarrierGate(2) };
        var client = factory.CreateClient();

        var responses = await TwoAtOnce(client, "/signup-unmapped", "omar@example.com");

        foreach (var r in responses)
        {
            var text = await r.Content.ReadAsStringAsync();
            output.WriteLine($"{(int)r.StatusCode} {text[..Math.Min(400, text.Length)]}");
        }
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.InternalServerError],
            responses.Select(r => r.StatusCode).Order());
        Assert.Equal(1, await factory.CountUsersAsync("omar@example.com"));
    }

    [Fact]
    public async Task With_the_index_and_the_catch_the_loser_gets_the_same_400()
    {
        using var factory = new SignUpFactory { Gate = new BarrierGate(2) };
        var client = factory.CreateClient();

        var responses = await TwoAtOnce(client, "/signup", "omar@example.com");
        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        var raceBody = await loser.Content.ReadAsStringAsync();
        output.WriteLine($"race loser: {(int)loser.StatusCode} {raceBody}");

        // Both requests passed the async check before either one saved.
        Assert.Equal(2, factory.Queries.Lookups);

        // A third, later request is stopped by validation instead. Same answer.
        var late = await client.PostAsJsonAsync("/signup", new { email = "omar@example.com" });
        var lateBody = await late.Content.ReadAsStringAsync();
        output.WriteLine($"late request: {(int)late.StatusCode} {lateBody}");

        Assert.Equal(HttpStatusCode.BadRequest, loser.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Equal(Normalize(lateBody), Normalize(raceBody));
        Assert.Equal(1, await factory.CountUsersAsync("omar@example.com"));
    }

    // Drops traceId, which differs per request.
    private static string Normalize(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }
}
