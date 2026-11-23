# Brief 02: agua e inundación con cuerpos de varios colliders (FIS-03, y FIS-04/06/07 si caen en los mismos archivos)

Proyecto: SpellSimulator (Unity 6 URP, C#). Lee `CLAUDE.md` y `Docs/CODESTYLE.md` antes de editar. Idioma: español en comentarios, identificadores en inglés. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: escribí código conservador. Cero allocations en `Update/FixedUpdate/LateUpdate`.

## Contexto (informe `Docs/AUDIT/03_fisicas.md`)
- **FIS-03** `Assets/_Project/Scripts/Reactions/WaterVolume.cs` (~líneas 140-161) y `FloodZone.cs` (~155-174): el seguimiento de cuerpos usa un array `bodies[]` y otro `bodyColliders[]`. En `OnTriggerEnter`, si entra un segundo collider del mismo `Rigidbody` se descarta (`bodies[i] == rb`), y `bodyColliders[i]` guarda solo el primero. Si ese primer collider sale del volumen mientras los demás siguen dentro, el cuerpo se da por salido (se pierde flotación/arrastre aunque siga en el agua).
- **FIS-04** `WaterDropSink.cs:53-58`, **FIS-06** `WaterVolume.cs:186-190`, **FIS-07** `WaterVolume.cs:224-233`: leé lo que dice el informe sobre cada uno. Arreglalos solo si el cambio es chico y está en los mismos archivos; si no, dejalos y listalos.

## Hacé
1. Verificá el bug de FIS-03 leyendo el código real (los números de línea pueden haber cambiado).
2. Cambiá el seguimiento para contar **cuántos colliders del mismo Rigidbody están dentro** (contador por cuerpo) y solo sacar el cuerpo cuando el contador llega a 0. Mantené las estructuras pre-alocadas (arrays/listas con capacidad fija, sin LINQ ni `new` en el bucle de física).
3. Aplicá el mismo arreglo en `WaterVolume` y `FloodZone`. Si la lógica se repite, extraela a una clase estática pura chica y testeable (por ejemplo un contador de colliders por cuerpo) con test EditMode en `Assets/_Project/Tests/EditMode/`.
4. No cambies constantes de tuning de flotación ni valores de arrastre.

## Devolvé (≤5 líneas)
Archivos tocados, qué hallazgos (03/04/06/07) arreglaste y cuáles dejaste, y el nombre del test.
