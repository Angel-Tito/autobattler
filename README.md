# ⚔️ Auto-Battler en Realidad Virtual

> Prototipo de alta fidelidad para Meta Quest 2 que traslada la interacción central de un auto-battler tradicional a una experiencia inmersiva en Realidad Virtual.

**Curso:** Interacción Humano-Computador

---

## 👥 Equipo
- Angel Ulises Tito Berrocal
- Luis David Torres Osorio
- Pedro Enrique Mori Ortiz

---

## 📖 Contexto y Narrativa
El jugador asume el rol de un **“Árbitro Eterno”** que organiza un torneo de campeones. Durante la fase de preparación, agarra miniaturas de campeones interactuando físicamente con ellas y las posiciona sobre un tablero rúnico. Al iniciar el combate, el jugador observa una simulación automática, experimentando la estrategia táctica desde una perspectiva omnisciente o inmersiva.

*(El proyecto busca validar la manipulación física directa de las unidades —agarrar, mover, colocar— y la comprensión espacial del tablero, más que implementar sistemas complejos de multijugador o economía).*

---

## 📸 Capturas del Prototipo

### Vista General
![Vista General del Tablero](img/E1-1.png)
*Vista estratégica del tablero rúnico con los campeones posicionados durante la fase de preparación.*

### Interacción Directa
![Interacción Directa con Mano](img/E1-2.png)
*Manipulación inmersiva y natural en VR: los usuarios pueden alcanzar, seleccionar y controlar a los campeones directamente con sus manos reales o virtuales.*

### Feedback Visual y Snap Inteligente
![Feedback Visual y Celda de Snap](img/E1-3.png)
*Uso de controladores Meta Quest: Al acercar un campeón al tablero, el sistema resalta la celda válida más cercana. Al soltarlo, se realiza un acoplamiento automático ("snap") a la cuadrícula.*

---

## 🚀 Características y Estado Actual

### ✅ Implementado (MVP)
- **Interacción Física (RF01):** Agarre y colocación básica de campeones usando eventos interactivos de Meta SDK.
- **Reposicionamiento (RF02):** El usuario puede volver a agarrar y reubicar las piezas dinámicamente.
- **Snap Automático (RF03):** Integración de cálculo espacial para deslizar unidades suavemente a la celda disponible más cercana.
- **Feedback Visual y Háptico:** Iluminación de celdas objetivo y patrones de vibración para proximidad de mano y confirmación de colocación en celda.
- **Soporte Híbrido:** Pruebas completas en VR y fallbacks implementados para pruebas en el Editor de Unity usando el ratón.

### 📌 Roadmap Propuesto (Siguientes Pasos)
- **Gestión de Partida:** Añadir un `GameManager` para controlar los estados (Preparación → Combate → Resultados).
- **Combate Automático (RF07):** Lógica y simulación de movimiento/ataque de las unidades en el tablero.
- **UI y Transiciones (RF05, RF06):** Gesto o inicio de combate y transición temporal de cámara estratégica a inmersiva.
- **Límites Dinámicos (RF04):** Restringir y balancear la cantidad máxima de unidades permitidas simultáneamente en la zona de juego.

---

## 🛠 Estructura y Tecnologías

### Stack Principal
- **Motor:** Unity `2022.3.62f3` LTS *(Requerido para evitar incompatibilidades)*.
- **Plataforma Objetivo:** Hardware Meta Quest 2.
- **Paquetes Principales:**
  - XR Interaction Toolkit
  - Oculus XR

### Estructura Clave de Assets
```text
Assets/
├── CampeonSnap.cs       # Núcleo de interacción: agarre, offset de cámara y snap hacia la celda.
├── GridManager.cs       # Administración lógica del tablero y control del radio de snap interactuable.
├── HapticFeedback.cs    # Singleton global con patrones vibratorios (proximidad, drop, combate).
├── Scenes/
│   └── SampleScene.unity # Escena principal del prototipo.
└── <Modelos>            # Modelos 3D, materiales y scripts de terceros.
```

---

## ⚙️ Instrucciones de Ejecución

### 💻 Cómo probar en el Editor de Unity
1. Clona el repositorio y añade la carpeta raíz `autobattler` a **Unity Hub**.
2. Ábrelo utilizando **Unity 2022.3.62f3**.
3. Abre la escena ubicada en `Assets/Scenes/SampleScene.unity`.
4. Selecciona el botón **Play**. *Puedes usar el clic del ratón para simular el agarre inmersivo (implementado de manera temporal mediante los callbacks `OnMouse`).*

### 🥽 Despliegue en Meta Quest 2
1. En Unity, ve a `File > Build Settings` y cambia la plataforma a **Android**.
2. Verifica que el **XR Plug-in Management** esté activado y asignado a Oculus.
3. Compila generando el APK (`Build`) y transfiérelo a las gafas Meta Quest usando un cable Link y software como *SideQuest* o el *Meta Quest Developer Hub*.

---

**⚠️ Aviso sobre el uso de Assets:** Los modelos 3D y elementos visuales provistos en este repositorio tienen fines estrictamente académicos. 
Para dudas o resolución de problemas en la ejecución, por favor contactar a los integrantes del equipo.

