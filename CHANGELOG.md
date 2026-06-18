# Changelog

## [Unreleased]

### Added
- Скаффолд проекта C#/.NET 8 (`src/PriceCheckPoe2`): solution, csproj с
  зависимостями (Tesseract, SharpHook, MahApps.Metro, Newtonsoft.Json), манифест
  с Per-Monitor-V2 DPI.
- Запуск в системный трей (`TrayApplicationContext`, `NotifyIcon`).
- Игровое меню по глобальному хоткею: затемнение экрана + центрированное окно
  (`Overlay/MenuOverlay`), перехват клавиш через SharpHook (`HotkeyManager`).
- Доменные модели и оценка пилонов (`PylonEvaluator`) — суммарная EV-ценность.
- Парсер названий наград с fuzzy-match по Левенштейну (`RewardParser`).
- Захват экрана (`ScreenCapturer`), калибровка области (`CalibrationOverlay`),
  OCR с предобработкой под тёмный фон (`OcrEngine`).
- Конфигурируемый клиент poe.ninja PoE2 (`PoeNinjaClient`) + кэш (`PriceCache`);
  дефолтная лига «Runes of Aldur», endpoint и категории вынесены в настройки.
- Оркестратор пайплайна `PylonScanner` (захват→OCR→стак→цена→EV) и рендер
  оверлея цен (`PriceOverlayForm`, click-through).
- Окно настроек (`SettingsForm`, WinForms): лига, URL цен, интервал, хоткеи,
  прозрачности; сохранение профиля калибровки.
- Unit-тесты парсера, evaluator и разбора количества в `PylonScanner` (xUnit),
  `InternalsVisibleTo` для тест-сборки.
- Поддержка нескольких пилонов: калибровка и оценка нескольких областей
  (`PylonScanner.ScanAllAsync`), подсветка лучшего пилона (★) на оверлее.
- Debug-режим оверлея (`F3`) — рамки откалиброванных областей.
- Настраиваемый порог бинаризации OCR и сохранение отладочных кадров.
- Меню расширено: добавить/сбросить пилон, пересканировать; отложенный клик
  против реентрантного закрытия формы меню.
- `docs/ENDPOINT.md` — как сверить endpoint poe.ninja для PoE2; раздел
  «Как пользоваться» в README.
- Журнал проекта в `.claude/skills/poe2-pricecheck/SKILL.md`.

### Changed
- Авто-режим: фоновый `RegionMonitor` + `ListDetector` сами показывают оверлей
  при открытии панели пилона и скрывают при закрытии; OCR запускается только при
  открытии/смене содержимого (детект по сигнатуре). Калибровка области → авто-скан.
  Меню: «Оверлей: пауза/возобновить», «Пересканировать сейчас».
- Подтверждён и прописан реальный endpoint poe.ninja PoE2
  (`…/exchange/current/overview?league=&type=Currency`); `PoeNinjaClient`
  переписан под схему core/lines/items с переводом в exalted.
- Имена наград матчатся по живому прайс-листу (`RewardParser.FromNames`),
  нормализация имён без пунктуации; `reward-aliases.json` — теперь fallback.
- OCR возвращает строки с координатами (`OcrLine`); оверлей рисует цену
  напротив каждой награды и компактный итог по пилону.

### Fixed
- Первый живой тест: всё оценивалось в 0.0 (неверный endpoint + имена PoE1 в
  словаре). Исправлено — цены реально считаются.
- Сборка на Windows (.NET 8): `KeyCode` из `SharpHook.Data` (не `.Native`);
  возвращены global usings `System.IO`/`System.Net.Http` (WinForms даёт урезанный
  набор); Tesseract 5.x — `Pix.LoadFromMemory` вместо `PixConverter`, алиас для
  `ImageFormat`, 2-арг конструктор `TesseractEngine`; DPI перенесён в
  `ApplicationHighDpiMode`. Сборка зелёная, 21/21 тест проходит.
