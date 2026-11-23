# Auditoría Técnica: Área PVP (Multijugador y Duelos en PC)

> **Fecha:** 2026-11-22  
> **Área:** PVP, Multijugador en red, sincronización, lobby, IA rival y modo Duelo  
> **Documentación de referencia:** `Docs/STATUS.md`, `Docs/KNOWN_ISSUES.md`, `Docs/MULTIPLAYER_PLAN.md`, `Docs/ARCHITECTURE.md`

---

## Resumen Ejecutivo

La infraestructura multijugador implementada sobre Netcode for GameObjects (NGO) y Unity Transport (WebSocket / Relay) presenta una base sólida para la sincronización de trazos y estado autoritativo en el anfitrión (F1–F5). Sin embargo, el área de **PVP y Duelo** exhibe brechas críticas de diseño e implementación:
1. **Ausencia total de IA rival (`AICaster`)**: El modo Duelo en solitario no existe; requiere obligatoriamente otro jugador conectado por red.
2. **Desincronización visual ante correcciones del servidor**: El cliente no recibe notificación cuando el servidor re-lee y modifica el elemento o sigilo de su propuesta de hechizo.
3. **Inmunidad física y de quemadura en duelistas**: No se aplican daños por colisión física (`ImpactDamage`) ni quemaduras por fuego continuo (`BurnDamage`).
4. **Carencia de herramientas en la arena**: No se suministran plumas ni tinteros en la arena aérea (Y=100), y no se ha implementado el mazo de lanzamiento rápido (`SealDeck`).
5. **Falta de compensación de latencia y gestión de caídas al vacío**.

A continuación se detallan los 16 hallazgos identificados.

---

## Tabla de Hallazgos

| ID | Severidad | Tipo | Archivo:Línea | Resumen |
|---|---|---|---|---|
| **PVP-01** | 🔴 Alta | Falta | `Assets/_Project/Scripts/Net/World/NetDuel.cs:138` | Inexistencia de IA rival (`AICaster`) y de modo Duelo en solitario offline |
| **PVP-02** | 🔴 Alta | Bug | `Assets/_Project/Scripts/Net/World/NetCast.cs:188` | Falta de notificación y corrección visual al cliente cuando el servidor re-lee un hechizo |
| **PVP-03** | 🔴 Alta | Bug | `Assets/_Project/Scripts/Net/World/NetDuel.cs:156` | Inmunidad total de los duelistas a impactos físicos (`ImpactDamage`) y quemaduras |
| **PVP-04** | 🟠 Media | Bug | `Assets/_Project/Editor/NetSetup.cs:78` | El avatar remoto (`NetPlayer`) carece de colliders, afectando colisiones y centro de `Health` |
| **PVP-05** | 🟠 Media | Bug | `Assets/_Project/Scripts/Net/World/DuelArena.cs:31` | Caída fuera de la arena aérea (Y=100) sin volumen de muerte ni reposicionamiento |
| **PVP-06** | 🔴 Alta | Falta | `Assets/_Project/Scripts/Net/World/NetDuel.cs:185` | La arena de Duelo no provee herramientas de dibujo (plumas/pinceles) para los jugadores |
| **PVP-07** | 🟠 Media | Falta | `Docs/MULTIPLAYER_PLAN.md:417` | Falta del mazo de sellos preparados (`SealDeck`) para combate dinámico en tiempo real |
| **PVP-08** | 🟠 Media | Falta | `Assets/_Project/Scripts/Net/World/NetCast.cs:142` | Ausencia de compensación de latencia en la fase de carga y manifestación de hechizos |
| **PVP-09** | 🟠 Media | Bug | `Assets/_Project/Scripts/Net/World/NetCanvasSync.cs:458` | Desincronización permanente de trazos extendidos descartados sin re-sincronización periódica |
| **PVP-10** | 🟢 Baja | Mejora | `Assets/_Project/Scripts/Net/World/DuelCover.cs:34` | Las coberturas se destruyen desapareciendo sin partículas, escombros ni audio |
| **PVP-11** | 🟠 Media | Bug | `Assets/_Project/Scripts/Net/NetSession.cs:289` | Desconexión del anfitrión aborta el duelo abruptamente sin declarar abandono (forfeit) |
| **PVP-12** | 🟠 Media | Bug | `Assets/_Project/Scripts/Net/NetPlayer.cs:48` | Posible excepción no controlada por desborde de 32 bytes UTF-8 en `FixedString32Bytes` con el nombre |
| **PVP-13** | 🟢 Baja | Bug | `Assets/_Project/Scripts/Net/World/NetDuel.cs:322` | El ángulo de inclinación vertical de cámara (`pitch`) no se resetea al teletransportar a la arena |
| **PVP-14** | 🟠 Media | Falta | `Assets/_Project/Scripts/Net/NetHud.cs:18` | Interfaz de Duelo basada en IMGUI incompatible con visualización en cascos VR/Quest |
| **PVP-15** | 🟢 Baja | Mejora | `Assets/_Project/Scripts/Net/World/NetRateLimiter.cs:30` | Riesgo de descarte de sellos compuestos legítimos por el limitador de tasa de lanzamiento |
| **PVP-16** | 🟠 Media | Falta | `Assets/_Project/Scripts/UI/MainMenu.Net.cs:116` | Carencia de sala de espera previa (Lobby con estado "Listo") y navegador de partidas públicas |

