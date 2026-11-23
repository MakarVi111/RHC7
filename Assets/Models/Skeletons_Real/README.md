# RHC7 — реальные скелеты (сканы музеев)

Два полных скелета динозавров, собранных из настоящих фотограмметрических сканов, а не из процедурных примитивов. Используются как музейные экспонаты/декорации; игровые кости для чистки лежат отдельно в `Assets/Models/Bones`.

| Модель | Файлы | Треугольники | Габариты (X × Y × Z), м | Текстуры |
|---|---|---|---|---|
| Теропод, T. rex | `Skeleton_Trex_Real.fbx` / `.glb` | 99 889 | 1,400 × 0,919 × 0,546 | 2K PBR, сгенерированные |
| Цератопс, Triceratops | `Skeleton_Triceratops_Real.fbx` / `.glb` | 89 999 | 1,200 × 0,339 × 0,571 | 4K PBR, оригинальные |
| Подставка цератопса | `Triceratops_Stand.fbx` / `.glb` | 948 | 0,400 × 0,288 × 0,324 | металл |

Сцена Blender: `Blender/RHC7_Skeletons_Real.blend` (скелеты, подставка, свет, камера, пол).  
Превью: `Screenshots/skeletons_real_pair.png`, `skeleton_trex_real.png`, `skeleton_triceratops_real.png`, `skeletons_real_sheet.png`, детали черепов `skeleton_trex_skull_detail.png`, `skeleton_triceratops_skull_detail.png`.

## Источники и лицензии

### T. rex

- **Автор:** Rigsters — <https://rigsters.com/>
- **Файл:** Wikimedia Commons, `File:Tyrannosaurus rex skeleton.stl`
- **Оригинал:** <https://commons.wikimedia.org/wiki/File:Tyrannosaurus_rex_skeleton.stl>
- **Sketchfab:** <https://sketchfab.com/3d-models/tyrannosaurus-rex-skeleton-77e21c4e5d65468bbe11780ae0557b95>
- **Лицензия:** **CC BY 4.0** — <https://creativecommons.org/licenses/by/4.0/>
- **Что это:** фотограмметрия (133 фото, Reality Capture) реплики скелета T. rex в парке Knuthenborg Safaripark, Дания. Оригинальная текстура в STL отсутствует.

### Triceratops

- **Институт:** Smithsonian Institution, Национальный музей естественной истории (NMNH)
- **Экземпляр:** *Triceratops horridus* Marsh, 1889, USNM PAL 500000 («Hatcher»)
- **3D-запись:** <https://3d.si.edu/object/3d/triceratops-horridus-marsh-1889:d8c623be-4ebc-11ea-b77f-2e728ce88125>
- **Файлы:** Wikimedia Commons `File:Triceratops horridus Marsh 1889-150k (Smithsonian Institute).stl`; текстурированный GLB — Innerscene 3D Parts Library, <https://www.innerscene.com/tools/library/3d-parts/triceratops-skeleton-39cc8434>
- **Лицензия:** **CC0 1.0** (общественное достояние) — <https://creativecommons.org/publicdomain/zero/1.0/>
- **Что это:** полный скелет в музейной позе «Хатчера» — голова опущена, одна нога приподнята, как при развороте в защиту. Скан содержит кости, но не металлическую арматуру крепления; поэтому добавлена отдельная подставка.

Атрибуция T. rex обязательна при распространении модели. Для Triceratops указание Smithsonian желательно, но не обязательно.

## Обработка

### T. rex

1. Исходный STL: 250 194 треугольника, 12 компонентов, без UV и без текстуры.
2. Удалены мелкие обрывки скана (< 80 треугольников), объединены дубликаты, пересчитаны нормали.
3. Decimate до **99 889** треугольников.
4. Smart UV Project, UV повторены 3× для плотности текстуры.
5. Сгенерированы 2K PBR-карты кости: albedo, normal, roughness, AO (проект RHC7).
6. Масштаб: длина 1,4 м; центр по X/Y, низ по Z = 0.

### Triceratops

1. Исходный GLB: 149 997 треугольников, UV + 4K base color / normal / AO.
2. Развёрнут из положения скана в вертикальную музейную позу, decimate до **89 999** треугольников, UV сохранены.
3. Масштаб: длина 1,2 м; центр по X/Y, низ по Z = 0.
4. 4K-текстуры извлечены из GLB в `Textures/` (JPEG), оригинальные UV не менялись.

## Ориентация, origin и Unity

- Длина по **X**, голова в **+X**.
- Origin в центре габарита по X/Y, низ (Z = 0) на полу.
- FBX/GLB экспортированы с Y-up, масштаб 1:1 в метрах.
- В Unity: Scale Factor = 1, материалы FBX/GLB с встроенными текстурами. При необходимости заменить на URP Lit и включить normal map.
- Подставка цератопса — отдельный объект; ставится в тот же origin, что и скелет. В FBX/GLB скелета её нет.
- Для столкновений использовать упрощённый `MeshCollider` (convex) или несколько боксов: 100k/90k треугольников в collision-меше избыточны.

## Проверка

- GLB заново импортирован в Blender:
  - T. rex — 99 889 треугольников, 1,400 × 0,919 × 0,546 м, 1 UV, 2K albedo/normal/roughness;
  - Triceratops — 89 999 треугольников, 1,200 × 0,339 × 0,571 м, 1 UV, 4K base color/normal/AO.
- FBX заново импортирован: те же размеры и треугольники, текстуры встроены (packed).
- Rendered-проверка: оба скелета, пара и крупные планы черепов — в `Screenshots/`.

## Ограничения

- T. rex — скан **реплики** без оригинальной текстуры; цвет и поры — сгенерированная PBR-карта, а не фотограмметрия. Издалека читается как кость, в очень крупном плане видны однотипные поры.
- Triceratops — настоящая 4K-текстура, но скан запечатлел позу с арматурой, которой в меше нет. Без подставки скелет висит в воздухе.
- Модели не ретопологизированы: это дисплейные ассеты, не для массовых инстансов и не для скелетной анимации.
- Исходные сканы имеют неровную толщину костей и шумовые артефакты; для игры на близком плане лучше сделать retopo + запечь normal/AO.
