# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.2

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive (v3.5.1+):** Sincronización **MASIVA (Batch)** con registros continuos.
*   **Alineación:** Registros de exactamente **171 bytes** pegados uno tras otro sin separadores.
*   **Marcador de Nombre:** Se utiliza el patrón `01 01`.
*   **Comando:** `digiwtcp.exe WR 37 {IP}`.

### 🏷️ Estructura de Pre-empaque (v3.5.2)
*   **Inyector de Pre-empaque (Fix Cantidad):** Se fuerza el valor **`00 01`** (Cantidad 1) en los bytes **+9 y +10** tras el marcador `FF 09`. 
    *   *Nota:* El uso de `01 01` (257) causaba fallos en el pre-empaque de artículos por Unidad/Cantidad. Cambiar a `00 01` restaura la impresión automática para estos artículos.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.2)
*   **Fix Pre-empaque para Unidades:** Se ajustó el valor inyectado de `01 01` a `00 01` para habilitar la impresión automática en artículos no pesables.

### 🗓️ 2026-04-10 (v3.5.1) - EL GANADOR
*   **Restauración de Velocidad:** Batch 1000 sin separadores + Detección flexible de éxito (`: 0`).

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **NO USAR SEPARADORES DE LÍNEA** en el archivo `F37`.
2. **USE 00 01** para el inyector de pre-empaque tras `FF 09`.

---
*Última actualización: 2026-04-10 (v3.5.2)*
