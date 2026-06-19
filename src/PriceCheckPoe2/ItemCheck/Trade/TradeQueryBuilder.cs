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
    public static JObject Build(
        IReadOnlyList<TradeFilter> filters,
        string? name = null,
        string? type = null,
        bool onlineOnly = true)
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

        var query = new JObject
        {
            ["status"] = new JObject { ["option"] = onlineOnly ? "online" : "any" },
            ["stats"] = new JArray
            {
                new JObject { ["type"] = "and", ["filters"] = statFilters },
            },
        };

        // Имя (для униакалов) и тип (база/валюта/омен) сужают поиск до нужного
        // предмета — без этого поиск по пустым статам возвращает мусор.
        if (!string.IsNullOrWhiteSpace(name))
        {
            query["name"] = name;
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query["type"] = type;
        }

        return new JObject
        {
            ["query"] = query,
            ["sort"] = new JObject { ["price"] = "asc" },
        };
    }
}
