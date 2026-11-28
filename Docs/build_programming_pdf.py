# -*- coding: utf-8 -*-
"""Build the RHC7 programming / architecture PDF in the artbook style."""
import base64
import io
import os
import shutil
import subprocess
from pathlib import Path

from PIL import Image
import fitz

DOCS = Path(__file__).resolve().parent
PROJECT = DOCS.parent
ROOT = PROJECT.parent
SUBMISSION = ROOT / "РХЦ-7_53842_Сдача"
SHOTS = PROJECT / "Screenshots"
ART = DOCS / "artbook_images"


def img_uri(path, max_w=1500, quality=88):
    im = Image.open(path).convert("RGB")
    if im.width > max_w:
        im = im.resize((max_w, round(im.height * max_w / im.width)), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=quality, optimize=True)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode("ascii")


VAL_CROP = DOCS / "validation_crop.png"
_val = Image.open(SHOTS / "demo_play_32_validation.png").convert("RGB")
_val.crop((20, 20, 820, 470)).save(VAL_CROP)

IMAGES = {
    "{{IMG_PREP}}": img_uri(SHOTS / "realism_prep_final.png", 1600, 87),
    "{{IMG_WIDE}}": img_uri(SHOTS / "realism_wide_final.png", 1700, 87),
    "{{IMG_BOOK}}": img_uri(SHOTS / "book_real_final2.png", 1400, 86),
    "{{IMG_VALID}}": img_uri(VAL_CROP, 1000, 88),
    "{{IMG_CRATE}}": img_uri(SHOTS / "crate_label_closeup2.png", 1400, 86),
    "{{IMG_CLEAN0}}": img_uri(ART / "p6_0.jpeg", 700, 90),
    "{{IMG_CLEAN1}}": img_uri(ART / "p6_2.jpeg", 700, 90),
    "{{IMG_CLEAN2}}": img_uri(ART / "p6_1.jpeg", 700, 90),
}

