# 🪄 SpellSimulator — Simulador de Ejecución de Hechizos

Sandbox de hechicería en **Unity 6** donde dibujás círculos y runas en un lienzo, una red neuronal local reconoce los glifos, un compilador los convierte en un hechizo y el mundo reacciona con físicas: el fuego quema la madera, el viento arrastra objetos y la tierra levanta muros. Funciona en PC (mouse y teclado), en el navegador (WebGL), con **multijugador en red** (cooperativo y duelo 1 contra 1) y está diseñado para **VR (Quest)**.

> Proyecto de Tamara D'Angelo y Alejo Warner. Es una **versión en Unity 3D / VR, con mundo físico y multijugador, del proyecto fan [wha-spell-simulator](https://github.com/cosykid/wha-spell-simulator)** de cosykid, inspirado en el manga *Witch Hat Atelier*. Ver [Créditos](#-créditos-y-origen).

> **¿Vas a continuar el proyecto?** Empezá por [`Docs/ONBOARDING.md`](Docs/ONBOARDING.md) (instalación, primer arranque, verificación y builds) y [`Docs/STATUS.md`](Docs/STATUS.md) (estado actual).
> Estado: modo **Libre** jugable en PC (menú principal, torre, pradera, grimorio, hechizos, VFX); build **WebGL** para itch.io; multijugador por Relay (F0–F5); **Quest** pendiente (M7).

## De un vistazo
- **Alcance:** unas 52.000 líneas de C# (374 scripts) en más de 100 commits, con documentación de arquitectura, decisiones (ADRs), changelog y problemas conocidos en [`Docs/`](Docs/Home.md).
- **Lo que se hizo en este repo (Unity):**
  - Lienzo de dibujo en GPU con `RenderTexture` e interacción FPS pensada para PC y VR.
  - Rasterizado de trazos hacia el tensor del modelo, con inferencia local en Unity Sentis (GPU en PC y Quest, CPU en WebGL).
  - Compilador de hechizos y mundo reactivo con físicas: fuego que se propaga, viento, tierra, agua, runas con desgaste y fusión, portales y reciclaje de objetos.
  - Inventario, tinta, grimorio y sellos, todo diegético, sin menús 2D.
  - Multijugador con Netcode for GameObjects y Relay: mundo compartido, dibujo y hechizos replicados, y duelo 1v1 al mejor de 3.
- **Calidad:** 473 tests de EditMode, smoke tests de Play, matriz de lienzos (425 chequeos), pruebas con entrada real y bots de red con latencia simulada. Se corren desde el menú `SpellSimulator > Harness`.
- **Origen de la IA de reconocimiento:** el modelo de glifos (CNN destilada exportada a ONNX), el diccionario de runas y la lógica del parser y del compilador vienen del proyecto de cosykid y se portaron a C#. El detalle de qué es fiel y qué se reinterpretó está en [`Docs/REFERENCE.md`](Docs/REFERENCE.md).
- **Uso de IA en el desarrollo:** el proyecto se desarrolló con asistencia de Claude Code (ver [`CLAUDE.md`](CLAUDE.md) y [`AGENTS.md`](AGENTS.md)), con tests y documentación obligatorios por cambio.

> Proyecto de la asignatura **Simuladores** (Evaluación Final).
> Motor: **Unity 3D (URP)** | IA: **Unity Sentis (ONNX)** | Target: **PC y WebGL → Oculus Quest (VR)**

---

## Descripción General

**SpellSimulator** es un simulador interactivo **sandbox** de hechicería impulsado por físicas reactivas y reconocimiento de patrones. El usuario dibuja runas o formas geométricas complejas en tiempo real; un modelo de inteligencia artificial local (Sentis) reconoce la geometría trazada y el sistema compila las reglas correspondientes para instanciar proyectiles, efectos físicos y reacciones en cadena.

A diferencia de un RPG tradicional, el enfoque de este proyecto es la **maestría mecánica y la interactividad emergente**. El entorno funciona como un laboratorio de pruebas donde el fuego quema la madera, el viento arrastra objetos, y los elementos físicos reaccionan de manera realista a los impactos mágicos.

El prototipo corre en PC (Mouse/Teclado) con un avanzado sistema híbrido de interacción FPS, diseñado para una transición limpia y directa hacia Realidad Virtual en **Oculus Quest**.

---

## 🧰 Tech Stack

| Capa | Tecnología |
|---|---|
| Motor Gráfico | Unity 3D |
| Pipeline de Renderizado | URP (Universal Render Pipeline) |
| Inferencia de IA | Unity Inference Engine / Sentis (local, sin red; GPU en PC/Quest, CPU en WebGL) |
| Modelo de Reconocimiento | GlyphStudent (CNN destilada 96×96) exportada como `.onnx` |
| Renderizado de Interacción | `RenderTexture` + GPU Blitting |
| Salida Visual / Física | VFX Graph + Shader Graph |
| Módulo VR (Fase Futura) | XR Interaction Toolkit |

