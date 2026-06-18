# PriceCheck — Path of Exile 2

Десктоп-оверлей для оценки наград в лиг-механике **пилонов** Path of Exile 2.
Приложение читает область экрана с наградами пилона через OCR, берёт цены с
poe.ninja и показывает суммарную ценность каждого пилона поверх игры — чтобы
быстро выбрать выгодный вариант.

За основу взят открытый проект
[PoeAncientsPriceHelper](https://github.com/pedro-quiterio/PoeAncientsPriceHelper);
мы адаптируем его под механику пилонов PoE2 и расширяем (суммарная EV-оценка,
запуск в трей с игровым меню по хоткею, гибкие настройки).

## Возможности

- **Трей + игровое меню по хоткею.** Приложение стартует в системном трее. По
  глобальной горячей клавише (по умолчанию `F2`) прямо в игре экран затемняется
  и по центру открывается меню (настройки, калибровка, оверлей цен). Повтор
  хоткея или `Esc` — закрывает.
- **OCR наград** с разовой калибровкой области (`F4`).
- **Суммарная EV-оценка** каждого пилона и подсветка лучшего выбора.
- **Цены с poe.ninja** (раздел PoE2), кэш с обновлением раз в 30 минут.

> Игра должна работать в режиме **windowed-fullscreen**, иначе оверлей и хоткеи
> не будут видны поверх неё.

## Стек

C# / .NET 8 (`net8.0-windows`), WPF + WinForms, Tesseract OCR, SharpHook
(глобальные хоткеи), MahApps.Metro, Newtonsoft.Json.

## Сборка (Windows)

```powershell
cd src
dotnet restore
dotnet build -c Release
dotnet test
```

Запуск: собранный `PriceCheckPoe2.exe` из каталога вывода.

## Структура

```
src/
  PriceCheckPoe2/        # приложение
    Tray/                # запуск в трее (TrayApplicationContext)
    Overlay/             # игровое меню (MenuOverlay) и оверлей цен
    Capture/             # захват экрана и калибровка
    Ocr/                 # Tesseract + парсер названий наград
    Pricing/             # poe.ninja клиент, кэш, EV-оценка пилонов
    Config/              # настройки и глобальные хоткеи
    Data/                # reward-aliases.json
  PriceCheckPoe2.Tests/  # xUnit-тесты (парсер, evaluator)
```

## Статус

В разработке. Текущая веха и журнал проекта — в
`.claude/skills/poe2-pricecheck/SKILL.md`.

## Лицензия

См. ниже / TODO. Сохраняем атрибуцию исходного проекта PoeAncientsPriceHelper.
