# RHC7 — Blender bones v2 (PBR)

Три детализированные стилизованные кости. Сделаны в Blender 5.1, экспортированы в FBX и GLB с PBR-текстурами.

| Кость | Вид | FBX | GLB | Треугольники | Длина |
|---|---|---|---|---|---|
| Бедро | теропод | `Bone_Femur.fbx` | `Bone_Femur.glb` | 24 704 | 0,302 м |
| Плечевая | цератопс | `Bone_Humerus.fbx` | `Bone_Humerus.glb` | 22 144 | 0,263 м |
| Большая берцовая | зауропод | `Bone_Tibia.fbx` | `Bone_Tibia.glb` | 25 984 | 0,344 м |

Исходник: `Blender/RHC7_Bones.blend`.  
Превью: `Screenshots/bones_textured_sheet.png`, `Screenshots/bones_textured_lineup.png`.  
Референсы: `Геймдизайн/Кости_динозавров.md`.

## Текстуры

В `Textures/` для каждой кости:

- `T_<Bone>_Albedo.png` — цвет, sRGB;
- `T_<Bone>_Normal.png` — normal map, Non-Color;
- `T_<Bone>_Roughness.png` — roughness, Non-Color;
- `T_<Bone>_AO.png` — ambient occlusion, Non-Color.

Разрешение 1024×1024. UV — цилиндрическая: U по окружности, V по длине. Это нужно для маски грязи в `BoneCleaning.shader`.

## Unity

- Импорт FBX: Scale Factor = 1, Y-up.
- На объекте кости нужен `MeshCollider`.
- `MeshFilter.mesh` заменить на нужный меш.
- Материал: сейчас в игре используется `RHC7/Bone Cleaning`, который рисует кость процедурно. Чтобы включить PBR-текстуры, нужно добавить в шейдер сэмплы `_BoneAlbedo`, `_BoneNormal`, `_BoneRoughness`, `_BoneAO` и назначить материалы на кости. Это следующий шаг интеграции.
- Если использовать GLB/URP Lit без кастомного шейдера, текстуры уже встроены в GLB.

## Важно

Модели стилизованные, не фотограмметрия. Анатомия читаемая, но упрощённая. Для научной точности нужен скан Smithsonian/MorphoSource + retopo.
