# Rediseño Interior de la Torre-Tutorial — Resumen y Geometría

> **Estado (2026-11-17):** este resumen es de la v1. Para **posiciones de estaciones, carteles y puzzles** la fuente de verdad es `carteles.md` y `puzzles.md` (v2), y los planos `01–04_*.svg` (planos de jugador simplificados, generados con `_generar_planos.py`; los saturados están en `v1_saturados/`). Auditorías: `auditoria_codex.md` y `auditoria_argon.md`. Las cotas de geometría de abajo siguen vigentes pendientes de la aprobación de Tamara.


> **Objetivo:** resolver los 6 problemas detectados por Tamara al probar a mano la torre-tutorial (`T1–T4` y `R1–R4`), garantizando zonas de trabajo amplias (≥ 3,5 × 3,5 m libres por ejercicio), circulación limpia entre rampas sin objetos en las escaleras, carteles cortos de 2 líneas ubicados siempre **antes** de lo que explican, utilería legible (tela reconocible, lienzos sobre soportes inclinados) y solo los libros que corresponden al flujo pedagógico.

---

## 1. Diagnóstico de los problemas actuales y cómo se resuelven

| # | Problema reportado al probar a mano | Causa raíz en el código actual (`TowerBuilder` / `MapTutorial`) | Solución en este rediseño |
|---|---|---|---|
| **1** | **Muy poco espacio entre el final de una rampa y el inicio de la siguiente; no entra ni un puzzle.** | Con `InnerRadius = 4,9 m`, `RampInner = 2,9 m` y `RampTurn = 270°`, el hueco ocupa `72°..270°` y el tramo bajo de la rampa que sube ocupa `0°..72°`. El disco central mide solo Ø 5,8 m (26,4 m²) y la baranda encierra al jugador en un pasillo anular de 1,95 m de ancho entre `0°` y `270°`. | **1)** Se amplía `OuterRadius` a **6,8 m** (`InnerRadius = 6,2 m`, Ø 12,4 m).<br>**2)** La rampa pasa a `RampInner = 4,8 m..6,15 m` (ancho 1,35 m) y **`RampTurn = 180°`** (hemisferio norte: sube de `0°` Este a `180°` Oeste).<br>**3)** Queda una **plaza continua de 96,6 m² libres** por piso (disco central de **Ø 9,6 m = 72,4 m²** + medio anillo sur de **180° = 24,2 m²**) entre la llegada de una rampa (`0°` Este) y la salida de la siguiente (`180°` Oeste). |
| **2** | **Piso 2: objetos en medio de la escalera al bajar; los carteles hablan de lienzos y no hay ninguno visible.** | `Exercises()` colocó la mesa con el lienzo (`14°`), los dos marcos (`30°`, `46°`) y la diana (`62°`) a `r = 4,0 m`, exactamente **adentro del tramo `0°..72°` donde baja la rampa `TowerRamp_2`**. El lienzo quedaba sepultado bajo la rampa y el jugador chocaba con los marcos mientras bajaba. | El tramo `0°..48°` del anillo exterior queda **100 % libre** como llegada de rampa. Los 3 ejercicios opcionales y su mesa con lienzo pasan al **disco central y cuadrante sureste (`r = 2,2..6,1 m`)**, con **3,5 × 3,5 m libres** cada uno. La pila obligatoria se ubica en `198°` (suroeste), a 2,5 m de la boca de bajada (`180°`), con una explanada de **4,2 × 4,0 m** delante para maniobrar. |
| **3** | **Piso 1: objetos, portal y libros mal acomodados; el puzzle no se resolvía; la tela no parecía tela; lienzo en el piso; cartel decía «rayo azul».** | `TutorialTable_1` y `TutorialPaper_Wind` también estaban en `14°, r = 4,0 m` (bajo la rampa, cayendo al piso por colisión); además coexistían con `Portal_tower-study`, `DraftingTable`, `Easel_Study`, `Book_motion` y `Book_shape` apiñados en 26 m². La tela era un bloque rígido beige sin cuerdas ni barral claro, y el cartel #3 decía «apuntá el rayo azul» confundiendo la guía con el hechizo. | **1)** Se quitan del Piso 1 el portal (va al Nexo, §17), los libros `motion`/`shape` (van al Archivo, R4) y las mesas repetidas.<br>**2)** El recorrido queda limpio en serie (`0° → 320° → 270° → 225° → 195°`): Mesa de tinta/pluma → Mesa con *Tomo I: el viento* → Atril inclinado con el lienzo agarrable (a 0,95 m de altura, nunca en el piso) → Cortina pesada en `195°`.<br>**3)** La tela se rediseña como **cortina/tapiz pesado de lona bordó/ocre** con barral superior, argollas, cuerdas tensoras laterales, pliegues profundos y plomada inferior.<br>**4)** Los carteles hablan de **hechizo de viento** y aclaran que la línea celeste es la **flecha de apunte del papel**. |
| **4** | **Los carteles se salen de su tarjeta y la tarjeta no se reescala.** | `Note()` creaba tarjetas chicas (`0,92 × 0,34 m` o `1,5 × 0,6 m`) con `NoWrap` cuando `height ≤ 0,4 m` o con textos de más de 90 caracteres en una línea (`Note_Tutorial_Steps`), desbordando el ancho de `TextMeshPro`. | Todos los carteles (`#1` a `#14`) tienen **máximo 2 líneas** y **≤ 38 caracteres visibles por línea**, con `TextWrappingModes.Normal`, margen interno de `0,08 m` y tarjetas dimensionadas a medida (`1,30 × 0,52 m` o `1,50 × 0,58 m`). |
| **5** | **Sobra un libro de símbolos raros en la terraza (dejar solo los libros que sirven).** | `MapDetails.LibraryBooks` seguía instanciando `BookPedestal` y `Book_rare` en `(0, TerraceY, 0)` además de `variants`, `motion` y `shape` dentro de la torre. | En la torre quedan **únicamente los 2 libros que sirven al tutorial**:<br>• **`wind-primer`** (*Tomo I: el viento*) en el **Piso 1** (`r = 1,6 m, ang = 270°, y = 5,36 m`).<br>• **`elements`** (*Los cuatro elementos*) en **Planta baja** (`r = 2,6 m, ang = 315°, y = 1,17 m`).<br>Se eliminan de la torre `rare` (y su pedestal en la terraza), `variants`, `motion` y `shape` (pasan al Archivo según `Docs/MAPA_FLUJO_PLAN.md` §17–§18). |
| **6** | **Cada cartel debe estar ANTES de lo que explica, en el camino natural y legible al llegar.** | Varios carteles estaban encima o detrás del obstáculo, o miraban a un punto genérico `(0, y, -1)` de espaldas al pie de la rampa. | Cada uno de los 14 carteles se posiciona **1,2 a 2,0 m antes** de su estación sobre la línea de avance del jugador, a altura de ojos (`Δy = 1,65–1,85 m`), con su normal `-Z` apuntando exactamente al punto (`viewer`) desde donde el jugador se aproxima. |