TEMPLATE = r"""<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<title>РХЦ-7 · Программирование</title>
<style>
  @page { size: A4 landscape; margin: 0; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    font-family: "Segoe UI", Arial, sans-serif;
    color: #2b1b10;
    background: #e9dfcf;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  :root {
    --paper: #f2ebdd;
    --paper2: #e9dfcd;
    --ink: #2b1b10;
    --muted: #5d4a38;
    --gold: #a9854e;
    --line: rgba(96, 70, 43, .28);
    --dark: #1c120d;
    --red: #7b2c1e;
  }
  .page {
    width: 297mm;
    height: 210mm;
    padding: 15mm 18mm 13mm;
    position: relative;
    overflow: hidden;
    page-break-after: always;
    background: var(--paper);
  }
  .page:last-child { page-break-after: auto; }
  .eyebrow {
    font-size: 7.2pt;
    letter-spacing: .22em;
    text-transform: uppercase;
    color: var(--gold);
    font-weight: 600;
    margin: 0 0 2mm;
  }
  .eyebrow::after {
    content: "";
    display: block;
    width: 28mm;
    height: .3mm;
    margin-top: 2.4mm;
    background: var(--gold);
    opacity: .7;
  }
  h1, h2, h3 { font-family: Georgia, "Times New Roman", serif; color: var(--ink); margin: 0; }
  h1 { font-size: 31pt; line-height: 1.04; letter-spacing: -.02em; margin: 0 0 2.5mm; }
  h2 { font-size: 15pt; line-height: 1.1; margin: 0 0 1.6mm; }
  h3 { font-size: 10.5pt; line-height: 1.15; margin: 0 0 1mm; }
  p { margin: 0; }
  .lead {
    font-size: 10.4pt;
    line-height: 1.42;
    color: #4b3828;
    max-width: 220mm;
  }
  .small { font-size: 7.6pt; line-height: 1.42; color: var(--muted); }
  .tiny { font-size: 6.8pt; line-height: 1.38; color: var(--muted); }
  .gold { color: var(--gold); }
  .red { color: var(--red); }
  .mono { font-family: "Cascadia Mono", Consolas, monospace; }
  .rule { height: .25mm; background: var(--line); margin: 4mm 0; }
  .folio {
    position: absolute;
    right: 18mm;
    bottom: 10.5mm;
    width: 15mm;
    height: 8.5mm;
    background: var(--dark);
    color: #f2ebdd;
    font-size: 7pt;
    display: flex;
    align-items: center;
    justify-content: center;
    letter-spacing: .08em;
  }
  .running {
    position: absolute;
    left: 18mm;
    bottom: 12mm;
    font-size: 6.4pt;
    letter-spacing: .18em;
    text-transform: uppercase;
    color: #8b6c45;
  }
  .cover { padding: 0; background: var(--dark); }
  .cover > img {
    position: absolute; inset: 0; width: 100%; height: 100%;
    object-fit: cover; opacity: .93;
  }
  .cover .shade {
    position: absolute; inset: 0;
    background: linear-gradient(90deg,
      rgba(20,12,8,.96) 0%, rgba(20,12,8,.86) 38%,
      rgba(20,12,8,.38) 70%, rgba(20,12,8,.10) 100%);
  }
  .cover .content {
    position: absolute; left: 18mm; top: 26mm; right: 95mm;
    color: #f2e7d2;
  }
  .cover .kicker {
    color: #d3ac72; font-size: 8pt; letter-spacing: .22em;
    text-transform: uppercase; font-weight: 600;
  }
  .cover .kicker::after {
    content: ""; display: block; width: 34mm; height: .35mm;
    background: #d3ac72; margin-top: 3mm; opacity: .8;
  }
  .cover h1 {
    color: #f4ead7; font-size: 92pt; line-height: .82;
    letter-spacing: -.045em; margin: 9mm 0 3mm;
  }
  .cover .sub {
    font-family: Georgia, "Times New Roman", serif;
    font-size: 22pt; line-height: 1.1; color: #f0e2c8;
    margin-bottom: 5mm;
  }
  .cover .meta {
    position: absolute; left: 18mm; bottom: 14mm;
    font-size: 8.2pt; line-height: 1.6; color: #cdbb9f;
    letter-spacing: .04em;
  }
  .cover .folio { background: #f2ebdd; color: var(--dark); }
  .diagram { width: 100%; display: block; }
  .arch { height: 79mm; margin-top: 2mm; }
  .cards3 { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6mm; margin-top: 2mm; }
  .card {
    background: #f6efe2;
    border: .3mm solid var(--line);
    padding: 3.2mm 3.5mm;
  }
  .card h3 { font-size: 10pt; }
  .card p { font-size: 7.4pt; line-height: 1.42; color: var(--muted); }
  .card .num {
    font-family: Georgia, serif; font-size: 15pt; color: var(--gold);
    float: right; line-height: 1;
  }
  .classdiagram { margin-top: 1mm; }
  .cls {
    border: .3mm solid #b18a56;
    background: #f7f0e3;
    padding: 2.4mm 3mm;
  }
  .cls .name {
    font-family: Georgia, serif; font-size: 10.4pt; font-weight: 700;
    color: #2b1b10; border-bottom: .25mm solid #cfb58e;
    padding-bottom: 1.2mm; margin-bottom: 1.2mm;
  }
  .cls .fields { font-family: "Cascadia Mono", Consolas, monospace; font-size: 6.7pt; line-height: 1.45; color: #4b3828; }
  .cls .fields b { color: #7b2c1e; font-weight: 600; }
  .branch { display: grid; grid-template-columns: 1fr 1fr; gap: 5mm; margin-top: 5mm; position: relative; }
  .branch::before {
    content: ""; position: absolute; left: 25%; right: 25%; top: -5mm; height: 5mm;
    border-left: .3mm solid #b18a56; border-right: .3mm solid #b18a56;
    border-top: .3mm solid #b18a56;
  }
  .level-top { position: relative; }
  .level-top::after {
    content: ""; position: absolute; left: 50%; bottom: -5mm; height: 5mm;
    border-left: .3mm solid #b18a56;
  }
  pre.code {
    background: #24170f;
    color: #e9dcc4;
    padding: 3mm 3.5mm;
    font-family: "Cascadia Mono", Consolas, monospace;
    font-size: 7.05pt;
    line-height: 1.42;
    white-space: pre-wrap;
    overflow: hidden;
    border-left: .8mm solid var(--gold);
  }
  pre.code .k { color: #d8b57a; }
  pre.code .s { color: #b9c79f; }
  pre.code .n { color: #d28a6a; }
  pre.code .c { color: #8d7a61; font-style: italic; }
  .caption {
    font-size: 6.6pt; letter-spacing: .13em; text-transform: uppercase;
    color: #7b5f3e; margin-top: 1.6mm;
  }
  .fig {
    border: .3mm solid #342116;
    background: #1b120c;
    overflow: hidden;
    position: relative;
  }
  .fig img { width: 100%; height: 100%; object-fit: cover; display: block; }
  .two { display: grid; grid-template-columns: 1fr 1fr; gap: 6mm; }
  .three { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 4mm; }
  .mutedbox {
    background: #ede3d2;
    border-left: .7mm solid var(--gold);
    padding: 2.6mm 3mm;
  }
  .mutedbox p { font-size: 7.5pt; line-height: 1.45; color: var(--muted); }
  .table { width: 100%; border-collapse: collapse; }
  .table th {
    font-size: 6.5pt; letter-spacing: .12em; text-transform: uppercase;
    color: #8b6c45; text-align: left; font-weight: 600;
    border-bottom: .3mm solid #bfa47c; padding: 1.2mm .8mm;
  }
  .table td {
    font-size: 7.6pt; padding: 1.5mm .8mm;
    border-bottom: .2mm solid rgba(120,90,55,.18);
    vertical-align: top;
  }
  .table td.num { font-family: Georgia, serif; font-size: 9pt; color: #7b2c1e; }
  .steps { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4mm; }
  .step { border-top: .45mm solid var(--gold); padding-top: 2mm; }
  .step .snum { font-family: Georgia, serif; font-size: 14pt; color: var(--gold); line-height: 1; }
  .step h3 { font-size: 8.6pt; margin: 1mm 0 .7mm; }
  .step p { font-size: 7.2pt; line-height: 1.4; color: var(--muted); }
  .pipeline { display: flex; align-items: stretch; gap: 0; }
  .pstep {
    flex: 1; border-top: .5mm solid #b18a56; padding: 2mm 2mm 0 0;
    font-size: 7pt; line-height: 1.35; color: var(--muted);
  }
  .pstep b { display: block; font-family: Georgia, serif; font-size: 9pt; color: var(--ink); margin-bottom: .5mm; }
  .parrow { color: #a9854e; font-size: 12pt; align-self: center; padding: 0 2mm; }
  .state-svg { height: 76mm; width: 100%; display: block; margin: 1mm 0 2mm; }
  .footer-note {
    position: absolute; left: 18mm; bottom: 22mm; right: 40mm;
    font-size: 6.6pt; color: #8b6c45; letter-spacing: .03em;
  }
  .checklist td:last-child { width: 17mm; text-align: right; color: #44633e; font-weight: 700; }
  .repo {
    background: #24170f; color: #eadcc3; padding: 3.4mm 4mm;
    border-left: .8mm solid var(--gold);
  }
  .repo .line { font-family: "Cascadia Mono", Consolas, monospace; font-size: 7.3pt; line-height: 1.55; }
  .repo .line b { color: #d8b57a; font-weight: 400; }
  .list { margin: 0; padding-left: 4mm; }
  .list li { font-size: 7.5pt; line-height: 1.45; color: var(--muted); margin-bottom: .8mm; }
  .list li b { color: var(--ink); }
</style>
</head>
<body>

<!-- 01 COVER -->
<section class="page cover">
  <img src="{{IMG_PREP}}" alt="">
  <div class="shade"></div>
  <div class="content">
    <div class="kicker">БЛОК · ПРОГРАММИРОВАНИЕ</div>
    <h1>РХЦ-7</h1>
    <div class="sub">Архитектура, данные,<br>механики</div>
    <div class="tiny" style="color:#cdbb9f;max-width:92mm;font-size:8pt;line-height:1.55;">
      Одна сцена, один цикл препарации и data-driven контент,
      который можно расширять без правки runtime-кода.
    </div>
  </div>
  <div class="meta">
    UNITY 6 · URP · C#<br>
    КОМАНДА 53842 · 2026
  </div>
  <div class="folio">01</div>
</section>

<!-- 02 ARCHITECTURE -->
<section class="page">
  <div class="eyebrow">Архитектура</div>
  <h1>Одна сцена, пять слоёв</h1>
  <p class="lead">Игра не перезагружает сцену между костями. Данные, проверка, сборка и runtime разделены: контент меняется в ассетах, а логика остаётся одной.</p>

  <svg class="diagram arch" viewBox="0 0 1000 330" xmlns="http://www.w3.org/2000/svg">
    <defs>
      <marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
        <path d="M0,0 L10,5 L0,10 z" fill="#a9854e"/>
      </marker>
      <style>
        .bt { font-family: 'Segoe UI', Arial, sans-serif; fill: #332216; }
        .bh { font-family: Georgia, 'Times New Roman', serif; font-weight: 700; fill: #2b1b10; }
      </style>
    </defs>
    <g>
      <!-- DATA -->
      <rect x="0" y="8" width="184" height="282" rx="6" fill="#eee4d4" stroke="#b18a56" stroke-width="1.3"/>
      <text class="bh" x="14" y="36" font-size="17">ДАННЫЕ</text>
      <line x1="14" y1="46" x2="170" y2="46" stroke="#cfb58e" stroke-width="1"/>
      <text class="bt" x="14" y="70" font-size="13.5">SpecimenDefinition</text>
      <text class="bt" x="14" y="94" font-size="13.5">SpeciesDefinition</text>
      <text class="bt" x="14" y="118" font-size="13.5">LevelDefinition</text>
      <text class="bt" x="14" y="142" font-size="13.5">JSON · readable copy</text>
      <text class="bt" x="14" y="166" font-size="13.5">dataVersion = 2</text>
      <text class="bt" x="14" y="200" font-size="12" fill="#7b5f3e">ScriptableObject</text>
      <text class="bt" x="14" y="216" font-size="12" fill="#7b5f3e">assets</text>

      <!-- VALIDATION -->
      <rect x="204" y="8" width="184" height="282" rx="6" fill="#e9dbc7" stroke="#b18a56" stroke-width="1.3"/>
      <text class="bh" x="218" y="36" font-size="17">ПРОВЕРКА</text>
      <line x1="218" y1="46" x2="374" y2="46" stroke="#cfb58e" stroke-width="1"/>
      <text class="bt" x="218" y="70" font-size="13.5">LevelValidator</text>
      <text class="bt" x="218" y="94" font-size="13.5">LevelValidationReport</text>
      <text class="bt" x="218" y="118" font-size="13.5">validSpecimens</text>
      <text class="bt" x="218" y="142" font-size="13.5">errors</text>
      <text class="bt" x="218" y="166" font-size="13.5">report / safe subset</text>
      <text class="bt" x="218" y="200" font-size="12" fill="#7b5f3e">до спавна</text>
      <text class="bt" x="218" y="216" font-size="12" fill="#7b5f3e">до загрузки сцены</text>

      <!-- BUILD -->
      <rect x="408" y="8" width="184" height="282" rx="6" fill="#e3d2ba" stroke="#b18a56" stroke-width="1.3"/>
      <text class="bh" x="422" y="36" font-size="17">СБОРКА</text>
      <line x1="422" y1="46" x2="578" y2="46" stroke="#cfb58e" stroke-width="1"/>
      <text class="bt" x="422" y="70" font-size="13.5">LevelDataBuilder</text>
      <text class="bt" x="422" y="94" font-size="13.5">PrepGameBuilder</text>
      <text class="bt" x="422" y="118" font-size="13.5">procedural textures</text>
      <text class="bt" x="422" y="142" font-size="13.5">post-processing</text>
      <text class="bt" x="422" y="166" font-size="13.5">archival photo plates</text>
      <text class="bt" x="422" y="200" font-size="12" fill="#7b5f3e">editor-time</text>
      <text class="bt" x="422" y="216" font-size="12" fill="#7b5f3e">menu rebuild</text>

      <!-- SCENE -->
      <rect x="612" y="8" width="184" height="282" rx="6" fill="#dcc9ad" stroke="#b18a56" stroke-width="1.3"/>
      <text class="bh" x="626" y="36" font-size="17">СЦЕНЫ</text>
      <line x1="626" y1="46" x2="782" y2="46" stroke="#cfb58e" stroke-width="1"/>
      <text class="bt" x="626" y="70" font-size="13.5">RHC7_PrepGame</text>
      <text class="bt" x="626" y="94" font-size="13.5">RHC7_DataDemo</text>
      <text class="bt" x="626" y="118" font-size="13.5">RHC7_ConfigValidation</text>
      <text class="bt" x="626" y="142" font-size="13.5">RHC7_InvalidDataGame</text>
      <text class="bt" x="626" y="166" font-size="13.5">Build Settings · main</text>
      <text class="bt" x="626" y="200" font-size="12" fill="#7b5f3e">3 / 4 / report / safe</text>

      <!-- RUNTIME -->
      <rect x="816" y="8" width="184" height="282" rx="6" fill="#d5c0a2" stroke="#b18a56" stroke-width="1.3"/>
      <text class="bh" x="830" y="36" font-size="17">RUNTIME</text>
      <line x1="830" y1="46" x2="986" y2="46" stroke="#cfb58e" stroke-width="1"/>
      <text class="bt" x="830" y="70" font-size="13.5">GameFlow</text>
      <text class="bt" x="830" y="94" font-size="13.5">BoneCleaningV2</text>
      <text class="bt" x="830" y="118" font-size="13.5">ToolShelfInteraction</text>
      <text class="bt" x="830" y="142" font-size="13.5">PrepNoteUI</text>
      <text class="bt" x="830" y="166" font-size="13.5">SpecimenTag</text>
      <text class="bt" x="830" y="200" font-size="12" fill="#7b5f3e">один цикл</text>
      <text class="bt" x="830" y="216" font-size="12" fill="#7b5f3e">без SceneManager</text>

      <!-- arrows -->
      <line x1="184" y1="150" x2="202" y2="150" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr)"/>
      <line x1="388" y1="150" x2="406" y2="150" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr)"/>
      <line x1="592" y1="150" x2="610" y2="150" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr)"/>
      <line x1="796" y1="150" x2="814" y2="150" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr)"/>
      <text class="bt" x="0" y="320" font-size="12.5" fill="#7b5f3e">изменение ассета → проверка → пересборка сцены → тот же runtime и тот же цикл</text>
    </g>
  </svg>

  <div class="cards3">
    <div class="card"><span class="num">01</span><h3>Данные</h3><p>ScriptableObject описывает кость, вид и смену. JSON — читаемая копия для ревью, а не runtime-источник.</p></div>
    <div class="card"><span class="num">02</span><h3>Проверка</h3><p>LevelValidator собирает отчёт до спавна: битая запись не мешает остальным и не ломает смену.</p></div>
    <div class="card"><span class="num">03</span><h3>Сборка</h3><p>PrepGameBuilder создаёт сцену из LevelDefinition. Количество слотов и страниц книги берётся из списков.</p></div>
  </div>

  <div style="margin-top:4.5mm;">
    <pre class="code" style="font-size:6.7pt;">var report = LevelValidator.Validate(level);
int slotCount = Mathf.Max(1, report.validCount);
foreach (var d in report.validSpecimens)
    SpawnSpecimen(d, crateRoot, boneMat);</pre>
    <div class="caption">PrepGameBuilder.BuildLevel · в сцену попадают только валидные записи</div>
  </div>
  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">02</div>
</section>

<!-- 03 DATA -->
<section class="page">
  <div class="eyebrow">Данные</div>
  <h1>Уровень — это ассет</h1>
  <p class="lead">Три типа ScriptableObject описывают смену. JSON повторяет структуру для сдачи и сравнения исходного и изменённого набора.</p>

  <div class="two" style="margin-top:6mm;grid-template-columns:1.06fr .94fr;gap:8mm;">
    <div class="classdiagram">
      <div class="cls level-top">
        <div class="name">LevelDefinition</div>
        <div class="fields">
          levelName · <b>dataVersion</b> · cleanThreshold<br>
          <b>List&lt;SpecimenDefinition&gt;</b> specimens<br>
          <b>List&lt;SpeciesDefinition&gt;</b> species
        </div>
      </div>
      <div class="branch">
        <div class="cls">
          <div class="name">SpecimenDefinition</div>
          <div class="fields">
            id · partName · identificationHint<br>
            species · isSkull<br>
            bonePrefab <b>или</b> completeMesh<br>
            boneMaterial<br>
            cratePosition · crateEuler<br>
            prepEuler · visualScale<br>
            <b>tagText</b>
          </div>
        </div>
        <div class="cls">
          <div class="name">SpeciesDefinition</div>
          <div class="fields">
            id · displayName · speciesValue<br>
            pageEyebrow · pageTitle<br>
            description<br>
            <b>mainArt</b> · extraArt
          </div>
        </div>
      </div>
      <div class="mutedbox" style="margin-top:5mm;">
        <p><b>dataVersion = 2.</b> Если ассеты актуальны, пересборка не перезаписывает правки дизайнера. Меняются только JSON-копии.</p>
      </div>
    </div>

    <div>
      <pre class="code"><span class="c">// Assets/Data/JSON/DemoLevel.json · фрагмент</span>
{
  <span class="k">"levelName"</span>: <span class="s">"РХЦ-7 · демо-расширение"</span>,
  <span class="k">"cleanThreshold"</span>: <span class="n">0.6</span>,
  <span class="k">"specimens"</span>: [
    {
      <span class="k">"id"</span>: <span class="s">"trex_pelvis"</span>,
      <span class="k">"partName"</span>: <span class="s">"Таз"</span>,
      <span class="k">"species"</span>: <span class="s">"Trex"</span>,
      <span class="k">"prefab"</span>: <span class="s">"Assets/Models/Bones_Real/Trex_Pelvis.fbx"</span>,
      <span class="k">"visualScale"</span>: <span class="n">1.6</span>,
      <span class="k">"tagText"</span>: <span class="s">"ТИРАННОЗАВР\nТАЗ"</span>
    }
  ],
  <span class="k">"species"</span>: [
    { <span class="k">"id"</span>: <span class="s">"trex"</span> },
    { <span class="k">"id"</span>: <span class="s">"triceratops"</span> }
  ]
}</pre>
      <div class="caption" style="margin-top:2mm;">Демо-смена · 4 образца · 4 слота · 2 вида</div>
    </div>
  </div>

  <div class="rule" style="margin:4mm 0 3mm;"></div>
  <div class="two" style="grid-template-columns:1.32fr .68fr;gap:8mm;">
    <div>
      <h2 style="font-size:11pt;">Добавить кость без кода</h2>
      <div class="steps" style="margin-top:2.5mm;">
        <div class="step"><div class="snum">01</div><h3>Ассет</h3><p>Создать или скопировать SpecimenDefinition в Assets/Data/Specimens.</p></div>
        <div class="step"><div class="snum">02</div><h3>Поля</h3><p>Заполнить id, часть, вид, меш/префаб, позицию и tagText.</p></div>
        <div class="step"><div class="snum">03</div><h3>Список</h3><p>Добавить ассет в DemoLevel.specimens.</p></div>
        <div class="step"><div class="snum">04</div><h3>Сборка</h3><p>Tools → RHC7 → Build Data Demo (4 bones).</p></div>
      </div>
    </div>
    <table class="table">
      <tr><th>Уровень</th><th>Образцы</th><th>Слоты</th><th>Виды</th></tr>
      <tr><td>BaseLevel</td><td class="num">3</td><td class="num">3</td><td class="num">2</td></tr>
      <tr><td>DemoLevel</td><td class="num">4</td><td class="num">4</td><td class="num">2</td></tr>
      <tr><td>InvalidLevel</td><td class="num">4</td><td class="num">1</td><td class="num">1</td></tr>
    </table>
  </div>
  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">03</div>
</section>

<!-- 04 CLEANING -->
<section class="page">
  <div class="eyebrow">Механика</div>
  <h1>Грязь живёт в вершинах</h1>
  <p class="lead">Слои породы хранятся в vertex colors меша: R — пыль, G — глина, B — матрица. Кисть не рисует текстуру, а вычитает вес из вершин под лучом.</p>

  <div class="two" style="margin-top:5mm;grid-template-columns:1.46fr .54fr;gap:6mm;align-items:start;">
    <div class="three">
      <figure style="margin:0;"><div class="fig" style="height:40mm;"><img src="{{IMG_CLEAN0}}" alt=""></div><div class="caption">До · пыль и глина</div></figure>
      <figure style="margin:0;"><div class="fig" style="height:40mm;"><img src="{{IMG_CLEAN1}}" alt=""></div><div class="caption">Скребок · слой снят</div></figure>
      <figure style="margin:0;"><div class="fig" style="height:40mm;"><img src="{{IMG_CLEAN2}}" alt=""></div><div class="caption">После · доступна книга</div></figure>
    </div>
    <div>
      <table class="table">
        <tr><th>Инструмент</th><th>Пыль</th><th>Глина</th><th>Матрица</th></tr>
        <tr><td>Кисть</td><td class="num">1.70</td><td>—</td><td>—</td></tr>
        <tr><td>Скребок</td><td class="num">1.10</td><td class="num">1.15</td><td>—</td></tr>
        <tr><td>Зубило + молоток</td><td class="num">0.85</td><td>—</td><td class="num">0.75</td></tr>
      </table>
      <div class="tiny" style="margin-top:2mm;">Коэффициенты вычитания на один шаг кисти. Пыль опциональна: структурные слои важнее.</div>
    </div>
  </div>

  <div class="two" style="margin-top:5mm;grid-template-columns:1.02fr .98fr;gap:8mm;">
    <div>
      <p class="small" style="color:var(--ink);font-size:8.2pt;line-height:1.5;">
        Луч камеры проходит через <b>BVH по треугольникам</b> меша, поэтому попадание точное даже без отдельного коллайдера. Затем фильтр: радиус 0.05 м, нормаль к камере ≥ 0.1, плавное затухание к краю кисти.
      </p>
      <div class="pipeline" style="margin-top:3mm;">
        <div class="pstep"><b>Луч</b>экран → мир</div><div class="parrow">›</div>
        <div class="pstep"><b>BVH</b>треугольник</div><div class="parrow">›</div>
        <div class="pstep"><b>Вершины</b>радиус + нормаль</div><div class="parrow">›</div>
        <div class="pstep"><b>Веса</b>RGB − tool rate</div><div class="parrow">›</div>
        <div class="pstep"><b>Итог</b>60% → книга</div>
      </div>
    </div>
    <pre class="code"><span class="c">// BoneCleaningV2.CleanAtPoint, фрагмент</span>
for (int i = 0; i &lt; vertices.Length; i++) {
  float d2 = (vertices[i] - local).sqrMagnitude;
  if (d2 &gt; r2) continue;
  if (Vector3.Dot(normals[i], viewLocal) &lt; 0.10f) continue;
  float falloff = 1f - Mathf.Sqrt(d2) * invR;
  int dec = Mathf.RoundToInt(amount * falloff * 255f);
  Color32 c = weights[i];
  c.g = (byte)Mathf.Max(0, c.g - Mathf.RoundToInt(dec * clayRate));
  weights[i] = c;
}
runtimeMesh.colors32 = weights;
runtimeMesh.UploadMeshData(false);</pre>
  </div>

  <div class="cards3" style="margin-top:4mm;">
    <div class="card"><h3>Точность</h3><p>BVH по треугольникам, а не грубый коллайдер: кисть попадает в ту же точку, которую видит камера.</p></div>
    <div class="card"><h3>Честный прогресс</h3><p>Процент считается по реально оставшимся весам вершин, а не по времени удержания кнопки.</p></div>
    <div class="card"><h3>Дешёвый слой</h3><p>Цвет породы живёт в vertex colors — без отдельных текстур, материалов и объектов на каждую кость.</p></div>
  </div>

  <div class="footer-note">Прогресс = 1 − остаток / начальный вес. При 60% открывается книга; слой может автоматически очиститься, когда остаток ниже порога.</div>
  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">04</div>
</section>

<!-- 05 STATE MACHINE -->
<section class="page">
  <div class="eyebrow">Состояния</div>
  <h1>Один switch, десять состояний</h1>
  <p class="lead">GameFlow — единственный владелец смены. Камера, крышка ящика, полёт кости и книга реагируют на состояние, а не на флаги в разных компонентах.</p>

  <svg class="diagram state-svg" viewBox="0 0 1000 300" xmlns="http://www.w3.org/2000/svg">
    <defs>
      <marker id="arr2" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
        <path d="M0,0 L10,5 L0,10 z" fill="#a9854e"/>
      </marker>
      <style>
        .st { font-family: 'Segoe UI', Arial, sans-serif; font-size: 14.5px; fill: #2b1b10; font-weight: 600; }
        .sl { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11.5px; fill: #7b5f3e; }
      </style>
    </defs>
    <g>
      <!-- row 1 -->
      <g>
        <rect x="0" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="56" y="55" text-anchor="middle">Closed</text>
        <rect x="124" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="180" y="55" text-anchor="middle">Opening</text>
        <rect x="248" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="304" y="55" text-anchor="middle">Inspecting</text>
        <rect x="372" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="428" y="55" text-anchor="middle">Taking</text>
        <rect x="496" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="552" y="55" text-anchor="middle">Cleaning</text>
        <rect x="620" y="28" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
        <text class="st" x="676" y="55" text-anchor="middle">BookReady</text>
        <rect x="744" y="28" width="112" height="44" rx="5" fill="#f2e3d1" stroke="#b18a56"/>
        <text class="st" x="800" y="55" text-anchor="middle">Identifying</text>
        <rect x="868" y="28" width="112" height="44" rx="5" fill="#f2e3d1" stroke="#b18a56"/>
        <text class="st" x="924" y="55" text-anchor="middle">Identified</text>
      </g>
      <g stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr2)">
        <line x1="112" y1="50" x2="122" y2="50"/>
        <line x1="236" y1="50" x2="246" y2="50"/>
        <line x1="360" y1="50" x2="370" y2="50"/>
        <line x1="484" y1="50" x2="494" y2="50"/>
        <line x1="608" y1="50" x2="618" y2="50"/>
        <line x1="732" y1="50" x2="742" y2="50"/>
        <line x1="856" y1="50" x2="866" y2="50"/>
      </g>
      <!-- returning / complete -->
      <rect x="744" y="170" width="112" height="44" rx="5" fill="#f4ecdf" stroke="#b18a56"/>
      <text class="st" x="800" y="197" text-anchor="middle">Returning</text>
      <rect x="868" y="170" width="112" height="44" rx="5" fill="#e8d6bd" stroke="#7b2c1e"/>
      <text class="st" x="924" y="197" text-anchor="middle">Complete</text>
      <path d="M924,72 L924,168" fill="none" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr2)"/>
      <text class="sl" x="934" y="124">все кости</text>
      <path d="M890,72 C890,120 840,110 820,168" fill="none" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr2)"/>
      <text class="sl" x="842" y="144">иначе</text>
      <path d="M744,192 L304,192 L304,74" fill="none" stroke="#a9854e" stroke-width="1.6" marker-end="url(#arr2)"/>
      <text class="sl" x="420" y="185">камера вернулась → следующая кость</text>
      <!-- wrong loop -->
      <path d="M800,72 C800,104 766,104 766,74" fill="none" stroke="#7b2c1e" stroke-width="1.4" stroke-dasharray="4 3" marker-end="url(#arr2)"/>
      <text class="sl" x="806" y="104" fill="#7b2c1e">неверно</text>
      <text class="sl" x="0" y="252">enum State · GameFlow.cs · один владелец переходов</text>
      <text class="sl" x="0" y="272">Клик обрабатывается только в текущем состоянии; камера и анимации не меняют state сами.</text>
    </g>
  </svg>

  <div class="two" style="grid-template-columns:1.02fr .98fr;gap:8mm;margin-top:2mm;">
    <pre class="code"><span class="c">// GameFlow.Update, фрагмент</span>
switch (state) {
  case State.Closed:
    UpdateHover();
    if (hoverCrate &amp;&amp; Input.GetMouseButtonDown(0))
      BeginOpening();
    break;
  case State.Cleaning:
    if (cleaning.TotalCleanFraction &gt;= cleanThreshold)
      BeginBookReady();
    break;
  case State.BookReady:
    if (Input.GetMouseButtonDown(0) &amp;&amp; BookClicked())
      BeginIdentifying();
    break;
}</pre>
    <div>
      <p class="small" style="color:var(--ink);font-size:8.2pt;line-height:1.5;">
        <b>Почему state machine, а не набор флагов:</b> камера, крышка, полёт и книга не спорят за владельца. Переход разрешён только из текущего состояния, а ошибка идентификации остаётся внутри <span class="mono">Identifying</span> и показывает подсказку.
      </p>
      <div class="mutedbox" style="margin-top:3mm;">
        <p><b>Возврат.</b> После верной бирки кость летит в слот коллекции, камера уходит на готовый ящик, затем <span class="mono">Returning → Inspecting</span> открывает следующую кость. Отдельной загрузки сцены нет.</p>
      </div>
    </div>
  </div>
  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">05</div>
</section>

<!-- 06 BOOK + VALIDATION -->
<section class="page">
  <div class="eyebrow">Книга и проверка</div>
  <h1>Данные проверяются до спавна</h1>
  <p class="lead">Книга строится из SpeciesDefinition. LevelValidator собирает отчёт и отдаёт только валидные записи: ошибка в конфиге не мешает остальным костям.</p>

  <div class="two" style="margin-top:5mm;gap:6mm;">
    <figure style="margin:0;">
      <div class="fig" style="height:63mm;"><img src="{{IMG_BOOK}}" alt=""></div>
      <div class="caption">Книга · страницы из SpeciesDefinition и архивных фото</div>
    </figure>
    <figure style="margin:0;">
      <div class="fig" style="height:63mm;"><img src="{{IMG_VALID}}" alt=""></div>
      <div class="caption">InvalidLevel · 1 из 4 записей валидна, причины в отчёте</div>
    </figure>
  </div>

  <div class="two" style="margin-top:4mm;grid-template-columns:1.05fr .95fr;gap:8mm;">
    <div>
      <h2 style="font-size:10.5pt;">Что проверяет валидатор</h2>
      <ul class="list" style="margin-top:1.5mm;">
        <li>пустой <b>id</b> и повторяющийся <b>id</b> образца;</li>
        <li>пустая часть скелета, отсутствие меша/префаба;</li>
        <li>неизвестный вид у образца и дубликаты видов;</li>
        <li>пустая бирка, отсутствие описания или картинки книги.</li>
      </ul>
      <p class="tiny" style="margin-top:1.5mm;">Из отчёта берутся <span class="mono">validSpecimens</span>; число слотов = <span class="mono">max(1, validCount)</span>. Поэтому безопасная сцена запускается даже с одной валидной записью.</p>
    </div>
    <pre class="code"><span class="c">// LevelValidator.Validate, фрагмент</span>
foreach (var d in level.specimens) {
  if (d == null) continue;
  bool valid = true;
  if (string.IsNullOrWhiteSpace(d.id)) valid = false;
  if (d.bonePrefab == null &amp;&amp; d.completeMesh == null) valid = false;
  if (!speciesValues.Contains(d.species)) valid = false;
  if (string.IsNullOrWhiteSpace(d.tagText)) valid = false;
  if (valid) {
    report.validSpecimens.Add(d);
    report.validCount++;
  }
}
report.ok = report.errors.Count == 0 &amp;&amp; report.validCount &gt; 0;</pre>
  </div>

  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">06</div>
</section>

<!-- 07 BUILD -->
<section class="page">
  <div class="eyebrow">Сборка</div>
  <h1>Комната собирается кодом</h1>
  <p class="lead">PrepGameBuilder создаёт сцену из LevelDefinition: архитектуру, ящики, инструменты, книгу, камеру и GameFlow. Количество слотов и страниц книги берётся из данных.</p>

  <div class="two" style="margin-top:5mm;grid-template-columns:1.42fr .58fr;gap:7mm;">
    <figure style="margin:0;">
      <div class="fig" style="height:92mm;"><img src="{{IMG_WIDE}}" alt=""></div>
      <div class="caption">Финальная сцена · 16,4 × 11,8 м · 3 кости основной смены</div>
    </figure>
    <div>
      <h2 style="font-size:10.8pt;">Что генерируется</h2>
      <ul class="list" style="margin-top:1.5mm;">
        <li>комната, окна, свет, постаменты и декор;</li>
        <li>ящики, слоты, кости и бумажные бирки;</li>
        <li>инструменты, книга, очистка и камеры;</li>
        <li>GameFlow, памятка и постобработка.</li>
      </ul>
      <pre class="code" style="margin-top:3mm;font-size:6.8pt;"><span class="c">// динамические слоты из данных</span>
int slotCount = Mathf.Max(1, report.validCount);
BuildFinishedCrate(..., slotCount, out slots);
foreach (var d in report.validSpecimens)
  SpawnSpecimen(d, crateRoot, boneMat);</pre>
    </div>
  </div>

  <div class="cards3" style="margin-top:3mm;">
    <div class="card"><span class="num">01</span><h3>Процедурные детали</h3><p>Паркет, штукатурка, пробка, pegboard, небо, доски ящиков и мягкая тень — генерируются в редакторе.</p></div>
    <div class="card"><span class="num">02</span><h3>Презентация</h3><p>ACES, bloom, виньетка, зерно, пыль, реальные фото-пластины и контактные тени под мебелью.</p></div>
    <div class="card"><span class="num">03</span><h3>Границы</h3><p>Сцена статична и пересобирается из меню. Загрузки уровней и сохранений нет: смена живёт один сеанс.</p></div>
  </div>
  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">07</div>
</section>

<!-- 08 VERIFICATION -->
<section class="page">
  <div class="eyebrow">Сдача</div>
  <h1>Что проверено</h1>
  <p class="lead">Основная смена, демо данных и ошибочный конфиг собираются из одного кода. Ниже — фактическое состояние проекта.</p>

  <div class="two" style="margin-top:6mm;grid-template-columns:1.06fr .94fr;gap:8mm;">
    <table class="table checklist">
      <tr><th>Проверка</th><th>Результат</th></tr>
      <tr><td>RHC7_PrepGame · 3 кости / 3 слота</td><td>PASS</td></tr>
      <tr><td>RHC7_DataDemo · 4 кости / 4 слота</td><td>PASS</td></tr>
      <tr><td>RHC7_ConfigValidation · 1 из 4 валидна</td><td>PASS</td></tr>
      <tr><td>RHC7_InvalidDataGame · безопасное подмножество</td><td>PASS</td></tr>
      <tr><td>Build Settings · только основная сцена</td><td>PASS</td></tr>
      <tr><td>Консоль · 0 ошибок</td><td>PASS</td></tr>
      <tr><td>Книга · 2 вида, реальные фото-пластины</td><td>PASS</td></tr>
    </table>
    <div class="repo">
      <div class="line"><b>github.com/MakarVi111/RHC7</b></div>
      <div class="line">ветка: <b>prep-game-final</b></div>
      <div class="line">Unity: <b>6000.0.76f1</b> · URP</div>
      <div class="line">сцена: <b>Assets/Scenes/RHC7_PrepGame.unity</b></div>
      <div class="line">запуск: открыть проект → Play</div>
    </div>
  </div>

  <div class="two" style="margin-top:4mm;grid-template-columns:1fr 1fr;gap:8mm;">
    <div>
      <h2 style="font-size:10.5pt;">Ограничения</h2>
      <ul class="list" style="margin-top:1.5mm;">
        <li>Книга свёрстана под два вида; третий вид потребует новых страниц.</li>
        <li>Очистка перебирает вершины меша на шаг кисти — нормально для 3–4 образцов, не для толпы.</li>
        <li>Нет сохранения и загрузки: смена проходит за один сеанс.</li>
        <li>Автотестов нет; валидатор и сцены проверки — ручной QA-контур.</li>
      </ul>
    </div>
    <div>
      <h2 style="font-size:10.5pt;">Артефакты</h2>
      <ul class="list" style="margin-top:1.5mm;">
        <li><b>Архитектура.pdf</b> / <b>РХЦ-7_53842_Программирование.pdf</b> — этот блок.</li>
        <li><b>RHC7_PrepGame_Source_2026-11-28.zip</b> — исходники без Library/Temp.</li>
        <li><b>RHC7_Assets_All_2026-11-28.unitypackage</b> — все ассеты.</li>
        <li>Реальные фото: <b>Assets/Textures/Book/CREDITS.md</b>.</li>
      </ul>
      <p class="tiny" style="margin-top:2mm;">Команда 53842 · РХЦ-7 · 2026</p>
    </div>
  </div>

  <div class="two" style="margin-top:4mm;grid-template-columns:1.08fr .92fr;gap:7mm;align-items:center;">
    <figure style="margin:0;">
      <div class="fig" style="height:39mm;background:#1b120c;"><img src="{{IMG_CRATE}}" style="object-fit:contain;" alt=""></div>
      <div class="caption">Бирка из tagText · вид и часть скелета</div>
    </figure>
    <div class="mutedbox">
      <p><b>Проверка вживую.</b> Открыть ящик, взять кость, очистить до 60%, открыть книгу и выбрать вид. После верной бирки кость уходит в слот коллекции. Повторить для трёх костей основной смены.</p>
    </div>
  </div>

  <div class="running">РХЦ-7 · ПРОГРАММИРОВАНИЕ</div><div class="folio">08</div>
</section>

</body>
</html>
"""


