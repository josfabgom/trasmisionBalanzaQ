# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.13

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Réplica de Trama Funcional (v3.5.13):** 
    *   **Sección:** Bytes 24-25 = `10 03` (Crítico para pre-empaque).
    *   **Control de Nombre:** Byte extra `07` en `numNameStart + 2`.
    *   **Precio:** 2 bytes BCD en Offset 12.
    *   **Barcode (Unidades):** Relleno `1111111` (Todo unos), produciendo `21 11 11 11`.
    *   **Marcadores de Nombre:** `01 01` (Bytes 33-34).
*   **Resultados Label:** Flag 2 + PLU (6) + Qty (5) + Checksum (1).

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.13)
*   **Réplica Forense de Trama:** Se ajustó el generador de registros para igualar bit a bit la trama manual exitosa.
    *   Se corrigió la sección a `10 03`.
    *   Se añadió el byte de control `07` que desplaza el nombre.
    *   Se revirtió el relleno de unidades a `1111111`.

### 🗓️ 2026-04-10 (v3.5.12)
*   **Funcionalidad Diagnóstico:** Consola de pruebas Hex en Mantenimiento.

### 🗓️ 2026-04-10 (v3.5.9)
*   **Reversión de Código de Barras (Rollback):** Restauración desde backup funcional.

---
*Última actualización: 2026-04-10 (v3.5.11)*
