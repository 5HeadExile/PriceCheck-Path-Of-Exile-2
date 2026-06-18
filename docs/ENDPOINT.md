# Endpoint цен poe.ninja (PoE2) — подтверждён

Проверено в браузере (DevTools → Network) на лиге **Runes of Aldur** 2026-06-18.

## Рабочий запрос

```
GET https://poe.ninja/poe2/api/economy/exchange/current/overview?league=Runes+of+Aldur&type=Currency
```

- `league` — имя лиги (пробелы как `+`/`%20`); список лиг: `…/poe2/api/data/index-state`.
- `type` — категория. `Currency` покрывает орбы (включая Jeweller's/Gemcutter's/
  Glassblower's, Orb of Transmutation/Augmentation и т.д.).

В приложении это `PriceApiBaseUrl` + `PriceOverviews` (настройки/`config.json`);
параметры `league`/`type` подставляет `PoeNinjaClient`.

## Схема ответа

```json
{
  "core":  { "rates": { "exalted": 195.5, "chaos": 8.86 }, "primary": "divine" },
  "lines": [ { "id": "exalted", "primaryValue": 0.005114, ... } ],
  "items": [ { "id": "exalted", "name": "Exalted Orb", ... } ]
}
```

- `lines[].id` → слаг; `lines[].primaryValue` → цена в основной валюте
  (`core.primary`, обычно **divine**).
- `items[]` — маппинг `id → name` (отображаемое имя, по нему матчим OCR).
- Перевод в exalted: `exaltedValue = primaryValue * core.rates.exalted`
  (при `primary == "exalted"` множитель = 1).

`PoeNinjaClient.MergeOverview` парсит ровно эту схему.

## Чего нет в Currency

`Uncut Spirit Gem` и прочие гемы — отдельная категория (другой `type`),
в `Currency` отсутствуют → показываются как `?`. Добавить можно, дописав нужный
`type` в `PriceOverviews` (уточнить имя категории тем же способом через Network).

## Замена источника

Источник заменяем без правок остального кода: реализуй `IPriceSource`
(например, поверх официального Trade API PoE2) и подставь в `PriceCache`.
