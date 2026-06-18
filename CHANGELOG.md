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
