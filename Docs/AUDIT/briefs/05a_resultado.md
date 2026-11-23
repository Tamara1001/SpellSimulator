# Informe de Auditoría 05a: Inventario de Partículas y Presupuesto para Quest (VFX-02)

> **Documento:** `Docs/AUDIT/briefs/05a_resultado.md`  
> **Área:** VFX, Rendimiento GPU Móvil (Meta Quest / PC URP)  
> **Referencia:** `Docs/AUDIT/02_modelado_vfx.md` (Hallazgo VFX-02)  
> **Modo:** Solo lectura (sin edición de código fuente)

---

## 1. Sistema Actual de Calidad (`Rendering/GraphicsQuality.cs`)

### 1.1 Perfiles existentes y parámetros
El sistema define el enum `GraphicsQualityLevel` con 3 niveles:
* **Low (Baja - 0):** `RenderScale = 0.7f`, `ShadowDistance = 15m`, `ShadowCascades = 1`, `SoftShadows = false`, `MsaaSamples = 1`, `Outline = false`, `ParticleScale = 0.5f`.
* **Medium (Media - 1):** `RenderScale = 0.85f`, `ShadowDistance = 30m`, `ShadowCascades = 2`, `SoftShadows = false`, `MsaaSamples = 2`, `Outline = true`, `ParticleScale = 0.75f`.
* **High (Alta - 2):** `RenderScale = 1.0f`, `ShadowDistance = 60m`, `ShadowCascades = 4`, `SoftShadows = true`, `MsaaSamples = 4`, `Outline = true`, `ParticleScale = 1.0f`.

### 1.2 Detección de Quest / VR
* `GraphicsQuality.DefaultFor(RuntimePlatform platform)` asigna `Medium` para `WebGLPlayer` y `Android` (Meta Quest standalone), y `High` para el resto (PC).
* **Limitación actual:** No consulta `PlatformDetector.IsVR`. En PCVR (Quest conectado por Link / AirLink / Virtual Desktop), la plataforma es `WindowsPlayer`/`OSXPlayer`, por lo que asigna `High`, sobrecargando el visor.
* **Estado de `ParticleScale`:** Actualmente solo se utiliza en `ElementVfx.Apply` (línea 58) para modular `maxParticles = Mathf.Max(8, Mathf.RoundToInt(look.maxParticles * GraphicsQuality.ParticleScale))` y en `LabWindVisual.cs` (líneas 302, 342). **No modula `rateOverTime`, ni bursts de hechizos, ni sistemas de fuego/agua de reacciones.**

---

## 2. Inventario de Creación de Partículas en Runtime

1. **`Assets/_Project/Scripts/Output/ElementVfx.cs`:**
   - `Create` (L34-52): Instancia el `GameObject` con `ParticleSystem` Shuriken y ejecuta `Apply`.
   - `Apply` (L55-150): Configura `maxParticles` escalado con `GraphicsQuality.ParticleScale`. Define tamaño, gradientes, turbulencia (Noise), Mesh streams y estelas (Trails).
   - `AddLayer` (L153-168): Duplica la pista para capas concurrentes (espuma, polvo, hojas, estelas) del `ElementLook`.
   - `AddImpacts` (L174-202): Añade módulo de colisión física Shuriken (`Collision3D`, calidad Low) y sub-emisores para cada impacto configurado al chocar y al morir (`crumble`).
   - `CreateImpact` (L204-220): Sub-emisor de impacto instanciado dinámicamente con ráfagas (`Burst`).
   - `OneShot` (L223-242): Instancia un sistema autónomo que emite un burst y se autodestruye (`stopAction = Destroy`). Usado en polvo de rocas.

