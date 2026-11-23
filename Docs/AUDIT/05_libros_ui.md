# Auditoría Técnica: Libros del Mundo, Grimorio y UI (Áreas LIB y UI2)

**Proyecto:** SpellSimulator (Unity 6 URP)  
**Módulos auditados:** Libros del mundo (`Learning/LibraryBook.cs`, `UI/BookReaderUI.cs`, `UI/ReaderPage.cs`, `UI/GlyphSketch.cs`), Grimorio flotante (`Learning/Grimoire*.cs`, `Learning/GrimoireTexts.cs`, `Learning/GrimoireButton.cs`), y Otras UI (`UI/HudController.cs`, `UI/MainMenu*.cs`, `UI/SpellUI.uss`, `Player/VrControlsPanel.cs`, `Player/VrUiPanel.cs`, `Player/PlayerInteractor*.cs`).  
**Fecha:** 2026-11-23  
**Documento:** `Docs/AUDIT/05_libros_ui.md`  

---

## 1. Resumen Ejecutivo

Se realizó una auditoría estática exhaustiva de código, configuración de UI Toolkit (USS/UXML) y componentes 3D diegéticos orientada a la experiencia de lectura, interfaz de usuario y accesibilidad en plataformas PC (teclado/mouse/gamepad) y Meta Quest (VR / OpenXR).

Se identificaron **24 hallazgos** (14 en Libros y Grimorio, 10 en Interfaces Generales y VR):
- 🔴 **Alta severidad:** 4 hallazgos
- 🟠 **Media severidad:** 10 hallazgos
- 🟢 **Baja severidad:** 10 hallazgos

Los problemas más críticos radican en la exclusión total de la experiencia de lectura de libros en VR (el lector modal se aborta de inmediato y aprende sin leer), el conflicto de entradas en VR con el grimorio abierto (el stick gira la cabeza 45° en vez de pasar páginas), el riesgo de pérdida física del grimorio por empuje dinámico fuera del rango de interacción, y los tamaños de tipografía en HUD/avisos que violan las cotas de accesibilidad y legibilidad a 1 metro en visor.

---

## 2. Hallazgos: Libros del Mundo y Grimorio Flotante (LIB)

### LIB-01: Exclusión total del lector de libros en Realidad Virtual
- **Severidad:** 🔴 Alta
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/BookReaderUI.cs:85` y `Assets/_Project/Scripts/Learning/LibraryBook.cs:141`
- **Descripción:** En `BookReaderUI.Open()`, la condición `if (Instance == null || PlatformDetector.IsVR || pages == null || pages.Count == 0) return false;` cancela la apertura del lector en VR. En consecuencia, `LibraryBook.Press()` evalúa `if (!UI.BookReaderUI.Open(...)) Learn();`, ejecutando inmediatamente el aprendizaje directo del glifo/portal/hechizo. El jugador en Quest nunca ve la portada, las páginas ilustradas con el trazado procedural (`GlyphSketch`), los nombres canónicos, ni los textos de colofón y caminos de portales. El aviso final se envía a `DiagnosticBus` que solo se dibuja en la HUD de pantalla plana (KI-054).
- **Impacto:** PC: Ninguno. VR: Crítico (rompe la experiencia didáctica central de la torre y el mundo).
- **Alternativas de arreglo:**
  1. *Panel diegético en el mundo o en VrUiPanel:* Abrir `BookReaderUI` renderizándolo en la `RenderTexture` de `VrUiPanel` permitiendo interacción con el rayo de los mandos. (Costo: Medio, ~3-4 h).
  2. *Lector 3D diegético:* Abrir el libro físico sostenido frente a la vista del jugador usando quads y TextMeshPro, análogo al Grimorio. (Costo: Alto, ~8 h).
  3. *Solución rápida de proyección:* Permitir que `BookReaderUI` abra en VR en el quad frontal de `VrUiPanel` desactivando temporalmente el bloqueo de `PlatformDetector.IsVR`. (Costo: Bajo, ~1.5 h).

---

### LIB-02: Libros del mundo son objetos estáticos no agarrables ni transportables
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/LibraryBook.cs:16`
- **Descripción:** `LibraryBook` implementa únicamente `IPressable` e `IHudInfo`. No implementa `IHoldable`, `IRemoteGrabbable` ni posee un `Rigidbody`. El jugador solo puede interactuar pulsando `E` a distancia para abrir el lector modal en el lugar en que se encuentra el libro. No es posible agarrar el libro, levantarlo, llevarlo a un caballete de dibujo o guardarlo.
- **Impacto:** PC / VR: Medio (limita la fantasía diegética de transportar tratados mágicos al puesto de trabajo).
- **Alternativas de arreglo:**
  1. Agregar `Rigidbody` + `World.Holdable` + `IRemoteGrabbable`, permitiendo arrastrar el tomo o sostenerlo en la mano secundaria en VR; pulsar `E` sobre el libro sostenido abre su lectura. (Costo: Medio, ~3 h).
  2. Mantener los libros como atriles fijos pero permitir "tomar notas" que copien la página al cuaderno de notas del jugador. (Costo: Alto, ~6 h).
  3. Conservar el comportamiento actual documentando explícitamente en el diseño que los libros son volúmenes pesados de consulta fija en mobiliario. (Costo: Nulo).