---

## 2. Decisiones de geometría (cotas en metros)

Sistema de coordenadas de `TowerBuilder.Polar(r, ang, y)` (relativo al centro de la torre en `(0, 16)`):
- **`ang = 0°`**: Este (`+X`) · **`ang = 90°`**: Norte (`+Z`, arriba en los planos) · **`ang = 180°`**: Oeste (`-X`) · **`ang = 270°`**: Sur (`-Z`, puerta de salida).

### 2.1 Comparativa de cotas estructurales (`TowerBuilder`)

| Parámetro | Valor actual | **Valor rediseñado** | Justificación constructiva y de diseño |
|---|---|---|---|
| `OuterRadius` | `5,50 m` | **`6,80 m`** | Ensancha la torre 1,3 m de radio manteniendo los 20 lados (`Sides = 20`). |
| `Wall` | `0,60 m` | **`0,60 m`** | Mismo espesor de sillares de piedra para ventanas y puerta. |
| `InnerRadius` | `4,90 m` | **`6,20 m`** | Diámetro interior de **12,40 m** (120,8 m² por planta vs. 75,4 m² actuales). |
| `FloorHeight` / `Floors` | `4,50 m` / `3` | **`4,50 m` / `3`** | Alturas de piso sin cambios: PB `0,05 m`, Piso 1 `4,50 m`, Piso 2 `9,00 m`, Terraza `13,50 m`. Altura libre bajo losa: `4,20 m`. |
| `TerraceRadius` / `TerraceOuter` | `7,00 m` / `7,50 m` | **`8,20 m` / `8,70 m`** | Conserva el voladizo de 1,4 m por fuera del muro (`D30`); pasillo de balcón de `2,00 m` de ancho (`r = 6,20..8,20 m`). |
| `BaseRadius` / `BaseTop` | `7,40 m` / `1,40 m` | **`8,60 m` / `1,40 m`** | Talud de piedra proporcional a la nueva silueta; zaguán abierto al sur (`270°`). |
| `PostRadius` / `RoofPosts` | `5,20 m` / `10` | **`6,50 m` / `10`** | Los 10 postes del techo apoyan sobre el muro macizo (`r = 6,50 m`), sin invadir ni el disco central ni el balcón. |
| `BarrierRadius` | `8,00 m` | **`9,20 m`** | Prisma de 20 colliders por fuera del voladizo de la terraza y del talud base. |
| `RampInner` .. `RampOuter` | `2,90 .. 4,85 m` (1,95 m) | **`4,80 .. 6,15 m` (1,35 m)** | Rampa pegada al muro de **1,35 m de ancho útil**; libera un **disco central de Ø 9,60 m (`r = 0..4,80 m`, 72,4 m²)** sin rampas en ningún piso. |
| `RampTurn` | `270°` | **`180°`** | Media vuelta exacta por el norte (`0°..180°`). Desarrollo a radio medio (`r = 5,48 m`): **17,20 m**; pendiente suave de **14,7°**. |
| `RampStart[0..2]` | `{ 0°, 0°, 0° }` | **`{ 0°, 0°, 0° }`** | Todas las rampas alineadas en el hemisferio norte: suben de `0°` (Este) a `180°` (Oeste) y **se bajan de `180°` (Oeste) a `0°` (Este)**. |
| `HoleFrom` .. `HoleTo` | `72° .. 270°` (`198°`) | **`48° .. 180°` (`132°`)** | Con `Headroom = 3,0 m` y `FloorThickness = 0,3 m`, el hueco ocupa solo `132°` en el arco norte-noroeste. Todo el hemisferio sur (`180°..360°`) tiene piso completo hasta el muro (`r = 6,20 m`). |