2. **`Assets/_Project/Scripts/Output/SpellManifestation.cs`:**
   - `Charge` (L96-99): Anillo de carga inicial. Emisión continua: `rateOverTime = 40 * density`.
   - `Burst` (L105-112): Ráfaga del golpe inicial. Emisión única: `burst = 220 * density * (1 + strength) * dampen`. Duplicado en capas.
   - `Jet` (L117-129): Chorro principal cónico. Emisión continua: `rateOverTime = 420 * density * baseDampen`. Capas concurrentes + colisión física + sub-emisores de impacto.
   - `Fan` (L131-139): Abanico circular plano. Emisión continua: `rateOverTime = 360 * density * baseDampen`. Capas concurrentes + colisión física + sub-emisores de impacto.
   - `Vortex` (L141-153): Vórtice espiral. Emisión continua: `rateOverTime = 400 * density * baseDampen` con velocidad orbital.
   - `Hold` (L155-168): Orbe concentrado. Emisión continua: `rateOverTime = 300 * density * baseDampen`.
   - `Intake` (L170-184): Succión esférica. Emisión continua: `rateOverTime = 260 * density * baseDampen`.
   - Pistas de Signos Modificadores (`ApplySignTracks`, L281-641):
     - `Sign_Crush`: Ráfaga repetitiva (`RepeatingBurst`) de ~108 partículas tipo Mesh 3D cada 0.8s.
     - `Sign_Crush_Shock`: Ráfaga repetitiva de 220 partículas estiradas a ras de suelo cada 0.8s.
     - `Sign_Crush_Flash`: Burst de 4 partículas de gran tamaño.
     - `Sign_Orb_Membrane` y `Sign_Orb_Shell`: Cascarón orbital con emisión de 14 a 330 part/s.
     - `Sign_Billowing`: Nube expansiva gigante con `rateOverTime` de hasta 432 part/s y tamaño 3.2x base.
     - `Sign_Cool_Mist`: Vaho denso que cae con emisión de ~270 part/s.
     - `Sign_Empower`: Ráfaga con 15 ciclos a 0.2s y burst de hasta 330 partículas.
     - `Sign_Entwine`: Espiral de doble eje con emisión de ~360 part/s.
     - `Sign_FocusAim`: Haz concentrado tipo láser con emisión de ~540 part/s.
     - `Sign_Gather` / `Collection` / `Repetition`: Ráfagas y flujos continuos de 110 a 220 part/s.
     - `Sign_Weave_A` y `Sign_Weave_B`: Dos hélices entrelazadas simultáneas con emisión total combinada de ~640 part/s.

3. **`Assets/_Project/Scripts/Output/Bodies/WaterSpell.cs`:**
   - `ConfigureDrops` (L52-93): Configura las pistas `Jet`, `Fan` y `Burst` de agua, forzando colisión física `Collision3D` (Low quality), mensajes de colisión y callbacks `Trigger` hacia recipientes.

4. **`Assets/_Project/Scripts/Reactions/BurnEffect.cs`:**
   - `Create` (L11-95): Llamas sobre objetos rígidos. `maxParticles = 160`, `rateOverTime = 20..110 part/s`, mallas de emisión o cajas, partículas estiradas (`Stretch`).
   - `Sparks` (L160-204): Sub-sistema de chispas en llamas de objetos y suelo. `maxParticles = 12`, `rateOverTime = 5 part/s`.
   - `CreateGround` (L113-158): Fuego de pasto por celda. `maxParticles = 60`, `rateOverTime = 38 part/s` (+ chispas).

5. **`Assets/_Project/Scripts/Reactions/WaterEffects.cs`:**
   - `Create` (L14-48): Sistema base de gotas de agua. `maxParticles = 400`, `rateOverTime = 0`.
   - `Burst` (L51-62) y `Stream` (L65-75): Disparos puntuales con `ps.Emit(count)` por cada salpicadura o succión de agua.

6. **`Assets/_Project/Scripts/Reactions/GroundMarks.cs`:**
   - `SpawnDust` (L115-124): Genera ráfagas de polvo al emerger rocas invocando `ElementVfx.OneShot` (`count = impactCount * 3`).

7. **`Assets/_Project/Scripts/World/CrystalShards.cs`:**
   - `Burst` (L9-36): Ráfaga al descristalizar. `maxParticles = 16`, burst de 10 a 14 mallas/esquirlas que se autodestruyen en 0.5s.

8. **`Assets/_Project/Scripts/Lab/LabWindVisual.cs`:**
   - `BuildEmitter` / `ConfigureSystems` (L284-340): Instancia en el Laboratorio 3 sistemas: Estelas (Trails), Hojas (Mesh/Spin) y Polvo (Billboard). Emisión configurada según velocidad del viento con tasas de hasta 60 part/s.

---

## 3. Tabla de Inventario de Partículas

