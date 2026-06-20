using PriceCheckPoe2.ItemCheck.Trade;
using Xunit;

namespace PriceCheckPoe2.Tests;

public class TradeTests
{
    [Fact]
    public void QueryBuilder_BuildsStatsAndStatusAndSort()
    {
        var body = TradeQueryBuilder.Build(new[]
        {
            new TradeFilter("explicit.stat_3299347043", 50, null),
            new TradeFilter("explicit.stat_3372524247", 30, 45),
        });

        Assert.Equal("online", (string?)body["query"]!["status"]!["option"]);
        Assert.Equal("asc", (string?)body["sort"]!["price"]);

        var filters = body["query"]!["stats"]![0]!["filters"]!;
        Assert.Equal("and", (string?)body["query"]!["stats"]![0]!["type"]);
        Assert.Equal("explicit.stat_3299347043", (string?)filters[0]!["id"]);
        Assert.Equal(50, (double?)filters[0]!["value"]!["min"]);
        Assert.Equal(30, (double?)filters[1]!["value"]!["min"]);
        Assert.Equal(45, (double?)filters[1]!["value"]!["max"]);
    }

    [Fact]
    public void QueryBuilder_Context_BuildsCategoryFiltersAndStats()
    {
        var ctx = new TradeQueryContext
        {
            CategoryId = "armour.helmet",
            Corrupted = false,
            Mirrored = false,
            Stats = new[] { new StatQuery("explicit.stat_3299347043", 90) },
        };

        var body = TradeQueryBuilder.Build(ctx);
        var filters = body["query"]!["filters"]!;

        Assert.Equal("armour.helmet", (string?)filters["type_filters"]!["filters"]!["category"]!["option"]);
        Assert.Equal("false", (string?)filters["misc_filters"]!["filters"]!["corrupted"]!["option"]);
        Assert.Equal("false", (string?)filters["misc_filters"]!["filters"]!["mirrored"]!["option"]);

        var stat = body["query"]!["stats"]![0]!["filters"]![0]!;
        Assert.Equal("explicit.stat_3299347043", (string?)stat["id"]);
        Assert.Equal(90, (double?)stat["value"]!["min"]);
    }

    [Fact]
    public void RateLimiter_ParsesPolicy()
    {
        var rules = RateLimiter.ParsePolicy("8:10:60,15:60:300");
        Assert.Equal(2, rules.Count);
        Assert.Equal(new RateRule(8, 10, 60), rules[0]);
        Assert.Equal(new RateRule(15, 60, 300), rules[1]);
    }

    [Fact]
    public void RateLimiter_WaitsWhenWindowFull()
    {
        var now = DateTime.UtcNow;
        // 8 запросов за последние ~3 секунды, лимит 8 за 10с → ждём ~7с.
        var history = Enumerable.Range(0, 8)
            .Select(i => now - TimeSpan.FromSeconds(3) + TimeSpan.FromMilliseconds(i * 10))
            .ToList();
        var rules = new[] { new RateRule(8, 10, 60) };

        var delay = RateLimiter.ComputeDelay(history, now, rules, TimeSpan.FromMilliseconds(50));
        Assert.InRange(delay.TotalSeconds, 6.5, 7.5);
    }

    [Fact]
    public void RateLimiter_NoHistory_NoDelay()
    {
        var delay = RateLimiter.ComputeDelay(
            Array.Empty<DateTime>(), DateTime.UtcNow, Array.Empty<RateRule>(), TimeSpan.FromMilliseconds(400));
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ParseSearch_ReadsIdHashesTotal()
    {
        var r = TradeClient.ParseSearch("""{"id":"abc","result":["h1","h2"],"total":42}""");
        Assert.Equal("abc", r.Id);
        Assert.Equal(new[] { "h1", "h2" }, r.Hashes);
        Assert.Equal(42, r.Total);
    }

    [Fact]
    public void ParseFetch_ReadsListing()
    {
        const string json = """
        {"result":[{"listing":{"price":{"amount":5,"currency":"exalted"},
        "account":{"name":"Bob"},"whisper":"@Bob hi"}}]}
        """;
        var listings = TradeClient.ParseFetch(json);
        var l = Assert.Single(listings);
        Assert.Equal("Bob", l.Account);
        Assert.Equal(5, l.Amount, 3);
        Assert.Equal("exalted", l.Currency);
        Assert.Equal("@Bob hi", l.Whisper);
    }
}