### 2.2 Verificación de áreas libres de trabajo por ejercicio (requisito ≥ 3 × 3 m)

| Piso | Zona / Ejercicio | Ubicación (`r`, `ang`) | Área libre sin obstáculos | Verifica |
|---|---|---|---|---|
| **Terraza** (`y = 13,5 m`) | Inicio y palanca de salteo | Norte (`r = 5,2..8,0 m`, `ang = 65°..115°`) | **4,5 × 2,8 m** antes de la viga | ✅ |
| **Terraza** (`y = 13,5 m`) | Ejercicio 1: Viga baja (`C` agacharse) | Este (`r = 4,8..8,2 m`, `ang = 25°`) | Corredor libre de **3,4 m de ancho × 6,5 m de largo** (`55° → 355°`) | ✅ |
| **Terraza** (`y = 13,5 m`) | Ejercicio 2: Muro bajo (`Espacio` saltar) | Sureste (`r = 4,8..8,2 m`, `ang = 325°`) | Corredor libre de **3,4 m de ancho × 6,5 m de largo** (`355° → 295°`) | ✅ |
| **Terraza** (`y = 13,5 m`) | Llegada a reja mágica y boca de rampa | Oeste (`r = 4,8..8,2 m`, `ang = 180°..240°`) | Descanso de **3,4 × 5,5 m** frente a la reja (`195°`) y la bajada (`180°`) | ✅ |
| **Piso 2** (`y = 9,0 m`) | Llegada de rampa superior (desembarco) | Este (`r = 4,8..6,2 m`, `ang = 335°..360°`) | **3,0 × 2,8 m** totalmente despejados al pie de la escalera | ✅ |
| **Piso 2** (`y = 9,0 m`) | Mesa de lienzo + Opcional 1 (Marco flotar) | Centro-Noreste (`r = 1,8..3,4 m`, `ang = 15°..65°`) | **3,8 × 3,6 m** en el disco central | ✅ |
| **Piso 2** (`y = 9,0 m`) | Opcional 2 (Marco de canto) | Centro-Noroeste (`r = 2,0..3,6 m`, `ang = 100°..145°`) | **3,6 × 3,5 m** en el disco central | ✅ |
| **Piso 2** (`y = 9,0 m`) | Opcional 3 (Diana y tiro con `Q`) | Sureste (`r = 3,2..6,1 m`, `ang = 305°..335°`) | Línea de tiro limpia de **3,2 × 3,5 m** hacia el muro sureste | ✅ |
| **Piso 2** (`y = 9,0 m`) | Puzzle obligatorio: Pila de cajas y barril | Suroeste (`r = 4,8..6,1 m`, `ang = 198°`, explanada `205°..255°`) | **4,2 × 4,0 m** libres delante de la pila para apilar o arrojar cajas | ✅ |
| **Piso 1** (`y = 4,5 m`) | Estación 1: Mesa de pluma y tinta + cinturón | Sureste (`r = 3,6 m`, `ang = 325°`) | **3,5 × 3,2 m** libres frente a la mesa | ✅ |
| **Piso 1** (`y = 4,5 m`) | Estación 2: Mesa con libro *Tomo I: el viento* | Sur-Centro (`r = 1,6 m`, `ang = 270°`) | **4,0 × 3,5 m** alrededor en el centro de la sala | ✅ |
| **Piso 1** (`y = 4,5 m`) | Estación 3: Atril con lienzo guiado + Cortina | Suroeste (`r = 2,6 m`, `ang = 220°` → tela en `195°`) | **4,5 × 4,0 m** libres entre el atril de dibujo y la cortina pesada | ✅ |
| **Planta baja** (`y = 0,05 m`) | Estación 1: Atril *Los cuatro elementos* + Mesa | Este-Centro (`r = 2,6 m`, `ang = 315°` y `r = 2,2 m`, `ang = 35°`) | **4,0 × 4,0 m** al pie de la rampa y centro de la biblioteca | ✅ |
| **Planta baja** (`y = 0,05 m`) | Estación 2: Lienzo guiado + 3 trabas + Puerta | Sur (`r = 0..6,2 m`, `ang = 230°..310°`) | Plaza sur de **5,5 × 4,8 m** frente a las 3 trabas y la puerta (`270°`) | ✅ |