---

### LIB-03: `BookReaderUI` carece de soporte de navegación para Gamepad y Raycast VR
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/BookReaderUI.cs:207-216`
- **Descripción:** La lectura de entradas en `BookReaderUI.Update()` consulta directamente `Keyboard.current` para flechas, PageUp/Down y Escape. No evalúa `Gamepad.current` ni los botones de navegación (`buttonSouth` para Aprender, `buttonEast` para Cerrar, cruceta para páginas). Si un usuario juega con gamepad en PC o con visor sin teclado, no puede avanzar páginas ni presionar el botón "Aprender este conocimiento" sin recurrir obligatoriamente a un ratón físico.
- **Impacto:** PC (Gamepad) / VR: Alto. PC (Teclado/Mouse): Bajo.
- **Alternativas de arreglo:**
  1. Integrar `PlayerInputReader` en `BookReaderUI`: leer `input.PageDelta`, asignar foco inicial con `learn.Focus()` al llegar a la última página y procesar submit con `NavigationSubmitEvent`. (Costo: Bajo, ~1.5 h).
  2. Asignar atajos directos en `Update`: `Gamepad.current.buttonSouth.wasPressedThisFrame` activa `Learn()`, `dpad.left/right` llama a `Step()`, `buttonEast` llama a `Close()`. (Costo: Muy bajo, ~30 min).

---

### LIB-04: Fuente de `BookReaderUI` acoplada por URL relativa en USS en vez de usar `UiFonts.Book`
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/UI/SpellUI.uss:842`
- **Descripción:** En la clase `.reader` de `SpellUI.uss`, la tipografía se define como `-unity-font: url("project:///Assets/_Project/Art/Fonts/Lora-Regular.ttf");`. Esto bypasses `UiFonts.Book` (`Resources.Load<TMP_FontAsset>("Fonts/Lora SDF")`). Si los assets de fuentes se reestructuran o no se empaquetan en disco crudo, la fuente falla silenciosamente al fallback por defecto de Unity, creando inconsistencias entre el texto 3D de TextMeshPro y los paneles de UI Toolkit.
- **Impacto:** PC / VR: Bajo (deuda técnica y riesgo de empaquetado en builds WebGL/Standalone).
- **Alternativas de arreglo:**
  1. Configurar la fuente en el asset `PanelSettings` asignando `Lora SDF` como fuente base para el contexto del lector. (Costo: Bajo, ~45 min).
  2. Asignar programáticamente `document.rootVisualElement.style.unityFontDefinition = new StyleFontDefinition(UiFonts.Book)` en `BookReaderUI.Bind()`. (Costo: Bajo, ~30 min).

---

### LIB-05: Paginación de `BookReaderUI` sin advertencia de páginas no leídas al cerrar
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/BookReaderUI.cs:174-188`, `268`
- **Descripción:** El botón `learn` ("Aprender este conocimiento") solo se muestra cuando `index == pages.Count - 1`. Si el jugador abre un tratado de 4 páginas, lee la primera y cierra el libro con `Esc` o haciendo clic en el backdrop, el libro se cierra sin aprender nada. No hay ninguna pista o indicador de que el conocimiento quedó pendiente de confirmación en la última página salvo el rótulo pequeño `counter`.
- **Impacto:** PC / VR: Medio (frustración de jugadores que creen haber aprendido el glifo al abrir el libro).
- **Alternativas de arreglo:**
  1. Mostrar el botón `learn` siempre visible en la esquina inferior derecha, deshabilitado con tooltip/clase atenuada hasta llegar a la última página. (Costo: Bajo, ~1 h).
  2. Permitir aprender en cualquier página si ya se exploraron todos los glifos, o aprender automáticamente al haber pasado por todas las páginas. (Costo: Medio, ~2 h).

---

### LIB-06: Asignación redundante de aprendizaje al pulsar repetidamente en VR
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/LibraryBook.cs:141`, `255-268`
- **Descripción:** Al pulsar repetidamente un libro en VR, como `BookReaderUI.Open` retorna `false`, se invoca `Learn()` en cada cuadro que se apriete el gatillo. Aunque `DiscoveryState.Read` previene duplicar glifos en el hashset, se vuelve a calcular `BuildPages(info)` (alocando strings y listas), se dispara el evento `BookRead` y se emite el mensaje a `DiagnosticBus`.
- **Impacto:** VR: Bajo (spam en logs y allocations innecesarias). PC: Nulo.
- **Alternativas de arreglo:**
  1. En `LibraryBook.Press()`, verificar primero `if (Discovery.Instance != null && Discovery.Instance.State.HasRead(bookId)) return;` antes de procesar el aprendizaje si se está en modo sin UI. (Costo: Muy bajo, ~15 min).
  2. Agregar cooldown de 1 segundo a las pulsaciones de `LibraryBook`. (Costo: Muy bajo, ~15 min).

