# Сверка endpoint цен poe.ninja (PoE2)

Точный API-путь экономики PoE2 на poe.ninja **не задокументирован** и может
меняться между лигами. Поэтому он вынесен в настройки (`PriceApiBaseUrl`,
`PriceOverviews` в `config.json`). Ниже — как подобрать рабочий URL и поля.

## Как найти URL

1. Открой в браузере страницу экономики текущей лиги, например:
   `https://poe.ninja/poe2/economy/runesofaldur/currency`
2. Открой DevTools (`F12`) → вкладка **Network** → фильтр **Fetch/XHR**.
3. Переключи категорию (Currency / Runes / Fragments…) — в списке появится
   запрос с JSON цен. Скопируй его полный URL и посмотри ответ.
4. Базовую часть URL (без `leagueName`/`overviewName` или их аналогов) впиши в
   настройках в поле **«URL цен»**. Имена категорий — в `PriceOverviews`
   (`config.json`).

Кандидат по умолчанию (требует проверки):
`https://poe.ninja/poe2/api/economy/currencyexchange/overview`
с параметрами `leagueName` и `overviewName`.

## Поля ответа

Парсер (`PoeNinjaClient.MergeOverview`) устойчив к нескольким формам и ищет:

- **массив** под ключом `lines` / `items` / `entries` (или корневой массив);
- **имя**: `name` / `currencyTypeName` / `itemName` / `text`;
- **цену**: `exaltedValue` / `chaosValue` / `value` / `receive`;
- **divine** (опц.): `divineValue`.

Если реальная схема PoE2 отличается (например, currency-exchange отдаёт
соотношения, а не значения в exalted) — поправь `MergeOverview` под фактические
поля. Структуру ответа удобно сверить прямо из DevTools (вкладка Response).

## Замена источника

Если poe.ninja не подойдёт, источник заменяем без правок остального кода:
реализуй `IPriceSource` (например, поверх официального Trade API PoE2) и
подставь его в `PriceCache` вместо `PoeNinjaClient`.
