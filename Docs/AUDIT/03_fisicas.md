# Auditoría Técnica: Área FÍSICAS
**Proyecto:** SpellSimulator (Unity 6 URP)  
**Módulo:** Físicas (Física general, viento, fuego, agua, destrucción del mundo)  
**Fecha:** 2026-11-22  
**Documento:** `Docs/AUDIT/03_fisicas.md`  

---

### Resumen Ejecutivo
Se realizó una auditoría de código estática sobre los módulos de simulación física, propagación y efectos de elementos (Viento, Fuego, Agua, Tierra/Piedra/Cristal), reactividad de materiales y destrucción del mundo en `Assets/_Project/Scripts/Output/`, `Assets/_Project/Scripts/Reactions/`, `Assets/_Project/Scripts/World/` y `Assets/_Project/Scripts/Combat/`. Se identificaron **15 hallazgos** (3 de severidad Alta, 7 de severidad Media y 5 de severidad Baja), abarcando desbordamientos de aceleración sin acotar, atenuación errónea de efectos térmicos por doble aplicación de delta time, desconexión prematura de flotación en colliders compuestos, bypass de invulnerabilidad en estados cristalizados y discrepancias de propagación generacional.

---

## Hallazgos

### FIS-01
- **Severidad:** Alta
- **Tipo:** bug
- **Archivo:** `Assets/_Project/Scripts/Output/MagicField.cs:476`
- **Descripción:** En `WindDrag()`, la aceleración `a` retornada por `WindMath.Drag` se encuentra correctamente acotada a `MaxAcceleration` (45 m/s²), pero luego se multiplica por `(Mathf.Min(strength, 2f) * windGain)` sin volver a acotar. Dado que `windGain` puede alcanzar hasta ~5.2 (dependiendo del área del sello y `WindPower`) y `strength` hasta 2.0, el multiplicador resultante puede ser de hasta 10.4x. Esto permite aceleraciones efectivas superiores a 460 m/s² sobre Rigidbodies, violando el límite máximo del campo y reintroduciendo el comportamiento descontrolado en cuerpos livianos (KI-040).
- **Alternativas de arreglo:**
  1. Envolver la llamada final con un clamp: `Vector3 finalA = Vector3.ClampMagnitude(a * (Mathf.Min(strength, 2f) * windGain), MaxAcceleration); rb.AddForce(finalA, ForceMode.Acceleration);`.
  2. Pasar `(Mathf.Min(strength, 2f) * windGain)` como multiplicador dentro de `WindMath.Drag` para que el clamp interno de `maxAcceleration` cubra la ganancia completa del viento.
  3. Definir una constante `MaxWindAcceleration` independiente y aplicar `Vector3.ClampMagnitude` antes de `rb.AddForce`.

---

### FIS-02
- **Severidad:** Alta
- **Tipo:** bug
- **Archivo:** `Assets/_Project/Scripts/Output/MagicField.cs:349`
- **Descripción:** En `ApplyCool`, la exposición de enfriamiento se aplica mediante `r.AddExposure(chill, Time.fixedDeltaTime * envelope)`. Sin embargo, `ElementReactive.AddExposure` acumula la exposición en `pending`, la cual es consumida en `MagicWorld.Tick` por `ReactionModel.Step`, donde ya se multiplica la tasa por `dt`. Al multiplicar por `Time.fixedDeltaTime` en `MagicField` y luego nuevamente por `dt` en el modelo reactivo, el calor negativo se atenúa al cuadrado (~0.02 * 0.067 = 0.0013x), reduciendo el efecto de enfriamiento a una fracción imperceptible y haciendo que el signo Cool apenas reduzca la temperatura de los objetos.
- **Alternativas de arreglo:**
  1. Modificar la llamada a `r.AddExposure(chill, envelope);`, igualando el estándar del resto del pipeline (`MagicWorld.ExposeToSpells`, `GrassFire.DamageAround`, etc.).
  2. Si se busca modular el enfriamiento por frame físico, acumular la exposición directamente como energía neta sin que `ReactionModel.Step` vuelva a escalarla por tiempo.
  3. Trasladar el cálculo del signo Cool a `MagicWorld.ExposeToSpells` para que se evalúe dentro del tick desacoplado de 15 Hz.

