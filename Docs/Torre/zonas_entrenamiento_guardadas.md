# Zonas de entrenamiento de la planta baja (guardadas para reutilizar)

Estaban bien armadas; el rediseño de la torre las saca de la planta baja. NO se pierden: viven en git y en este código.

- **Tag de git:** `zonas-entrenamiento-torre` (commit `5d80091`, antes del rediseño). Para verlas tal cual: `git show zonas-entrenamiento-torre:Assets/_Project/Editor/Map/TowerProps.cs`.
- **Estación de viento** (sureste, a la derecha de la entrada, ~306°): `TowerProps.WindStation` (`Assets/_Project/Editor/Map/TowerProps.cs`, ~líneas 152-190). Pizarra de pared `WallBoard_Library` (en `CanvasDesigns.cs`, ángulo `TowerProps.WindAngle`), mesa fija con papeles, plumas y velas apagadas, 24 hojas livianas (`WindLeaf_*`, componente `Flutter`) y una guirnalda de banderines con bisagra. Cartel `Sign_WindStation`.
- **Estación de agua** (oeste bajo la ventana, ~160°): `TowerProps.WaterStation` (~líneas 330-410). Pila de piedra con agua real (`WaterVolume`), barquito `ToyBoat` (`KeepUpright`, masa ≤ 0,1), balde y el lienzo grande enmarcado `BigCanvas_Library` (`CanvasDesigns.cs`, ángulo `TowerProps.WaterAngle`). Cartel `Sign_WaterStation` («Dibujá el sigilo Agua…»).
- **Chequeos del smoke que las cubren:** «tutorial de viento: muchas hojas livianas que revolotean» y «tutorial de agua: barquito liviano que se mantiene boca arriba» (`HarnessPlaySmoke.cs`).

## Plan de reubicación (pedido de Tamara)
- **Viento:** dejarla como zona de práctica en el **norte del piso 1** (junto al Tomo I del viento), o mantenerla en planta baja; decidir al implementar. Ojo: el norte de cada piso es la rampa, así que usar el disco central.
- **Agua:** dejarla guardada (Archivo, nicho `shape`/`motion` del plan §18, o su propia sala) sin borrarla del código; si se quita de `TowerProps.Build`, conservar `WaterStation` como función sin llamar.
- Al sacar las estaciones de la planta baja, actualizar los dos chequeos del smoke (o moverlos con la zona).
