# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.6

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Barcode (Fix v3.5.6):** Estructura Estricta **2-5-5**. 
    *   **Flag (2 dígitos):** `20`.
    *   **PLU (5 dígitos):** Código con ceros a la izquierda.
    *   **Peso/Cantidad (5 dígitos):** `11111` (Pesable) o `00001` (Unidad).
    *   *Ejemplo PLU 22:* `200002200001` (12 dígitos).

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.6)
*   **Aparición del Formato 2-5-5:** Implementación de la estructura definitiva solicitada, corrigiendo el desplazamiento de bits al separar el flag del PLU.

---
*Última actualización: 2026-04-10 (v3.5.6)*
