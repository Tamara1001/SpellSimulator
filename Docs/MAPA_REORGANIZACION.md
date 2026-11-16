# Reorganización del mapa del modo Libre — propuesta

Solo propuesta: no se tocó código, escena ni datos. Números calculados con `ValleyShape`, `MapStructures.Sites()`,
`WorldZones.json` (sus x/z se reemplazan al construir por la posición resuelta) y `MapOverrides.json` (29 entradas).
Unidades: metros, X este, Z norte. Spawn (0,-6), torre (0,16). Radio jugable 68 m (~14.500 m²), montañas desde 64 m.

## 1. Diagnóstico

| Zona | Pos (x,z) | Tamaño aprox. | Contenido | Dist. torre | Dist. spawn | Vecino más cercano |
|---|---|---|---|---|---|---|
| Torre (loma) | 0, 16 | loma r 20 (1.250 m²) | biblioteca, estudio, alquimia, mapa | 0 | 22 | cabaña N 31 |
| Entrenamiento | 16, -46 | plano r 8 (200 m²) | blancos, muñecos, atril, mesas | **64** | 43 | santuario 21 |
| Laboratorio | 47, -27 | plataforma r 5,2 + pista 12×10 (~120 m²) | consola, pista, toldo | **64** | **52** | santuario 16 |
| Santuario | 34, -36 | r 3,8 | cambio de runas/papeles | 62 | 45 | laboratorio 16 |
| Círculo de piedras (portal) | 50, -10 | r 5,8 | portal `field-circle` | 56 | 50 | laboratorio 17 |
| Pradera / campo viejo | 28, -6 | plano r 11 (380 m²) | casi vacía (legado de mesas) | 36 | 28 | círculo 22 |
| Lago | -34, -16 | elipse 16×11 (550 m²) | agua, cascada | 47 | 35 | lab. del lago 20 |
| Lab. del lago | -44, 1 | r 5 | ejercicios de agua | 47 | 45 | lago 20 |
| Cabaña bosque | -28, 34 | r 3,6 | libro fuego/tierra | 33 | 49 | cueva 18 |
| Cueva (portal) | -32, 52 | r 6,5 | portal, tinta, metal | 48 | 66 | cabaña bosque 18 |
| Cabaña norte | -8, 46 | r 3,6 | libro viento/agua | 31 | 53 | puesto 18 |
| Puesto suministros | 10, 48 | r 2,8 | papeles/runas | 34 | 55 | cabaña norte 18 |
| Taller de piedra | 40, 30 | r 6 | rocas, muros, puentes | 42 | 54 | atalaya **15** |
| Torre de vigía (portal) | 37, 44 | r 4,5 | rampa, portal | 47 | 63 | taller 15 |

Desbalances concretos:

1. **Las dos zonas de práctica están en el extremo sureste, a 64 m de la torre.** El recorrido torre -> libros -> práctica
   -> laboratorio hoy es: spawn -> torre 22 -> cabaña del bosque 33 (libro) -> entrenamiento **91** -> laboratorio 36.
   Hay un salto de 91 m (~18 s corriendo, ~65 s caminando) justo entre aprender y practicar, y sin portal que lo acorte.
2. **Cuadrante sureste apiñado:** entrenamiento, laboratorio, santuario, círculo, peñasco 1 y pradera en ~35 × 40 m
   (6 de 14 zonas en ~15 % del área). Santuario y laboratorio están a 16 m; la pista del laboratorio (17 m) apunta al campo.
3. **Cuadrante suroeste vacío:** fuera del lago no hay nada entre x -56..-12 y z -56..-20. El 19 % del valle jugable
   queda a más de 20 m de cualquier zona y el 8 % a más de 25 m; casi todo ese vacío es el SO y el sur-centro.
4. **Spawn rodeado de nada:** a menos de 20 m del spawn solo hay caminos. Los 3 caminos de `ValleyShape.Paths` miden
   ~20 m cada uno; **ninguno llega a entrenamiento, laboratorio ni al norte** (cabañas, cueva, atalaya, puesto).
5. **Norte en fila pareja pero sin ancla:** cabaña N, puesto, cabaña bosque y cueva a 18 m entre sí, todo a 31-66 m del spawn:
   se recorre de a una sin un punto de reunión. Taller de piedra y atalaya a solo 15 m (apiñados, mismo tema alto/piedra).
