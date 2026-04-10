# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.10

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Pre-empaque Fix (v3.5.10):** 
    *   **Inyector:** Byte 16 = `0x05`.
    *   **Relleno Pre-empaque (Unidades):** Uso de máscara de unos (`1111111`) en el campo de cantidad.
    *   **Marcadores de Nombre:** `01 01` (Bytes 33-34).
*   **Barcode:** Estructura original funcional.
    *   **Mapeo:** PLU (5) + Relleno (7).
    *   *Ejemplo PLU 22 (Unidad):* `00 02 21 11 11 11` (BCD).

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.10)
*   **Hex Fix - Pre-empaque Unidades:** Implementación de correcciones basadas en análisis hexadecimal comparativo.
    *   Se forzó el byte 16 a `05`.
    *   Se implementó relleno de `1`s en el código de barras para artículos por unidad.
    *   Se forzaron marcadores de nombre `01 01`.

### 🗓️ 2026-04-10 (v3.5.9)
*   **Reversión de Código de Barras (Rollback):** Se restauró el servicio desde el backup funcional eliminando la estructura 02-5-5.

---
*Última actualización: 2026-04-10 (v3.5.10)*