---

### LIB-07: Contraste insuficiente en notas y folios de páginas en `BookReaderUI`
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/UI/SpellUI.uss:944-955`, `1034-1050`, `Assets/_Project/Scripts/UI/BookReaderUI.cs:266`
- **Descripción:** En `.note`, el fondo es `rgba(92, 58, 124, 0.1)` y el borde `rgba(92, 58, 124, 0.35)`. Sobre la textura de pergamino amarillento (`#F6EACE`), el texto con color violeta o tinta suave (`#6E5238`) presenta un ratio de contraste de ~3.2:1, por debajo del estándar WCAG AA (4.5:1 para texto normal). En monitores con brillo bajo o visores VR es difícil de leer.
- **Impacto:** PC / VR: Medio (accesibilidad visual).
- **Alternativas de arreglo:**
  1. Ajustar el color de texto de notas a un tono más oscuro (`#321A44` o `#231307`) y subir la opacidad del borde. (Costo: Muy bajo, ~15 min).
  2. Añadir un fondo sólido apergaminado ligeramente más claro en la caja de notas para elevar el contraste. (Costo: Bajo, ~30 min).

---

### LIB-08: Distancia de sujeción mínima descalibrada en `ObjectMover` para el Grimorio
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/ObjectMover.cs:214-216`
- **Descripción:** Al abrir el grimorio con `T`, se ubica a `openDistance = 0.72f` (72 cm de la cámara). Sin embargo, si el jugador pulsa `E` (o grip en VR) para arrastrarlo y reubicarlo, `ObjectMover.Begin` calcula `radius = BoundsRadius(rb) ≈ 0.35m` y define `minHoldDistance = Mathf.Max(0.8f, radius * 1.5f + 0.7f) = 1.225m`. Esto provoca un salto brusco hacia adelante de medio metro (+70% de distancia) en cuanto el libro es tomado. A 1.23 m de distancia, los botones y tipografías se vuelven pequeños y difíciles de apuntar.
- **Impacto:** PC / VR: Alto (incomodidad inmediata al mover el libro).
- **Alternativas de arreglo:**
  1. Permitir que `IRemoteGrabbable` defina una distancia mínima propia opcional (ej. `grabbable.PreferredMinDistance => 0.6f`). (Costo: Bajo, ~45 min).
  2. En `ObjectMover.Begin`, si `rb.GetComponent<IRemoteGrabbable>() != null`, omitir el `HoldMargin` extra de 0.7 m reservado para rocas/cajas grandes. (Costo: Muy bajo, ~20 min).

---

### LIB-09: El golpe de stick en VR (`VrFlickX`) rota la vista 45° en vez de pasar páginas del Grimorio
- **Severidad:** 🔴 Alta
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/PlayerLook.cs:88-92`, `Learning/Grimoire.cs:230-231`
- **Descripción:** Cuando el grimorio está flotando en el mundo frente al jugador en VR, `PlayerLook.HandleSnapTurn` evalúa `if (flick == 0 || (interactor != null && (interactor.HoldsPageable || interactor.IsManipulating))) return;`. Como el grimorio en el aire no está registrado en `inventory.HandSecondary` (no es `HoldsPageable`), el stick derecho ejecuta un snap-turn de 45° rotando todo el cuerpo del jugador y sacando el libro del campo de visión. A su vez, `Grimoire.Update()` lee únicamente `input.PageDelta` (teclado Z/X/flechas), ignorando `VrFlickX`. El jugador VR no puede pasar páginas con el mando sin desorientarse.
- **Impacto:** VR: Crítico. PC: Nulo.
- **Alternativas de arreglo:**
  1. En `PlayerLook.HandleSnapTurn`, agregar condición: `if (Grimoire.Instance != null && Grimoire.Instance.IsOpen) return;`.
  2. En `Grimoire.Update()`, sumar `input.VrFlickX` a `delta` si `PlatformDetector.IsVR`. (Costo combinado: Bajo, ~45 min).