---

## Detalle de Hallazgos

### PVP-01: Inexistencia de IA rival (`AICaster`) y de modo Duelo en solitario offline
- **Severidad:** 🔴 Alta
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetDuel.cs:138` (y `Assets/_Project/Scripts/UI/MainMenu.Net.cs:55`)
- **Descripción:** El modo Duelo sólo puede jugarse si se conecta un segundo cliente humano a través de red (`ConnectedRival`). La clase `AICaster` prevista en la arquitectura multijugador (`MULTIPLAYER_PLAN.md` §4 y `ROADMAP.md` D2) no fue implementada. Si un usuario hace clic en "Modo Duelo" en el menú principal, se le abre la ventana para hospedar una sala Relay esperando a otro usuario. Los bots de `NetBot.cs` son únicamente guiones de pruebas batch automatizadas para el harness, no oponentes interactivos para un jugador humano.
- **Alternativas de arreglo:**
  1. Implementar `AICaster` como un agente en el servidor que opere sobre un `IPlayerContext` simulado (ID reservado ej. 255), con una máquina de estados para desplazarse entre coberturas de la arena y dibujar proceduralmente sellos usando `StampPattern` o plantillas del diccionario.
  2. Implementar un modo de entrenamiento contra un bot local cinemático que dispare hechizos predeterminados a intervalos regulares usando `SpellOutputController.SpawnSpellEffect`.
  3. Deshabilitar temporalmente el botón de Modo Duelo para un solo jugador o añadir una advertencia en la UI indicando que requiere 2 jugadores conectados.

---

### PVP-02: Falta de notificación y corrección visual al cliente cuando el servidor re-lee un hechizo en Duelo
- **Severidad:** 🔴 Alta
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetCast.cs:188-193`
- **Descripción:** En modo Duelo, el servidor no confía en la clasificación del cliente y re-lee el lienzo con su modelo Sentis. Si la lectura del servidor difiere de la propuesta del cliente (ej. el cliente propuso Fuego pero el servidor dictamina Tierra), el servidor ejecuta su versión y la anuncia a todos los clientes *excepto* al lanzador (`Announce` filtra `id != except`). Como consecuencia, el cliente lanzador sigue reproduciendo su predicción visual desincronizada (ej. partículas y VFX de fuego), mientras que en el mundo del servidor y del rival se manifiesta tierra. Además, si el hechizo es rechazado por falta de tinta (`ProposalsNoInk`), el servidor retorna silenciosamente sin avisar al cliente, dejando el efecto predictivo activo en su pantalla.
- **Alternativas de arreglo:**
  1. Crear un mensaje con nombre `sc.correct` (`MCorrect`) que el anfitrión envíe al cliente lanzador cuando `corrected` sea true, permitiéndole actualizar o sustituir su `SpellManifestation` local por la corregida.
  2. Enviar un acuse de recibo de casteo (`sc.cast_ack`) que confirme la ejecución o notifique rechazo (falta de tinta / fuera de ronda), destruyendo la manifestación predictiva en caso de rechazo.
  3. Modificar `Announce` para incluir al lanzador únicamente cuando `isCorrection` sea true, indicando que debe sustituir su manifestación local.

