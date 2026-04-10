# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth) - v3.5.14

### 📡 Protocolo Digi SM-300 (Driver Singapur 2.10.1)
*   **Estabilidad de Protocolo (v3.5.14):** 
    *   **Tamaño de Registro:** Cada registro F37 debe medir exactamente **132 bytes**. Se usa padding/completado desde el template para evitar `READ_FILE_ERR`.
    *   **Precio (Unidades):** Multiplicador omitido (`Math.Round(Price)`) para evitar desborde BCD en precios altos (>999.9) y error de escala decimal 10x.
    *   **Sección:** Bytes 24-25 = `10 03`.
    *   **Control de Nombre:** Byte extra `07` en `numNameStart + 2`.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.5.14)
*   **Fix de Estabilidad y Precio:**
    1. Se estandarizó el tamaño del registro a 132 bytes fijos eliminando variaciones por longitud de nombre.
    2. Se corrigió el multiplicador de precio para artículos por unidad (eliminando el x10 innecesario).
    3. Se resolvió el error de auditoría `READ_FILE_ERR` al asegurar estructura íntegra de archivo.

### 🗓️ 2026-04-10 (v3.5.13)
*   **Réplica Forense de Trama:** Ajuste de sección `10 03` y byte `07`.

### 🗓️ 2026-04-10 (v3.5.9)
*   **Reversión de Código de Barras (Rollback):** Restauración desde backup funcional.

---
*Última actualización: 2026-04-10 (v3.5.11)*
