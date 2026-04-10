# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.7

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Barcode (Fix v3.5.7):** Estructura **3+3 Bytes BCD Puros**. 
    *   **Bytes 18-20:** PLU del artículo (3 bytes). *Ejemplo PLU 22:* `00 00 22`.
    *   **Bytes 21-23:** Peso/Cant (3 bytes). `11 11 11` (Pesable) o `00 00 01` (Unidad).
    *   **Lógica:** Esta alineación por bytes evita desplazamientos y permite que la balanza identifique el código 22 y la cantidad 1 perfectamente, aplicando el prefijo "2" configurado internamente.

### 🏷️ Estructura de Pre-empaque (v3.5.3+)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1). Habilita la impresión automática.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.7)
*   **Aparición del Formato de Bytes 3+3:** Implementación definitiva por alineación hexadecimal de bytes para el código de barras, eliminando errores de conversión de strings.

---
*Última actualización: 2026-04-10 (v3.5.7)*