---

### PVP-03: Inmunidad total de los duelistas a impactos físicos (`ImpactDamage`) y quemaduras
- **Severidad:** 🔴 Alta
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetDuel.cs:156-162` (y `Assets/_Project/Scripts/Reactions/MagicWorld.cs:103-105`)
- **Descripción:** La función `NetDuel.HealthOf()` añade dinámicamente el componente `Health` al objeto raíz del jugador pero no le agrega `ImpactDamage`. Por ende, las colisiones con rocas lanzadas (`RockSpell`), cajas arrojadas o escombros no infligen daño a los jugadores. Asimismo, ningún jugador posee el componente `ElementReactive`, de modo que `MagicWorld.DamageHealth()` nunca puede aplicar el daño continuo por quemadura (`BurnDamage = 4/s`), anulando una mecánica clave del elemento Fuego descrita en el diseño de juego.
- Alternativas de arreglo:
  1. En `NetDuel.HealthOf()`, añadir también el componente `ImpactDamage` y enlazarlo con la tabla de balance `DamageTable`.
  2. Añadir `ElementReactive` básico al prefab del jugador o registrar una interfaz `IDamageReceiver` que admita estados de quemadura y congelamiento.
  3. Modificar `MagicWorld.DamageHealth()` para evaluar exposición térmica sobre la posición del jugador sin requerir estrictamente `ElementReactive`.

---

### PVP-04: El avatar remoto (`NetPlayer`) carece de colliders, afectando colisiones y centro de `Health`
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Editor/NetSetup.cs:78` (y `Assets/_Project/Scripts/Combat/Health.cs:37`)
- **Descripción:** En la generación del avatar (`NetSetup.cs`), se eliminan intencionalmente todos los colliders (`Object.DestroyImmediate(go.GetComponent<Collider>())`). Esto causa dos problemas: 1) en `Health.Awake()`, `body = GetComponentInChildren<Collider>()` resulta `null`, por lo que `Health.Center` evalúa a `transform.position` (nivel del suelo/pies) en lugar del torso del rival; 2) los proyectiles de física y rayos de hechizos atraviesan al jugador remoto sin colisionar con él.
- **Alternativas de arreglo:**
  1. Mantener un `CapsuleCollider` en el avatar configurado como Trigger o en una capa de física que colisione con hechizos y proyectiles sin bloquear el movimiento físico.
  2. Ajustar `Health.Center` para que, en caso de no hallar collider, añada un offset vertical (ej. `Vector3.up * 0.9f`) para centrar el cálculo de exposición.
  3. Agregar un componente `PlayerHitbox` específico en `NetPlayer` dedicado al cálculo de impactos de magia.

---