---

## 🏗️ Arquitectura Modular

El sistema está dividido en **5 módulos desacoplados**. Cada módulo expone una interfaz pública mínima y no tiene conocimiento directo de sus vecinos, salvo a través de esa interfaz.

```
┌─────────────────────────────────────────────────────────────────┐
│                         FLUJO DE DATOS                          │
│                                                                 │
│  [Input]──UV coords──▶[Canvas]──RenderTexture──▶[Sentis]        │
│                                                      │          │
│                                               Predictions       │
│                                                      │          │
│                                             [Compiler/AST]      │
│                                                      │          │
│                                              Spell Params       │
│                                                      │          │
│                                              [VFX Output]       │
└─────────────────────────────────────────────────────────────────┘
```

### Módulo 1 — Motor de Trazado (Canvas Dinámico)

**Responsabilidad:** Renderizar trazos continuos sobre un plano 3D de forma eficiente en GPU.

- Utiliza `RenderTexture` y `Graphics.DrawTexture` para estampar un material de pincel.
- **Prohibido** el uso de `Texture2D.SetPixel` o cualquier acceso iterativo por CPU a píxeles.
- Implementa interpolación **Lerp** entre posiciones UV consecutivas para cubrir los gaps generados por altas velocidades de entrada entre fotogramas.
- **Interfaz pública:** `DrawAt(Vector2 uvPosition)` — el lienzo no sabe nada del origen de la coordenada.

### Módulo 2 — Controlador de Entrada (Input)

**Responsabilidad:** Capturar la intención del usuario y traducirla a coordenadas UV del lienzo.

| Fase | Implementación |
|---|---|
| Prototipo (PC) | `Physics.Raycast` desde cámara principal, disparado por clic del mouse |
| Producción (VR) | Colisión física del periférico XR contra el collider del lienzo |

- El módulo no conoce al Canvas directamente; entrega las coordenadas UV a través de un evento o referencia a interfaz.

### Módulo 3 — Motor de Inferencia (Sentis)

