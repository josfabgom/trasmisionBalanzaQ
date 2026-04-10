# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.8

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Barcode (Fix v3.5.8):** Estructura **02-5-5**. 
    *   **Dígitos 1-2:** `02` (Asegura Flag 2 en la segunda posición).
    *   **Dígitos 3-7:** PLU exacto (5 dígitos).
    *   **Dígitos 8-12:** Peso/Cant filler (5 dígitos).
    *   *Ejemplo PLU 22:* `020002200001`.

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.8)
*   **Aparición del Formato 02-5-5:** Fix definitivo para el código de barras alineando la bandera al segundo dígito para evitar el error de PLU desplazado.

---
*Última actualización: 2026-04-10 (v3.5.8)*
