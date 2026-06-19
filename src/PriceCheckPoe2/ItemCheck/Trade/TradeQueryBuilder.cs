using Newtonsoft.Json.Linq;

namespace PriceCheckPoe2.ItemCheck.Trade;

/// <summary>
/// Собирает тело JSON для POST официального trade2 search. Формат:
/// <code>
/// { "query": { "status": {"option":"online"},
///              "stats": [ { "type":"and", "filters":[ { "id", "value":{min,max} } ] } ] },
///   "sort": { "price":"asc" } }
/// </code>
/// </summary>
public static class TradeQueryBuilder
{
    public static JObject Build(IReadOnlyList<TradeFilter> filters, bool onlineOnly = true)
    {
        var statFilters = new JArray();
        foreach (var f in filters)
        {
            var entry = new JObject
            {
                ["id"] = f.TradeId,
                ["disabled"] = false,
            };

            var value = new JObject();
            if (f.Min.HasValue)
            {
                value["min"] = f.Min.Value;
            }

            if (f.Max.HasValue)
            {
                value["max"] = f.Max.Value;
            }

            if (value.HasValues)
            {
                entry["value"] = value;
            }

            statFilters.Add(entry);
        }

        return new JObject
        {
            ["query"] = new JObject
            {
                ["status"] = new JObject { ["option"] = onlineOnly ? "online" : "any" },
                ["stats"] = new JArray
                {
                    new JObject { ["type"] = "and", ["filters"] = statFilters },
                },
            },
            ["sort"] = new JObject { ["price"] = "asc" },
        };
    }

    public static string BuildJson(IReadOnlyList<TradeFilter> filters, bool onlineOnly = true) =>
        Build(filters, onlineOnly).ToString(Newtonsoft.Json.Formatting.None);
}