---

### FIS-03
- **Severidad:** Alta
- **Tipo:** bug
- **Archivo:** `Assets/_Project/Scripts/Reactions/WaterVolume.cs:140-161` y `Assets/_Project/Scripts/Reactions/FloodZone.cs:155-174`
- **Descripción:** Falla en el seguimiento de cuerpos rígidos con múltiples colliders (compound colliders). En `OnTriggerEnter`, si entra un segundo collider del mismo Rigidbody, se descarta porque `bodies[i] == rb`. Sin embargo, en `bodyColliders[i]` quedó registrado solo el primer collider. Si ese primer collider sale del volumen de agua mientras los demás colliders del cuerpo continúan sumergidos, `OnTriggerExit` coincide con `bodyColliders[i]` y elimina de inmediato al Rigidbody de la simulación de flotación, privándolo de empuje de Arquímedes y fricción viscosa.
- **Alternativas de arreglo:**
  1. Mantener un conteo de colliders sumergidos por Rigidbody (por ejemplo `Dictionary<Rigidbody, int>` o una estructura de pares) y eliminar el cuerpo de la lista únicamente cuando el conteo llegue a cero.
  2. En `OnTriggerExit`, antes de eliminar el Rigidbody, verificar si alguno de los otros colliders de `rb.GetComponentsInChildren<Collider>()` continúa intersectando los bounds del agua.
  3. Almacenar pares `(Rigidbody, Collider)` permitiendo múltiples entradas de colliders por Rigidbody y sumar la fracción sumergida normalizada en `FixedUpdate`.

---

### FIS-04
- **Severidad:** Media
- **Tipo:** bug
- **Archivo:** `Assets/_Project/Scripts/Reactions/WaterDropSink.cs:53-58`
- **Descripción:** Bloqueo de actualización y falta de reducción en ráfagas de agua con tinta baja. En `WaterDropSink.Update`, la rama para ajustar ráfagas (Burst) es `else if (ps.particleCount > 0 && flowMultiplier > 1f)`. Si el hechizo se emite con tinta baja o agotada (`flowMultiplier < 1f`, por ejemplo 0.5f según `WaterMath.FlowMultiplier`), la condición nunca se satisface. Esto causa que `boostPending` permanezca en `true` chequeando en cada frame innecesariamente, y la ráfaga de agua jamás se reduzca, produciendo siempre el 100% de gotas y charcos independientemente de la tinta.
- **Alternativas de arreglo:**
  1. Cambiar la condición a `else if (ps.particleCount > 0)` y, tras evaluar si `flowMultiplier > 1f` (emitiendo gotas adicionales) o `< 1f` (truncando o no emitiendo), marcar siempre `boostPending = false`.
  2. Escalar el conteo inicial de burst directamente en `SpellManifestation.AddBurst` usando el caudal de la tinta al instanciar el efecto, eliminando la necesidad de corrección posterior en `WaterDropSink.Update`.
  3. Desactivar el componente `WaterDropSink` una vez transcurrido el tiempo de carga (`SinceStrike >= 0`).

---

### FIS-05
- **Severidad:** Media
- **Tipo:** bug / falta
- **Archivo:** `Assets/_Project/Scripts/Combat/Health.cs` y `Assets/_Project/Scripts/Reactions/MagicWorld.cs:96-105`
- **Descripción:** Ausencia de inmunidad a daño directo en entidades con `Health` cristalizadas (KI-107). Aunque `Crystallized.cs` marca `ElementReactive.Crystallized = true` (inmunizando al material de prenderse fuego, mojarse o quebrarse), `MagicWorld.DamageHealth` calcula y aplica `DamageModel.SpellDamage(...)` sobre cualquier objeto con `Health` viva sin verificar si está bajo los efectos de `Crystallized`. En consecuencia, muñecos de entrenamiento (`TrainingDummy`) o coberturas cristalizadas continúan recibiendo daño directo de hechizos y pueden morir/romperse durante el periodo en que deberían ser completamente indestructibles.
- **Alternativas de arreglo:**
  1. En `MagicWorld.DamageHealth`, añadir la comprobación: `if (health.TryGetComponent(out World.Crystallized crystal) || health.GetComponentInParent<World.Crystallized>() != null) continue;`.
  2. Implementar una propiedad `Health.IsInvulnerable` que `Crystallized.Begin` y `Restore` activen y desactiven.
  3. Modificar `Health.Damage(amount)` para ignorar reducciones de vida si el GameObject contiene un componente `Crystallized` activo.

