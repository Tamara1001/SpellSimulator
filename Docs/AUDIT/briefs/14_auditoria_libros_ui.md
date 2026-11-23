# Brief 14: auditoría de libros y UI (solo lectura)

Proyecto: SpellSimulator (Unity 6 URP, PC y Meta Quest). Lee `CLAUDE.md`. **No edites código.** Creá solamente el informe `Docs/AUDIT/05_libros_ui.md`.

## Alcance
1. **Libros del mundo** (`Learning/LibraryBook.cs`, `BookReaderUI.cs`, `ReaderPage.cs`, libros en `World/`): cómo se agarran, abren y leen en PC y en VR; legibilidad (tamaño, contraste, fuentes `UiFonts`), paginación, cómo se aprende un glifo al leer.
2. **Grimorio flotante (T)** (`Learning/Grimoire*.cs`, `GrimoireTexts.cs`, `GrimoireButton.cs`): usabilidad en VR (puntero con rayo, botones, distancia, escala, pasar páginas sin rueda/teclado), modo fijo (B), tamaños de texto, retorno al inventario. Hoy es un cuerpo dinámico empujable (cambio reciente).
3. **Otras UI**: `HudController`, `MainMenu*`, `UI/*.uss`, `Player/VrControlsPanel.cs`: respuesta, consistencia visual, accesibilidad (contraste, tamaño mínimo ~ 18 px en pantalla / legible a 1 m en VR), gamepad/VR, estados de foco, allocations en Update.

## Formato del informe
Para cada hallazgo: id `LIB-nn`/`UI2-nn`, severidad (Alta/Media/Baja), archivo:línea, descripción corta, impacto (PC/VR), **2 o 3 alternativas** con su costo. Al final una tabla resumen y una propuesta de orden de trabajo. Verificá cada cosa leyendo el código real; no inventes. Máximo ~25 hallazgos.

## Devolvé (≤5 líneas)
Ruta del informe, cantidad de hallazgos por severidad y los 3 más importantes.