def build_html() -> Path:
    html = TEMPLATE
    for token, uri in IMAGES.items():
        html = html.replace(token, uri)
    out = DOCS / "programming_block.html"
    out.write_text(html, encoding="utf-8")
    return out


def find_chrome():
    for path in [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    ]:
        if Path(path).exists():
            return path
    raise RuntimeError("Chrome/Edge not found")


def print_pdf(html_path: Path, pdf_path: Path):
    chrome = find_chrome()
    profile = Path(os.environ.get("TEMP", str(DOCS))) / "rhc7_pdf_profile"
    profile.mkdir(parents=True, exist_ok=True)
    cmd = [
        chrome,
        "--headless=new",
        "--disable-gpu",
        "--no-sandbox",
        "--disable-dev-shm-usage",
        f"--user-data-dir={profile}",
        "--no-pdf-header-footer",
        "--print-to-pdf-no-header",
        f"--print-to-pdf={pdf_path}",
        "--virtual-time-budget=10000",
        html_path.as_uri(),
    ]
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=180)
    if not pdf_path.exists():
        raise RuntimeError(
            "PDF not created\nSTDOUT:\n" + result.stdout + "\nSTDERR:\n" + result.stderr
        )


def write_repo_file():
    text = """РХЦ-7 · Музейная препарация
Команда: 53842
Репозиторий: https://github.com/MakarVi111/RHC7
Ветка: prep-game-final
Кодовая база: f506d26 (ветка prep-game-final)
Unity: 6000.0.76f1 (Unity 6.0.76f1), URP
Основная сцена: Assets/Scenes/RHC7_PrepGame.unity
Демо данных: Assets/Scenes/RHC7_DataDemo.unity
Отчёт валидации: Assets/Scenes/RHC7_ConfigValidation.unity
Безопасное подмножество: Assets/Scenes/RHC7_InvalidDataGame.unity
Запуск: открыть RHC7 через Unity Hub, открыть основную сцену, Play.
Сборка: Build Settings содержит только основную сцену.
Архив исходников: RHC7_PrepGame_Source_2026-11-28.zip
Пакет ассетов: RHC7_Assets_All_2026-11-28.unitypackage
Документация: Архитектура.pdf
Блок программирования: РХЦ-7_53842_Программирование.pdf
"""
    (ROOT / "Репозиторий.txt").write_text(text, encoding="utf-8")
    (DOCS / "Репозиторий.txt").write_text(text, encoding="utf-8")
    SUBMISSION.mkdir(exist_ok=True)
    (SUBMISSION / "Репозиторий.txt").write_text(text, encoding="utf-8")


