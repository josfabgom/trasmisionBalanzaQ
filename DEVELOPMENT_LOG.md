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
*   **Estructura de Lote (v3.4.1):** Concatenación continua de registros de **171 bytes** (342 caracteres hex). **SIN SALTOS DE LÍNEA** (`\n` o `\r\n`) entre registros para mantener la alineación perfecta del driver.

### 🏷️ Estructura de Barcode (EAN-13)
*   **Formato Usado:** **17** (Llave para Pre-empaque).
*   **Trama Item Code:** `000` + `PLU` + `111...` (Estructura de 12 dígitos).

### ⚙️ Lógica de Sincronización
*   **Inyector de Pre-empaque:** Cantidad `01 01` forzada en los bytes **+9 y +10** tras el marcador `FF 09` en la cola del artículo.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.4.3)
*   **Alineación Total (Cadena Continua + WR 37):** Se eliminaron los saltos de línea entre registros para mantener la alineación de 171 bytes, y se mantuvo el comando original `WR 37`. Esta combinación elimina tanto el error de "Solo el primer PLU" como el error `WRIT_FILE_ERR`.

### 🗓️ 2026-04-10 (v3.4.2)
*   **Restauración de Comando Original:** Se volvió al formato de comando `WR 37 {IP}` tras detectar que el formato anterior (`/I: /S: /F:`) provocaba un error `WRIT_FILE_ERR` en la versión del driver del cliente. Se mantuvo el uso de `\n` como separador que ha demostrado ser estable.

### 🗓️ 2026-04-10 (v3.4.1)
*   **Transmisión en Cadena Continua:** Se eliminaron los saltos de línea (`\r\n`) entre registros en el envío por lotes. Esto asegura que la alineación de 171 bytes por registro sea perfecta y que el driver no se desplace al leer el segundo PLU. Es la solución técnica estándar para archivos de longitud fija.

### 🗓️ 2026-04-09 (v3.4.0)
*   **Regreso a Batch con Auditoría y Fix Inyector:** Se revirtió la estrategia de envío individual por el envío por lotes (1000 items). Se corrigió el inyector de Pre-empaque (Bytes +9 y +10) y se restauró el sistema de auditoría y logs.

### 🗓️ 2026-04-09 (v3.3.0 - v3.3.9)
*   **Pruebas de Transmisión Individual:** Se experimentó con envíos de 1 por 1 con delays (v3.3.8 y v3.3.9), pero se descartaron por inestabilidad de red y lentitud. La lección aprendida fue que el problema no era la cantidad de archivos, sino la alineación de bytes causada por los saltos de línea.

### 🗓️ 2026-04-09 (v3.2.7)
*   **Estrategia de Clonación de Plantilla:** Se implementó `templateBytes.Clone()` para asegurar que cada registro parta de una base binaria conocida y funcional al 100%.

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **NO AGREGAR SALTOS DE LÍNEA** en el archivo F37. El driver `digiwtcp` requiere flujo continuo de registros de 171 bytes.
2. **MODO AUTOMÁTICO:** Siempre inyectar `01 01` en los bytes +9 y +10 después de `FF 09`.
3. **BARCODE:** Mantener el patrón `000 + PLU` como Item Code.

---
*Última actualización: 2026-04-10*