---

### LIB-10: Modo fijo (B) inaccesible: tecla eliminada en `PlayerInputReader` y sin control en VR
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/PlayerInputReader.cs:252-253`, `Learning/Grimoire.cs:25`, `206-219`
- **Descripción:** La documentación (`Docs/GAME_DESIGN.md`, `CLAUDE.md`) y los comentarios en `Grimoire.cs` indican que con `B` se fija el libro frente a la cámara en pantalla casi completa. Sin embargo, en `PlayerInputReader.cs`, la acción `bookFocusAction` fue desvinculada (comentario: `// B eliminada: el grimorio a pantalla completa ahora es Esc`), y en `Grimoire.cs` nunca se consulta `IsTogglingBookFocus`. En PC solo se puede entrar a pantalla completa con `Esc` (lo cual confunde ya que Esc abre la pantalla de pausa). En VR, los mandos Touch no tienen tecla Esc ni mapeo alguno para alternar el modo fijo.
- **Impacto:** PC: Medio (desincronización de controles). VR: Alto (imposible fijar el libro a la vista).
- **Alternativas de arreglo:**
  1. Reincorporar binding de tecla `B` en teclado y botón en Gamepad/VR (o toque secundario del botón menú) para alternar `EnterFocus()` / `ExitFocus()`. (Costo: Bajo, ~1 h).
  2. Agregar un botón diegético visible en el lomo del libro (ej. "Fijar a la vista" con icono de ojo) accionable con raycast/clic. (Costo: Medio, ~2 h).

---

### LIB-11: Grimorio dinámico empujable puede quedar flotando fuera del rango de interacción
- **Severidad:** 🔴 Alta
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/Grimoire.cs:89-93`, `256-273`, `Player/PlayerInteractor.cs:43`
- **Descripción:** Tras el cambio reciente (commit `0229c5b`), el libro es un cuerpo dinámico con collider físico (`isTrigger = false`, masa 0.6). Si el jugador camina contra él, o recibe una explosión/fuerza de viento, el libro sale despedido. Si queda flotando a una distancia entre 3.1 m y 11.9 m:
  - El jugador no puede hacer clic en los botones ni agarrarlo porque `interactRange` en `PlayerInteractor` es de 3.0 m.
  - El libro no se cierra ni vuelve al inventario porque `ReturnDistance` es de 12.0 m.
  Queda en un "limbo" inalcanzable hasta que el jugador se aleje deliberadamente más de 12 metros del lugar.
- **Impacto:** PC / VR: Alto (bloquea el uso del grimorio).
- **Alternativas de arreglo:**
  1. Reducir `ReturnDistance` a ~3.5 m o hacer que al presionar `T` con el libro abierto pero a más de 2 m, se reposicione automáticamente frente a la vista en lugar de solo cerrarse. (Costo: Bajo, ~45 min).
  2. Implementar un anclaje elástico suave hacia la posición relativa del jugador cuando esté suelto para que no se aleje más de 1.5 m. (Costo: Medio, ~2 h).

---

### LIB-12: Rotación física libre puede voltear el Grimorio de espaldas
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/Grimoire.cs:73`, `1230-1236`
- **Descripción:** El Rigidbody del libro tiene `angularDamping = 4f` pero no tiene congelada la rotación en X y Z (`freezeRotation = false`). Un golpe tangencial con una herramienta, cuerpo del jugador o hechizo puede hacerlo girar sobre su eje vertical o tumbarlo boca abajo. Las páginas y botones quedan mirando hacia afuera de la vista, mostrando únicamente la contratapa de cuero con el emblema.
- **Impacto:** PC / VR: Medio (requiere agarrar y manipular con clic derecho para reorientarlo manualmente).
- **Alternativas de arreglo:**
  1. Aplicar en `FixedUpdate` un torque restaurador hacia la orientación del jugador (`Quaternion.Slerp` / muelle angular) cuando no esté agarrado. (Costo: Bajo, ~1 h).
  2. Congelar rotación en X y Z en el Rigidbody (`RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ`), permitiendo únicamente giro suave en Y. (Costo: Muy bajo, ~20 min).

---