---

### FIS-06
- **Severidad:** Media
- **Tipo:** bug
- **Archivo:** `Assets/_Project/Scripts/Reactions/WaterVolume.cs:186-190`
- **Descripción:** Flotabilidad anómala de objetos densos y masivos en el lago. En `WaterVolume.BuoyancyOf`, cualquier Rigidbody que no posea un componente `ElementReactive` o cuyo perfil no declare flotabilidad recibe `defaultBuoyancy = 1.2f`. A diferencia de `FloodZone` (que utiliza `WaterMath.MaxFloatMass = 8f`), `WaterVolume.FixedUpdate` aplica empuje hacia arriba a cualquier cuerpo sin importar su peso. Como resultado, rocas de 500 kg, cofres pesados o escombros sin perfil reactivo flotan en la superficie en lugar de hundirse al fondo del agua.
- **Alternativas de arreglo:**
  1. Incorporar el chequeo de masa máxima en `WaterVolume.FixedUpdate`: si `rb.mass > WaterMath.MaxFloatMass`, limitar la flotabilidad a un valor que no supere la gravedad (p. ej. 0 o factor de hundimiento).
  2. Asignar dinámicamente un `MaterialProfile` de piedra (`buoyancy = 0.2f`) a todos los props densos y rocas generadas por hechizos.
  3. Calcular la flotabilidad por defecto estimando la densidad volumétrica: `defaultBuoyancy = Mathf.Clamp01(volume / rb.mass)`.

---

### FIS-07
- **Severidad:** Media
- **Tipo:** bug / mejora
- **Archivo:** `Assets/_Project/Scripts/Reactions/WaterVolume.cs:224-233`
- **Descripción:** Muestreo espacial deficiente en la detección de hechizos que contactan el agua. `ProbeSpells` genera una cuadrícula fija de 3x3 puntos sobre los bounds del agua (`water.size / 4f`). En lagos de tamaño medio o grande (15x15m o más), la distancia entre muestras consecutivas es de 4 a 5 metros. Un chorro focalizado de agua o fuego con radio de 0.5m-1.5m impactando sobre la superficie casi siempre caerá entre los puntos de muestreo fijos y lejos de `closest`, resultando en `bestExposure <= 0.05f` e ignorando el impacto (no se generan ondas, vapor ni salpicaduras).
- **Alternativas de arreglo:**
  1. Trazar un rayo desde `field.Frame.Origin` en la dirección principal `field.MainDirection` hacia el plano `y = SurfaceY` para ubicar el centro exacto del impacto sobre el agua.
  2. Muestrear en abanico o círculo concéntrico alrededor de la proyección del sello sobre el plano del agua, acotado por `field.ReachRadius`.
  3. Emplear un `Physics.Raycast` o `OverlapBox` usando la máscara del agua para registrar el punto de colisión directo.

---

### FIS-08
- **Severidad:** Baja
- **Tipo:** inconsistencia / bug
- **Archivo:** `Assets/_Project/Scripts/Reactions/GrassFire.cs:541` y `Assets/_Project/Scripts/Reactions/GrassFire.cs:14`
- **Descripción:** Discrepancia entre la documentación y la implementación del decaimiento de fuego (KI-125). El comentario de clase indica que "desde maxGenerations en adelante ya no propagan", pero la condición en `SpreadStep` es `if (g > maxGenerations) continue;`. Esto hace que una celda de generación `g == maxGenerations` continúe contagiando a sus vecinas, engendrando celdas con `g = maxGenerations + 1`. El fuego avanza así una generación adicional respecto al límite nominal.
- **Alternativas de arreglo:**
  1. Modificar la condición a `if (g >= maxGenerations) continue;` para que las celdas de generación `maxGenerations` no inicien nuevos focos.
  2. Actualizar la documentación y la UI del Laboratorio para clarificar que `maxGenerations` define el último nivel emisor y que las celdas quemadas alcanzan `maxGenerations + 1`.