---

## 3. Recorrido completo del jugador (de arriba hacia abajo)

1. **Terraza (`y = 13,5 m` — Movilidad básica):**
   - **Spawn:** el jugador aparece en el balcón norte (`r = 6,8 m, ang = 90°`), mirando al Este-Sureste (`yaw = 115°`), porque una **valla alta de madera (`FenceAngle = 115°`, `r = 4,8..8,2 m`, alto `2,4 m`)** cierra el paso hacia el noroeste.
   - **Salteo opcional:** a su izquierda inmediata (`r = 7,4 m, ang = 102°`) está la **palanca de salteo** con el Cartel **#1** (`<b>E</b> Saltear tutorial`).
   - **Paso 1 (Caminar):** delante del spawn (`r = 6,8 m, ang = 72°`) ve el Cartel **#2** (`<b>WASD</b> caminar · <b>Mouse</b> mirar`). Avanza en sentido horario hacia el Este.
   - **Paso 2 (Agacharse):** en `ang = 40°` lee el Cartel **#3** (`Mantené <b>C</b> para agacharte`) antes de la **viga baja** en `BeamAngle = 25°` (`r = 0..8,2 m`, luz libre `1,30 m`; el disco central `r < 4,8 m` está cerrado por la baranda circular de `r = 4,8 m`, obligando a cruzar por el corredor anular `r = 4,8..8,2 m`). Al pasar agachado bajo la viga se enciende el **farol testigo 1** (`ang = 22°`).
   - **Paso 3 (Saltar):** sigue hacia el Sureste; en `ang = 342°` lee el Cartel **#4** (`Apretá <b>Espacio</b> para saltar`) antes del **muro bajo de piedra** en `JumpWallAngle = 325°` (`r = 4,8..8,2 m`, alto `0,70 m`). Al saltarlo se enciende el **farol testigo 2** (`ang = 322°`).
   - **Paso 4 (Bajar):** al cumplirse ambas acciones se apaga la **reja mágica** en `GateAngle = 195°` (precedida por el Cartel **#5** en `ang = 212°`). El jugador entra a `TowerRamp_2` en **`180°` (Oeste)** y baja por el arco norte (`180° → 90° → 0°`).

2. **Piso 2 (`y = 9,0 m` — Mover objetos y lienzos):**
   - **Llegada:** desembarca en **`0°` (Este, `r = 5,5 m`)** mirando al Sur (`270°`); se enciende el **Farol de Nivel 1** en el muro (`ang = 350°, y = 11,2 m`). El área de desembarco (`335°..360°`) está 100 % despejada.
   - **Ejercicios opcionales (en el disco central y sureste, fuera del camino de la rampa):**
     - A su derecha (`r = 2,4 m, ang = 20°`) ve el Cartel **#6** y la **mesa con el lienzo de práctica (`TutorialPaper`)** apoyado a `0,87 m` de altura.
     - Si quiere practicar con el lienzo o las cajas, tiene 3 estaciones radiales con su farolito testigo y carteles cortos:
       - **Estación 1 (`r = 3,2 m, ang = 55°`, Cartel #7):** marco vertical para dejar el lienzo flotando (`E` para agarrar, `E` de nuevo en el aire).
       - **Estación 2 (`r = 3,2 m, ang = 105°`, Cartel #8):** marco horizontal para girar el lienzo (`Clic der. + mouse` / `F`) y dejarlo flotando de canto.
       - **Estación 3 (`r = 6,1 m, ang = 315°`, Cartel #9):** diana en el muro sureste con una caja chica en un pedestal en `r = 3,6 m, ang = 315°` para arrojar con `Q`.
   - **Puzzle obligatorio (destrabar la bajada):** siguiendo por el sur hacia el Suroeste (`ang = 225°`), lee el Cartel **#10** (`<b>E</b> agarrar/soltar · <b>Q</b> arrojar`) frente a la **pila de 4 cajas y 1 barril** que bloquea el paso en **`PileAngle = 198°`** (`r = 4,8..6,15 m`, entre la baranda del hueco y el muro). Quita las cajas hacia la explanada sur (`205°..255°`) y entra a `TowerRamp_1` en **`180°` (Oeste)**.

3. **Piso 1 (`y = 4,5 m` — Objetos mágicos y primer hechizo):**
   - **Llegada:** desembarca en **`0°` (Este, `r = 5,5 m`)**; se enciende el **Farol de Nivel 2** (`ang = 350°, y = 6,7 m`).
   - **Paso 1 (Pluma, tinta y cinturón):** en `ang = 338°` lee el Cartel **#11** (`<b>E</b> agarrá la pluma y el tintero`) delante de la **mesa de trabajo (`Workbench_Study`)** en `r = 3,6 m, ang = 325°`, donde están la pluma, el pincel, la goma, los tinteros (fijo y portátil) y el **cinturón (`Belt`)**.
   - **Paso 2 (Leer el libro de viento):** unos pasos al centro-sur (`ang = 285°`), lee el Cartel **#12** (`<b>E</b> Leé el libro de viento`) frente a la mesa `StudyTable` (`r = 1,6 m, ang = 270°`) que sostiene únicamente el libro **`wind-primer` (*Tomo I: el viento*)**.
   - **Paso 3 (Dibujar la ráfaga en el lienzo guiado y destrabar la tela):** en `ang = 232°` lee el Cartel **#13** (`Dibujá el sello azul en el papel`) junto al **atril bajo (`TutorialTable_1`)** en `r = 2,6 m, ang = 220°`. Sobre el atril (inclinado 25° hacia el jugador, a `0,95 m` del suelo para que jamás caiga al piso) está el **lienzo agarrable `TutorialPaper_Wind`** con la plantilla fantasma azul de la ráfaga (`gust`: círculo + viento + 2 columnas) y la **flecha celeste de apunte (`EmissionNormal`)**.
   - **Resolución:** el jugador calca el sello con la pluma, agarra el lienzo con `E` (o dibuja directo mirando a la tela; si la tinta quedó del otro lado, usa `F` para invertir la cara emisora) y apunta la flecha celeste hacia la **cortina pesada (`TutorialCloth`)** colgada en **`ClothAngle = 195°`** (`r = 4,8..6,15 m`). La ráfaga de viento empuja la cortina (≥ `0,30 m` o ≥ `1,0 m/s`), que se suelta de sus argollas, vuela y desaparece, liberando la bajada en **`180°` (Oeste)**.

4. **Planta baja (`y = 0,05 m` — Abrir la puerta con un hechizo):**
   - **Llegada:** desembarca en **`0°` (Este, `r = 5,5 m`)**; se enciende el **Farol de Nivel 3** (`ang = 350°, y = 2,3 m`).
   - **Opcional / Lore:** al noreste del centro (`r = 2,2 m, ang = 35°`) está la mesa de trabajo con reposición de tinta, plumas y cuadernos; y en el camino hacia el sur (`r = 2,6 m, ang = 315°`) está el atril con el libro **`elements` (*Los cuatro elementos*)**, donde quien quiera explorar descubre agua y tierra.
   - **Solución única guiada (R4):** frente a la puerta sur (`ang = 270°`), en `r = 3,0 m, ang = 282°`, el jugador encuentra el Cartel **#14** (`Calca el viento y apuntá al pestillo`) y el **atril con el lienzo guiado `TutorialPaper_Door`** (con `TutorialPaperGuide` receta `gust`), orientado hacia las **3 trabas de la puerta** en el muro sur-sureste:
     1. **Pestillo de madera (`DoorLatch_Bar`, `ang = 258°`, `r = 5,85 m`, `y = 1,45 m`)** — **solución guiada**: una ráfaga de viento (`gust`) lo corre lateralmente y destraba la puerta.
     2. **Contrapeso de hierro (`DoorLatch_Weight`, `ang = 282°`, `r = 5,80 m`, `y = 1,00 m`)** — **atajo opcional sin cartel**: una corriente ascendente (`updraft`) o un chorro de agua/viento hacia arriba lo levanta `0,70 m`.
     3. **Traba de piedra (`TutorialDoorStone`, `ang = 294°`, `r = 5,80 m`, `y = 0,05 m`)** — **atajo opcional sin cartel**: tierra con `crush` (*Wall Breaker*) la rompe.
   - **Salida al mundo:** al ceder cualquiera de las 3 trabas, `TowerDoor` se abre sola (`-150°`), se enciende el **Farol de Nivel 4** sobre el dintel (`ang = 270°, y = 3,35 m`) y, al cruzar el vano (`r > 6,55 m, ang = 270° ± 12°`), se apaga la barrera mágica y concluye el tutorial rumbo al **Nexo (`0, -6`)**.

---

## 4. Diseño visual propuesto para la «Tela Pesada» del Piso 1

Para resolver el reporte #3 (*«la tela no parecía tela»*), `MapTutorial.MagicFloor` reemplaza los 6 prismas beige planos por un **tapiz/cortina pesada de lona** inmediatamente reconocible en low-poly:
- **Pórtico y barral de hierro (`y = 7,45 m`, `ang = 195°`):** viga superior de madera oscura (`r = 4,75..6,20 m`) con un **barral cilíndrico de hierro** debajo y **7 argollas metálicas** (`Iron` `#33333A`) de las que cuelga la tela.
- **Paño de lona con pliegues en zigzag y caída asimétrica:** 7 bandas verticales (`ancho total = 1,32 m`, `alto = 2,80 m`) alternando **bordó profundo (`#8F2622`)** y **sombra carmesí (`#631816`)** con **guarda horizontal dorada (`#E0B045`)** a `0,35 m` del borde inferior y un **emblema de viento en ocre claro (`#EDD9A3`)** cosido al centro (indica visualmente que reacciona al viento). Cada pliegue alterna `±0,05 m` en Z e inclina levemente los bordes para dar volumen textil.
- **Cuerdas tensoras laterales y plomada inferior:** dos **sogas de cáñamo (`#BFA36F`)** atadas a alcayatas en la baranda (`r = 4,82 m`) y en el muro (`r = 6,14 m`) a media altura (que se sueltan cuando `TutorialCloth.Open()` dispara), más un **listón de lastre con borlas doradas** en el ruedo inferior (`y = 4,62 m`, dejando `12 cm` de luz sobre el piso para que se vea oscilar con el resorte).

---

## 5. Libros y objetos que quedan vs. los que salen de la torre

| Objeto / Libro | Estado actual | **Acción en el rediseño** | Ubicación final |
|---|---|---|---|
| `Book_wind-primer` (*Tomo I: el viento*) | Piso 1 `(0.6, 5.36, 0.5)` | **SE QUEDA** (único libro del Piso 1) | **Piso 1**, sobre `StudyTable` en `r = 1,6 m, ang = 270°, y = 5,36 m` |
| `Book_elements` (*Los cuatro elementos*) | Planta baja `(-1.29, 1.17, -2.60)` | **SE QUEDA** (único libro de Planta baja) | **Planta baja**, sobre el atril en `r = 2,6 m, ang = 315°, y = 1,17 m` |
| `Book_rare` (*Signos raros*) + `BookPedestal` | Terraza `(0, 13.5, 0)` | **SE QUITA de la terraza** (reporte #5) | Pasa al **Archivo** (`Docs/MAPA_FLUJO_PLAN.md` §18) |
| `Book_variants` (*Variantes y parientes*) | Planta baja `(-0.6, 0.86, 0.22)` | **SE QUITA de la torre** (D1 / §18) | Pasa al **Archivo** (nicho 1) |
| `Book_motion` (*Signos del movimiento*) | Piso 1 `(0.9, 5.36, 0.2)` | **SE QUITA de la torre** (R4 / §18) | Pasa al **Archivo** (nicho 2) |
| `Book_shape` (*Dar forma a la magia*) | Piso 1 `(0.25, 5.36, 0.75)` | **SE QUITA de la torre** (R4 / §18) | Pasa al **Archivo** (nicho 3) |
| `Portal_tower-study` | Piso 1 `(-2.0, 4.5, 0.65)` | **SE MUDA fuera de la torre** (D19 / §17) | Pasa al **Nexo** (`0, -6`) |
| `TowerAlchemy` + `AlchemyTable` | Piso 2 `(0, 9.0, 0)` | **SE MUDA fuera de la torre** (§17 / §18) | Pasa al **Archivo**; despeja el disco central del Piso 2 |
| `Belt` (cinturón de cuero) | Piso 2 (sobre `AlchemyTable`) | **SE REUBICA en Piso 1** | **Piso 1**, extremo derecho de `Workbench_Study` (`r = 3,6 m, ang = 315°, y = 5,32 m`) |
| `WindStation` / `WaterStation` (viejas) | Planta baja (`324°` y `180°`) | **SE SIMPLIFICAN / MUDAN** (R4) | Se quitan sus carteles viejos (`Sign_WindStation`, `Sign_WaterStation`); la pila de agua en `165°` y la guirnalda en `330°` quedan solo como decoración reactiva sin interferir con la puerta |

---

## 6. Lista de cambios al generador en alto nivel (cuando se implemente)

1. **`TowerBuilder.cs`**:
   - Actualizar constantes: `OuterRadius = 6.8f`, `InnerRadius = 6.2f`, `TerraceRadius = 8.2f`, `TerraceOuter = 8.7f`, `BaseRadius = 8.6f`, `PostRadius = 6.5f`, `RampInner = 4.8f`, `RampTurn = 180f`, `RampStart = { 0f, 0f, 0f }`.
   - En `Interior()`: quitar `LibraryDesk` central que tapaba el paso y reubicar `StudyTable` en `Polar(1.6f, 270f, FloorHeight)`.
2. **`TutorialRules.cs`, `TerraceRules.cs` y `SpawnRules.cs`**:
   - `TutorialRules`: `InnerRadius = 6.2f`, `OuterRadius = 6.8f`, `BarrierRadius = 9.2f`.
   - `TerraceRules`: `Radius = 8.2f`, `BeamAngle = 25f`, `JumpWallAngle = 325f`, `GateAngle = 195f`.
   - `SpawnRules`: `TerraceOffset = (0f, 6.8f)` (`ang = 90°`, balcón norte), `TerraceYaw = 115f` (mirando hacia el camino del Este).
3. **`MapTutorial.cs`**:
   - Actualizar `HoleFrom = 48f`, `HoleTo = 180f`, `FenceAngle = 115f`, `ObstacleFrom = 4.8f` (con baranda circular completa en `r = 4.8 m` en la terraza para encauzar por el balcón), `PileAngle = 198f`, `RailEnd = 205f`, `ClothAngle = 195f`, `LatchAngle = 258f`, `WeightAngle = 282f`, `StoneAngle = 294f`.
   - Reubicar `Exercises()` del Piso 2 al disco central/sureste (`ang = 20°, 55°, 105°, 315°`).
   - En `MagicFloor()` (Piso 1) y `DoorLocks()` (Planta baja): colocar el lienzo guiado (`TutorialPaper_Wind` y el nuevo `TutorialPaper_Door` para R4) sobre un soporte inclinado 25° con tope inferior para que no se caiga al piso, y actualizar la malla de `TutorialCloth` al diseño de cortina con barral, argollas, pliegues y tensores.
   - Reemplazar las llamadas a `Note()` por las 14 tarjetas de `Docs/Torre/carteles.md`.
4. **`MapDetails.cs`, `TowerProps.cs`, `MapBelt.cs` y `MapStructures.cs`**:
   - `MapDetails.LibraryBooks`: dejar en la torre solo `elements` y `wind-primer`; quitar `BookPedestal` y `rare` de la terraza, y mudar `variants`, `motion`, `shape` y `rare` al Archivo.
   - `TowerProps`: quitar `Alchemy()` del Piso 2 y eliminar `Sign_WindStation` / `Sign_WaterStation` de Planta baja; mover `Workbench_Study` a `Polar(3.6f, 325f, FloorHeight)`.
   - `MapBelt`: apoyar el cinturón en `Workbench_Study` (Piso 1).
   - `MapStructures.StudyPortal`: mudar el portal del Piso 1 al Nexo.