### LIB-13: Truncamiento silencioso de texto en pantallas densas del Grimorio
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/Grimoire.cs:82`, `1115-1116`, `1340`, `Learning/GrimoireTexts.cs:205`
- **Descripción:** En `Grimoire.SetBody()`, el tamaño mínimo de autosizing está acotado a `fontSizeMin = size * 0.7f` (para cuerpo: `0.135 * 0.7 = 0.0945`). En `MakeText`, el componente tiene configurado `t.overflowMode = TextOverflowModes.Truncate;`. En pantallas con descripciones extensas (como la tabla de Controles o hechizos avanzados con múltiples párrafos), el texto que no alcanza a reducirse por debajo de 0.0945 se corta sin mostrar advertencia, puntos suspensivos ni barra de desplazamiento.
- **Impacto:** PC / VR: Medio (pérdida de información en tutoriales y reglas).
- **Alternativas de arreglo:**
  1. Permitir que `fontSizeMin` baje hasta `size * 0.5f` y ajustar márgenes de interlineado (`lineSpacing = 4f` en vez de 8f). (Costo: Muy bajo, ~30 min).
  2. Dividir las páginas con exceso de texto en dos spreads consecutivos en `GrimoireTexts.cs`. (Costo: Bajo, ~1.5 h).

---

### LIB-14: Inconsistencias diegéticas en los textos de ayuda del Grimorio
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/Learning/GrimoireTexts.cs:73`, `80-81`
- **Descripción:** En la página "Objetos", el texto indica: `"V la fija a la pantalla para dibujar cómodo."`. En la página "Este libro", dice: `"T lo abre y lo cierra (Esc lo abre a pantalla completa y lo cierra)... Z y X pasan las páginas... Esc lo trae a pantalla completa."`. No se mencionan las flechas de teclado, ni el comportamiento en VR (menú de mano izquierda / stick), y se menciona `Esc` dos veces en vez de la tecla canónica de fijar.
- **Impacto:** PC / VR: Bajo (confusión en jugadores novatos).
- **Alternativas de arreglo:**
  1. Actualizar los textos estáticos en `GrimoireTexts.cs` para reflejar con precisión los controles vigentes acordados en `GAME_DESIGN.md`. (Costo: Muy bajo, ~30 min).

---

## 3. Hallazgos: Otras Interfaces (HUD, Menú Principal, USS, VR) (UI2)

### UI2-01: Accesibilidad y tamaño de tipografías en HUD y avisos (< 14px en pantalla y sub-pixel en VR)
- **Severidad:** 🔴 Alta
- **Archivo:Línea:** `Assets/_Project/UI/SpellUI.uss:57`, `189-192`, `238-242`, `280-285`, `450-460`, `829`
- **Descripción:** Múltiples clases de `SpellUI.uss` utilizan tamaños tipográficos extremadamente reducidos:
  - `.keycap--small`: `11px`
  - `.slot-name`: `12px`
  - `.hint-text`, `.width-label`, `.ink-pct`, `.ink-kind`: `13px`
  - `.notice-text`, `.page-label`: `14px`
  Para PC, el estándar común de legibilidad accesible exige al menos 16-18px para textos funcionales. En VR, donde `VrUiPanel` renderiza el panel de 1920x1080 sobre un plano de 1.7 m a 1.6 m de distancia, una letra de 12px ocupa menos de 2 píxeles efectivos por grado visual en el panel del visor, haciéndola borrosa e ilegible sin acercarse físicamente al quad.
- **Impacto:** PC: Medio. VR: Crítico (textos de inventario y teclas ilegibles).
- **Alternativas de arreglo:**
  1. Elevar la escala global o remuestrear tamaños en `SpellUI.uss`: slots a 15-16px, hints a 16px, keycaps a 16px, avisos a 18px. (Costo: Medio, ~2 h).
  2. Implementar una variable CSS de factor de escala para perfil VR en `VrUiPanel` (`--ui-scale: 1.35`). (Costo: Medio, ~3 h).

---

