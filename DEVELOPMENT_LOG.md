# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth)

### 📡 Protocolo Digi SM-300
*   **Driver:** `digiwtcp.exe` localizado en `\Digi`.
*   **Estrategia de Ejecución:** Vía `run_sync.bat` para aislamiento de proceso y manejo de tiempos de espera (`15s timeout`).
*   **Archivos de Intercambio:**
    *   `SM<IP>F37.DAT`: Productos y Barcodes.
    *   `RESULT`: Archivo de confirmación.
*   **Estrategia de Transmisión (v3.4.8):** Sincronización **MASIVA** con alineación perfecta. Se usa `\r\n` (CRLF) como separador de registros para asegurar que el driver identifique cada PLU correctamente. Se utiliza el comando verificado `WR 37 {IP}`.

### 🏷️ Estructura de Registro
*   **Tamaño:** 171 bytes fijos.
*   **Marcador de Nombre:** Se reemplaza `03 07` por `01 01` (pattern detectado en muestras exitosas).
*   **Barcode (EAN-13):** Formato **17** (Llave para Pre-empaque).

### ⚙️ Lógica de Sincronización
*   **Inyector de Pre-empaque:** Cantidad `01 01` forzada en los bytes **+9 y +10** tras el marcador `FF 09` en la cola del artículo.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.4.8)
*   **Alineación CRLF + 0101 Pattern:** Se optimizó el envío masivo usando saltos de línea estilo Windows (`\r\n`) y ajustando el marcador de nombre a `01 01`. Esto resuelve el problema de que solo pasara el primer PLU del lote.

### 🗓️ 2026-04-10 (v3.4.5 - v3.4.7)
*   **Pruebas de Cadena Continua:** Se probó el envío sin separadores, resultando en inestabilidad para el segundo artículo en adelante.

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **USAR CRLF (\r\n)** como separador entre registros en el archivo `F37` para envíos masivos.
2. **MAINTAIN 01 01 MARKER** antes del nombre para asegurar la identificación visual en la balanza.
3. **COMANDO:** Mantener `digiwtcp.exe WR 37 {IP}`.

---
*Última actualización: 2026-04-10*
