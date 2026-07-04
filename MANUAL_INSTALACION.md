# Manual de Instalación y Despliegue — Auto-Battler VR

**Proyecto Final — CS4053 Interacción Humano Computador**
Autores: Angel Ulises Tito Berrocal · Luis David Torres Osorio · Pedro Enrique Mori Ortiz

Este documento describe los requisitos, la instalación del entorno de desarrollo, la configuración del proyecto y el proceso de despliegue del prototipo **Auto-Battler VR** en el dispositivo **Meta Quest 2**.

---

## 1. Requisitos

### 1.1 Hardware

| Componente | Requisito |
|---|---|
| Headset | Meta Quest 2 (con controllers Touch; hand tracking opcional) |
| PC de desarrollo | Windows 10/11 de 64 bits, 16 GB RAM recomendados, GPU compatible con DX11+ |
| Cable | USB-C con soporte de datos (para despliegue por cable) |
| Espacio físico | Área libre mínima de **1.5 m × 1.5 m** (modo *roomscale* o *stationary*) |

### 1.2 Software

| Software | Versión |
|---|---|
| Unity Hub | 3.x |
| Unity Editor | **2022.3.62f3 LTS** (con módulo **Android Build Support**, incluyendo *OpenJDK* y *Android SDK & NDK Tools*) |
| Meta XR SDK (Meta SDK) | Instalado vía Unity Package Manager |
| Meta Quest Developer Hub (MQDH) o SideQuest | Opcional, para instalar el APK y capturar video |
| Git | Cualquier versión reciente |
| Cuenta de desarrollador Meta | Necesaria para activar el modo desarrollador del headset |

---

## 2. Preparación del dispositivo (Meta Quest 2)