### UI2-02: Generación constante de basura (GC Allocations) en `HudController.SetHints`
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/HudController.cs:685-700`
- **Descripción:** Cada vez que la mirada del jugador pasa sobre un objeto con pistas diferentes (o deja de mirar un interactuable), `SetHints` ejecuta `row.Clear()` e instancia nuevos elementos DOM: `new VisualElement()`, `new Label(h.Key)`, `new Label(h.Action)`. Si el jugador explora una mesa con plumas, tinteros y papeles moviendo la cámara rápidamente, se generan decenas de allocations de UI Toolkit por segundo en el hilo principal.
- **Impacto:** PC / VR: Medio (micro-tirones por recolección de basura recurrente).
- **Alternativas de arreglo:**
  1. Implementar un pool fijo de `VisualElement` + `Label` keycap + `Label` text (ej. 4 pares fijos) y modificar su visibilidad y texto (`text = ...`, `display = Flex/None`) sin destruir ni instanciar nodos. (Costo: Bajo, ~1.5 h).
  2. Cachear combinaciones completas de hints en templates prearmados. (Costo: Medio, ~2.5 h).

---

### UI2-03: `MainMenu.cs` no admite navegación por sticks analógicos ni thumbsticks de VR
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/MainMenu.cs:452-454`
- **Descripción:** En `MainMenu.HandleKeys()`, la navegación vertical solo consulta: `(k != null && (k.downArrowKey... || k.sKey...)) || (g != null && g.dpad.down.wasPressedThisFrame)`. No se lee el stick analógico del gamepad (`g.leftStick.down`) ni se integra la entrada de mandos de VR. Cualquier jugador que tome un control estándar (Xbox/PlayStation) e intente navegar las opciones con la palanca izquierda no obtiene respuesta.
- **Impacto:** PC (Gamepad): Alto. VR: Medio.
- **Alternativas de arreglo:**
  1. Añadir lectura con zona muerta y umbral de disparo para `g.leftStick.y` con cooldown de repetición (~0.25 s). (Costo: Muy bajo, ~30 min).
  2. Utilizar el sistema de navegación estándar de UI Toolkit (`NavigationMoveEvent`) configurando `navigation.mode = Navigation.Mode.Vertical`. (Costo: Medio, ~2 h).

---

### UI2-04: Animación procedural de motas en `MainMenu.Update` causa repintado y recalculo de estilo en cada frame
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/MainMenu.cs:496-513`
- **Descripción:** `AnimateMotes(now)` modifica `m.style.translate` y `m.style.opacity` en 26 elementos visuales de UI Toolkit cada cuadro en `Update()`. Esto invalida las mallas vectoriales y el render tree del documento UI Toolkit en cada frame a 60-90 Hz.
- **Impacto:** PC / VR: Bajo (sobrecarga en CPU en pantalla de menú).
- **Alternativas de arreglo:**
  1. Reemplazar las motas de UI Toolkit por un sistema de partículas URP liviano o un quad con shader de partículas motas en el fondo 3D de la cámara de órbita. (Costo: Medio, ~2 h).
  2. Bajar la tasa de actualización de motas a 25-30 Hz o animar mediante transitions nativas de USS en bucle. (Costo: Bajo, ~1 h).

---

### UI2-05: `VrControlsPanel` bloquea completamente la visión al iniciar el juego
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/VrControlsPanel.cs:13`, `51-62`, `86`
- **Descripción:** El cartel de controles en VR se abre de manera automática a los 3 segundos de empezar la escena (`AutoOpenDelay = 3f`) y permanece activo durante 40 segundos (`AutoCloseSeconds = 40f`). Mide 1.7 m de ancho por 1.0 m de alto a solo 1.25 m de la cabeza. Al no acompañar al jugador, tapa de inmediato la visión frontal del tutorial y el entorno.
- **Impacto:** VR: Alto (interrupción agresiva de la inmersión al inicio). PC: Nulo.
- **Alternativas de arreglo:**
  1. Desactivar la apertura automática (`autoShown = true` al inicio); mostrar únicamente una notificación pequeña en muñeca o HUD avisando cómo abrirlo ("Mantené stick izq.: controles"). (Costo: Muy bajo, ~15 min).
  2. Reducir `AutoCloseSeconds` a 10 s y permitir cerrarlo instantáneamente con cualquier movimiento o pulsación de botón. (Costo: Muy bajo, ~20 min).

---

### UI2-06: `VrControlsPanel` usa fuente por defecto sin heredar `UiFonts.Body`
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/VrControlsPanel.cs:128-136`
- **Descripción:** En `AddText()`, el componente `TextMeshPro` no asigna `tmp.font = UiFonts.Body`. Utiliza la fuente por defecto del asset TMP (LiberationSans SDF), la cual dispara en consola avisos de caracteres no encontrados (`…`) según KI-142 y genera discrepancia visual con el resto de la interfaz basada en Nunito.
- **Impacto:** VR: Bajo (estética y advertencias en consola).
- **Alternativas de arreglo:**
  1. Asignar `if (UiFonts.Body != null) tmp.font = UiFonts.Body;` al crear cada etiqueta de texto. (Costo: Muy bajo, ~10 min).

---

### UI2-07: Inconsistencia de estados de foco en botones de `BookReaderUI`
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/UI/SpellUI.uss:1060-1110`
- **Descripción:** En los estilos de botones de navegación (`.nav-btn`, `.btn-close`, `.btn-learn`) existen reglas para `:hover` y `:active`, pero no para `:focus`. Al navegar mediante accesibilidad o teclas, no se visualiza qué botón tiene el foco activo.
- **Impacto:** PC / Accesibilidad: Bajo.
- **Alternativas de arreglo:**
  1. Duplicar las pseudo-clases en USS: `.nav-btn:hover, .nav-btn:focus { ... }`. (Costo: Muy bajo, ~15 min).

