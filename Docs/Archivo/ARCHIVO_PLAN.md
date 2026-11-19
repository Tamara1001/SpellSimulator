# Plan de Diseño: Archivo (Rotonda de la Sabiduría)

Planta general: [archivo_plano.svg](file:///D:/Programas/Unity/SpellSimulator/Docs/Archivo/archivo_plano.svg)  
Referencias: [MAPA_FLUJO_PLAN.md (§18, D19–D27)](file:///D:/Programas/Unity/SpellSimulator/Docs/MAPA_FLUJO_PLAN.md#L676-L688) y [LIENZOS_PLAN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/LIENZOS_PLAN.md).

---

## 1. Concepto y Estética

El **Archivo** es el corazón del aprendizaje intermedio: una **biblioteca al aire libre** concebida como una rotonda abierta (sin muros opacos, transitable, ideal para VR y ligera en Quest). Es el núcleo al que el aprendiz regresa para consultar libros, probar signos y reponer insumos antes de partir hacia los campos elementales.

- **Estética visual:** Estilo *Witch Hat Atelier*, low-poly cartoon. Vigas y columnas de madera cálida envejecida (roble y abeto), suelo de losas claras con mosaicos rúnicos concéntricos, pérgola superior en anillo con enredaderas y faroles colgantes de bronce de luz cálida.
- **Acceso norte:** Dos tiendas estilo bazar medieval al aire libre con toldos de lona a rayas de colores vivos y mostradores de madera clara.

---

## 2. Medidas y Coordenadas de Referencia

- **Ubicación en el mundo Unity:** `(0.0, -46.0)` en el plano XZ.
- **Sistema de coordenadas locales:** Origen `(0.0, 0.0)` en el centro de la rotonda.
  - `+z`: Norte (hacia el Nexo en `z = -6` y la Torre en `z = +16`).
  - `-z`: Sur (hacia los campos de Fuego y Tierra).
  - `+x`: Este (hacia el Campo de Viento).
  - `-x`: Oeste (hacia el Campo de Agua).
- **Dimensiones base:** Rotonda de radio **9.0 m** (diámetro 18.0 m). Claro de bosque talado/despejado de radio **14.0 m**. Camino de entrada norte de **3.0 m de ancho** (`x ∈ [-1.5, +1.5]`).

---

## 3. Disposición y Coordenadas Locales de Elementos

| Elemento | Posición local `(x, z)` | Medidas | Orientación / Rumbo | Descripción |
|---|---|---|---|---|
| **Mesa Central** | `(0.0, 0.0)` | Radio 1.5 m | Frente hacia el Norte (0°) | Mesa redonda de nogal; atril con el libro *Primeros Hechizos*, tintero cerámico y plumas de reposición. |
| **Columnas perimetrales (12)** | `r = 8.8 m` en anillo | Diámetro 0.5 m, h=3.8 m | Radiales cada 30° | Columnas cilíndricas de madera con ménsulas de piedra, sostienen la pérgola anular abierta y faroles. |
| **Arco de Entrada Norte** | `(0.0, +8.8)` | Luz 3.2 m | Rumbo 180° (mira al norte) | Vano principal desde el camino del Nexo/Torre. |
| **Arco Oeste (Agua)** | `(-8.8, 0.0)` | Luz 2.6 m | Rumbo 90° (salida a -x) | Estandarte azul zafiro; conecta con la senda al lago. |
| **Arco Suroeste (Fuego)** | `(-6.2, -6.2)` | Luz 2.6 m | Rumbo 45° (salida a -x, -z) | Estandarte bermellón/naranja; conecta con el campo de fuego. |
| **Arco Sureste (Tierra)** | `(+6.2, -6.2)` | Luz 2.6 m | Rumbo 315° (salida a +x, -z) | Estandarte verde/ocre; conecta con el campo de tierra. |
| **Arco Este (Viento)** | `(+8.8, 0.0)` | Luz 2.6 m | Rumbo 270° (salida a +x) | Estandarte ámbar/dorado; conecta con el campo de viento. |
| **Nicho 1: `variants`** | `(-4.4, +4.4)` | 1.8 × 1.2 m | Rumbo 135° (hacia el centro) | Atril con *Variantes y parientes* (luz, cristal, formas de aire) bajo marquesina en sombra. |
| **Zona de prueba `variants`** | `(-6.0, +3.8)` | 2.0 × 2.0 m | Contigua al nicho | Rincón en penumbra con pedestal de piedra, prisma facetado y lámpara de pie apagada reactiva. |
| **Nicho 2: `motion`** | `(-4.7, -4.2)` | 1.8 × 1.2 m | Rumbo 45° (hacia el centro) | Atril con *Signos del movimiento* (dispersión, atracción, flotar, levitar). |
| **Zona de prueba `motion`** | `(-3.5, -5.8)` | 2.0 × 2.0 m | Contigua al nicho | Caja liviana de mimbre/madera, frasco con plumas flotantes y canasta suspendida de objetivo. |
| **Nicho 3: `shape`** | `(+4.7, -4.2)` | 1.8 × 1.2 m | Rumbo 315° (hacia el centro) | Atril con *Dar forma a la magia* (columna, convergencia, región, aplastar). |
| **Zona de prueba `shape`** | `(+3.5, -5.8)` | 2.0 × 2.0 m | Contigua al nicho | Bandeja baja de madera de 2×2 m llena de arena fina y estacas a distintas alturas. |
| **Altillo / Nicho Apartado** | `(+4.4, +4.4)` | 2.2 × 1.4 m | Rumbo 225° (hacia el centro) | Atril para *Signos Raros* (`rare`) y mesa de alquimia con alambiques y frascos trasladada de la torre. |
| **Estantes de la memoria** | `r = 8.2 m` (Noroeste y Noreste) | 2 tramos curvados de 3.5 m | Frente hacia el centro | Estanterías bajas de roble; se llenan con lomos de color al leer libros en el valle (`DiscoveryState`). |
| **Portal al Nexo** | `(0.0, -6.8)` | Radio 1.4 m | Frente hacia el Norte (0°) | Plataforma rúnica de piedra violeta; atajo de retorno instantáneo hacia el Nexo (D17, §18). |

---

## Estado real (2026-11-19)

> Lo de abajo (§4) es el diseño original; **lo construido difiere**. Fuente de verdad: `Assets/_Project/Editor/Map/MapRestocker.cs` (se llama desde `CanvasDesigns.Build`).

Los puestos NO están en `(±5.5, +11.5)` (caía dentro del campo de entrenamiento): están en el borde norte del claro, respecto del centro del Archivo `(0, −46)`, con el frente hacia la torre (+Z mundo):

| Puesto | Local `(x, z)` | Guiñada | Qué muestra |
|---|---|---|---|
| Tienda de plumas | `(−7.5, 5.5)` | +25° | 5 lapiceros (pluma fina 4 mm, lápiz 8, pincel 20, pincel plano 40, brocha 80) con 5/5/5/4/3 plumas reales cada uno |
| Almacén de lienzos | `(+7.5, 5.5)` | −25° | 7 pilas en el mostrador (libretas S circular/M/L, hojas S y M cuadradas y rectangulares) + 2 hojas L en bahías laterales bajo el mismo toldo (ancho 6,5 m, mostrador 3,9 m) |
| Puesto de tintas | `(−2.6, 6.6)` | +12° | 3 bandejas × 3 frascos portátiles (normal, poderosa ×2,5, resistente); E guarda el frasco como cartucho de 5 usos |
| Puesto de sellos | `(+2.6, 6.6)` | −12° | sellos S (×3), M (×2) y L (×1) vacíos, clonados de `Stamp_S_0/M_1/L_2` |

**Mecánica común:** cada hueco clona un objeto REAL desde una plantilla desactivada (hija de `Restocker`) y lo deja quieto; cuando el jugador se lo lleva, `DisplayStock` (plumas, frascos, sellos) o `CanvasPile` (lienzos, libretas) repone otro. En sesiones de red no se exhibe nada. Los lienzos de exhibición flotan sin límite (`PhysicalCanvas.PinUntimed`) y los llevados cuentan para el tope de `RestockRules.MaxSpawnedPerKind`.
**Libretas:** siempre 8 hojas (`CanvasRules.DefaultPages`); la libreta S es circular y sus hojas son un disco.
**Tipografía:** títulos de cartel en Lora, plaquetas y pizarras en Nunito ([UI_ESTETICA](../UI_ESTETICA.md)).
**Pendiente del Archivo:** la rotonda en sí (suelo, columnas, nichos, atriles, portal; fases 1-4 del §5) sigue sin construirse; las coordenadas de §4 deben leerse como históricas.

## 4. Tiendas al Aire Libre (Acceso Norte)

Ubicadas a ambos lados del camino principal de llegada, creando una plazoleta comercial previa al arco de entrada.

### 4.1 Tienda de plumas
- **Ubicación local (centro):** `x = -5.5 m, z = +11.5 m`
- **Dimensiones:** `3.0 m` (ancho) × `2.0 m` (fondo), altura 2.8 m.
- **Orientación:** Mostrador orientado hacia el Sureste (rumbo ~120°), mirando a la explanada de acceso.
- **Detalle visual:** Toldo a rayas verde salvia y crema; mostrador de madera cálida; repisas con frascos cilíndricos de vidrio que contienen pinceles de diversos grosores y cerdas (`BrushSpec`), tinteros cerámicos y plumas de ave.
- **Posición del jugador al interactuar:** `x = -3.5 m, z = +11.0 m`.  
  *Margen libre:* Queda a `2.0 m` de distancia del borde oeste del camino principal (`x = -1.5 m`), garantizando paso libre a otros jugadores o tránsito fluido.

### 4.2 Almacén de lienzos y libretas
- **Ubicación local (centro):** `x = +5.5 m, z = +11.5 m`
- **Dimensiones:** `3.0 m` (ancho) × `2.0 m` (fondo), altura 2.8 m.
- **Orientación:** Mostrador orientado hacia el Suroeste (rumbo ~240°), mirando a la explanada de acceso.
- **Detalle visual:** Toldo a rayas teja y mostaza; expositores escalonados con pilas de lienzos cuadrados y rectangulares (S, M, L), libretas circulares y rectangulares según [LIENZOS_PLAN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/LIENZOS_PLAN.md); incluye el mueble interactivo de restock (`CanvasRestocker`).
- **Posición del jugador al interactuar:** `x = +3.5 m, z = +11.0 m`.  
  *Margen libre:* Queda a `2.0 m` de distancia del borde este del camino principal (`x = +1.5 m`), sin entorpecer el acceso a la rotonda.

---

## 5. Orden de Construcción Sugerido

1. **Fase 1 · Terreno y Trazado:** Aplanado de la loma en `(0, -46)` con radio de despeje de árboles r=14 m (`CanPlantTree`). Conectar las 5 ramas de caminos en `ValleyShape.Paths` (norte al Nexo, oeste a agua, suroeste a fuego, sureste a tierra, este a viento).
2. **Fase 2 · Estructura de la Rotonda:** Generación paramétrica del suelo circular de losas (r=9 m), las 12 columnas en r=8.8 m y el aro de pérgola superior con los 4 vanos elementales y el vano de entrada norte.
3. **Fase 3 · Nichos Interiores y Zonas de Prueba:** Colocar los atriles de los 3 nichos (`variants`, `motion`, `shape`), las 3 zonas de prueba de 2×2 m con sus props físicos (prisma, lámpara, caja liviana, plumas, bandeja de arena con estacas) y el nicho noreste (`rare` + alquimia).
4. **Fase 4 · Mesa Central, Portal y Estantes:** Instalar la mesa central con el atril de *Primeros Hechizos*, la plataforma del portal rúnico al sur en `(0, -6.8)` y los estantes perimetrales vinculados a `DiscoveryState`.
5. **Fase 5 · Tiendas al Aire Libre:** Levantar los dos puestos de madera y lona en `(-5.5, +11.5)` y `(+5.5, +11.5)` con sus mostradores, frascos de pinceles y el mueble restocker de lienzos/libretas. Ajustar faroles y colisiones para validar paso continuo sin trabas.
