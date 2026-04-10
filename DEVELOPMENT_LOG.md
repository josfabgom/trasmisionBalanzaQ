# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.12

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Herramienta de Diagnóstico (v3.5.12):** 
    *   **Consola Hex:** Nueva sección en Mantenimiento para envío manual de tramas crudas (Archivo 37). Permite pruebas en caliente sin recargas de código.
*   **Configuración de Pre-empaque (Fix v3.5.11):** 
    *   **Precio:** 2 bytes BCD en Offset 12 (Anula desplazamiento de datos).
    *   **Inyector:** Byte 16 = `0x05`.
    *   **Barcode (Unidades):** Relleno `1110811` para forzar **Formato de Unidad (08)** en lugar de Peso (11).
    *   **Marcadores de Nombre:** `01 01` (Bytes 33-34).

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.12)
*   **Nueva Funcionalidad - Consola de Pruebas Hex:** Se añadió una sección en la página de Mantenimiento para enviar tramas hexadecimales manualmente. Esto permite a los técnicos realizar pruebas unitarias rápidas con diferentes valores hexadecimales para depurar el comportamiento de la balanza.

### 🗓️ 2026-04-10 (v3.5.11)
*   **Ajuste de Precisión Hex:** Offset de precio y formato 08.

### 🗓️ 2026-04-10 (v3.5.9)
*   **Reversión de Código de Barras (Rollback):** Restauración desde backup funcional.

---
*Última actualización: 2026-04-10 (v3.5.11)*