### PVP-05: Caída fuera de la arena aérea (Y=100) sin volumen de muerte ni reposicionamiento
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/DuelArena.cs:31-38` (y `Assets/_Project/Scripts/Player/PlayerSpawn.cs:45`)
- **Descripción:** La arena de duelo es una plataforma suspendida a 100 metros de altura en el cielo con muros perimetrales de 2.5 metros. Si un jugador es expulsado de la arena por una columna de viento, levitación o explosión, cae 100 metros hacia el terreno de la pradera. Dado que el límite de caída del juego (`PlayerSpawn.FellOut`) está calibrado para Y < -20, el jugador aterriza intacto en la pradera y queda atrapado sin poder regresar a la arena ni morir, forzando a esperar a que expire el tiempo de la ronda.
- **Alternativas de arreglo:**
  1. Instanciar en `DuelArena.Build()` un collider trigger `KillPlane` horizontal a Y=80 que, al ser atravesado por un jugador, invoque `health.Damage(9999f)` o fuerce `Respawn`.
  2. En `NetDuel.Tick()`, verificar si la posición Y de algún contendiente cae por debajo de `center.y - 10f`, otorgando la ronda al rival inmediatamente por caída al vacío.
  3. Incrementar la altura de los muros perimetrales de la arena a 10 metros o colocar un muro invisible superior que impida salir del recinto.

---

### PVP-06: La arena de Duelo no provee herramientas de dibujo (plumas/pinceles) para los jugadores
- **Severidad:** 🔴 Alta
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetDuel.cs:185-206`
- **Descripción:** Al iniciar la ronda, `SpawnPapers()` clona 6 lienzos de papel (`PaperTemplate`), pero no se genera ninguna herramienta de dibujo (`PhysicalTool`: pluma, pincel, lápiz) en la arena. Al comenzar una partida multijugador desde el menú principal, `PlayerSpawn.Begin()` invoca `interactor.ClearHands()`, por lo que los duelistas aparecen en la arena sin pluma equipada y no tienen forma de dibujar en los lienzos disponibles.
- **Alternativas de arreglo:**
  1. En `NetDuel.SpawnPapers()`, clonar e instanciar junto a los papeles al menos una pluma (`PenTemplate`) para cada jugador.
  2. Al teletransportar a los contendientes en `Respawn()`, equipar automáticamente en su mano principal la pluma predeterminada si su mano está vacía.
  3. Añadir atriles fijos en la arena con herramientas incrustadas que se repongan automáticamente con cada ronda.

---

### PVP-07: Falta del mazo de sellos preparados (`SealDeck`) para combate dinámico en tiempo real
- **Severidad:** 🟠 Media
- **Tipo:** Falta
- **Archivo:Línea:** `Docs/MULTIPLAYER_PLAN.md:417`
- **Descripción:** El diseño de Duelo especifica un mazo de 3 sellos preparados (`SealDeck`) alimentados desde las estampas del grimorio. Sin este componente, los jugadores deben agacharse, interactuar con un papel físico suelto en el suelo y dibujar a pulso con el ratón durante 8 a 15 segundos mientras se encuentran bajo fuego enemigo, lo que resulta inviable durante un enfrentamiento dinámico.
- **Alternativas de arreglo:**
  1. Implementar la clase pura `SealDeck` con 3 ranuras asociadas a teclas de acceso rápido (1, 2, 3) que permitan estampar un sello instantáneo en el papel que se sostiene.
  2. Proveer estampas (`Stamp`) precargadas en los pedestales de la arena para lanzamiento con un solo click.
  3. Implementar un selector radial rápido en pantalla que permita seleccionar hechizos conocidos del grimorio para estampar.

---

### PVP-08: Ausencia de compensación de latencia en la fase de carga y manifestación de hechizos
- **Severidad:** 🟠 Media
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetCast.cs:142-154`
- **Descripción:** La planificación multijugador (ADR-035) indicaba que el servidor debía descontar el retraso de ida y vuelta (RTT/2, hasta 150 ms) del tiempo de carga del sello (`SealCharge`) para compensar el lag de red. En la implementación actual, `MPropose` no envía marca de tiempo y el servidor ejecuta el hechizo comenzando la carga en su propio tiempo local, acumulando un retraso visible de 100–300 ms en la manifestación de efectos entre clientes.
- **Alternativas de arreglo:**
  1. Serializar el tiempo de emisión en `MPropose` y ajustar `activatedAt` y la duración del efecto restando la latencia calculada por el transporte.
  2. Adelantar la simulación de partículas y fuerzas (`FastForward`) en el servidor en una fracción igual al RTT medio reportado por `UnityTransport`.
  3. Iniciar la animación de resplandor (`SealFeedback`) en el receptor con aceleración temporal si el mensaje llegó con retardo.

---

### PVP-09: Desincronización permanente de trazos extendidos descartados sin re-sincronización periódica
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetCanvasSync.cs:458`
- **Descripción:** En la recepción de extensiones de trazos (`OpExtend`), si el número de puntos acumulados localmente no coincide con el índice de partida (`st.Points.Count != from`), el mensaje es descartado con la premisa de que `"la próxima instantánea lo arregla"`. Sin embargo, `SendFull` (la instantánea completa) solo se transmite una vez cuando un cliente ingresa a la sesión. Si un paquete de extensión se desordena o descarta por fluctuaciones de red, el trazo queda permanentemente incompleto en ese cliente, provocando que un sello cerrado sea visto como abierto.
- **Alternativas de arreglo:**
  1. Implementar una solicitud de recuperación `RequestCanvasSyncRpc(canvasId)` cuando se detecte una discrepancia en `st.Points.Count != from`.
  2. Concatenar los puntos recibidos incluso si difieren de `from`, o aplicar una reconciliación por interpolación entre los puntos conocidos.
  3. Enviar un hash o versión periódica de cada lienzo activo; si no coincide, forzar el envío de la instantánea completa de ese lienzo.