6. **Pradera "decorativa":** campo plano de 380 m² ya aplanado por `ValleyShape.Height` y casi sin contenido (las mesas
   viejas del campo siguen en `StampArea` 28,-16,5, "se dejan libres"). Es el mejor terreno plano y no se usa.
7. **Portales mal repartidos:** torre, atalaya (NE), círculo (E), cueva (NO). Cero en el sur y el suroeste, donde están
   práctica y laboratorio: el atajo no ayuda justo donde más lejos se está.

Además: `MapOverrides.json` solo tiene 29 entradas y casi todas dentro de la torre o del grupo `Practice/*_Path8/_Path9`
(en torno a (18,2) y (28,-3), restos del campo viejo). **Ninguna toca laboratorio, entrenamiento ni estructuras**, así que mover zonas es seguro.

## 2. Propuesta (opción B recomendada)

Criterios: aprendizaje torre -> libros -> práctica -> laboratorio en un anillo corto; distancias entre estaciones consecutivas
<= 35 m (<= 7 s corriendo, cómodo en VR); ninguna zona a menos de ~18 m de otra; sin huecos mayores a 20 m; el laboratorio conserva su pista de 17 m libre.

| Cambio | Antes | Después | Por qué |
|---|---|---|---|
| Entrenamiento (`ValleyShape.TrainingOrigin`) | 16,-46 | **8,-30** | a 25 del spawn y 47 de la torre (antes 43 / 64); sigue a >12 m del peñasco 1 (12 m) y 17 del peñasco 3 |
| Laboratorio (`LaboratoryPreferred`) | 47,-27 | **-20,-44** | rompe el apiñado SE y llena el SO vacío; a 31 del entrenamiento y 31 del lago (afinidad con el agua); pista hacia el oeste, despejada |
| Taller de piedra (`Find` en `Sites()`) | 40,30 | **34,12** | sale de la fila de la atalaya (ahora a 33); queda entre torre y círculo, en ruta al portal del este |
| Pradera | vacía | **se queda abierta** como plaza de práctica libre (ya plana); añadir bancos/carteles del recorrido (opcional) | sin costo de terreno |
| Santuario, círculo, lago, lab. del lago, cabañas, cueva, puesto, atalaya, torre | — | **sin cambios** | ya están pineados por tests y bien espaciados (>= 18 m) |
| Caminos (`ValleyShape.Paths`) | 3 tramos cortos | + spawn->entrenamiento (0,-6)->(8,-30); entrenamiento->laboratorio; torre->cabañas (norte) | el recorrido se ve en el suelo |
| Portal nuevo `training` (opcional, alternativa C) | — | en el entrenamiento o laboratorio | atajo al sur/suroeste |

Distancias del recorrido nuevo: spawn -> torre 22; torre -> cabaña bosque 33; cabaña bosque -> entrenamiento **~70** (sigue larga: ver nota) ->
laboratorio 31. Mejor: hacer el libro de la **cabaña del bosque** opcional y que *Los cuatro elementos* + papel (torre) alcancen para ir al entrenamiento
(torre -> entrenamiento 47 m). Con el portal de C esa pierna baja a ~10 s.

Sobre `MapOverrides.json`: **no hay que borrar nada** de torre, `Practice/*` ni `Map/Tower/*` para la opción B. Revisar al implementar:
(a) `Practice/*_Path8/_Path9`: dependen de `LegacyStation`/`FieldCenter`, que no se mueven; (b) si luego se ajusta a mano el laboratorio, `MapLayout` lo guardará solo.
Si en alguna pasada se usa `MapLayout.DropEntries` para un objeto mudado, hacerlo antes de `ApplyAndRecord` (ya está en `Build()`).

## 3. Mapas (vista cenital, 4 m por carácter, norte arriba)

Leyenda: T torre, S spawn, E entrenamiento, L laboratorio, ~ lago, A lab. del lago, B cabaña bosque, H cabaña norte, P puesto,
U cueva (portal), V atalaya (portal), W taller de piedra, M pradera, C círculo (portal), R santuario.

ACTUAL
```
      ..                 ..
     .  U                  .
    .         H   P         .
   .                     V   .
  .                           .
 ..                           ..
 .       B                W    .
..                             ..
.                               .
.                               .
                T



     A

                S      M    C

        ~
.                               .
.                               .
..                          L  ..
 .                             .
 ..                     R     ..
  .                           .
   .                         .
    .               E       .
     .                     .
      ..                 ..
```

