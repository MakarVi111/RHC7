# Импорт текстур костей

Все карты 1024×1024, UV — цилиндрическая развёртка (U по окружности, V по длине).

## Настройки в Unity

| Файл | Texture Type | sRGB | Прочее |
|---|---|---|---|
| `T_*_Albedo.png` | Default | **Да** | без alpha |
| `T_*_Normal.png` | Normal map | **Нет** | Import as Normal Map, Create from Grayscale = off |
| `T_*_Roughness.png` | Default | **Нет** | Use in material roughness |
| `T_*_AO.png` | Default | **Нет** | multiplication в base color / occlusion |

## Если используете URP Lit

Создать материал:
- Base Map → Albedo;
- Normal Map → Normal, strength 1;
- Metallic = 0;
- Smoothness = 1 − Roughness (или использовать Custom shader);
- Occlusion → AO.

## Если используете RHC7/Bone Cleaning

Сейчас шейдер рисует кость процедурно и не читает эти карты. Чтобы текстуры появились в игре, нужно добавить в шейдер сэмплы и свойства `_BoneAlbedo`, `_BoneNormal`, `_BoneRoughness`, `_BoneAO` и назначить материалы на три кости. Без этого текстур в игре не будет.
