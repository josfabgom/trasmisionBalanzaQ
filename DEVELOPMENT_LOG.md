# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.5

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Barcode (Restore v3.5.5):** Estructura **6+6 Limpia**. 
    *   **Dígitos 1-6:** PLU exacto (6 dígitos).
    *   **Dígitos 7-12:** Filler de Peso/Cantidad (6 dígitos).
    *   **Importante:** Se eliminó la inyección manual del Flag '2' porque causaba desplazamientos de bytes. La balanza añade el Flag automáticamente.

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.5)
*   **Restauración de Barcode:** Regreso a la estructura 6+6 pura (PLU 6 + Peso 6) para asegurar alineación perfecta de bytes.

---
*Última actualización: 2026-04-10 (v3.5.5)*