---

### UI2-08: Doble capa de oscurecimiento simultáneo en `BookReaderUI` y `FocusMode`
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/BookReaderUI.cs:162-163`, `Assets/_Project/UI/SpellUI.uss:860-864`
- **Descripción:** Al abrir `BookReaderUI`, se invoca `FocusMode.Enter()` (que aplica oscurecimiento y desenfoque al fondo del mundo) y al mismo tiempo el elemento `.reader-backdrop` de UI Toolkit aplica un fondo `rgba(12, 6, 18, 0.72)`. El resultado acumulado es un fondo prácticamente opaco (95%+ negro) que desconecta por completo al jugador de la escena 3D.
- **Impacto:** PC / VR: Bajo (estética y sensación de aislamiento).
- **Alternativas de arreglo:**
  1. Reducir `.reader-backdrop` a `rgba(12, 6, 18, 0.25)` o quitar el tinte de fondo de UI Toolkit dejando que `FocusMode` maneje el sombreado visual del mundo. (Costo: Muy bajo, ~15 min).

---

### UI2-09: Posible pérdida de foco y estado del cursor al alternar entre Menú y Grimorio
- **Severidad:** 🟢 Baja
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/MainMenu.cs:253-261`, `Learning/Grimoire.cs:365`, `379`
- **Descripción:** En `MainMenu.OpenInfo()`, se abre el grimorio en pantalla completa y se asigna `InputGate.BookOnly`. Al cerrar el grimorio, `Grimoire.ExitFocus()` llama a `FocusMode.Exit()` (bloqueando el cursor), mientras que `MainMenu.ShowMenu()` vuelve a llamar a `FocusMode.Enter()`. La carrera de eventos en el mismo frame entre desuscripción y callbacks puede dejar el cursor bloqueado con el menú principal activo en pantalla si la animación de fade out se interrumpe.
- **Impacto:** PC: Bajo (desincronización aislada de cursor).
- **Alternativas de arreglo:**
  1. Asegurar en `MainMenu.OnGrimoireClosed` una llamada explícita y forzada a `FocusMode.Enter()`. (Costo: Muy bajo, ~15 min).

---