---

### PVP-10: Las coberturas se destruyen desapareciendo sin partículas, escombros ni audio
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/DuelCover.cs:34-37`
- **Descripción:** Al destruirse una cobertura en la arena (`SetBroken(true)`), el código únicamente desactiva su `Renderer` y `Collider` (`shownRenderer.enabled = !broken; solid.enabled = !broken;`). No se activan efectos de fractura de malla, ni partículas de escombros, ni avisos sonoros de impacto estructural, restando impacto visual a la destrucción del escenario.
- **Alternativas de arreglo:**
  1. Invocar `Shatter.Break()` en la posición de la cobertura al romperse para generar fragmentos físicos de piedra.
  2. Disparar un sistema de partículas de polvo/roca prefabricado y un cue sonoro a través de `GameAudio`.
  3. Mostrar una etapa previa de modelo agrietado cuando la vida de la cobertura baje del 50%.

---

### PVP-11: Desconexión del anfitrión aborta el duelo abruptamente sin declarar abandono (forfeit)
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/NetSession.cs:289-301` (y `Assets/_Project/Scripts/Combat/DuelMatch.cs:85-91`)
- **Descripción:** Si el rival se desconecta, el anfitrión ejecuta `match.Forfeit(1)` y gana la partida limpiamente. Sin embargo, si el que se desconecta o cierra la sala es el anfitrión, el cliente ejecuta `DropConnection()`, resetea el mundo y vuelve al menú principal con un aviso de error genérico. El duelo no concluye formalmente en el cliente ni se le muestra la victoria por abandono.
- **Alternativas de arreglo:**
  1. En `DropConnection()`, verificar si había un `DuelMatch` en curso y disparar una pantalla de final de partida mostrando victoria por abandono del anfitrión antes del reset.
  2. Guardar el estado de la última partida en `PlayerPrefs` o logs para permitir visualizar el historial tras la desconexión.
  3. Enviar un mensaje de cierre ordenado antes de que el host invoque `Shutdown()`.

---

### PVP-12: Posible excepción no controlada por desborde de 32 bytes UTF-8 en `FixedString32Bytes` con el nombre
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/NetPlayer.cs:48` (y `Assets/_Project/Scripts/UI/MainMenu.Net.cs:134`)
- **Descripción:** En el spawn del avatar de red se ejecuta `playerName.Value = new FixedString32Bytes(NetSession.PlayerName)`. Si el jugador ingresa en la interfaz un nombre cuya longitud en bytes UTF-8 sea superior a 32 (posible con caracteres con tilde, diéresis o cadenas de texto largas), Unity lanza una `ArgumentException` durante el arranque de red, abortando el spawn y desestabilizando la sesión.
- **Alternativas de arreglo:**
  1. Limitar el `maxLength` del campo de texto `netName` en `MainMenu.Net.cs` a 16 caracteres.
  2. Sanitizar y recortar el string en bytes UTF-8 seguros antes de pasarlo al constructor de `FixedString32Bytes`.
  3. Reemplazar `FixedString32Bytes` por `FixedString64Bytes` en `NetPlayer`.

---

### PVP-13: El ángulo de inclinación vertical de cámara (`pitch`) no se resetea al teletransportar a la arena
- **Severidad:** 🟢 Baja
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetDuel.cs:322-335`
- **Descripción:** Al reposicionar a los jugadores al comienzo de cada ronda (`TeleportLocal`), `Move` fija la posición del Rigidbody y la rotación horizontal (`yaw`), pero no reinicia la variable privada `pitch` de `PlayerLook`. Si el duelista estaba mirando hacia el suelo o hacia arriba al momento de la teletransportación, aparece en la arena conservando esa inclinación vertical en lugar de mirar horizontalmente hacia el rival.
- **Alternativas de arreglo:**
  1. Agregar un método público en `PlayerLook` (ej. `ResetPitch()`) e invocarlo dentro de `TeleportLocal`.
  2. Restablecer la rotación local del `cameraTransform` a `Quaternion.identity` y colocar `pitch = 0f`.
  3. Alinear automáticamente el vector de vista hacia el centro del oponente en la fase de cuenta regresiva (`Countdown`).

