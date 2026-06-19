# Vendored data — Exiled-Exchange-2 (MIT)

Файлы `stats.ndjson` и `items.ndjson` в этой папке — сгенерированные игровые
данные Path of Exile 2 (english), взятые из открытого проекта
**Exiled-Exchange-2** и используемые для сопоставления текста модов с trade
`stat id` и для справочника баз предметов.

- **Источник:** https://github.com/Kvan7/Exiled-Exchange-2
- **Путь в источнике:** `renderer/public/data/en/{stats,items}.ndjson`
- **Зафиксированный коммит:** см. `SOURCE_COMMIT.txt`
- **Лицензия:** MIT

Exiled-Exchange-2 — форк **Awakened PoE Trade**:

- https://github.com/SnosMe/awakened-poe-trade — MIT

## Лицензия MIT (обоих проектов)

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Схема (кратко)

`stats.ndjson` — по строке на стат:
`{"ref": "# to maximum Life", "matchers": [{"string": "# to maximum Life"}],
"trade": {"ids": {"explicit": ["explicit.stat_3299347043"], ...}}, "id": "..."}`

`items.ndjson` — по строке на предмет/базу:
`{"name": "...", "refName": "...", "namespace": "GEM|ITEM|UNIQUE|...",
"craftable": {"category": "..."}, "w": 1, "h": 1, ...}`

Обновление: перекачать те же файлы с нужного коммита EE2 и обновить
`SOURCE_COMMIT.txt`.