### UI2-10: Puntero de VR (`VrUiPanel`) escala botones manipulando estilo inline
- **Severidad:** 🟠 Media
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/VrUiPanel.cs:181`, `187`
- **Descripción:** `SetHover()` aplica el resaltado mediante `hovered.style.scale = new StyleScale(...)`. La manipulación inline de estilos invalida transiciones suaves declaradas en USS y si el rayo sale del canvas sin disparar `ClearHover()` (por ejemplo al desactivar el panel), los botones pueden quedar trabados con escala 1.05x permanentemente.
- **Impacto:** VR: Medio (glitches visuales en botones del menú).
- **Alternativas de arreglo:**
  1. Usar clases de USS: `hovered.AddToClassList("mm-btn--hover")` y `hovered.RemoveFromClassList(...)`. (Costo: Muy bajo, ~20 min).

---

## 4. Tabla Resumen de Hallazgos

| ID | Área | Título Corto | Severidad | Impacto | Archivo:Línea |
|---|---|---|:---:|:---:|---|
| **LIB-01** | Libros | Exclusión total del lector de libros en VR | 🔴 Alta | VR | `BookReaderUI.cs:85` |
| **LIB-02** | Libros | Libros del mundo son objetos estáticos (no agarrables) | 🟠 Media | PC/VR | `LibraryBook.cs:16` |
| **LIB-03** | Libros | `BookReaderUI` sin soporte para Gamepad | 🟠 Media | PC/VR | `BookReaderUI.cs:207` |
| **LIB-04** | Libros | Fuente de lector acoplada por URL en USS | 🟢 Baja | Todos | `SpellUI.uss:842` |
| **LIB-05** | Libros | Paginación sin aviso de páginas no leídas | 🟢 Baja | Todos | `BookReaderUI.cs:268` |
| **LIB-06** | Libros | Aprendizaje repetido sin cooldown al pulsar | 🟢 Baja | VR | `LibraryBook.cs:141` |
| **LIB-07** | Libros | Bajo contraste en notas y folios del lector | 🟢 Baja | Todos | `SpellUI.uss:1040` |
| **LIB-08** | Grimorio | Sujeción mínima en `ObjectMover` empuja el libro a 1.23 m | 🟠 Media | PC/VR | `ObjectMover.cs:214` |
| **LIB-09** | Grimorio | Stick en VR gira la cabeza 45° en vez de pasar páginas | 🔴 Alta | VR | `PlayerLook.cs:88` |
| **LIB-10** | Grimorio | Modo fijo (B) inaccesible (tecla B eliminada, sin VR) | 🟠 Media | PC/VR | `PlayerInputReader.cs:252` |
| **LIB-11** | Grimorio | Libro empujable queda varado entre 3 m y 12 m | 🔴 Alta | PC/VR | `Grimoire.cs:89` |
| **LIB-12** | Grimorio | Rotación física libre puede voltearlo de espaldas | 🟠 Media | PC/VR | `Grimoire.cs:1230` |
| **LIB-13** | Grimorio | Truncamiento silencioso de textos densos | 🟠 Media | PC/VR | `Grimoire.cs:1115` |
| **LIB-14** | Grimorio | Textos de ayuda inconsistentes con controles reales | 🟢 Baja | Todos | `GrimoireTexts.cs:73` |
| **UI2-01** | UI | Fuentes de HUD/avisos < 14px ilegibles en VR/pantalla | 🔴 Alta | PC/VR | `SpellUI.uss:57` |
| **UI2-02** | UI | Allocations de GC en `HudController.SetHints` | 🟠 Media | Todos | `HudController.cs:685` |
| **UI2-03** | UI | `MainMenu` no responde a palancas analógicas | 🟠 Media | PC | `MainMenu.cs:452` |
| **UI2-04** | UI | Animación de motas en `Update` repinta UI Toolkit | 🟢 Baja | PC | `MainMenu.cs:496` |
| **UI2-05** | UI | `VrControlsPanel` tapa la visión 40 s al arrancar | 🟠 Media | VR | `VrControlsPanel.cs:13` |
| **UI2-06** | UI | `VrControlsPanel` no usa `UiFonts.Body` | 🟢 Baja | VR | `VrControlsPanel.cs:128` |
| **UI2-07** | UI | Botones del lector sin estado `:focus` | 🟢 Baja | PC | `SpellUI.uss:1060` |
| **UI2-08** | UI | Doble oscurecimiento acumulado (`FocusMode` + USS) | 🟢 Baja | Todos | `BookReaderUI.cs:162` |
| **UI2-09** | UI | Posible carrera de cursor entre Menú y Grimorio | 🟢 Baja | PC | `MainMenu.cs:253` |
| **UI2-10** | UI | `VrUiPanel` altera `style.scale` inline | 🟠 Media | VR | `VrUiPanel.cs:181` |

---

## 5. Propuesta de Orden de Trabajo

### Fase 1: Lo Crítico y Correcciones Rápidas (Sprint Inmediato)
1. **LIB-09:** Bloquear snap-turn en `PlayerLook` si el grimorio está abierto y rutear `VrFlickX` a `Grimoire.Step()`.
2. **LIB-11:** Ajustar distancias de grimorio: retorno si `distance > 3.5m` o reapertura frontal al pulsar `T`.
3. **UI2-05:** Desactivar auto-open de 40 s en `VrControlsPanel`, dejando apertura solo manual.
4. **LIB-08:** Omitir `HoldMargin` en `ObjectMover` para objetos de interfaz como el Grimorio (mantener distancia de agarre en ~0.7 m).
5. **UI2-02:** Pool de elementos para `HudController.SetHints` evitando garbage collection continuo.

### Fase 2: Experiencia y Usabilidad en VR y Gamepad
6. **LIB-01:** Habilitar proyección de `BookReaderUI` en `VrUiPanel` permitiendo lectura interactiva en Quest.
7. **LIB-03 + UI2-03:** Añadir soporte de gamepad/analógico en `BookReaderUI` y `MainMenu`.
8. **LIB-10:** Restaurar atajo canónico para modo fijo del Grimorio en PC y añadir control equivalente en VR.
9. **UI2-01:** Incrementar tamaños tipográficos mínimos en `SpellUI.uss` (mínimo 16px).
10. **LIB-12:** Limitar rotación física del grimorio con constraints y estabilización suave.

### Fase 3: Pulido Visual, Textos y Accesibilidad
11. **LIB-13:** Ajustar autosizing y espaciado de textos densos en `Grimoire.cs`.
12. **LIB-07 + UI2-08:** Calibrar contraste de notas y atenuar el backdrop del lector de libros.
13. **LIB-14:** Sincronizar textos de ayuda en `GrimoireTexts.cs` con la realidad del juego.
14. **UI2-10:** Reemplazar `style.scale` inline por clases CSS en `VrUiPanel`.
15. **LIB-02:** Evaluar mecánica de transporte físico de libros en biblioteca.