PROPUESTO (B): E a (8,-30), L a (-20,-44), W a (34,12)
```
      ..                 ..
     .  U                  .
    .         H   P         .
   .                     V   .
  .                           .
 ..                           ..
 .       B                     .
..                             ..
.                               .
.                               .
                T
                        W


     A

                S      M    C

        ~
.                               .
.                               .
..                             ..
 .                E            .
 ..                     R     ..
  .                           .
   .       L                 .
    .                       .
     .                     .
      ..                 ..
```

## 4. Costo y riesgo

| Archivo | Qué tocar |
|---|---|
| `Scripts/World/Map/ValleyShape.cs` | `TrainingOrigin` (8,-30); posible giro de `TrainingForward`; agregar tramos a `Paths`. La loma plana del entrenamiento sale sola de `Height`. |
| `Editor/Map/MapStructures.cs` | `LaboratoryPreferred` (-20,-44) y semilla de `Find` del taller de piedra (34,12); `TrainingKeepOut` sigue valiendo. `LaboratoryFront` elige la pista más despejada (esperado: hacia el oeste). |
| `Data/WorldZones.json` | solo referencia (x/z se pisan en `MapTowerMap`); actualizar el comentario `use` si se agrega portal. |
| `Editor/Map/MapBuilder.cs` | nada (usa `TryGetSite("Laboratory")` y `TrainingGround`). Luego `Build Pradera`. |

Pasos chicos (un commit cada uno, compilar y probar entre medio): 1) mover entrenamiento + actualizar tests; 2) mover laboratorio + tests + revisar pista y fogata
(`MinFireDistance` 8 m); 3) taller de piedra; 4) caminos; 5) regenerar mapa, capturas del mapa de la torre, `HarnessPlaySmoke`; 6) docs (`GAME_DESIGN.md`, `STRUCTURE`, `CHANGELOG`).

Tests/smoke que se rompen o requieren actualizar:
- `StructureSitesPinTests`: lista `Pinned` (Laboratory 47,-27; EarthWorkshop 40,30), el chequeo del frente del laboratorio (-0.966, 0.259) y los de entrenamiento vs laboratorio / `TrainingKeepOut`.
- `TowerMapTests`: "los marcadores no se superponen" (distancia >= 2×`MarkerRadius`) y "cae dentro del cuadro": revisar con las posiciones nuevas.
- `TrainingLayoutTests` solo usa `LegacyStation`: no cambia. `ValleyShapeTests`: el spawn y la pradera libres de árboles siguen valiendo.
- `HarnessPlaySmoke`: chequeo "estructuras: 4 portales, 3+ puntos de reaparición y estación de reciclaje" (se mantiene; si C agrega un portal, subir a 5) y el bloque del laboratorio (posición del jugador al probarlo).
- Riesgos: terreno bajo el laboratorio nuevo (pendiente del SO; `Find` lo corre unos metros), árboles sobre la pista (`CanPlantTree` de `ValleyShape`), el entrenamiento nuevo quedando en un hueco entre peñascos 1 y 3, y la torre de vigía/escena guardada con `.unity` regenerado (revisar el diff de `Pradera.unity`, no tocar a mano).

## 5. Alternativas

| | A. Conservadora | B. Recomendada | C. Ambiciosa |
|---|---|---|---|
| Cambios | solo entrenamiento -> (8,-30) y taller -> (34,12) | A + laboratorio al SO (-20,-44) + caminos | B + portal nuevo `training`, caminos a todo, pradera con función (plaza de práctica libre / mini-arena de pruebas), 2 estaciones nuevas en huecos |
| Esfuerzo | ~2 commits, 3 tests | ~5 commits, 4 tests | ~10 commits, portal con receta nueva (`PortalRecipeTests`, `PortalKnowledgeTests`, smoke de 5 portales) |
| Resuelve | #1, #5 | #1, #2, #3, #4, #5 | todo, incluido #6 y #7 |
| Riesgo | bajo | medio (laboratorio recién ajustado v2-v6; solo cambia el sitio, no el código del banco) | alto (lógica y datos de portales) |

**Recomendación:** B. Es el menor cambio que corrige el apiñado del sureste y el vacío del suroeste sin tocar el código del laboratorio
(solo su sitio) ni los overrides. Después de probarlo en el juego, decidir si vale la pena el portal de C. Preguntar antes a Tamara por qué se movió el entrenamiento
desde el campo viejo (28,-14): si el motivo era la pista del laboratorio, la variante A+ (entrenamiento en la pradera, ~(18,-14)) acorta aún más el recorrido.
