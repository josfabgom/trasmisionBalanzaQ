# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.11

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Configuración de Pre-empaque (Fix v3.5.11):** 
    *   **Precio:** 2 bytes BCD en Offset 12 (Anula desplazamiento de datos).
    *   **Inyector:** Byte 16 = `0x05`.
    *   **Barcode (Unidades):** Relleno `1110811` para forzar **Formato de Unidad (08)** en lugar de Peso (11).
    *   **Marcadores de Nombre:** `01 01` (Bytes 33-34).
*   **Barcode:** Estructura original funcional.
    *   **Mapeo:** PLU (5) + Relleno (7).
    *   *Ejemplo PLU 22 (Unidad):* `00 02 21 11 08 11` (BCD).

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.11)
*   **Ajuste de Precisión Hex:** 
    *   Se movió el precio a 2 bytes BCD en el offset 12 para coincidir con la trama modelo `09 80`.
    *   Se cambió el relleno del código de barras de unidades a `1110811` para apuntar al formato `08` (Unidad).

### 🗓️ 2026-04-10 (v3.5.10)
*   **Hex Fix - Pre-empaque Unidades:** Implementación inicial de correcciones basadas en análisis hexadecimal comparativo.

### 🗓️ 2026-04-10 (v3.5.9)
*   **Reversión de Código de Barras (Rollback):** Restauración desde backup funcional.

---
*Última actualización: 2026-04-10 (v3.5.11)*
