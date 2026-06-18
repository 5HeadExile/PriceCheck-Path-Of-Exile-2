# План: PriceCheck для пилонов наград в Path of Exile 2

> **Это исходный утверждённый план (намерение).** Фактическое состояние
> реализации, отклонения и «где остановились» — в журнале проекта
> `.claude/skills/poe2-pricecheck/SKILL.md` (он и есть источник правды).
> Известные отклонения от плана: окно настроек сделано на WinForms (не WPF/
> MahApps); поддержка нескольких областей-пилонов вместо одной; имена файлов
> местами отличаются (`Tray/TrayApplicationContext.cs`, `Overlay/PriceOverlayForm.cs`,
> добавлен `Scanning/PylonScanner.cs`).

## Контекст

Создаём десктоп-приложение, которое оценивает награды в новой лиг-механике
Path of Exile 2 — пилоны с наградами. Игрок выбирает один пилон из нескольких,
и приложение должно помочь решить, какой выбор выгоднее, показав суммарную
ценность наград каждого пилона поверх игры.

За основу берём открытый проект
[PoeAncientsPriceHelper](https://github.com/pedro-quiterio/PoeAncientsPriceHelper) —
оверлей на C#/.NET 8, который через Tesseract OCR читает область экрана,
тянет цены с poe.ninja и рисует click-through оверлей.

**Решения пользователя:**
- Стек: **C#/.NET 8** (WPF + WinForms), как в референсе.
- Источник цен: **poe.ninja (раздел PoE2)**.
- Распознавание наград: **OCR-оверлей** с калибровкой области экрана.
- Главные улучшения: **суммарная EV-оценка выбора пилона**, **поддержка
  механики/типов наград PoE2**, **лучше UX и настройки**.

## Архитектура

Поток данных: область экрана → захват кадра → предобработка → Tesseract OCR →
нормализация названий → лукап цен poe.ninja → расчёт суммарной ценности пилона →
рендер click-through оверлея.

**Зависимости (NuGet):** `Tesseract` 5.2.0 (+`Tesseract.Data.English` 4.0.0),
`SharpHook` 7.1.2, `MahApps.Metro` 2.4.10, `Newtonsoft.Json` 13.0.3.
TargetFramework `net8.0-windows`, `UseWPF` + `UseWindowsForms`, Per-Monitor-V2 DPI.

## Компоненты

1. **Захват и калибровка** (`Capture/`) — drag-select области пилона (F4),
   координаты в конфиг.
2. **OCR** (`Ocr/OcrEngine.cs`) — Tesseract + предобработка (grayscale, threshold,
   upscale); `RewardParser` приводит текст к каноническим именам через
   `Data/reward-aliases.json` и fuzzy-match (Левенштейн); при низкой уверенности — `?`.
3. **Цены poe.ninja** (`Pricing/`) — `PoeNinjaClient` (через `IPriceSource`,
   заменяемый), `PriceCache` с рефрешем раз в N минут. Endpoint PoE2 уточняется
   (см. `docs/ENDPOINT.md`).
4. **EV-оценка** (`Pricing/PylonEvaluator.cs`) — суммарная ценность (цена×стак),
   подсветка лучшего пилона.
5. **Оверлей цен** (`Overlay/`) — click-through, цены и итог по пилону;
   F3 — debug-боксы, Esc/Ctrl+Click — скрыть.
6. **Трей + игровое меню** (`Tray/`, `Overlay/MenuOverlay`) — запуск в трей;
   меню по глобальному хоткею: затемнение + центрированное окно. Игра должна быть
   в windowed-fullscreen.
7. **Настройки** (`Settings/`, `Config/`) — лига, рефреш, прозрачности,
   переназначение хоткеев; конфиг в JSON рядом с exe.

## Поэтапные вехи

- **M0** — скилл-журнал.
- **M1** — скаффолд (sln, csproj, манифест, запуск в трее).
- **M2** — трей + игровое меню.
- **M3** — захват + OCR + калибровка.
- **M4** — цены poe.ninja (клиент + кэш).
- **M5** — EV-оценка + оркестратор + оверлей.
- **M6** — UX/настройки.
- **M7** — тесты + README/docs.

## Верификация (выполняется на Windows)

- `dotnet build -c Release` без ошибок; `dotnet test` (xUnit: `RewardParser`,
  `PylonEvaluator`, `PylonScanner`).
- Ручная проверка OCR на скриншотах пилонов; калибровка порога OCR.
- Сверка endpoint poe.ninja PoE2 в браузере (`docs/ENDPOINT.md`).
- E2E: запуск поверх PoE2, калибровка, проверка сумм по пилонам.

## Риски и открытые вопросы

- **Endpoint poe.ninja PoE2 не подтверждён** — заменяемый `IPriceSource`.
- **UI пилонов PoE2** точно не известен — `RewardParser`/`reward-aliases.json`
  расширяемы, калибровка гибкая (несколько областей).
- Точность OCR на тёмном фоне — предобработка + настраиваемый порог + fuzzy-match.
- Лицензия референса — сохранить атрибуцию PoeAncientsPriceHelper.

## Ветка

Вся разработка — на `claude/poe2-rewards-app-plan-v0tpdz`.