**Responsabilidad:** Clasificar la geometría dibujada usando el modelo local de reconocimiento (GlyphStudent, ver [Créditos](#-créditos-y-origen)).

1. Captura la `RenderTexture` del Canvas al finalizar el trazo.
2. Convierte la imagen a `Tensor` en el formato esperado por el modelo.
3. Ejecuta la inferencia local con **Unity Sentis** (sin llamadas a red).
4. **Salida:** vector de probabilidades → clase dominante (ej. `Fuego`, `Aire`, `ModificadorEscala`).

### Módulo 4 — Intérprete Lógico (Compilador / AST)

**Responsabilidad:** Traducir las predicciones a un árbol de sintaxis abstracta (AST) que define el hechizo a ejecutar.

**Reglas del MVP:**
- Detecta condición de **cierre** (trazo que forma un anillo continuo).
- Procesa hasta **2 elementos primarios** (`Fuego`, `Aire`) y **1 modificador** (`Escala`).
- Emite un objeto `SpellDescriptor` con los parámetros compilados hacia el Módulo 5.

### Módulo 5 — Motor Físico de Salida (VFX Output)

**Responsabilidad:** Instanciar y dirigir los sistemas de partículas y proyectiles en base al `SpellDescriptor`.

- Los efectos visuales son grafos de VFX Graph parametrizados.
- Recibe los parámetros compilados (tipo, escala, dirección) e inyecta los valores para generar proyectiles físicos o efectos de área.
- Gestiona el ciclo de vida de los efectos (instanciación, duración, destrucción).

### Módulo 6 — Entorno Reactivo (Sandbox)

**Responsabilidad:** Proveer un ecosistema de físicas y materiales que reaccionen a la salida del Módulo 5.

- **Destrucción y Físicas:** Integración de `Rigidbody` a todos los objetos. Aplicación de fuerzas radiales (explosiones), fuerzas centrípetas (Tornados) y fracturas dinámicas en el escenario.
- **Propagación:** Objetos inflamables (`Flammable.cs`) que esparcen el fuego, o tornados que absorben elementos cercanos (ej: Tornado de Fuego).
- **Dummies Ragdoll:** Muñecos de prueba articulados para evaluar de forma realista el impacto cinético y destructivo de los hechizos sin depender de rutinas de Inteligencia Artificial enemiga.

---

## 📖 Sistemas Inmersivos y Mecánicas (QoL)

Para maximizar la inmersión sin depender de Interfaces de Usuario (UI) tradicionales en 2D, el simulador emplea sistemas 100% diegéticos:

- **Sistema de Dos Manos (Dual Wielding):** El jugador puede sostener un lienzo/grimorio dinámico en la mano izquierda, mientras utiliza herramientas físicas (lápiz o goma) con la mano derecha. El tamaño del lienzo es completamente dinámico y escalable en tiempo real sin deformar los trazos.
- **El Grimorio (Diccionario):** Un libro físico sostenido en la mano izquierda que actúa como archivo de conocimiento mágico. Registra los glifos descubiertos exactos que debes dibujar. Incluye animaciones automáticas de tinta en sus páginas para enseñar al jugador el orden y velocidad de los trazos requeridos.
- **Sellos Mágicos en Blanco (Stamps):** Herramientas físicas de maestría. El jugador puede transferir el conocimiento de un glifo desde una página del Grimorio hacia un "Sello en Blanco". Al estampar este sello contra un lienzo, se dibuja instantáneamente el patrón perfecto, agilizando la experimentación.
- **Retroalimentación Háptica (Haptics):** Los controladores (Gamepad/VR) reaccionan a la fricción de la tinta y emiten un pulso contundente al compilar el hechizo exitosamente.
- **Activación canónica:** como en Witch Hat Atelier, el hechizo se lanza al **cerrar el anillo**. El input secundario de las herramientas (click derecho / botón A) deshace el último trazo.

---

## 📁 Estructura de Carpetas

Todo el código y assets propios viven en `Assets/_Project/` (scripts en `Scripts/<Modulo>/`, asmdef `SpellSimulator.Runtime`).
Mapa completo y actualizado en [`Docs/STRUCTURE.md`](Docs/STRUCTURE.md). Arquitectura detallada en [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md).

> 📚 La documentación completa está en [`Docs/`](Docs/Home.md) (se puede abrir como vault de Obsidian).
> Reglas para agentes de IA: [`CLAUDE.md`](CLAUDE.md).

---

## ⚙️ Directivas de Código

Estas reglas aplican a **todos** los scripts del proyecto:

1. **Sin GC en Update:** Cero allocations dinámicas dentro de `Update()`, `FixedUpdate()` o `LateUpdate()`. Pre-alocar buffers y reutilizarlos.
2. **Encapsulamiento estricto:** Estado interno `private` o `protected`. Configuración de inspector con `[SerializeField]`.
3. **Matemática vectorial nativa:** Usar `Vector3`, `Vector2`, `Mathf`, y operaciones de Unity en lugar de bucles manuales sobre componentes.
4. **Interfaz mínima por módulo:** Cada módulo expone sólo los métodos necesarios para su integración. Sin dependencias cruzadas directas.

---

## 🗺️ Roadmap

PC y VR (Quest) se desarrollan en paralelo. Roadmap detallado y vivo en [`Docs/ROADMAP.md`](Docs/ROADMAP.md);
bugs y deuda técnica en [`Docs/KNOWN_ISSUES.md`](Docs/KNOWN_ISSUES.md).

---

## 🚀 Cómo correr el prototipo

1. Abrir el proyecto con **Unity 6000.3.14f1**.
2. Cargar `Assets/_Project/Scenes/Escena.unity`.
3. **Play**. En PC: `WASD` + mouse, `E` agarra/suelta (lápiz a la derecha, lienzo a la izquierda),
   click izquierdo dibuja y click derecho deshace. Dibujá el sigilo y los signos y **cerrá el anillo al final**:
   el hechizo se lanza solo.
4. En VR (Quest Link/OpenXR): grip agarra, gatillo dibuja, botón A deshace.
5. Tests: menú `SpellSimulator > Harness > Run EditMode Tests` (resultado en `Logs/harness-editmode.txt`).

---

## 🙏 Créditos y origen

- **Tamara D'Angelo** ([@Tamara1001](https://github.com/Tamara1001)) y **Alejo Warner**: desarrollo del proyecto en Unity.
- **[cosykid/wha-spell-simulator](https://github.com/cosykid/wha-spell-simulator)** (GPL-3.0): proyecto fan en el que se basa este. De ahí provienen el modelo de reconocimiento de glifos (`Assets/_Project/Data/glyph-recognizer.onnx`), el diccionario de sigilos y signos, y la lógica del parser y del compilador de hechizos, que acá se portó a C#. Gracias a su autor por publicarlo.
- **[Kenney](https://kenney.nl)**: efectos de sonido (RPG Audio, Impact Sounds, Interface Sounds), licencia CC0.
- *Witch Hat Atelier* y sus nombres, símbolos y marcas pertenecen a sus titulares. Este proyecto es **no oficial, hecho por fans**, con fines educativos, y no está afiliado, respaldado ni patrocinado por sus creadores ni editores.

---

## 📄 Licencia

Este proyecto es una obra derivada de [wha-spell-simulator](https://github.com/cosykid/wha-spell-simulator) y se distribuye bajo la **GNU General Public License v3.0**. Ver [`LICENSE`](LICENSE).

Los audios de Kenney mantienen su licencia CC0 (con su `License.txt` en cada carpeta). El manga *Witch Hat Atelier* y su contenido no están incluidos ni licenciados por este repositorio.
