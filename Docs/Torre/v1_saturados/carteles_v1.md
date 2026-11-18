# Catálogo Completo de Carteles de la Torre-Tutorial (`FloatingNote`)

> **Reglas aplicadas (problemas #3, #4 y #6 reportados por Tamara):**
> 1. **Máximo 2 líneas por cartel** y **máximo 38 caracteres visibles por línea** (sin contar etiquetas `<b>...</b>`), para que jamás desborden su tarjeta.
> 2. **Tamaño de tarjeta a medida** (`ancho × alto` en metros) con margen interior de `0,08 m` (`rectTransform.sizeDelta = (width - 0.16f, height - 0.12f)`), `TextWrappingModes.Normal` siempre activo y `fontSizeMax = 0,72f`.
> 3. **Ubicación ANTES del obstáculo o estación**, sobre la línea natural de avance del jugador, a altura de lectura cómoda (`Δy = 1,65–1,95 m` sobre el nivel del piso) y con la cara legible (`-Z` local) orientada directamente hacia el punto (`viewer`) desde donde el jugador se aproxima.
> 4. **Terminología consistente:** ningún cartel llama «rayo azul» al hechizo; se distingue claramente el **hechizo de viento** de la **guía celeste de apunte** del lienzo.

---

## 1. Tabla maestra de todos los carteles (`#1` a `#14`)

> **Convención de coordenadas (`TowerBuilder.Polar(r, ang, y)`):**
> - `0°` = Este (`+X`), `90°` = Norte (`+Z`), `180°` = Oeste (`-X`), `270°` = Sur (`-Z`, puerta).
> - `Δy` = altura del centro de la tarjeta sobre su piso; `y abs.` = altura respecto al origen de la torre (`y = 0`).
> - **Caracteres visibles por línea:** contados sin los tags `<b>` y `</b>`.

| # | Piso | Nombre en código | Texto exacto en español (2 líneas máx., teclas en `<b>`) | Largo máx. (car./línea) | Tamaño tarjeta (`ancho × alto`) | Posición (`ang°`, `r`, `Δy` / `y abs.`) | Hacia dónde mira (punto `viewer` / rumbo) | Ubicación respecto a lo que explica |
|---|---|---|---|---|---|---|---|---|
| **1** | **Terraza** (`y = 13,5 m`) | `Note_Tutorial_Skip` | `<b>E</b> · Saltear tutorial`<br>`Ir directo al Nexo` | 20 / 18 | `1,15 × 0,44 m` | `100°`, `r = 7,40 m`<br>`Δy = 1,85 m` (`y = 15,35 m`) | Mira al Spawn `(90°, r = 6,8 m)`<br>(hacia el Este, rumbo `15°`) | `0,25 m` delante y arriba de la palanca de salteo (`102°, r = 7,4 m`). |
| **2** | **Terraza** (`y = 13,5 m`) | `Note_Tutorial_Move` | `<b>WASD</b> caminar · <b>Mouse</b> mirar`<br>`Avanzá por el balcón hacia el este` | 26 / 34 | `1,40 × 0,52 m` | `72°`, `r = 6,80 m`<br>`Δy = 1,75 m` (`y = 15,25 m`) | Mira al Spawn `(90°, r = 6,8 m)`<br>(hacia el Noroeste, rumbo `162°`) | `2,10 m` delante del punto de aparición (`90°`), al inicio del pasillo este. |
| **3** | **Terraza** (`y = 13,5 m`) | `Note_Tutorial_Crouch` | `Mantené <b>C</b> para agacharte`<br>`y pasar bajo la viga` | 24 / 20 | `1,30 × 0,50 m` | `40°`, `r = 6,60 m`<br>`Δy = 1,85 m` (`y = 15,35 m`) | Mira a `(58°, r = 6,8 m)`<br>(hacia quien llega del Norte, `125°`) | **`1,75 m` ANTES** de la viga baja (`BeamAngle = 25°`). |
| **4** | **Terraza** (`y = 13,5 m`) | `Note_Tutorial_Jump` | `Apretá <b>Espacio</b> para saltar`<br>`por encima del muro` | 26 / 19 | `1,30 × 0,50 m` | `342°`, `r = 6,60 m`<br>`Δy = 1,75 m` (`y = 15,25 m`) | Mira a `(2°, r = 6,6 m)`<br>(hacia quien sale de la viga, `72°`) | **`1,95 m` ANTES** del muro bajo de piedra (`JumpWallAngle = 325°`). |
| **5** | **Terraza** (`y = 13,5 m`) | `Note_Tutorial_Gate` | `Agachate en la viga y saltá el muro`<br>`para apagar la reja y bajar` | 35 / 27 | `1,45 × 0,52 m` | `212°`, `r = 5,50 m`<br>`Δy = 1,80 m` (`y = 15,30 m`) | Mira a `(232°, r = 6,0 m)`<br>(hacia quien llega por el Sur, `295°`) | **`1,65 m` ANTES** de la reja mágica (`GateAngle = 195°`) y la rampa (`180°`). |
| **6** | **Piso 2** (`y = 9,0 m`) | `Note_Tutorial_Free` | `Opcional · <b>E</b> agarrar el lienzo`<br>`<b>E</b> de nuevo lo deja flotando` | 30 / 27 | `1,40 × 0,52 m` | `8°`, `r = 3,10 m`<br>`Δy = 1,70 m` (`y = 10,70 m`) | Mira al pie de rampa `(0°, r = 5,5 m)`<br>(hacia el Este, rumbo `12°`) | **`0,85 m` ANTES** de la mesa con el lienzo de práctica (`20°, r = 2,5 m`), fuera de la rampa. |
| **7** | **Piso 2** (`y = 9,0 m`) | `Note_Tutorial_Float` | `Opcional 1 · Lienzo flotando`<br>`Dejalo quieto dentro del marco` | 28 / 30 | `1,35 × 0,50 m` | `48°`, `r = 2,70 m`<br>`Δy = 1,75 m` (`y = 10,75 m`) | Mira a `(25°, r = 2,6 m)`<br>(hacia la mesa del lienzo, `350°`) | **`0,75 m` ANTES** del marco 1 (`60°, r = 3,1 m`), en el disco central. |
| **8** | **Piso 2** (`y = 9,0 m`) | `Note_Tutorial_Upright` | `Opcional 2 · Lienzo de canto`<br>`<b>Clic der.</b> girar · <b>F</b> dar vuelta` | 28 / 30 | `1,35 × 0,50 m` | `95°`, `r = 2,70 m`<br>`Δy = 1,75 m` (`y = 10,75 m`) | Mira a `(65°, r = 2,8 m)`<br>(hacia el marco 1, rumbo `15°`) | **`0,85 m` ANTES** del marco 2 (`110°, r = 3,1 m`), en el disco central. |
| **9** | **Piso 2** (`y = 9,0 m`) | `Note_Tutorial_Target` | `Opcional 3 · Tiro al blanco`<br>`Agarrá la caja con <b>E</b> y arrojala con <b>Q</b>` | 27 / 37 | `1,45 × 0,52 m` | `328°`, `r = 3,80 m`<br>`Δy = 1,70 m` (`y = 10,70 m`) | Mira a `(350°, r = 4,8 m)`<br>(hacia el desembarco Este, `25°`) | **`0,90 m` ANTES** de la caja de tiro (`315°, r = 3,5 m`) mirando hacia la diana (`315°, r = 6,1 m`). |
| **10** | **Piso 2** (`y = 9,0 m`) | `Note_Tutorial_Grab` | `<b>E</b> agarrar o soltar · <b>Q</b> arrojar`<br>`Despejá las cajas para bajar` | 30 / 28 | `1,40 × 0,52 m` | `216°`, `r = 5,35 m`<br>`Δy = 1,75 m` (`y = 10,75 m`) | Mira a `(238°, r = 5,3 m)`<br>(hacia la explanada Sur, `305°`) | **`1,75 m` ANTES** de la pila de cajas y barril (`PileAngle = 198°`). |
| **11** | **Piso 1** (`y = 4,5 m`) | `Note_Tutorial_Ink` | `Paso 1 · <b>E</b> agarrá pluma y tintero`<br>`Con <b>R</b> recargás tinta al dibujar` | 33 / 31 | `1,45 × 0,52 m` | `340°`, `r = 4,10 m`<br>`Δy = 1,70 m` (`y = 6,20 m`) | Mira al pie de rampa `(0°, r = 5,5 m)`<br>(hacia el Este, rumbo `32°`) | **`1,15 m` ANTES** de la mesa de herramientas y tinta (`325°, r = 3,6 m`). |
| **12** | **Piso 1** (`y = 4,5 m`) | `Note_Tutorial_Book` | `Paso 2 · <b>E</b> leé el libro de viento`<br>`Con <b>T</b> abrís tu grimorio cuando quieras` | 33 / 38 | `1,50 × 0,54 m` | `295°`, `r = 2,20 m`<br>`Δy = 1,65 m` (`y = 6,15 m`) | Mira a `(325°, r = 3,4 m)`<br>(hacia la mesa de tinta, `55°`) | **`1,05 m` ANTES** de la mesa central con *Tomo I: el viento* (`270°, r = 1,6 m`). |
| **13** | **Piso 1** (`y = 4,5 m`) | `Note_Tutorial_Steps` | `Paso 3 · Calcá con <b>Clic izq.</b> el viento`<br>`Agarrá con <b>E</b> y apuntá la guía a la tela` | 38 / 39 | `1,55 × 0,56 m` | `238°`, `r = 2,90 m`<br>`Δy = 1,70 m` (`y = 6,20 m`) | Mira a `(268°, r = 2,0 m)`<br>(hacia la mesa del libro, `120°`) | **`0,90 m` ANTES** del atril con `TutorialPaper_Wind` (`220°, r = 2,6 m`), mirando hacia el jugador antes de apuntar a la cortina (`195°`). |
| **14** | **Planta baja** (`y = 0,05 m`) | `Note_Tutorial_Door` | `Puerta trabada · Calcá el sello de viento`<br>`y apuntá la guía al pestillo para salir` | 40 / 39 | `1,55 × 0,56 m` | `285°`, `r = 2,90 m`<br>`Δy = 1,65 m` (`y = 1,70 m`) | Mira a `(320°, r = 3,2 m)`<br>(hacia el atril de `elements`, `58°`) | **`1,20 m` ANTES** del atril con `TutorialPaper_Door` (`262°, r = 3,0 m`) frente a la puerta (`270°`). |

---

## 2. Carteles eliminados / fusionados respecto de la versión anterior

Para cumplir con la directriz de Tamara (*«menos carteles, textos cortos que entren en su tarjeta, sin confusiones y cada uno antes de lo que explica»*) y con el paso `R4` de `Docs/MAPA_FLUJO_PLAN.md`:
- En **Piso 2**, `Note_Tutorial_Grab` y `Note_Tutorial_Throw` (que flotaban superpuestos sobre la misma pila) se unifican en el **Cartel #10**, ubicado `1,75 m` antes de la pila.
- En **Piso 1**, se elimina el cartel suelto `Note_Tutorial_Cloth` (*«apuntá el rayo azul a la tela»*, que hizo creer que el hechizo era un rayo láser azul) y se integra la instrucción clara en el **Cartel #13** junto al atril del lienzo guiado: *«Paso 3 · Calcá con Clic izq. el viento / Agarrá con E y apuntá la guía a la tela»*.
- En **Planta baja**, se eliminan `Note_Tutorial_Locks` (*«viento · agua · tierra»*) y los dos carteles viejos de madera (`Sign_WindStation` y `Sign_WaterStation`), dejando únicamente el **Cartel #14** para la solución guiada con viento sobre el pestillo (mientras que el contrapeso y la piedra quedan como atajos opcionales sin cartel, tal como especifica `R4`).

---

## 3. Especificación técnica de `MapTutorial.Note(...)` para evitar desbordes

En `Assets/_Project/Editor/Map/MapTutorial.cs`, el método `Note(...)` debe ajustarse con estos parámetros exactos:

```csharp
// Tarjeta por defecto de 1,35 x 0,52 m (dos líneas cómodas), con margen interno de 8 cm en X y 6 cm en Y.
private static void Note(
    Transform parent, Material material, string name,
    Vector3 at, Vector3 viewer, string text,
    float width = 1.35f, float height = 0.52f, float fontMax = 0.72f)
{
    // ...
    label.rectTransform.sizeDelta = new Vector2(width - 0.16f, height - 0.12f);
    label.alignment = TMPro.TextAlignmentOptions.Center;
    label.enableAutoSizing = true;
    label.fontSizeMin = 0.22f;
    label.fontSizeMax = fontMax;
    label.textWrappingMode = TMPro.TextWrappingModes.Normal; // SIEMPRE Normal, nunca NoWrap
    label.overflowMode = TMPro.TextOverflowModes.Truncate;
    label.lineSpacing = -4f;
    label.color = new Color(0.14f, 0.08f, 0.04f);
    label.text = text;
}
```