| Archivo:Línea | Efecto / Pista | `maxParticles` Base | Emisión (`rateOverTime` / `burst`) | Tamaño Base | Blending / Overdraw | ¿Tope por Escena? |
|---|---|---|---|---|---|---|
| `SpellManifestation.cs:96` | Carga Hechizo (`Charge`) | `look.maxParticles` (140-260) | Rate: 40 part/s | 0.12 - 0.42 m | Aditivo / Alpha (según elemento) | Máx 6 hechizos activos (`SpellOutputController`) |
| `SpellManifestation.cs:105` | Ráfaga Golpe (`Burst`) | `look.maxParticles` (140-260) | Burst: 220 part. | 0.12 - 0.42 m | Aditivo / Alpha (gran pico instantáneo) | Máx 6 hechizos activos |
| `SpellManifestation.cs:121` | Chorro (`Jet`) | `look.maxParticles` (140-260) | Rate: 420 part/s | 0.12 - 0.42 m | Aditivo / Alpha + Colisión física activa | Máx 6 hechizos activos |
| `SpellManifestation.cs:133` | Abanico (`Fan`) | `look.maxParticles` (140-260) | Rate: 360 part/s | 0.05 - 0.18 m | Aditivo / Alpha + Colisión física activa | Máx 6 hechizos activos |
| `SpellManifestation.cs:143` | Vórtice (`Vortex`) | `look.maxParticles` (140-260) | Rate: 400 part/s | 0.12 - 0.42 m | Aditivo / Alpha (alta densidad angular) | Máx 6 hechizos activos |
| `SpellManifestation.cs:158` | Orbe / Retención (`Hold`) | `look.maxParticles` (140-260) | Rate: 300 part/s | 0.12 - 0.42 m | Aditivo / Alpha | Máx 6 hechizos activos |
| `SpellManifestation.cs:172` | Succión (`Intake`) | `look.maxParticles` (140-260) | Rate: 260 part/s | 0.12 - 0.42 m | Aditivo / Alpha (esfera convergente) | Máx 6 hechizos activos |
| `SpellManifestation.cs:378` | Signo `Billowing` | `look.maxParticles` (140-260) | Rate: ~432 part/s | 0.38 - 1.35 m (3.2x) | Alpha / Muy alto overdraw (volumen opaco) | Máx 6 hechizos activos |
| `SpellManifestation.cs:320` | Signo `Crush Shock` | `look.maxParticles` (140-260) | Burst cíclico: 220 c/0.8s | 0.15 - 0.50 m | Aditivo / Alto overdraw en suelo | Máx 6 hechizos activos |
| `SpellManifestation.cs:456` | Signo `FocusAim` | `look.maxParticles` (140-260) | Rate: ~540 part/s | 0.10 - 0.35 m | Aditivo (haz denso concentrado) | Máx 6 hechizos activos |
| `SpellManifestation.cs:616` | Signo `Weave` (A + B) | `look.maxParticles` (140-260) | Rate combinado: ~640 part/s | 0.20 - 0.70 m | Aditivo / Alpha (doble hélice cruzada) | Máx 6 hechizos activos |
| `ElementVfx.cs:153` | Capas del Look (`AddLayer`) | `layer.maxParticles` (60-140) | Rate: 0.2x tasa de pista base | 0.05 - 0.20 m | Alpha / Estelas / Mallas rotatorias | Multiplica x2 o x3 cada pista |
| `ElementVfx.cs:204` | Impactos (`CreateImpact`) | `impact.maxParticles` (40-80) | Burst: 4 part. por choque | 0.05 - 0.15 m | Sub-emisores dinámicos al colisionar | Tope colisiones: 16 formas/pista |
| `ElementVfx.cs:223` | Ráfaga Suelta (`OneShot`) | `look.maxParticles` (140) | Burst: 9 - 18 part. | 0.10 - 0.30 m | Alpha / Autodestrucción rápida | Máx 24 marcas (`MaxMarks`) |
| `BurnEffect.cs:28` | Fuego en Objeto (`Flames`) | 160 | Rate: 20 - 110 part/s | 0.08 - 0.38 m | Aditivo (Stretch) / Alto overdraw | Sin tope global (1 por objeto en fuego) |
| `BurnEffect.cs:123` | Fuego en Pasto (`GrassFlames`)| 60 | Rate: 38 part/s | 0.28 - 0.60 m | Aditivo / Alto overdraw en suelo | Celdas activas de propagación |
| `BurnEffect.cs:170` | Chispas (`Sparks`) | 12 | Rate: 5 part/s | 0.03 - 0.07 m | Aditivo / Bajo costo | 1 hijo por cada sistema de llamas |
| `WaterEffects.cs:24` | Agua y Salpicaduras | 400 | Rate: 0 (Bursts: 10 - 50 part.)| 0.06 - 0.14 m | Alpha blend suave | 1 por cuerpo/volumen de agua |
| `CrystalShards.cs:24` | Esquirlas Cristal | 16 | Burst: 10 - 14 part. | 0.04 - 0.08 m | Malla 3D / Desaparece en 0.5s | Sin tope (autodestrucción rápida) |
| `LabWindVisual.cs:284` | Viento Lab (Estelas/Hojas/Polvo)| 20 - 80 | Rate: 10 - 60 part/s | 0.05 - 0.50 m | Alpha / Trails / Billboards | 1 rig único en el mapa |

---

## 4. Análisis de Overdraw y Presupuesto por Perfil

