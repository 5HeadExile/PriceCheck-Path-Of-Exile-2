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

## Категории (`type`)

Все вкладки экономики идут через ОДИН и тот же endpoint, меняется только `type`.
Тянем все стакаемые категории актуальной лиги (есть данные, проверено DevTools):

`Currency`, `Fragments`, `Runes`, `Essences`, `SoulCores`, `UncutGems`,
`LineageSupportGems`, `Idols`, `Expedition`, `Verisium`.

`type` = PascalCase от slug вкладки (`soul-cores` → `SoulCores`). Уники
(`UniqueWeapons` и т.п.), `Omens`, `AbyssalBones`, `LiquidEmotions`,
`BreachCatalyst`, `PrecursorTablets` через этот endpoint пустые (другой механизм)
и пилонами как стак не выдаются — не тянем.

Список `PriceOverviews` помечен `[JsonIgnore]` (встроенный каталог): он всегда
берётся из кода, старый `config.json` не «заморозит» урезанный набор.

### Гемы (нюанс)

`UncutGems` оцениваются ПО УРОВНЮ (`Uncut Skill Gem (Level 12)` и т.д.). Награда
вида «Uncut Spirit Gem» без уровня надёжно не сопоставляется → показывается `?`
(лучше, чем угадать чужой уровень). Точный матчинг по уровню — отдельная доработка.

## Замена источника

Источник заменяем без правок остального кода: реализуй `IPriceSource`
(например, поверх официального Trade API PoE2) и подставь в `PriceCache`.
