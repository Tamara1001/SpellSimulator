---
tags: [doc, plan]
---
# Plan de la sexta tanda (feedback de la quinta prueba manual)

> Origen: prueba manual de la tanda UI Toolkit/inventario/grimorio/torre (2026-10-07). Se divide en 3 fases con agentes.
> Reglas de costo: ver [STATUS.md](STATUS.md#reglas-de-trabajo-baratas-aprendidas) y la memoria `cost-lessons`.

## Desglose (todo lo pedido, agrupado)

| # | Tema | Detalle | Fase | Modelo |
|---|---|---|---|---|
| A1 | Grimorio descubrible | Nadie sabe que se abre con TAB: mostrarlo en la HUD; ESC también lo abre/cierra en pantalla completa; **slot del grimorio en la hotbar**; abierto en modo no pantalla completa NO oculta la HUD | 1 | Sonnet |
| A2 | Layout del grimorio | "Clic der. + mouse", "Stick izq./der. (clic)" pisan el texto: columna de teclas más ancha, textos de acción con salto de línea (`GrimoireTexts.cs`, shader/layout de página) | 1 | Haiku |
| A3 | Dibujo de sigilos en el libro | Más calidad (resolución/suavizado) y misma velocidad para todos (remuestreo por longitud de arco, duración fija) | 1 | Sonnet |
| B1 | Descubrimiento obligatorio | Arrancar solo con tierra, agua, viento y fuego; el resto (16→4 conocidos) se aprende leyendo libros | 1 | Haiku (data) |
| B2 | Libros | Brillo/outline para destacarlos; "Libro X de Y" numerado y contador de los que faltan; el libro de los 4 elementos debe explicar **Fuego** (hoy no) | 1 | Sonnet |
| C1 | Controles | Rueda con pincel + cuaderno en mano: la rueda hace solo grosor; las páginas pasan con **Z / X** | 1 | Sonnet |
| C2 | HUD | Textos cortados en la fila de Pincel/Lienzo; mayúsculas (Dibujar, Deshacer, Grosor); más espacio arriba; quitar rótulos "Mano derecha/izquierda"; brillo de selección cortado | 1 | Sonnet |
| D1 | Sellos | Un sello normal copia **solo el símbolo elegido, sin el círculo**; nuevo glifo "Círculo Mágico" (círculo casi cerrado) en el libro de glifos | 1 | Sonnet |
| E1 | Spawn | Jugador nace dentro de la torre frente a un libro básico de Magia (mismo texto que la categoría Magia del grimorio) | 2 | Haiku |
| E2 | Puerta | La puerta de la torre es interactuable (abre/cierra) | 2 | Haiku |
| E3 | Objetos movibles | Más objetos movibles (libros, sillas, mesas, velas…) con dificultad según peso | 2 | Sonnet |
| E4 | Lienzos y prácticas | Más lienzos y zonas de práctica por el mundo, también cerca del lago | 2 | Sonnet |
| E5 | Torre: estética y distribución | Rediseño estético y de distribución: primeros experimentos ahí, incitar a probar primero viento y agua | 2 | **Opus** |
| F1 | Hechizos | Apartado "Hechizos" en el grimorio con hechizos básicos listos (círculo casi terminado, con columnas y otros glifos) + libros de hechizos aprendibles | 2 | Sonnet (datos) |
| F2 | Guardar hechizos | **Solo planificar** (sistema para guardar un hechizo dibujado): ADR corto, sin código | 2 | **Opus** (plan) |
| G1 | Menú principal | Sandbox (actual), Duelo (próximamente), Información (grimorio a pantalla completa), Créditos ("Tamara D'Angelo"), Salir | 3 | **Opus** |
| G2 | VFX de elementos | Fuego ok (revisar el aspecto de objetos prendidos); agua y viento mejorar un poco; **tierra horrible**, rehacer | 3 | **Opus** |
| H | Futuro (solo registrar) | Configuración de teclas en el menú; multijugador | — | — |

## Decisiones a confirmar antes de la Fase 1
1. **Conflicto de teclas:** Z = tumbarse y X = zoom hoy. Propuesta: páginas con Z/X; tumbarse pasa a **C** (mantener = tumbarse, toque = agacharse) y zoom a **Alt izq.** mantenido.
2. Slot del grimorio en la hotbar: propuesta, un **4.º slot fijo a la izquierda** (tecla TAB) que abre/cierra el libro; no ocupa los 3 slots de la mano principal.

## Fases y reparto de agentes

**Principios (para no gastar límite):**
- Un agente = un tema con archivos propios (sin solaparse) y especificación cerrada en su prompt (leer `Docs/STATUS.md` y esta página; no leer el repo entero; sin graphify).
- Los agentes **escriben código y no compilan en bucle**: yo compilo una vez por fase (`AssetDatabase.Refresh` + `grep "error CS"`), corro `HarnessTestRunner` + `HarnessPlaySmoke` y arreglo lo menor.
- Haiku para datos/textos/ajustes de 1 archivo; Sonnet para lógica de varios archivos; Opus solo para 3D/UI estética y planificación profunda.
- Un commit por fase, docs mínimos (CHANGELOG + KNOWN_ISSUES + lo que cambió).

### Fase 1 — Controles, grimorio, libros y HUD (sin tocar el mundo; todo Sonnet/Haiku)
- Agente S1 (Sonnet): C1 + A1 + C2 → `PlayerInputReader`, `PlayerInteractor`, `NotebookPages`, `PlayerLook`/`PlayerMovement` (teclas), `Scripts/UI/*`, `UI/Hud.uxml`, `SpellUI.uss`, `Grimoire.cs` (apertura TAB/ESC, no ocultar HUD), `HudModel`.
- Agente S2 (Sonnet): A3 + B2 + D1 → dibujo de sigilos del libro (`Grimoire*`, shaders `GrimoirePage/GrimoireInk`), `LibraryBook` (X de Y, brillo/outline), `Stamp.cs`, `SpellCaster`/glifo "Círculo Mágico", diccionario.
- Agente H1 (Haiku): A2 + B1 → `GrimoireTexts.cs` (layout/texto), estado inicial de glifos conocidos (`LearningSystem`/datos), texto del libro de los 4 elementos (Fuego).
- Cierre (yo): compilar, tests, PlaySmoke (+ chequeos: 4 glifos conocidos, Z/X pasan página sin cambiar grosor, TAB/ESC abren el grimorio, estampa sin círculo), commit.

### Fase 2 — Mundo y contenido (Opus para la torre y el plan; el resto Sonnet/Haiku)
- Agente O1 (**Opus**): E5 → `TowerBuilder.cs`, `TowerProps.cs` (estética y recorrido de experimentos viento/agua). Entrega escena regenerada por `MapBuilder.Build()` y capturas.
- Agente O2 (**Opus**, solo documento, ~poco código): F2 → ADR "guardar hechizos" en `DECISIONS.md` + diseño de datos.
- Agente S3 (Sonnet): E3 + E4 → `Movable`, `MapContraptions`/`CanvasDesigns` (más objetos, masas, lienzos y prácticas junto al lago).
- Agente S4 (Sonnet): F1 → apartado "Hechizos" del grimorio, datos de hechizos básicos, libros de hechizos aprendibles.
- Agente H2 (Haiku): E1 + E2 → spawn dentro de la torre frente al libro de Magia; puerta interactuable.
- Orden: S3, S4, H2 en paralelo; O1 después de H2 (comparten `TowerBuilder`); cierre y commit.

### Fase 3 — Menú principal y VFX (todo Opus por estética)
- Agente O3 (**Opus**): G1 → escena/UI Toolkit de menú (UISetup nuevo), botones Sandbox/Duelo (desactivado "Próximamente")/Información (abre el grimorio en pantalla completa)/Créditos/Salir; arranque y vuelta al menú (ESC).
- Agente O4 (**Opus**): G2 → VFX de tierra (rehacer), mejoras de agua y viento y aspecto de objetos prendidos (`BurnEffect`, `Scripts/Rendering`, prefabs/materiales de VFX).
- Cierre: compilar, tests, PlaySmoke (+ menú presente y Sandbox arranca), build y smoke del exe, commit.

## Presupuesto
- Hoy (queda ~15 %): solo la **Fase 1** si se aprueba (no usa Opus). Fases 2 y 3 en la próxima ventana de 5 h, una por vez.
- Cada agente recibe el prompt cerrado de su fila; evitar sondeos con sleeps largos y capturas innecesarias.