### 4.1 Los 3 efectos más caros en Overdraw
1. **`Sign_Billowing` (Nube de expansión / humo / vapor):**  
   Partículas con escalado de 3.2x respecto al tamaño base del look (diámetros de hasta 1.5 metros), con curva de crecimiento continuo y turbulencia de ruido. Al lanzarse en interiores o en combate cercano en VR, ocupa toda la pantalla del jugador, multiplicando la profundidad de dibujado (overdraw) por más de 12 capas transparentes sucesivas.
2. **`Jet` + Capas de estelas + Colisiones de Impacto (Chorro elemental de Fuego / Agua):**  
   Tasa continua de 420 part/s más capas secundarias y sub-emisores en colisión. Con la concurrencia permitida de 6 hechizos activos, genera hasta 3.000 partículas transparentes con colisiones físicas activas simultáneas, superando el ancho de banda térmico de la GPU de Quest.
3. **`Sign_Crush_Shock` + `Sign_Crush` (Onda de choque + Escombros en suelo):**  
   Ráfagas cíclicas cada 0.8s que superan las 300 partículas a ras del suelo, combinando cuádruples planos estirados horizontales con mallas 3D cúbicas en rotación aleatoria. Al intersecar el suelo y el punto de vista del usuario genera severo sobrecosto de rasterizado.

### 4.2 Propuesta de Presupuesto Numérico por Perfil

| Parámetro | Quest / VR (`IsVR = true`) | PC Calidad Baja | PC Calidad Media | PC Calidad Alta |
|---|---|---|---|---|
| **Multiplicador `maxParticles`** | **0.35x** | **0.50x** | **0.75x** | **1.00x** |
| **Multiplicador `rateOverTime` / Bursts** | **0.35x** | **0.50x** | **0.75x** | **1.00x** |
| **Tope Global Partículas Vivas** | **1.200** | **2.500** | **4.500** | **8.000** |
| **Hechizos Activos Simultáneos (`maxActive`)** | **3** | **4** | **5** | **6** |
| **Colisiones de Partículas (`collision.enabled`)**| Solo Agua (gotas) | Sí (Low quality) | Sí (Low quality) | Sí (Low quality) |

---

## 5. Arquitectura de Implementación y Archivos a Modificar

### 5.1 Punto centralizado en `GraphicsQuality.cs`
Se recomienda centralizar toda la modulación en `GraphicsQuality.cs`:
1. **Extender `Preset`:** Incorporar `EmissionScale` (Quest: 0.35, Baja: 0.50, Media: 0.75, Alta: 1.0) y `MaxActiveSpells` (Quest: 3, Baja: 4, Media: 5, Alta: 6).
2. **Integrar `PlatformDetector.IsVR`:** Proveer propiedades estáticas:
   ```csharp
   public static float ParticleScale => PlatformDetector.IsVR ? 0.35f : Get(Current).ParticleScale;
   public static float EmissionScale => PlatformDetector.IsVR ? 0.35f : Get(Current).EmissionScale;
   public static int MaxActiveSpells => PlatformDetector.IsVR ? 3 : Get(Current).MaxActiveSpells;
   ```
3. **Función Helper Estática:**
   ```csharp
   public static void ConfigureBudget(ParticleSystem ps, ref float rate, ref int burst, int baseMax)
   {
       var main = ps.main;
       main.maxParticles = Mathf.Max(4, Mathf.RoundToInt(baseMax * ParticleScale));
       rate *= EmissionScale;
       burst = Mathf.Max(1, Mathf.RoundToInt(burst * EmissionScale));
   }
   ```

### 5.2 Archivos a intervenir al implementar
* `Assets/_Project/Scripts/Rendering/GraphicsQuality.cs`: Incorporar `EmissionScale`, detección de VR y funciones helper de escalado.
* `Assets/_Project/Scripts/Output/ElementVfx.cs`: Aplicar `EmissionScale` en `Create`, `Apply`, `AddLayer`, `CreateImpact` y `OneShot`. Desactivar colisiones secundarias en Quest si no son indispensables.
* `Assets/_Project/Scripts/Output/SpellManifestation.cs`: Escalar las tasas de emisión (`AddBody`) y ráfagas (`AddBurst`, `RepeatingBurst`) de todas las pistas y signos con `GraphicsQuality.EmissionScale`.
* `Assets/_Project/Scripts/Output/SpellOutputController.cs`: Asignar `maxActive = GraphicsQuality.MaxActiveSpells` en lugar de la constante 6.
* `Assets/_Project/Scripts/Reactions/BurnEffect.cs`: Escalar `MaxParticles`, `rateOverTime` y `Sparks` con los multiplicadores de calidad.
* `Assets/_Project/Scripts/Reactions/WaterEffects.cs`: Escalar `MaxParticles` y ráfagas emitidas con `EmissionScale`.
* `Assets/_Project/Scripts/World/CrystalShards.cs`: Escalar la cantidad del burst por `EmissionScale`.
