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

    /// <summary>
    /// Полный запрос из <see cref="TradeQueryContext"/> — портирован из EE2
    /// createTradeRequest (MIT): name/type, type_filters (категория/редкость/ilvl/
    /// quality), req_filters (lvl), misc_filters (corrupted/mirrored/identified/
    /// gem_level/sockets) и стат-группа and.
    /// </summary>
    public static JObject Build(TradeQueryContext ctx)
    {
        var statFilters = new JArray();
        foreach (var s in ctx.Stats)
        {
            var entry = new JObject { ["id"] = s.Id, ["disabled"] = false };
            var value = new JObject();
            if (s.Min.HasValue) value["min"] = s.Min.Value;
            if (s.Max.HasValue) value["max"] = s.Max.Value;
            if (s.Option is not null) value["option"] = s.Option;
            if (value.HasValues) entry["value"] = value;
            statFilters.Add(entry);
        }

        var filters = new JObject();

        void Set(string group, string key, JToken val)
        {
            if (filters[group] is not JObject g)
            {
                g = new JObject { ["filters"] = new JObject() };
                filters[group] = g;
            }

            ((JObject)g["filters"]!)[key] = val;
        }

        JObject Range(int? min, int? max)
        {
            var r = new JObject();
            if (min.HasValue) r["min"] = min.Value;
            if (max.HasValue) r["max"] = max.Value;
            return r;
        }

        JObject Option(string opt) => new() { ["option"] = opt };
        JObject Bool(bool v) => new() { ["option"] = v ? "true" : "false" };

        if (ctx.CategoryId is not null) Set("type_filters", "category", Option(ctx.CategoryId));
        if (ctx.RarityOption is not null) Set("type_filters", "rarity", Option(ctx.RarityOption));
        if (ctx.ItemLevelMin.HasValue || ctx.ItemLevelMax.HasValue)
            Set("type_filters", "ilvl", Range(ctx.ItemLevelMin, ctx.ItemLevelMax));
        if (ctx.QualityMin.HasValue) Set("type_filters", "quality", Range(ctx.QualityMin, null));

        if (ctx.RequireLevelMax.HasValue) Set("req_filters", "lvl", Range(null, ctx.RequireLevelMax));

        if (ctx.GemLevelMin.HasValue || ctx.GemLevelMax.HasValue)
            Set("misc_filters", "gem_level", Range(ctx.GemLevelMin, ctx.GemLevelMax));
        if (ctx.SocketsMin.HasValue) Set("misc_filters", "gem_sockets", Range(ctx.SocketsMin, null));
        if (ctx.Corrupted.HasValue) Set("misc_filters", "corrupted", Bool(ctx.Corrupted.Value));
        if (ctx.Mirrored.HasValue) Set("misc_filters", "mirrored", Bool(ctx.Mirrored.Value));
        if (ctx.Identified.HasValue) Set("misc_filters", "identified", Bool(ctx.Identified.Value));

        var query = new JObject
        {
            ["status"] = new JObject { ["option"] = ctx.ListingType },
            ["stats"] = new JArray { new JObject { ["type"] = "and", ["filters"] = statFilters } },
            ["filters"] = filters,
        };

        if (!string.IsNullOrWhiteSpace(ctx.Name)) query["name"] = ctx.Name;
        if (!string.IsNullOrWhiteSpace(ctx.Type)) query["type"] = ctx.Type;

        return new JObject { ["query"] = query, ["sort"] = new JObject { ["price"] = "asc" } };
    }
}
