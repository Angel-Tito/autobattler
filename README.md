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

*(El proyecto busca validar la manipulación física directa de las unidades (agarrar, mover, colocar) y la comprensión espacial del tablero, más que implementar sistemas complejos de multijugador o economía).*

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
- **Inicio Deliberado de Combate (RF05):** Botón espacial junto al tablero que bloquea las piezas y arranca la pelea, sin UI plana.
- **Modo Espectador (RF06):** Transición con *fade* y cambio de escala del rig para observar el combate a escala real desde dentro del campo de batalla.
- **Combate Automático (RF07):** IA básica de unidades — selección de objetivo, movimiento, ataques, muerte, victoria y botón de revancha para reiniciar el ciclo.
- **Onboarding Espacial:** Tutorial anclado al mundo con marcadores visuales que señalan la primera acción.
- **Feedback Visual y Háptico:** Iluminación de celdas objetivo y tres patrones de vibración — proximidad (80 ms), colocación (150 ms) e inicio de combate (400 ms).
- **Soporte Híbrido:** Pruebas completas en VR (controllers y hand tracking) y fallbacks implementados para pruebas en el Editor de Unity usando el ratón.

### 📌 Roadmap Propuesto (Siguientes Pasos)
- **Visualización de Sinergias (RF08):** Indicadores de bonificaciones activas por clases de campeones.
- **Legibilidad del Combate:** Mayor contraste en la celda destino y resaltado de ataques, daño y resultado de la pelea.
- **Telemetría de Evaluación:** Instrumentar tiempo al primer agarre y conteo de errores para futuras pruebas de usabilidad.
- **Economía y Gestión de Partida:** Rondas múltiples, compra de unidades y balance de composiciones.

---

## 🛠 Estructura y Tecnologías

### Stack Principal
- **Motor:** Unity `2022.3.62f3` LTS *(Requerido para evitar incompatibilidades)*.
- **Plataforma Objetivo:** Hardware Meta Quest 2.
- **Paquetes Principales:**
  - Meta XR SDK (Meta SDK)

### Estructura Clave de Assets
```text
Assets/
├── CampeonSnap.cs       # Núcleo de interacción: agarre, offset de cámara y snap hacia la celda.
├── GridManager.cs       # Administración lógica del tablero y control del radio de snap interactuable.
├── CombatManager.cs     # Botón espacial, bloqueo de piezas, fade, escala del rig (Modo Espectador) y revancha.
├── CampeonCombat.cs     # IA de unidades: vida, objetivo, movimiento, ataques, muerte, victoria y reinicio.
├── TutorialManager.cs   # Tutorial espacial anclado al mundo y marcadores visuales de onboarding.
├── HapticFeedback.cs    # Singleton global con patrones vibratorios (proximidad, drop, combate).
├── Scenes/
│   └── SampleScene.unity # Escena principal del prototipo.
└── <Modelos>            # Modelos 3D, materiales y scripts de terceros.
```

---

## 📋 Requisitos

### Hardware

| Componente | Requisito |
|---|---|
| Headset | Meta Quest 2 (con controllers Touch; hand tracking opcional) |
| PC de desarrollo | Windows 10/11 de 64 bits, 16 GB RAM recomendados, GPU compatible con DX11+ |
| Cable | USB-C con soporte de datos (para despliegue por cable) |
| Espacio físico | Área libre mínima de **1.5 m × 1.5 m** (modo *roomscale* o *stationary*) |

### Software

| Software | Versión |
|---|---|
| Unity Hub | 3.x |
| Unity Editor | **2022.3.62f3 LTS** (con módulo **Android Build Support**, incluyendo *OpenJDK* y *Android SDK & NDK Tools*) |
| Meta XR SDK | Instalado vía Unity Package Manager |
| Meta Quest Developer Hub (MQDH) o SideQuest | Opcional, para instalar el APK y capturar video |
| Git | Cualquier versión reciente |
| Cuenta de desarrollador Meta | Necesaria para activar el modo desarrollador del headset |

---

## 🔧 Preparación del dispositivo (Meta Quest 2)