---

### FIS-09
- **Severidad:** Media
- **Tipo:** falta / estabilidad
- **Archivo:** `Assets/_Project/Scripts/World/MeshBreaker.cs:50-71` y `Assets/_Project/Scripts/World/Breakable.cs:64-70`
- **Descripción:** Inestabilidad estructural y costo síncrono en la destrucción del entorno con Wall Breaker (KI-123). Al abrir brechas en estructuras:
  1) Los elementos soportados (techos, vigas, adornos de la torre) no cuentan con soporte dinámico ni chequeo de integridad, quedando suspendidos en el aire tras recortar las paredes base.
  2) La reasignación de `cutCollider.sharedMesh = cutMesh` fuerza una cocción síncrona de PhysX en el hilo principal de Unity, generando pausas perceptibles en mallas con alto conteo de polígonos.
- **Alternativas de arreglo:**
  1. Agregar detectores de soporte o componentes de colapso en techos y vigas para que caigan como escombros con `Rigidbody` al perder contacto con la pared.
  2. Limitar el ritmo de corte a una tasa máxima por segundo o pre-cocinar piezas destructibles por bloques (pre-fractura Voronoi).
  3. Asegurar que las mallas base asignadas a `Breakable` sean modelos de colisión de baja resolución independientes del renderizado visual.

---

### FIS-10
- **Severidad:** Baja
- **Tipo:** bug / caso borde
- **Archivo:** `Assets/_Project/Scripts/World/Flutter.cs:33-34`
- **Descripción:** Cancelación de turbulencia lateral en caída vertical libre. En `Flutter.FixedUpdate`, la fuerza lateral se calcula con `Vector3.Cross(v / speed, Vector3.up)`. Si una pluma o papel cae con velocidad orientada exactamente hacia abajo (`v = -Vector3.up`), el producto cruz resulta en `Vector3.zero`, anulando completamente el vaivén horizontal y haciendo que el objeto descienda en línea recta hasta que una fuerza externa lo desestabilice.
- **Alternativas de arreglo:**
  1. Comprobar si `side.sqrMagnitude < 0.01f`, y en ese caso usar `Vector3.Cross(v / speed, Vector3.forward)` o un vector aleatorio generado en base a `phase`.
  2. Introducir un vector de orientación inherente al objeto (`transform.right`) para derivar las fuerzas de sustentación aerodinámica.

---

### FIS-11
- **Severidad:** Baja
- **Tipo:** inconsistencia / código muerto
- **Archivo:** `Assets/_Project/Scripts/Output/LevitationMath.cs:40-45` y `Assets/_Project/Scripts/Output/MagicField.cs:325-327`
- **Descripción:** Parámetros sin efecto y lógica duplicada en la aceleración vertical de levitación. `LevitationMath.VerticalAcceleration` acepta parámetros `gravity` y `cancel` para contrarrestar el peso (`gravity * cancel`). Sin embargo, `MagicField.cs` envía `0f, 0f` a la función, calculando luego el vector de compensación de gravedad `weight = -Physics.gravity * cancel * ...` por separado en el llamador. Esto vuelve redundantes los dos primeros argumentos de la función pura y confunde la responsabilidad del cálculo.
- **Alternativas de arreglo:**
  1. Quitar los parámetros `gravity` y `cancel` de `LevitationMath.VerticalAcceleration`, documentando que calcula exclusivamente la respuesta del resorte amortiguado.
  2. Integrar el vector completo de gravedad en la función matemática y retornar la aceleración total resultante.

---

