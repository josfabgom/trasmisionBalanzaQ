# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.4

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Barcode (Fix v3.5.4):** Estructura **1-6-5**. 
    *   **Dígito 1:** `2` (Flag/Prefijo).
    *   **Dígitos 2-7:** PLU (6 dígitos con ceros).
    *   **Dígitos 8-12:** Filler de Peso/Cantidad (5 dígitos).
    *   *Ejemplo PLU 22:* `200002200001` (La balanza añade el dígito verificador 13).

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1) tras el marcador `FF 09`. 
*   **Estabilidad de Archivo:** Escritura síncrona + delay de 150ms.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.4)
*   **Estructura 1-6-5 del Barcode:** Implementación del formato exacto `Flag 2 + PLU 6 + Filler 5` para asegurar que el código de barras impreso sea idéntico al requerido por el cliente.

### 🗓️ 2026-04-10 (v3.5.3)
*   **Fix READ_FILE_ERR:** Estabilidad de escritura lograda con delay y sincronismo.

---
*Última actualización: 2026-04-10 (v3.5.4)*