---

### PVP-14: Interfaz de Duelo basada en IMGUI incompatible con visualización en cascos VR/Quest
- **Severidad:** 🟠 Media
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/NetHud.cs:18-39` (y `Assets/_Project/Scripts/Net/World/NetDuel.cs:341-372`)
- **Descripción:** Toda la información del combate (vidas, barra de tinta, cuenta regresiva, victorias por ronda y anuncios de ganador) se dibuja mediante Unity IMGUI (`OnGUI`). Los cascos de realidad virtual como Meta Quest bajo URP no proyectan la capa IMGUI en los displays estereoscópicos, dejando a los usuarios de VR sin interfaz visual durante el duelo.
- **Alternativas de arreglo:**
  1. Migrar el HUD de Duelo a un Canvas en espacio de mundo anclado frente a la cámara o en el antebrazo del avatar cuando `PlatformDetector.IsVR` sea verdadero.
  2. Representar la vida y la tinta de forma diegética en el mundo (ej. runas luminosas en los pilares de la arena o un marcador flotante en la tribuna).
  3. Utilizar UI Toolkit con soporte para World Space Panels sobre la arena.

---

### PVP-15: Riesgo de falsos positivos en el limitador de tasa de cast ante sellos compuestos
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora
- **Archivo:Línea:** `Assets/_Project/Scripts/Net/World/NetRateLimiter.cs:30` (y `Assets/_Project/Scripts/Net/World/NetCast.cs:174`)
- **Descripción:** El limitador de tasa para `NetChannel.Cast` tiene una recarga de 5 fichas por segundo y un tope de ráfaga de 8. Un sello compuesto complejo con 2 o 3 anillos interiores cerrados simultáneamente envía múltiples mensajes casi instantáneos. Si el usuario reintenta un casteo o encadena sellos rápidos, el limitador descarta parte de las propuestas legítimas como tráfico abusivo.
- **Alternativas de arreglo:**
  1. Aumentar el tope de ráfaga para `NetChannel.Cast` a 15 fichas.
  2. Empaquetar todos los interiores de un sello compuesto en una única propuesta de red consolidada en lugar de disparar una propuesta por cada interior.
  3. Eximir del rate limiting a propuestas que apunten al mismo lienzo ya autorizado recientemente (`ChargedRecently`).

---

### PVP-16: Carencia de sala de espera previa (Lobby con estado "Listo") y navegador de partidas públicas
- **Severidad:** 🟠 Media
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/MainMenu.Net.cs:116-132`
- **Descripción:** No existe un flujo de lobby previo con botón de confirmación ("Listo" / "Ready") ni selector de opciones de duelo (rondas a ganar, tiempo por ronda). En el momento en que el segundo jugador introduce el código de Relay y se conecta, la partida arranca automáticamente teletransportando a ambos contendientes con una cuenta regresiva de sólo 3 segundos, sin dar tiempo a prepararse ni comprobar latencia.
- **Alternativas de arreglo:**
  1. Agregar una pantalla intermedia de Lobby donde ambos jugadores deban pulsar "Listo" antes de que el anfitrión active el inicio del duelo.
  2. Permitir al anfitrión configurar las reglas (`DuelRules`) en la interfaz del menú antes de crear la sala.
  3. Integrar un indicador visual del ping/RTT en la interfaz de la sala previa al inicio del combate.
