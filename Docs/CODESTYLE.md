---
tags: [doc]
---
# Code Style

Convenciones **observadas en el código existente** + reglas del proyecto. Ante la duda, imitar el archivo vecino.

## Rendimiento (Quest es el piso)
1. **Cero GC en `Update/FixedUpdate/LateUpdate`.** Nada de `new` de clases/arrays, LINQ, lambdas que capturan,
   concatenación de strings, `GetComponent` ni `Find*` por frame. Cachear en `Awake`.
2. El trabajo pesado (limpieza, inferencia, compilación) ocurre **sólo al castear**, nunca por frame.
3. Lienzo por GPU: `RenderTexture` + `GL`/`Graphics.DrawTexture`. Prohibido `SetPixel` en el lienzo.
   Minimizar cambios de `RenderTexture.active` (tile-based GPU del Quest).
4. Pasar datos entre módulos como `struct` cuando es por valor (`SpellIR`).

## Estructura y encapsulamiento
- Un tipo público por archivo, nombre de archivo = nombre del tipo (excepción: archivos de contratos
  como `SpellDescriptor.cs`, `InteractionInterfaces.cs`, `DrawingData.cs`).
- Namespace `SpellSimulator.<Modulo>` igual a la carpeta (`Canvas`, `Core`, `Compiler`, `Inference`, `Input`,
  `Output`, `Player`, `World`, `Interfaces`, `DebugTools` para `Debug/`).
- Estado `private`; configuración de Inspector con `[SerializeField] private` + `[Tooltip]`/`[Header]`/`[Range]`.
- Exponer lo mínimo: propiedades `{ get; private set; }` o métodos. Sin campos públicos en MonoBehaviours.
- Lógica pura en **clases `static`** sin dependencias de escena (ver `Core/`), testeadas en EditMode.
- Dependencias entre módulos por `[SerializeField]` a interfaz/componente o eventos (`DiagnosticBus`).
  Evitar `FindObjectOfType`/`FindFirstObjectByType` en runtime (ver KI-009).
- `[RequireComponent]` cuando un componente depende de otro en el mismo GameObject.

## Nombres
- Tipos, métodos, propiedades: `PascalCase`. Campos privados y locales: `camelCase` (sin `_` ni `m_`).
- Constantes: `PascalCase` (`private const float FocusBase`).
- Campos públicos de `struct` de datos (`SpellIR`): `camelCase` (portados de la referencia TS).
- Vocabulario del dominio consistente: *stroke, session, ring, sigil, sign, glyph, cast*. No inventar sinónimos.
- Identificadores en **inglés**; comentarios, logs y docs en **español**.

## Comentarios y logs
- `/// <summary>` en clases y API pública: qué hace y por qué. Detalles de implementación inline.
- Comentarios explican el *por qué*; no narrar el *qué*.
- Logs con prefijo de módulo entre corchetes: `[Compilador]`, `[VFX Output]`, `[PlayerInteractor]`, `[Harness]`.
- Los textos sin tildes en código existente se toleran (encoding); en código nuevo se pueden usar (UTF-8).

## Plataforma
- Bifurcar PC/VR **sólo** leyendo `PlatformDetector.IsVR`. Mantener la feature funcionando en ambos.
- Input nuevo: agregar la acción en `PlayerInputReader` con bindings para teclado/mouse, gamepad y XR.

## Unity
- Assets propios bajo `Assets/_Project/` según [[STRUCTURE]]. Prefabs para todo lo que vive en escena.
- No editar YAML de escenas/prefabs a mano. Usar el Editor / Unity MCP.
- Constantes de tuning portadas de la referencia: citar el archivo de origen en un comentario.

## Checklist antes de cerrar
- [ ] Compila sin errores ni warnings nuevos.
- [ ] Tests EditMode verdes; lógica pura nueva tiene test.
- [ ] Sin allocations por frame.
- [ ] Funciona (o no rompe) en PC y VR.
- [ ] Docs afectadas actualizadas + [[CHANGELOG]].
