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
- Стабы захвата экрана, OCR, клиента poe.ninja и оверлея цен под следующие вехи.
- Unit-тесты парсера и evaluator (xUnit).
- Журнал проекта в `.claude/skills/poe2-pricecheck/SKILL.md`.
