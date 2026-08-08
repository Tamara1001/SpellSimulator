# 🪄 SpellSimulator — Simulador de Ejecución de Hechizos

> Evaluación Final — Asignatura: **Simuladores**  
> Motor: **Unity 3D (URP)** | IA: **Unity Sentis (ONNX)** | Target: **PC → Oculus Quest (VR)**

---

## Descripción General

**SpellSimulator** es un simulador interactivo de físicas y sistemas de partículas activado mediante el trazado en tiempo real de diagramas geométricos sobre una superficie virtual. El usuario dibuja runas o formas sobre un "lienzo" 3D; un modelo de inteligencia artificial local reconoce la geometría trazada y el sistema compila las reglas correspondientes para instanciar efectos físicos visuales.

El prototipo corre en PC (Mouse/Teclado) y está diseñado para una transición limpia hacia Realidad Virtual en **Oculus Quest**, con una arquitectura estrictamente modular que lo permite sin refactorización masiva.

---

## 🧰 Tech Stack

| Capa | Tecnología |
|---|---|
| Motor Gráfico | Unity 3D |
| Pipeline de Renderizado | URP (Universal Render Pipeline) |
| Inferencia de IA | Unity Sentis (local, sin red) |
| Modelo de Reconocimiento | ResNet18 exportado como `.onnx` |
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

**Responsabilidad:** Clasificar la geometría dibujada usando el modelo ResNet18 local.

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

**Responsabilidad:** Instanciar y dirigir los sistemas de partículas en base al `SpellDescriptor`.

- Los efectos visuales son grafos de VFX Graph parametrizados.
- Recibe los parámetros compilados (tipo, escala, dirección) e inyecta los valores en el grafo activo.
- Gestiona el ciclo de vida de los efectos (instanciación, duración, destrucción).

---

## 📁 Estructura de Carpetas (Assets/)

```
Assets/
├── Scripts/
│   ├── Canvas/          # Módulo 1: DrawingCanvas.cs, BrushStamper.cs
│   ├── Input/           # Módulo 2: MouseInputHandler.cs, VRInputHandler.cs
│   ├── Inference/       # Módulo 3: SentisInferenceEngine.cs, TensorUtils.cs
│   ├── Compiler/        # Módulo 4: SpellCompiler.cs, SpellDescriptor.cs, AST/
│   └── Output/          # Módulo 5: SpellOutputController.cs
├── Models/              # Archivos .onnx (ResNet18 y variantes)
├── VFX/                 # VFX Graph assets (Fire, Air, etc.)
├── Shaders/             # Shader Graph: BrushMaterial, CanvasComposite
├── Scenes/
│   ├── PrototypePC.unity
│   └── VRDeployment.unity
├── Settings/            # URP Renderer, Pipeline Assets
└── Tests/               # PlayMode y EditMode tests por módulo
```

> ⚠️ Esta estructura es la **convención objetivo**. Las carpetas se crearán a medida que se implementen los módulos.

---

## ⚙️ Directivas de Código

Estas reglas aplican a **todos** los scripts del proyecto:

1. **Sin GC en Update:** Cero allocations dinámicas dentro de `Update()`, `FixedUpdate()` o `LateUpdate()`. Pre-alocar buffers y reutilizarlos.
2. **Encapsulamiento estricto:** Estado interno `private` o `protected`. Configuración de inspector con `[SerializeField]`.
3. **Matemática vectorial nativa:** Usar `Vector3`, `Vector2`, `Mathf`, y operaciones de Unity en lugar de bucles manuales sobre componentes.
4. **Interfaz mínima por módulo:** Cada módulo expone sólo los métodos necesarios para su integración. Sin dependencias cruzadas directas.

---

## 🗺️ Roadmap

| Fase | Objetivo | Estado |
|---|---|---|
| **Fase 0** | Setup del proyecto, URP, estructura de carpetas | 🔄 En curso |
| **Fase 1** | Módulo Canvas + Input (PC) funcional | ⬜ Pendiente |
| **Fase 2** | Integración Sentis + modelo ResNet18 | ⬜ Pendiente |
| **Fase 3** | Compilador lógico (MVP: Fuego, Aire, Escala) | ⬜ Pendiente |
| **Fase 4** | VFX Graph: efectos parametrizados | ⬜ Pendiente |
| **Fase 5** | Port a XR Interaction Toolkit (Oculus Quest) | ⬜ Pendiente |

---

## 🚀 Cómo correr el prototipo (PC)

1. Abrir el proyecto en **Unity 6** (o la versión LTS compatible con Sentis).
2. Cargar la escena `Assets/Scenes/PrototypePC.unity`.
3. Asegurarse de que el modelo `.onnx` está en `Assets/Models/`.
4. Presionar **Play**. Hacer clic y arrastrar sobre el plano del lienzo para dibujar.
5. Soltar el botón para disparar la inferencia y ver el efecto compilado.

---

## 📄 Licencia

Proyecto académico — Evaluación Final, Asignatura Simuladores.  
No distribuir sin autorización del autor.
