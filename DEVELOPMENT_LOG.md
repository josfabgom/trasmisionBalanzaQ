# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.3

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Driver:** `digiwtcp.exe` (Versión 2.10.1.0, 2018, English Singapore).
*   **Estrategia de Transmisión Definitive:** Sincronización **MASIVA (Batch)** con registros continuos (Sin separadores).
*   **Alineación de Registro:** 171 bytes exactos.
*   **Barcode (Fix v3.5.3):** Estructura de **6 dígitos para PLU** + **6 dígitos para Peso/Filler**. 
    *   *Ejemplo PLU 22:* `00 00 22 11 11 11`. Esto corrige el problema donde la cantidad aparecía en cero en la balanza por desalineación de bytes.

### 🏷️ Estructura de Pre-empaque (v3.5.3)
*   **Inyector de Pre-empaque:** Valor **`00 01`** (Cantidad 1) tras el marcador `FF 09`. Habilita la impresión automática en artículos de unidad.
*   **Estabilidad de Archivo:** Escritura **síncrona** del archivo `.DAT` con un retraso de 150ms antes de ejecutar el driver para evitar el error `READ_FILE_ERR` (bloqueo de archivo por el sistema operativo).

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.3)
*   **Alineación de Barcode:** Fix de 6+6 dígitos para asegurar que el PLU y la cantidad se ubiquen en los bytes correctos.
*   **Fix READ_FILE_ERR:** Implementada escritura síncrona y delay de seguridad.

### 🗓️ 2026-04-10 (v3.5.1 - v3.5.2)
*   **Hito de Velocidad:** Restauración de modo Batch y fix de inyector de pre-empaque (`00 01`).

---
*Última actualización: 2026-04-10 (v3.5.3)*