def write_submission_manifest():
    SUBMISSION.mkdir(exist_ok=True)
    text = """РХЦ-7 · Музейная препарация
Команда 53842 · 2026

Состав папки:
- РХЦ-7_53842_Артбук.pdf — артбук команды.
- РХЦ-7_53842_Программирование.pdf — блок «Программирование».
- Архитектура.pdf — формальная копия блока программирования.
- Репозиторий.txt — ссылка, ветка, сцены и запуск.

Репозиторий: https://github.com/MakarVi111/RHC7
Ветка: prep-game-final
Unity: 6000.0.76f1, URP
"""
    (SUBMISSION / "README.txt").write_text(text, encoding="utf-8")


def main():
    html_path = build_html()
    pdf_program = DOCS / "РХЦ-7_53842_Программирование.pdf"
    print_pdf(html_path, pdf_program)
    shutil.copyfile(pdf_program, DOCS / "Архитектура.pdf")
    shutil.copyfile(pdf_program, ROOT / "РХЦ-7_53842_Программирование.pdf")
    shutil.copyfile(pdf_program, ROOT / "Архитектура.pdf")
    SUBMISSION.mkdir(exist_ok=True)
    shutil.copyfile(pdf_program, SUBMISSION / "РХЦ-7_53842_Программирование.pdf")
    shutil.copyfile(pdf_program, SUBMISSION / "Архитектура.pdf")
    write_repo_file()
    write_submission_manifest()

    doc = fitz.open(pdf_program)
    print("PDF pages:", doc.page_count, "size MB:", round(pdf_program.stat().st_size / 1048576, 2))
    out_dir = DOCS / "programming_pages"
    out_dir.mkdir(exist_ok=True)
    for i, page in enumerate(doc):
        pix = page.get_pixmap(matrix=fitz.Matrix(1.4, 1.4), alpha=False)
        pix.save(out_dir / f"page_{i+1}.png")
    print("rendered pages to", out_dir)


if __name__ == "__main__":
    main()