### FIS-12
- **Severidad:** Media
- **Tipo:** fuga de referencias / estado
- **Archivo:** `Assets/_Project/Scripts/Output/Bodies/EarthShapes.cs:41-45, 186-191`
- **Descripción:** Falta de reseteo de estructuras estáticas en `EarthShapes` durante el reinicio del mundo. `EarthShapes` mantiene listas estáticas `live`, `temporary` y `permanent`. Mientras que `ExpiringBody` destruye los GameObjects en `OnWorldReset`, `EarthShapes` no se suscribe a `MagicWorld.WorldReset`. Como consecuencia, la lista `live` retiene referencias a GameObjects destruidos que solo se purgan al invocar nuevos métodos, y la propiedad estática `SolidPiece.Count` carece de reinicio en cambios de dominio de Enter Play Mode.
- **Alternativas de arreglo:**
  1. Suscribir `EarthShapes` al evento `MagicWorld.WorldReset` para limpiar explícitamente `live.Clear()`, `temporary.Clear()` y `permanent.Clear()`.
  2. Incorporar un método estático con atributo `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` que restablezca `SolidPiece.Count = 0` y vacíe las colecciones estáticas.

---

### FIS-13
- **Severidad:** Media
- **Tipo:** limitación / bug silencioso
- **Archivo:** `Assets/_Project/Scripts/Output/MagicField.cs:20-22` y `Assets/_Project/Scripts/Reactions/MagicWorld.cs:27-28`
- **Descripción:** Truncamiento silencioso de colisiones en zonas de alta densidad por límite rígido de buffer. Tanto `MagicField` como `MagicWorld` preasignan buffers estáticos `hits` de tamaño 64 y ejecutan `Physics.OverlapSphereNonAlloc` con máscara `~0` (todas las capas). En sectores con alta acumulación de props como el Archivo (20+ lienzos, exhibidores, plumas, triggers y frascos), una esfera de radio 5-8 m puede superar con facilidad los 64 colliders. Los colliders excedentes son descartados silenciosamente por Unity, provocando que objetos reactivos y cuerpos físicos no respondan a los efectos de los hechizos.
- **Alternativas de arreglo:**
  1. Elevar `MaxHits` a 128 o 256 en ambos módulos para tolerar escenas densas.
  2. Restringir la máscara de capas (`LayerMask`) para consultar únicamente capas interactivas y reactivas en lugar de `~0` (filtrando terreno estático, decoración y UI).
  3. Agregar una comprobación condicional en modo debug: si `count == MaxHits`, registrar una advertencia de saturación de buffer.

---

### FIS-14
- **Severidad:** Baja
- **Tipo:** optimización
- **Archivo:** `Assets/_Project/Scripts/Reactions/GroundReactions.cs:64-77`
- **Descripción:** Disparo de raycasts redundantes hacia el suelo. En cada ciclo de sondeo de `GroundReactions.Probe`, se evalúan tres direcciones fijas: `directions[0] = field.MainDirection`, `directions[1] = frame.Normal` y `directions[2] = Vector3.down`. En lienzos apoyados horizontalmente en el suelo, `frame.Normal` es `Vector3.up` o `Vector3.down`, provocando que las direcciones 1 y 2 sean colineales o directamente paralelas a la gravedad, duplicando innecesariamente las llamadas a `Physics.Raycast` contra el terreno.
- **Alternativas de arreglo:**
  1. Verificar si `Vector3.Dot(frame.Normal, Vector3.down) > 0.9f`, y en tal caso saltar la tercera dirección de sondeo.
  2. Cachear el resultado de contacto del frame anterior si el hechizo y el lienzo se mantienen estacionarios.

---

### FIS-15
- **Severidad:** Baja
- **Tipo:** falta
- **Archivo:** `Assets/_Project/Scripts/Output/Bodies/WindMath.cs:14` y `Assets/_Project/Scripts/Output/MagicField.cs:468`
- **Descripción:** Falta de soporte de coeficientes de arrastre aerodinámico (`Cd`) por material (KI-108). Todos los cuerpos aplican `WindMath.DefaultCd = 1f`. Cuerpos esféricos, planos o irregulares (como rocas, plumas o telas) experimentan el mismo factor aerodinámico por defecto, dependiendo únicamente del área proyectada de su caja envolvente.
- **Alternativas de arreglo:**
  1. Añadir el campo `public float dragCoefficient = 1f;` en `MaterialParams` y `MaterialProfile`.
  2. En `MagicField.WindDrag`, leer `reactive.Profile.parameters.dragCoefficient` si el cuerpo cuenta con `ElementReactive`, usando `DefaultCd` como respaldo.