1. Crear/usar una cuenta en [developer.meta.com](https://developer.meta.com) y crear una *organización* de desarrollador.
2. En la app **Meta Horizon** del celular (con el headset vinculado):
   `Dispositivos → Meta Quest 2 → Configuración del headset → Modo de desarrollador → Activar`.
3. Conectar el headset al PC por USB-C y, dentro del visor, aceptar el diálogo **"Permitir depuración USB"** (marcar *Permitir siempre desde esta computadora*).
4. (Opcional) Activar **seguimiento de manos**: en el headset, `Configuración → Movimiento → Seguimiento de manos y cuerpo → Activar`. El prototipo soporta ambas modalidades: controllers y hand tracking.

---

## ⚙️ Instrucciones de Ejecución

### 💻 Cómo probar en el Editor de Unity
1. Clona el repositorio y añade la carpeta raíz `autobattler` a **Unity Hub**:
   ```bash
   git clone https://github.com/Angel-Tito/autobattler.git
   cd autobattler
   ```
2. Ábrelo utilizando **Unity 2022.3.62f3**. Si Unity solicita instalar la versión exacta, hacerlo desde Unity Hub incluyendo **Android Build Support**.
3. Verifica en `Window → Package Manager` que el **Meta XR SDK** esté instalado; si falta, instálalo desde el registro de paquetes de Unity.
4. Abre la escena ubicada en `Assets/Scenes/SampleScene.unity`.
5. Selecciona el botón **Play**. *Puedes usar el clic del ratón para simular el agarre inmersivo (implementado de manera temporal mediante los callbacks `OnMouse`): clic sostenido sobre un campeón del banco, arrastrar sobre el tablero y soltar sobre una celda válida.*

> Esta modalidad sirve solo para validación funcional; la evaluación de confort, presencia y háptica requiere el headset real.

### 🥽 Despliegue en Meta Quest 2

**Configuración previa del proyecto** (`Edit → Project Settings`):
- **XR Plug-in Management → Android:** proveedor **Meta XR** habilitado.
- **Player → Other Settings:** *Minimum API Level* Android 10 (API 29) o superior · *Scripting Backend* IL2CPP · *Target Architectures* ARM64.
- **Quality:** perfil optimizado para mantener **72 FPS** en Quest 2.

**Opción A — Build & Run por cable (recomendada)**
1. Conecta el Quest 2 por USB-C (con depuración USB aceptada).
2. `File → Build Settings → Android → Switch Platform`.
3. En **Run Device**, selecciona el Quest 2 (usa *Refresh* si no aparece).
4. Presiona **Build And Run**. Unity compila el APK y lo instala directamente en el headset.
5. En el visor, la aplicación queda disponible en `Biblioteca → Fuentes desconocidas → autobattler`.

**Opción B — Prueba por Quest Link (sin instalar APK)**
1. Instala la aplicación de escritorio **Meta Quest Link** en el PC.
2. Conecta el headset por cable Link o Air Link y activa **Quest Link** en el visor.
3. Presiona **Play** en el editor de Unity: la escena se renderiza directamente en el headset.

---

## 🎮 Flujo de la Experiencia

1. Delimita el límite de seguridad (*Guardian*) con al menos 1.5 m × 1.5 m libres e inicia la aplicación de pie.
2. **Orientación:** el tablero rúnico aparece al frente; el tutorial espacial señala la primera acción.
3. **Preparación:** toma campeones del banco con el gatillo del controller (o con pinza en hand tracking) y colócalos en las celdas iluminadas; el snap ajusta la posición y el controller vibra al confirmar.
4. **Combate:** presiona el **botón espacial de inicio** junto al tablero.
5. **Modo Espectador:** tras la transición, observa el combate a escala real; puedes desplazarte físicamente dentro del área.
6. **Revancha:** al finalizar la pelea, usa el botón de revancha para reiniciar el ciclo.

**⚠️ Aviso sobre el uso de Assets:** Los modelos 3D y elementos visuales provistos en este repositorio tienen fines estrictamente académicos.
Para dudas o resolución de problemas en la ejecución, por favor contactar a los integrantes del equipo.
