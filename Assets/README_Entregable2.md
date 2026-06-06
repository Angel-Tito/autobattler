# Entregable 2 - Cuadricula inteligente VR Auto-Battler

## Estado final para presentacion

El prototipo queda enfocado en el Entregable 2: seleccion directa y a distancia de campeones, feedback visual/haptico, colocacion con snap en cuadricula y un avance de combate controlado que usa animaciones reales sin romper escala.

## Requisitos cubiertos

- RF01 - Agarre y colocacion: campeones seleccionables con mano y controles mediante Meta Interaction SDK.
- RF02 - Reposicionamiento tactico: una ficha colocada puede volver a agarrarse y moverse.
- RF03 - Snap automatico: al soltar una ficha cerca del tablero, se ajusta al centro de la celda valida.
- RNF05 - Feedback haptico: vibracion por proximidad, agarre y colocacion.
- RNF06 - Onboarding sin texto: campeon con aura al hover y celda amarilla al colocar.
- RNF04 - Interfaz espacial: la interaccion ocurre en el mundo, no con instrucciones pegadas a camara.

## Seleccion y colocacion

- Se mantienen activos los `ISDK_DistanceGrabInteraction` para seleccionar desde lejos.
- Se removieron `ReticleDataMesh` y `ReticleDataIcon` porque estaban incompletos y generaban `UnassignedReferenceException`.
- `CampeonSnap.radioColocacionLejana = 0.75`, permitiendo colocar desde una distancia mas amable para VR.
- `GridManager.distanciaMaximaValida = 0.45` para el snap cercano.
- La celda valida se ilumina en amarillo mientras se sostiene una ficha.

## Feedback haptico

`HapticFeedback` implementa tres pulsos:

- Proximidad: 80 ms, suave.
- Agarre: 100 ms, medio.
- Colocacion: 150 ms, firme.

## Avance de combate

El boton de combate se conserva como avance del Entregable 3/4. Se mejoro para que sea demostrable:

- `CombatManager` coloca a los campeones sobre el campo antes de activar IA.
- El rig se mantiene en escala normal `(1,1,1)`.
- Los campeones se fuerzan a escala `0.28`, evitando modelos gigantes.
- La camara queda cercana y por encima del tablero (`combatHeightOffset = 0.60`, `combatDistanceOffset = 1.25`).
- `CampeonCombat` detecta triggers reales por AnimatorController, no nombres fijos.
- Se dispara `Run` al avanzar y ataques reales al entrar en rango.
- Se evita muerte durante los primeros 20 segundos de demo para que la pelea sea visible y no termine inmediatamente.

Animaciones verificadas:

- Aatrox: `Skeleton|Attack1`
- Mordekaiser: `Skeleton|Attack1.001`
- Akali: `Skeleton|Attack1`
- Aurora: `Skeleton|Attack1a`

## Limpieza de consola

En Play Mode ya no aparecen los errores rojos de `ReticleDataMesh`. Quedan solo logs/warnings informativos del runtime Meta/OpenXR, como display frequency, controller helper y boundary visibility.

## Como presentar

1. Acercar mano/control a un campeon: debe brillar y vibrar suavemente.
2. Agarrar desde cerca o distancia: debe vibrar con pulso de agarre.
3. Llevarlo hacia el tablero: una celda debe tornarse amarilla.
4. Soltar: la ficha debe hacer snap a la celda y vibrar con confirmacion.
5. Repetir con otra ficha para demostrar reposicionamiento tactico.
6. Opcionalmente presionar combate: los campeones se ubican sobre el campo, avanzan, atacan con animaciones reales y mantienen escala controlada.

## Archivos clave

- `Assets/CampeonSnap.cs`
- `Assets/HapticFeedback.cs`
- `Assets/GridManager.cs`
- `Assets/CampeonHoverFeedback.cs`
- `Assets/CampeonCombat.cs`
- `Assets/CombatManager.cs`
