# Brief 16: lector de libros del mundo en VR (LIB-01, LIB-03, LIB-06)
Proyecto: SpellSimulator (Unity 6 URP, PC y Quest). Lee `CLAUDE.md`, `Docs/CODESTYLE.md` y `Docs/AUDIT/05_libros_ui.md` (leé la sección de cada hallazgo y verificá en el código real). Solo editás scripts C# (y tests) dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: código conservador. Cero allocations en Update. Lógica de plataforma solo vía `PlatformDetector.IsVR`. Todo debe funcionar en PC y VR. No hagas commits.
Archivos permitidos: `UI/BookReaderUI.cs`, `UI/ReaderPage.cs`, `Learning/LibraryBook.cs`, `UI/VrUiPanel.cs` (o el equivalente: buscalo con grep) y un test EditMode si sale lógica pura. No toques `Grimoire*`, `PlayerLook`, `ObjectMover`.
Hacé:
- LIB-01: que `BookReaderUI` abra también en VR. Usá la alternativa más simple y segura: renderizarlo como panel en el mundo con el mecanismo de `VrUiPanel` (puntero por rayo de los mandos) si ya existe y es reutilizable; si requiere crear infraestructura grande, implementá la variante mínima y explicá qué falta. Al abrir en VR NO se debe aprender el glifo "a ciegas": el aprendizaje ocurre al leer/cerrar igual que en PC.
- LIB-03: navegación del lector con gamepad (cruceta/stick: página siguiente y anterior, botón este para cerrar) y con rayo VR.
- LIB-06: evitar aprendizaje repetido al pulsar varias veces (cooldown o flag).
## Devolvé (≤6 líneas)
Por hallazgo: hecho / parcial / pospuesto, y archivos tocados.
