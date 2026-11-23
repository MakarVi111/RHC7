# RHC7 — Blender bones v2

Три стилизованные game-ready кости для прототипа. Сделаны в Blender 5.1.

| Кость | Вид | FBX | GLB | Треугольники | Длина |
|---|---|---|---|---|---|
| Бедро | теропод | `Bone_Femur.fbx` | `Bone_Femur.glb` | 24 704 | 0,300 м |
| Плечевая | цератопс | `Bone_Humerus.fbx` | `Bone_Humerus.glb` | 22 144 | 0,260 м |
| Большая берцовая | зауропод | `Bone_Tibia.fbx` | `Bone_Tibia.glb` | 25 984 | 0,340 м |

Исходник: `Blender/RHC7_Bones.blend`.  
Превью: `Screenshots/bones_sheet.png`, `Screenshots/bones_final_lineup.png`.  
Референсы и анатомия: `Геймдизайн/Кости_динозавров.md`.

## Для Unity

- Меш центрирован, длина по **X**.
- Один UV-слой `UVMap`: U — по окружности, V — по длине. Это важно для маски грязи в `BoneCleaning.shader`.
- Материал в FBX — превью `BonePreviewMat`; в игре его заменяет материал из `BoneSurface`.
- Для попадания курсора нужен `MeshCollider`.
- Импорт: Scale Factor = 1, Y-up; при необходимости Generate Colliders.

## Подключение

1. Импортировать FBX в Unity.
2. Заменить `MeshFilter.mesh` на нужный меш.
3. Обновить `MeshCollider`.
4. Проверить ссылки в `CleaningController.boneMeshes`.
5. Проверить UV: грязь должна сниматься полосой по окружности, а не пятнами.

## Ограничения

Модели стилизованные, не фотограмметрия. Суставы и торцы упрощены; на игровом расстоянии читаются нормально. Для фотореализма нужен скан Smithsonian/MorphoSource + retopo.
