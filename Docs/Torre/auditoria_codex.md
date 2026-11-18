# Auditoría de la torre-tutorial

## Veredicto general

**RECHAZADO en su forma actual como material para explicar o revisar el diseño.** La propuesta contiene decisiones de recorrido útiles, pero los planos intentan ser a la vez mapa, plano técnico, guion pedagógico y documento de implementación. Los paneles laterales, cotas, notas y leyendas compiten con la información que debería saltar a la vista. Además, encontré contradicciones de geometría, orden y terminología entre los documentos.

No puedo confirmar que los carteles quepa en sus tarjetas reales: los SVG no muestran esas tarjetas ni sus medidas en pantalla, y `carteles.md` no aporta una prueba de renderizado. Que una línea tenga menos de 38 caracteres no garantiza que se lea en la tarjeta, en particular en VR. Sí se puede afirmar que **la propia especificación supera ese límite en varios casos**.

## 1. Estética y legibilidad

Conté elementos `<text>` dentro de cada SVG; el conteo incluye títulos, números y etiquetas de leyenda. El rango de fuente visible es **9,5–18 px**. La mayoría de las etiquetas están entre 10 y 12,5 px: pueden servir en un plano ampliado, pero son demasiado pequeñas para que todo se entienda de un vistazo.

| Plano | `<text>` | Diagnóstico |
|---|---:|---|
| Terraza | 75 | Saturación alta. Sobran panel técnico, descripciones de recorrido paso a paso, medidas repetidas y entradas extensas de leyenda. |
| Piso 2 | 70 | Saturación alta. Tres opcionales, recorrido, geometría, objetos de la pila, marcas numeradas y sus textos técnicos compiten por atención. |
| Piso 1 | 64 | Saturación alta. La secuencia didáctica es rescatable, pero la guía, la cortina, las cotas y el panel lateral la entierran. |
| Planta baja | 61 | Saturación alta. El plano explica la puerta, tres trabas y soluciones, más el relato de recorrido, la geometría y toda la simbología. |

El problema no es solo el número: las etiquetas cercanas al dibujo —por ejemplo, las tres trabas de la puerta— se amontonan alrededor del mismo sector. No pude medir intersecciones exactas de texto en pantalla sin renderizar los SVG; **sí hay densidad suficiente y proximidad suficiente para que el plano no sea legible al tamaño de vista normal**.

### Cambios visuales recomendados

- Separar cada plano en dos piezas: **mapa de jugador** y **ficha técnica**. El mapa de jugador solo necesita entrada, estaciones principales, obstáculo, salida y una línea de recorrido.
- Sacar de los planos el relato numerado, las especificaciones de implementación, los radios y las coordenadas angulares. Dejar las cotas estrictamente necesarias en una ficha aparte.
- Reducir la leyenda a **5–7 símbolos**; agrupar libros y mesas como “estación”, y los elementos ornamentales en una sola categoría.
- Usar etiquetas breves directamente junto al icono. No repetir el mismo dato en el dibujo, el panel y la leyenda.
- Jerarquía visual sugerida: **1) llegada y salida; 2) camino; 3) puzzle obligatorio; 4) estación de aprendizaje; 5) opcionales; 6) decoración y cotas**.
- Para un plano de presentación, apuntar a **12–18 rótulos en total**, incluidos los de la leyenda; **máximo 20** si se agrupan bien. Para el plano técnico, mantener los detalles en otra lámina.
- Si se mantienen estas densidades, hay que ampliar mucho el formato y renderizarlo para comprobarlo. Reducir la fuente para hacer entrar más información agravaría el problema.

## 2. Flujo de juego por piso

El análisis de flujo es sobre lo descrito en los documentos, no sobre una prueba en Unity. Hay decisiones espaciales que parecen despejar el paso, pero no se puede asegurar que haya un único camino claro: cada plano dibuja una ruta, mientras que las notas y el resumen describen otras.

### Terraza — **RECHAZADO**

