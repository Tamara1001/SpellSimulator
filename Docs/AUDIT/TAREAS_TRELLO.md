# Tareas propuestas para Trello (pendiente de aprobación)

Origen: informes `01`–`04` de Antigravity + decisiones de Tamara (2026-11-23). Lista destino: **Por Hacer**. Etiqueta = área.

## PVP (decisiones: sin IA por ahora, kit al entrar, todo daña con tuning, victoria por abandono)
1. [PVP][Alta] Kit de dibujo al entrar a la arena (pluma, lienzo, tinta ilimitada) — PVP-06
2. [PVP][Alta] Re-lectura del servidor: avisar al cliente y corregir el hechizo visual — PVP-02
3. [PVP][Alta] Daño por impacto físico y quemaduras en duelistas, constantes nombradas — PVP-03
4. [PVP][Media] Victoria por abandono si el anfitrión se desconecta — PVP-11
5. [PVP][Media] Colliders en NetPlayer (colisión y centro de Health) — PVP-04
6. [PVP][Media] Volumen de muerte/reposición al caer de la arena y reset de pitch — PVP-05, PVP-13
7. [PVP][Media] Re-sincronizar trazos descartados + compensación de latencia — PVP-08, PVP-09
8. [PVP][Media] Nombre > 32 bytes UTF-8 (FixedString32Bytes) y límite de cast con sellos compuestos — PVP-12, PVP-15
9. [PVP][Media] HUD de duelo en IMGUI → UI compatible con VR — PVP-14
10. [PVP][Media] Sala de espera con "Listo" y navegador de partidas — PVP-16
11. [PVP][Baja] Cobertura destruida con partículas/escombros/audio — PVP-10
    (IA rival AICaster y mazo SealDeck: aplazados, quedan en ROADMAP)

## FÍSICAS
12. [FIS][Alta] Viento: re-acotar aceleración tras multiplicar por windGain — FIS-01
13. [FIS][Alta] Enfriamiento: doble multiplicación por fixedDeltaTime — FIS-02
14. [FIS][Alta] Agua/inundación: cuerpos con varios colliders se pierden — FIS-03, FIS-04, FIS-06, FIS-07
15. [FIS][Media] Health/MagicWorld, fuga de referencias en EarthShapes y límites de MagicField — FIS-05, FIS-12, FIS-13
16. [FIS][Media] Fractura adaptativa y vigas flotantes al destruir la torre — FIS-09, MOD-05, MOD-06
17. [FIS][Baja] Limpieza: GrassFire, Flutter, LevitationMath, GroundReactions, WindMath — FIS-08, 10, 11, 14, 15

## MAPA
18. [MAPA][Media] Actualizar WorldZones.json (portal exterior, Archivo, campos Fuego/Viento) y TowerMap — MAP-01, MAP-02
19. [MAPA][Media] Tutorial: detección prematura de nivel y radio exterior de puerta — MAP-03, MAP-04
20. [MAPA][Media] Zonas de práctica vs Laboratorio — MAP-05
21. [MAPA][Media] CanvasPile: cooldown + tope en escena con reciclado — MAP-06
22. [MAPA][Baja] Allocations en DisplayStock y sitios fijos de peñasco/cabaña — MAP-07, MAP-08

## UI
23. [UI][Media] Libro 3D legible en VR (aprender exige leer) — UI-03
24. [UI][Media] Hints VR: quitar "Tab: Vista completa" y acciones tapadas en marcadores — UI-01, UI-02
25. [UI][Media] FloatingNote legible por ambas caras — UI-04
26. [UI][Baja] Elipsis en Nunito SDF, modal del tutorial en VR, polling de tinta en HUD — UI-05, 06, 07

## MODELADO
27. [MOD][Media] Manos VR y avatar de red low-poly cartoon (mismo estilo) — MOD-01, MOD-02
28. [MOD][Media] Libreta circular: colisión y trazo circulares — MOD-03
29. [MOD][Media] Lapiceros del Archivo: collider y separación — MOD-04
30. [MOD][Baja] Ángulo de suelta/reposo de lienzos en VR — MOD-07

## VFX
31. [VFX][Alta] Shader Binarize a HLSL/URP con soporte estéreo VR — VFX-01
32. [VFX][Alta] Presupuesto de partículas por perfil de calidad (Quest bajo) — VFX-02
33. [VFX][Media] Luces puntuales dinámicas: hornear o limitar en interiores/combate — VFX-04
34. [VFX][Media] Suelo quemado proyectado en muros (StylizedLit) — VFX-03
35. [VFX][Media] Post-procesado/viñeta adaptado a VR — VFX-07
36. [VFX][Baja] Culling de ondas de agua y salpicadura de cascada — VFX-05, VFX-06
