# BalanzaQ Development & Implementation Log

Este documento sirve como memoria técnica y registro de decisiones críticas para mantener la estabilidad del sistema de transmisión Digi SM-300 y asegurar la continuidad entre sesiones de desarrollo.

## 🛠️ Especificaciones Técnicas Críticas (Ground Truth)

### 📡 Protocolo Digi SM-300
*   **Driver:** `digiwtcp.exe` localizado en `\Digi`.
*   **Estrategia de Ejecución:** Vía `run_sync.bat` para aislamiento de proceso y manejo de tiempos de espera (`15s timeout`).
*   **Archivos de Intercambio:**
    *   `SM<IP>F37.DAT`: Productos y Barcodes.
    *   `SM<IP>F52.DAT`: Formatos de Etiqueta.
    *   `RESULT`: Archivo de salida del driver.
*   **Estrategia de Transmisión (v3.4.5):** Sincronización **MASIVA (Batch)** mejorada. Se envían todos los registros en un solo archivo `F37` sin separadores (flujo continuo de 171 bytes por registro). Se utiliza el comando verificado `WR 37 {IP}`.

### 🏷️ Estructura de Barcode (EAN-13)
*   **Formato Usado:** **17** (Llave para Pre-empaque).
*   **Trama Item Code:** `000` + `PLU` + `111...` (Estructura de 12 dígitos).

### ⚙️ Lógica de Sincronización
*   **Inyector de Pre-empaque:** Cantidad `01 01` forzada en los bytes **+9 y +10** tras el marcador `FF 09` en la cola del artículo.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.4.5)
*   **Lote Continuo Definitivo:** Se regresó al modo Batch (hasta 1000 items) pero eliminando definitivamente los saltos de línea. El archivo es una cadena binaria pura de registros de 171 bytes. Comando verificado: `WR 37`.

### 🗓️ 2026-04-10 (v3.4.4)
*   **Sincronización Individual:** Intento de envío 1-a-1. Descartado por saturar el driver y causar errores de lectura de archivo (`READ_FILE_ERR`).

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **NO USAR SALTOS DE LÍNEA** en transmisiones masivas. El driver requiere registros de 171 bytes alineados perfectamente.
2. **MODO AUTOMÁTICO:** Siempre inyectar `01 01` en los bytes +9 y +10 después de `FF 09`.
3. **COMANDO:** Mantener `digiwtcp.exe WR 37 {IP}` como estándar de comunicación.

---
*Última actualización: 2026-04-10*
