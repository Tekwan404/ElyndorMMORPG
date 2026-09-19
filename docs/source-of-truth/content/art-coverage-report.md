# Каталог соответствия игрового контента и изображений

Снимок собран импортёром из текущих content/items, content/monsters и файлов web/elyndor-web/src/assets. Всего предметов: 655; мобов: 244.

`DIRECT_ASSET` — файл найден по canonical `iconId` / `artId`; `MISSING_ASSET` и `MISSING_DIRECT_ASSET` перечисляют контент без собственного файла. Для предметов UI использует существующий glyph fallback. Для мобов без direct asset может отображаться существующий semantic/location fallback, поэтому он не гарантирует точный портрет конкретного вида. Неоднозначные источники не назначаются автоматически.

Полный список по каждому шаблону, файлу, исходному листу и ячейке: [art-coverage-catalog.csv](art-coverage-catalog.csv).

| Тип и статус | Количество |
| --- | ---: |
| EnemyPortrait, NO_EXACT_RUNTIME_MONSTER | 16 |
| Item, DIRECT_ASSET | 374 |
| Item, MISSING_ASSET | 171 |
| Item, NO_ICON_ID | 110 |
| Monster, DIRECT_ASSET | 86 |
| Monster, MISSING_DIRECT_ASSET | 158 |
| SetSheet, SOURCE_HAS_NO_CURRENT_SET | 24 |
| SourceItem, NO_EXACT_RUNTIME_ITEM | 26 |
