# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth)

### 📡 Protocolo Digi SM-300
*   **Driver:** `digiwtcp.exe` localizado en `\Digi`.
*   **Estrategia de Ejecución:** Vía `run_sync.bat` para aislamiento de proceso y manejo de tiempos de espera (`15s timeout`).
*   **Archivos de Intercambio:**
    *   `SM<IP>F37.DAT`: Productos y Barcodes.
    *   `SM<IP>F52.DAT`: Formatos de Etiqueta.
    *   `RESULT`: Archivo de salida del driver (se lee con reintentos para evitar bloqueos).
*   **Códigos de Error Relevantes:**
    *   `0`: Éxito.
    *   `-5`: Error de red (No hay conexión a la balanza).
    *   `-9`: Error de escritura retornado por la balanza.

### 🏷️ Estructura de Barcode (EAN-13)
*   **Formato Usado:** **17** (Llave para Pre-empaque).
*   **Flag/Byte 16:** `0x05` (Habilita el modo correcto en SM-300).
*   **Composición de Trama (v3.1 2026-04-09):**
    *   `PLU (5 dígitos)` + `FILLER ("11111")` + `SUFFIX ("11")`.
    *   **Actualización:** Ahora se usa el filler `11111` tanto para pesables como para unidades en modo Pre-empaque para asegurar compatibilidad.

### 🧩 Diferencias Estructurales (7C vs 7D)
*   **Artículos Pesables (7C):**
    *   Longitud de Header: `numNameStart + 3`.
    *   Cola de Metadata: Típicamente `00 00 03 07 <LEN>`.
*   **Artículos por Unidad (7D):**
    *   Longitud de Header: **1 byte más corto** (`numNameStart + 2`).
    *   Cola de Metadata: Hardcoded a `01 01 07 <LEN>` basado en hex verificado.
    *   **Razón:** La escala Digi SM-300 requiere esta alineación específica para procesar correctamente productos no pesables en modo etiquetado automático.

### ⚙️ Lógica de Sincronización
*   **Batching:** Lotes de **1000 registros** para evitar saturación del puerto TCP/IP de la balanza.
*   **Mantenimiento:** Soportado `RD` (Lectura), `WR` (Escritura) y `DELFI` (Borrado integral).

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-09 (v3.2.6)
*   **Fix Crítico READ_FILE_ERR:** Corregido error de lectura del driver `digiwtcp`. Se agregaron saltos de línea (`Environment.NewLine`) entre registros PLU y se restauró la integridad del marcador de nombre `03 07` que estaba siendo sobreescrito.
*   **Vencimiento y Sección Dinámicos:** Se mapearon correctamente los campos `item.ShelfLife` y `item.Section` en la trama F37 (offsets 24-27). Ya no se usa el valor hardcodeado de 30 días.

### 🗓️ 2026-04-09 (v3.2.5)
*   **Cierre de Navegador:** Integración de JS (`window.close`) en la lógica de salida para cerrar la pestaña/limpiar pantalla antes de matar el proceso del servidor, evitando el mensaje de "Reconnecting".

### 🗓️ 2026-04-09 (v3.2.4)
*   **Fix Interactividad UI:** Implementado `@rendermode InteractiveServer` en `NavMenu.razor` para habilitar los eventos de clic (`@onclick`) que estaban inactivos por el modo estático de Blazor 8.

### 🗓️ 2026-04-09 (v3.2.3)

### 🗓️ 2026-04-08 (v3.1.0)
*   **Barra de Progreso:** Implementación en UI (`Articulos.razor`) con feedback en tiempo real `_currentProgress / _totalProgress`.
*   **Manejo de Procesos:** Migración a sistema de `.bat` temporal para mayor estabilidad en SingleFile deployments.

### 🗓️ 2026-04-07
*   **Corrección de EAN-13:** Ajuste de flags para excluir precios del código de barras.
*   **Sincronización Local:** Implementación de `ImportService` para leer `data.dat` (ABM) directamente.

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **NO TOCAR** el cálculo hexadecimal de la trama en `DigiService.cs` (sección Item Code) sin validar contra una balanza física o el "Hex Bueno" registrado.
2. **SIEMPRE** verificar la existencia de `TEMPLATE.DAT` antes de iniciar una sincronización masiva.
3. El driver `digiwtcp.exe` **DEBE** ejecutarse desde su directorio de trabajo (`\Digi`) para encontrar sus dependencias.

---
*Última actualización: 2026-04-09*