La secuencia viga → muro → reja → rampa es comprensible **si** la reja bloquea de verdad el acceso a la rampa. Pero el documento menciona una cerca alta que cierra el noroeste y una ruta horaria; el plano también dibuja camino, viga, muro, puerta y panel de instrucciones. No queda claro a primera vista qué obstáculo está bloqueando qué paso ni cuál es el destino inmediato tras la llegada.

Para alguien nuevo, el orden resulta exigente: el primer cartel ofrece saltar el tutorial; después vienen instrucciones distintas de movimiento y dos pruebas. El cartel de la reja resume acciones ya explicadas, así que repite en vez de orientar.

**Cambios:** mostrar solo spawn, primer tramo, viga, muro, reja y bajada. Marcar inequívocamente el sentido. Mantener los ejercicios obligatorios en la ruta y sacar adornos, cotas y detalles de implementación del plano de jugador.

### Piso 2 — **CON CAMBIOS**

La llegada despejada y la pila como bloqueo principal pueden funcionar. Pero hay **tres ejercicios opcionales antes de la salida**, con cinco carteles en el piso (#6–#10). Es demasiado desvío para un nivel cuyo objetivo declarado es mover objetos y bajar. El jugador que aún no conoce los controles tiene que diferenciar cuáles estaciones son opcionales y cuál obstáculo es obligatorio.

Hay además una discrepancia espacial importante: el resumen ubica la mesa de práctica en `20°`, la estación 1 en `55°`, la 2 en `105°` y la diana en `315°`. `carteles.md` ubica los carteles #6–#9 en `8°`, `48°`, `95°` y `328°`; el SVG debe ser la referencia de implementación, pero su representación no resuelve de forma simple dónde se encuentran entre sí la mesa, los marcos y la línea hacia la pila.

**Cambios:** conservar como mucho un opcional visible en la ruta, y poner los otros fuera del corredor visual o eliminarlos. El foco al desembarcar debe ser la pila y la bajada; una sola instrucción clara antes de la pila.

### Piso 1 — **CON CAMBIOS**

Es el piso con la progresión didáctica más lógica: conseguir herramientas, leer el libro, usar el lienzo y despejar la bajada con viento. Pero suma mesa de herramientas, cinturón, mesa con libro, atril, plantilla fantasma, flecha de apunte, cortina y carteles. El aprendiz puede confundir la flecha celeste de apunte con el efecto del hechizo; el propio historial indica que ya ocurrió.

La ruta también cambia de dirección entre la llegada y las estaciones, pero el SVG la dibuja como una secuencia técnica con ángulos, no como una ruta que se lea de un vistazo. Las instrucciones #11–#13 describen varios pasos y controles antes de que el jugador vea su resultado.

**Cambios:** hacer que el camino visual conecte, en ese orden, herramientas → libro → lienzo → cortina/rampa. Mostrar la flecha de apunte como una ayuda discreta y distinta del efecto de viento. Eliminar explicaciones duplicadas y precisar qué acción realiza el jugador sobre el lienzo.

### Planta baja — **RECHAZADO**

El objetivo declarado es abrir la puerta con viento, pero hay **tres trabas**, de las cuales dos son atajos sin cartel. Esto puede premiar experimentación, pero para alguien que no conoce hechizos no queda claro si debe leer `elements`, usar el lienzo guiado o probar soluciones alternativas. El plano rotula las soluciones opcionales con agua, aire y *Crush*, lo que añade una lista de vocabulario nuevo justo en la salida del tutorial.

También hay una tensión entre “un cartel que guía” y “abrir por cualquiera de tres vías”: el cartel no puede explicar la puerta de forma breve y completa si solo enseña una de ellas.

**Cambios:** definir si el piso enseña una solución principal o si la puerta es un reto de experimentación. Si es enseñanza, destacar visualmente solo el pestillo y dejar los atajos sin rótulos en el plano de jugador. Si los atajos deben ser descubribles, mostrar pistas visuales ambientales consistentes, no tres explicaciones técnicas en el cartel.

## 3. Consistencia y contradicciones

Estas son las discrepancias que encontré entre `00_resumen.md`, `carteles.md`, los SVG y el contexto de `MAPA_FLUJO_PLAN.md §17–18`.

### Estructura y geometría

1. **Apertura del hueco:** el resumen especifica `HoleFrom = 48°` y `HoleTo = 180°` (arco norte); §17 dice que la rampa tiene una llegada/salida en `180°` y desciende por el arco norte hasta `0°`. Sin embargo, `02_piso2.svg` y `03_piso1.svg` dibujan las rampas como si ocuparan el arco `180°→0°`, mientras los demás textos tratan el espacio de rampa y hueco con límites distintos. Hay que fijar una convención única y comprobarla contra el código.
2. **Dirección de rampa y llegada:** el resumen dice que se entra a la rampa en `180°` y se baja `180°→90°→0°`; eso implica desembarcar en `0°`. Los planos describen esos mismos extremos, pero la trayectoria dibujada y las flechas no siempre dejan claro qué sentido es subida y cuál bajada.
3. **Piso 2, ubicación de pila:** resumen: `198°`; leyenda del SVG/Piso 2 y `carteles.md`: verificar: el texto maestro usa la estación del cartel en `216°`, pero no declara explícitamente la discrepancia de ubicación con el obstáculo. La separación “1,75 m antes” no acredita que el cartel esté realmente antes sobre la trayectoria.
4. **Piso 2, ubicación de ejercicios:** resumen y `carteles.md` no coinciden en las posiciones de la mesa y sus estaciones (ver detalle arriba). Tampoco queda claro si el cartel #8 apunta al segundo marco: el catálogo dice que mira “hacia el marco 1”.
5. **Terraza, secuencia y posición:** el catálogo dice que el cartel #4 mira al punto `2°`, mientras el muro está en `325°`; su descripción del punto de vista (“quien sale de la viga”) no basta para verificar que cartel, obstáculo y jugador queden bien orientados.
6. **Piso 1, cartel #11:** `carteles.md` lo ubica a `340°`, `r=4,1 m`, mirando al pie de la rampa y al Este. El resumen dice `338°`, delante de la mesa a `325°`, `r=3,6 m`. No son la misma posición ni describen el mismo encuadre.
7. **Piso 1, cartel #12:** catálogo: `295°`, `r=2,2 m`; resumen: `285°`, delante del libro a `270°`, `r=1,6 m`.
8. **Piso 1, cartel #13 y atril:** el catálogo dice que está a `238°`, `r=2,9 m`, a `0,90 m` del atril; resumen: `232°` y el atril en `220°`, `r=2,6 m`. Además, el texto habla de “calcar” una plantilla fantasma, pero no especifica con precisión si se calca con la pluma o si el jugador debe dibujar sin esa herramienta.
9. **Planta baja, cartel #14:** catálogo: `285°`, `r=2,9 m`; resumen: `282°`, `r=3,0 m`. El SVG también lo nombra como #14, pero la ubicación del texto en su panel y el marcador dibujado no permiten concluir que el jugador lo encuentre antes de entender la estación.
10. **Salida y desbloqueo:** §17 describe una puerta que se abre con viento, tierra o columna de agua y queda abierta para siempre. El resumen agrega que al cruzar el vano se apaga la barrera mágica y concluye el tutorial. Los SVG lo muestran como salida al Nexo/fin del tutorial. Ese cruce y condición de cierre no aparecen en §17.
11. **Cuatro elementos vs. tres soluciones:** el libro de planta baja se llama “Los cuatro elementos”, pero el resumen solo enumera tres soluciones de puerta (viento, agua/aire y tierra); el contexto dice que fuego se aprende afuera. Falta aclarar cómo presentar los cuatro elementos sin insinuar que también hay una lección de fuego en la torre.

### Carteles, nombres y contenido

12. **Cartel #5:** catálogo: “Agáchate en la viga y saltá el muro para apagar la reja y bajar”; el SVG/resumen lo describen como un cartel ante la reja, pero la instrucción repite dos pruebas ya hechas. La acentuación y el voseo tampoco están normalizados con el resto.
13. **Cartel #6–#9:** `carteles.md` los llama opcionales; el piso 2 del resumen describe las mismas estaciones, pero el plano presenta una cantidad de información comparable al objetivo obligatorio. En la práctica visual no hay jerarquía suficiente para distinguir opcional de obligatorio.
14. **Cartel #9:** usa “tiro al blanco” y “arrojala”; `00_resumen.md` habla de una diana y `Q`; el texto exacto no usa el nombre de la acción coherentemente con #10.
15. **Cartel #11:** el resumen dice `E` agarrar pluma y tintero; catálogo añade `R` recargar tinta. No se establece en §17 que `R` sea el control de recarga.
16. **Cartel #12:** catálogo añade `T` para abrir el grimorio, aunque el resumen solo indica leer el libro con `E`. Es información no respaldada por el flujo descrito.
17. **Cartel #13 y #14:** usan “sello”, pero la propuesta declara que no se enseñan sellos/runas/portales en la torre. En §17 el jugador dibuja un hechizo de viento; conviene usar “hechizo” o el nombre de la plantilla, no “sello”.
18. **Cartel #14:** en el catálogo dice “Calcá el sello de viento”; en el SVG dice “Puerta trabada: lanzá viento al pestillo”. El resumen dice “Calca el viento y apuntá al pestillo”. Son tres instrucciones distintas.
19. **Rayo/flecha/guía:** el resumen usa “flecha de apunte”, el catálogo “guía” y los SVG alternan “AimGuide”, “flecha guía” y “flecha celeste”. El término del efecto se confunde además con “rayo” en el historial. Fijar un único nombre: **flecha de apunte del lienzo**.
20. **Rampa de Piso 1:** resumen sitúa la tela en `195°` y la salida en `180°`; el plano y el texto del recorrido describen la cortina/liberación, pero la dirección de avance dibujada no deja obvio que la cortina sea el bloqueo de esa bajada.

## 4. Carteles: evaluación y propuesta de texto final

Los textos siguientes son propuestas para los carteles descritos en `carteles.md`. Buscan una acción principal por cartel, sin vocabulario extra. **No puedo certificar que entren en sus tarjetas** sin ver el texto renderizado en el tamaño real del juego.

1. **Terraza, salteo — pasa con cambios.** “Saltear el tutorial” es claro, pero la segunda línea “Ir directo al Nexo” repite el resultado.  
   **Final:** `E · Saltear tutorial`

2. **Terraza, caminar — no pasa.** La segunda línea introduce una dirección que puede contradecir la ruta si cambia el giro.  
   **Final:** `WASD · Moverse` / `Mouse · Mirar`

3. **Terraza, agacharse — pasa con cambios.** “Mantené” es largo para una tarjeta; el cartel debe dar una acción.  
   **Final:** `C · Agacharse`

4. **Terraza, saltar — pasa con cambios.** Menciona también “por encima del muro”, que puede resolverse con el obstáculo mismo.  
   **Final:** `Espacio · Saltar`

5. **Terraza, reja — no pasa.** Repite dos instrucciones ya aprendidas y no dice qué hacer en la reja.  
   **Final:** `Reja cerrada` / `Completá ambos pasos`

6. **Piso 2, opcional — no pasa.** “E agarrar” en una estación opcional puede leerse como instrucción del objetivo obligatorio; el segundo renglón es largo.  
   **Final:** `Opcional · Practicá con el lienzo`

7. **Piso 2, marco de flotado — no pasa.** “Dejalo quieto dentro del marco” no dice cómo hacerlo.  
   **Final:** `E · Agarrar` / `E · Dejar flotando`

8. **Piso 2, lienzo de canto — no pasa.** “Dar vuelta” y “girar de canto” son acciones distintas; además, el cartel dice “marco 1” en la orientación.  
   **Final:** `Clic derecho · Girar` / `F · Cambiar la cara`

9. **Piso 2, diana — no pasa.** La línea excede el máximo declarado y reúne dos acciones.  
   **Final:** `E · Agarrar la caja` / `Q · Arrojarla a la diana`

10. **Piso 2, pila — pasa con cambios.** Es relativamente claro, pero “despejá” no indica qué mover ni el destino.  
    **Final:** `E · Mover las cajas` / `Liberá la bajada`

11. **Piso 1, tinta — no pasa.** “Agarrar pluma y tintero” y “recargar tinta” parecen tareas, pero el segundo control no está corroborado en el contexto.  
    **Final:** `E · Agarrar la pluma`  
    *(Agregar recarga solo si Tamara confirma que R es el control vigente.)*

12. **Piso 1, libro — no pasa.** La línea sobre el grimorio no pertenece al objetivo inmediato y `T` no está corroborado en estos documentos.  
    **Final:** `E · Leer el tomo de viento`

13. **Piso 1, hechizo — no pasa.** “Calcá el viento” es ambiguo, y “guía” sin referente puede confundirse con el efecto mágico.  
    **Final:** `Dibujá viento en el lienzo` / `Apuntá la flecha a la cortina`

14. **Planta baja, puerta — no pasa.** Hay tres versiones distintas. “Sello” no es el término adecuado y el cartel promete salir antes de resolver la acción.  
    **Final:** `Dibujá viento en el lienzo` / `Apuntá la flecha al pestillo`

## 5. Cambios priorizados

### P0 — bloqueantes

- Reducir y separar los SVG en mapa de recorrido y ficha técnica. Con 61–75 textos, hoy no cumplen el requisito de entenderse de un vistazo.
- Resolver las discrepancias de posiciones y orientación antes de implementar: especialmente rampas/hueco, estaciones del Piso 2 y carteles #11–#14.
- Elegir una fuente de verdad para el texto de cada cartel. Actualmente el catálogo, el resumen y los SVG no coinciden.
- Confirmar los controles de interacción (`R`, `T`, clic derecho, `F`, `E`, `Q`) contra los controles vigentes del juego.
- Definir si la planta baja enseña una única solución guiada o propone tres soluciones equivalentes; hoy la jerarquía pedagógica no corresponde con las tres trabas descritas.
- Comprobar el ajuste real de las tarjetas mediante una captura del texto renderizado. El límite de caracteres no alcanza para validar el encaje.

### P1 — importantes

- Reducir el Piso 2 a un objetivo obligatorio claramente dominante y, como máximo, un ejercicio opcional en el recorrido principal.
- Distinguir visualmente la flecha de apunte del lienzo del hechizo de viento.
- Normalizar los nombres en todos los documentos: viento, lienzo, plantilla, flecha de apunte, pestillo, bajada.
- Cambiar “sello” por “hechizo” o por el nombre de la plantilla que corresponda.
- Simplificar los carteles a una acción principal y retirar controles o consejos que no se necesitan en esa estación.
- Aclarar el punto de cierre: si completar el tutorial exige cruzar la puerta, documentarlo también en §17.

### P2 — pulido

- Sacar del plano de jugador las coordenadas angulares, radios, alturas y nombres de objetos de implementación.
- Agrupar las leyendas y reducir cotas repetidas.
- Revisar la consistencia de voseo, acentos y puntuación en los 14 carteles.
- Renderizar los SVG en la escala de presentación prevista y revisar los cruces de etiquetas; el conteo no sustituye esa comprobación.

**Veredicto por piso:** Terraza — **RECHAZADO**; Piso 2 — **CON CAMBIOS**; Piso 1 — **CON CAMBIOS**; Planta baja — **RECHAZADO**.

## PREGUNTAS

1. ¿Qué controles son los vigentes para recargar tinta, abrir el grimorio, rotar y cambiar la cara del lienzo? En particular, ¿`R`, `T`, clic derecho y `F` están confirmados?
2. ¿La puerta de planta baja debe enseñar únicamente viento, o querés que las tres soluciones sean visibles como alternativas desde el primer intento?
3. ¿El cartel de la reja de la terraza debe repetir las dos pruebas o indicar solo que se completaron y que la bajada quedó abierta?
4. ¿`puzzles.md` existe en otra ubicación o todavía no fue creado?