1. Crear/usar una cuenta en [developer.meta.com](https://developer.meta.com) y crear una *organización* de desarrollador.
2. En la app **Meta Horizon** del celular (con el headset vinculado):
   `Dispositivos → Meta Quest 2 → Configuración del headset → Modo de desarrollador → Activar`.
3. Conectar el headset al PC por USB-C y, dentro del visor, aceptar el diálogo **"Permitir depuración USB"** (marcar *Permitir siempre desde esta computadora*).
4. (Opcional) Activar **seguimiento de manos**: en el headset, `Configuración → Movimiento → Seguimiento de manos y cuerpo → Activar`. El prototipo soporta ambas modalidades: controllers y hand tracking.

---

## 3. Obtención del proyecto

```bash
git clone https://github.com/Angel-Tito/autobattler.git
cd autobattler
```

---

## 4. Configuración en Unity

1. Abrir **Unity Hub → Add → Add project from disk** y seleccionar la carpeta clonada.
2. Abrir el proyecto con **Unity 2022.3.62f3 LTS**. Si Unity solicita instalar la versión exacta, hacerlo desde Unity Hub incluyendo **Android Build Support**.
3. Esperar la resolución de paquetes. Verificar en `Window → Package Manager` que el **Meta XR SDK** (Meta XR Core / Interaction SDK) esté instalado. Si falta, instalarlo desde el registro de paquetes de Unity o mediante *Add package by name*.
4. Cambiar la plataforma de compilación:
   `File → Build Settings → Android → Switch Platform`.
5. Verificar la configuración del proyecto en `Edit → Project Settings`:
   - **XR Plug-in Management → Android**: proveedor **Oculus/Meta** habilitado.
   - **Player → Other Settings**:
     - *Minimum API Level*: Android 10 (API 29) o superior.
     - *Scripting Backend*: IL2CPP · *Target Architectures*: ARM64.
   - **Quality**: configuración optimizada para mantener **72 FPS** en Quest 2 (objetivo de rendimiento fijado por `CombatManager.cs`).
6. Abrir la escena principal del prototipo desde `Assets/Scenes/` y añadirla a `Build Settings → Scenes in Build` si no está incluida.

### 4.1 Estructura relevante del proyecto

| Script | Responsabilidad |
|---|---|
| `CampeonSnap.cs` | Agarre, suelta controlada y snap automático a celda; iluminación de la celda candidata; soporte de prueba con ratón en el editor |
| `GridManager.cs` | Administración de celdas válidas y resolución de la celda más cercana |
| `CombatManager.cs` | Botón espacial de inicio, bloqueo de piezas, *fade*, cambio de escala del rig (Modo Espectador), objetivo de 72 FPS y botón de revancha |
| `CampeonCombat.cs` | IA de unidades: vida, selección de objetivo, movimiento, ataques, muerte, victoria y reinicio |
| `TutorialManager.cs` | Tutorial espacial anclado al mundo y marcadores visuales de onboarding |
| `HapticFeedback.cs` | Tres patrones hápticos: proximidad (80 ms), colocación (150 ms) e inicio de combate (400 ms) |

---

## 5. Prueba rápida en el editor (sin headset)

`CampeonSnap.cs` permite simular el agarre con el **ratón** dentro del editor:

1. Abrir la escena principal y presionar **Play**.
2. Hacer clic sostenido sobre un campeón del banco para tomarlo, arrastrarlo sobre el tablero y soltarlo sobre una celda válida (la celda candidata se ilumina).

Esta modalidad sirve solo para validación funcional; la evaluación de confort, presencia y háptica requiere el headset real.

---

## 6. Despliegue en Meta Quest 2

### Opción A — Build & Run por cable (recomendada)

1. Conectar el Quest 2 por USB-C (con depuración USB aceptada).
2. `File → Build Settings → Android`.
3. En **Run Device**, seleccionar el Quest 2 (usar *Refresh* si no aparece).
4. Presionar **Build And Run**. Unity compila el APK y lo instala directamente en el headset.
5. En el visor, la aplicación queda disponible en
   `Biblioteca → Fuentes desconocidas → autobattler`.

### Opción B — Instalación manual del APK

1. `File → Build Settings → Build` y guardar `autobattler.apk`.
2. Instalar con **ADB**:

   ```bash
   adb devices          # verificar que el Quest aparezca como "device"
   adb install -r autobattler.apk
   ```

   Alternativamente, arrastrar el APK en **Meta Quest Developer Hub** o **SideQuest**.
3. Ejecutar desde `Biblioteca → Fuentes desconocidas`.

### Opción C — Prueba por Quest Link (sin instalar APK)

1. Instalar la aplicación de escritorio **Meta Quest Link** en el PC.
2. Conectar el headset por cable Link o Air Link y activar **Quest Link** en el visor.
3. Presionar **Play** en el editor de Unity: la escena se renderiza directamente en el headset.

---

## 7. Ejecución de la experiencia

1. Delimitar el límite de seguridad (*Guardian*) con al menos 1.5 m × 1.5 m libres.
2. Iniciar la aplicación de pie, frente al espacio libre.
3. Flujo de uso:
   1. **Orientación:** el tablero rúnico aparece al frente; el tutorial espacial señala la primera acción.
   2. **Preparación:** tomar campeones del banco con el gatillo del controller (o con pinza en hand tracking) y colocarlos en las celdas iluminadas; el snap ajusta la posición y el controller vibra al confirmar.
   3. **Combate:** presionar el **botón espacial de inicio** junto al tablero.
   4. **Modo Espectador:** tras la transición, observar el combate a escala real; es posible desplazarse físicamente dentro del área.
   5. **Revancha:** al finalizar la pelea, usar el botón de revancha para reiniciar el ciclo.

---

## 8. Solución de problemas

| Problema | Causa probable | Solución |
|---|---|---|
| El Quest no aparece en *Run Device* | Depuración USB no aceptada o cable sin datos | Reconectar, aceptar el diálogo en el visor, probar otro cable/puerto |
| `adb devices` muestra *unauthorized* | Permiso pendiente en el headset | Ponerse el visor y aceptar "Permitir depuración USB" |
| Pantalla negra al iniciar en el headset | Proveedor XR no habilitado para Android | Activar Oculus/Meta en `XR Plug-in Management → Android` y recompilar |
| Caídas por debajo de 72 FPS | Calidad gráfica excesiva | Usar el perfil de calidad optimizado del proyecto; verificar texturas/mallas optimizadas |
| Las manos no se detectan | Hand tracking desactivado | Activarlo en la configuración de movimiento del headset |
| La app no aparece en la Biblioteca | APK instalado como fuente desconocida | Buscar en `Biblioteca → Fuentes desconocidas` |
