# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.1

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive (v3.5.1):** Sincronización **MASIVA (Batch)** con registros continuos.
*   **Alineación:** Registros de exactamente **171 bytes** pegados uno tras otro. 
*   **Separadores:** **NINGUNO**. No usar `\n` ni `\r\n`. El driver de Singapur interpreta mejor la cadena binaria pura para identificar el lote completo.
*   **Marcador de Nombre:** Se utiliza el patrón `01 01` (en lugar de `03 07`) detectado en las muestras hexadecimales exitosas del cliente.
*   **Comando:** `digiwtcp.exe WR 37 {IP}`.

### 🏷️ Estructura de Auditoría (Fix Visual)
*   **Captura de Éxito:** El driver de Singapur devuelve el formato `{IP} : 0`. El sistema ahora es flexible y detecta este patrón para marcar el estado como **"Exitoso" (Verde)** en la UI.
*   **Timeout:** Se mantiene un reintento de lectura de 5-10 segundos para el archivo `RESULT`.

### ⚙️ Lógica de Sincronización
*   **Inyector de Pre-empaque:** Cantidad `01 01` forzada en los bytes **+9 y +10** tras el marcador `FF 09` en la cola del artículo.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.1) - EL GANADOR
*   **Restauración de Velocidad:** Se regresó al modo masivo (Batch 1000) eliminando los separadores de línea.
*   **Fix de Auditoría:** Se implementó la detección flexible de éxito para el driver de Singapur.
*   **Resultado:** Envío instantáneo y reporte visual correcto.

### 🗓️ 2026-04-10 (v3.4.9 - v3.5.0)
*   **Pivote a 1-a-1:** Se probó el envío individual con esperas de 1.5s debido a sospechas de saturación del driver. Funcionó pero resultó demasiado lento para el flujo de trabajo del cliente.

### 🗓️ 2026-04-10 (v3.4.5 - v3.4.8)
*   **Experimentos de Alineación:** Se probaron envíos masivos con `\n` y `\r\n`, resultando en que la balanza solo procesara el primer PLU del lote.

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **NO USAR SEPARADORES DE LÍNEA** en el archivo `F37`. La cadena debe ser continua (171 bytes x N registros).
2. **MARCADOR 01 01:** Siempre usar este patrón antes del nombre del producto.
3. **VELOCIDAD:** El cliente requiere modo masivo (Batch). Solo usar 1-a-1 como último recurso de depuración.

---
*Última actualización: 2026-04-10 (v3.5.1)*
