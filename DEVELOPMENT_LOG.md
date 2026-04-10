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
*   **Estrategia de Transmisión (v3.4.4):** Sincronización **INDIVIDUAL** (1 a 1). Se envía un archivo con un solo registro y se ejecuta el driver. Esto soluciona la limitación del driver de solo procesar el primer PLU de un lote.

### 🏷️ Estructura de Barcode (EAN-13)
*   **Formato Usado:** **17** (Llave para Pre-empaque).
*   **Trama Item Code:** `000` + `PLU` + `111...` (Estructura de 12 dígitos).

### ⚙️ Lógica de Sincronización
*   **Inyector de Pre-empaque:** Cantidad `01 01` forzada en los bytes **+9 y +10** tras el marcador `FF 09` en la cola del artículo.

---

## 📜 Historial de Cambios Recientes

### 🗓️ 2026-04-10 (v3.4.4)
*   **Sincronización Individual Robusta:** Se cambió la estrategia a envío de 1-a-1 usando el comando verificado `WR 37`. Esto garantiza que todos los PLUs sean procesados por la balanza, evitando el cierre prematuro de conexión del driver tras el primer artículo.

### 🗓️ 2026-04-10 (v3.4.3)
*   **Alineación Total (Cadena Continua + WR 37):** Intento de envío masivo sin saltos de línea. Descartado porque el driver solo lee el primer registro de todas formas.

### 🗓️ 2026-04-10 (v3.4.1 - v3.4.2)
*   **Pruebas de Formato de Comando:** Se identificó que el formato `/I: /S: /F:` no era compatible con el driver del cliente, restaurando el comando `WR 37`.

---

## ⚠️ Reglas de Oro para Futuros Cambios
1. **USAR TRANSMISIÓN INDIVIDUAL** si el lote no llega completo. El driver parece tener limitaciones de buffer para múltiples registros en un solo archivo.
2. **MODO AUTOMÁTICO:** Siempre inyectar `01 01` en los bytes +9 y +10 después de `FF 09`.
3. **COMANDO:** Mantener `digiwtcp.exe WR 37 {IP}` como estándar de comunicación.

---
*Última actualización: 2026-04-10*
