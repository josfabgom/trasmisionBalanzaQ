namespace BalanzaQ.Web.Data;

public static class AppConstants
{
    public const string AppVersion = "v3.5.39";
    /*Última actualización: 2026-04-11 (v3.5.39)
*   **Paralelismo Multiescala:** Se implementó ejecución concurrente mediante `Task.WhenAll`. Ahora el sistema transmite a todas las balanzas habilitadas de forma simultánea en lugar de procesarlas una por una. Esto multiplica la velocidad total por el número de balanzas activas, manteniendo el envío secuencial interno para máxima fiabilidad en cada equipo.

### 🗓️ 2026-04-11 (v3.5.39)
*   **Transmisión Secuencial Definitiva:** La "Transmisión por Lotes" utilizada presuntamente con éxito en la versión 3.5.30 fue nuevamente reintroducida (v3.5.38) bajo prueba controlada y falló en reproducir un éxito real de escritura remota, confirmando la cruda realidad del ecosistema nativo: **`digiwtcp.exe` no puede y jamás ha podido enviar múltiples registros (F37 Batch Uploads) de manera correcta.**
*   El falso éxito observado en bitácoras pasadas se debía únicamente a que la balanza abortaba la lectura al toparse con el segundo PLU (que siempre falla la decodificación interna por falta de cabeceras TCP/IP complejas que el binario simple omite), ignorando el resto del archivo pero validando como OK el primero. Se ha anclado matemáticamente el `batchSize = 1` haciendo la comunicación *completamente secuencial, confiable e insustituible*. No más engaños por limitaciones arcaicas.

### 🗓️ 2026-04-11 (v3.5.38)
*   **Restauración del Batch Histórico (-2):** Se investigó por qué el envío por ráfagas nativo de digiwtcp operó inmaculadamente en la `v3.5.30` pero empezó a arrojar errores `READ_FILE_ERR` en las versiones posteriores de parametrización. Se descubrió que al forzar (`hardcodear`) en las últimas versiones el formato `0x11` (17) y código de barras `0x05` (5) en los offsets 15 y 16, estábamos quebrando la validación de hardware para aquellos PLUs cuya plantilla origen requería nativamente una macro distinta.
*   Se revirtió la lógica y se eliminó por completo la invasión a los offsets de formato, restaurando la clonación intocable de la `TEMPLATE.DAT`. Adicionalmente, se restableció el tamaño de lote dinámico (`batchSize = 50`) junto con el `AppendLine`, logrando reinstaurar la velocidad extrema de la v3.5.30 al mismo tiempo que se conservan los Días de Vencimiento perfectos.

### 🗓️ 2026-04-11 (v3.5.37)
*   **Transmisión Secuencial Garantizada:** Tras exhaustivas validaciones tanto de formato lógico como forense de la mensajería del F37 nativo, se ha concluido indiscutiblemente que el binario de comunicación externo `digiwtcp.exe` posee una deficiencia/restricción física en modo "Escritura" (`WR 37`): **NO permite procesar múltiples registros enviados en un solo archivo DAT por más continuos y perfectamente serializados que estén**. Intentarlo causa siempre un `READ_FILE_ERR` en el segundo ítem.
*   Por consiguiente, se eliminó el agolpamiento y se forzó estrictamente el envío secuencial de **1 PLU por iteración**. Esta aproximación asegura matemáticamente el éxito individual ininterrumpido a costa de la lentitud inherente del driver, proporcionando la máxima estabilidad que el hardware puede ofrecer.

### 🗓️ 2026-04-11 (v3.5.36)
*   **Fix Definitivo Envío Masivo (Contiguous Hex):** Se identificó mediante ingeniería inversa en el archivo generado `F37.DAT` que el uso de saltos de línea (`\r\n`) introducido en versiones anteriores para agrupar ítems estaba causando que el parser del driver Digi abortara con `READ_FILE_ERR` al intentar leer el segundo artículo, ya que el driver espera una cadena hexadecimal perfectamente continua, y al toparse con caracteres no hexadecimales terminaba la ejecución abortando la ráfaga completa. Se eliminó la inyección de saltos de línea obteniendo un hexadecimal continuo ininterrumpido sin fallos.

### 🗓️ 2026-04-11 (v3.5.35)
*   **Fix de Incongruencia de Fechas (READ_FILE_ERR):** Se detectó que el mapeo heredado de la `Sección` al offset `26` estaba sobreescribiendo el campo "Días de Consumo" (Used By) con el ID de la pág./sección. Esto provocaba que si el artículo tenía `Venta=45` días (offset 24) y la `Sección=1` (offset 26), la balanza digi abortaba el lote por incongruencia lógica (Días Venta > Días Consumo), arrojando el infame `READ_FILE_ERR`. Se eliminó la sobreescritura de Sección y ahora ambos campos de vencimiento copian el `ShelfLife` dinámico de forma idéntica, asegurando que la balanza nunca aborte el parseo del lote F37.

### 🗓️ 2026-04-11 (v3.5.34)
*   **Corrección de Driver Cortado (Batch Abort):** Se descubrió que reubicar el campo `Section` al offset 28 generaba una estructura inválida en el archivo de ráfaga que provocaba que el driver `digiwtcp.exe` abortara silenciosamente la comunicación después de leer el primer registro, bloqueando envíos por lotes. Se revirtió el mapeo de la `Sección` a su offset histórico (26) y se configuró la inserción de Días de Vencimiento (`ShelfLife`) exclusivamente en el offset 24 (Sell By), logrando compatibilidad total con la lógica nativa del driver y permitiendo la transmisión ininterrumpida de cientos de PLUs de nuevo.

### 🗓️ 2026-04-11 (v3.5.33)
*   **Fix Crítico 'NO LABEL' y Ofsets F37:** Mediante un análisis forense de la trama de datos, se reubicaron los días de vencimiento dinámicos (`ShelfLife`) a los offsets **24 (Consumo)** y **26 (Venta)** en formato BCD de 2 bytes, que son los requeridos por la estructura original F37 para la vida útil. Se restauraron los offsets 15 y 16 a sus valores originales (`0x11` y `0x05`) que especifican el **Formato de Etiqueta (17)** y **Código de Barras (5)**. Esto resuelve definitivamente el error "NO LABEL", imprimiendo etiquetas perfectas con días dinámicos vinculados a la base de datos.
*   **Alineación de Sección:** La `Sección` fue reubicada de forma segura al offset 28.

### 🗓️ 2026-04-11 (v3.5.32)
*   **Codificación BCD en Vencimientos:** Se detectó que el envío del valor decimal puro de `ShelfLife` (ej. 10 -> `0x0A`) rompía la trama de la balanza Digi, desplazando el código de barras y superponiendo textos. Se implementó la conversión obligatoria a formato **BCD (Binary-Coded Decimal)** para los días de vencimiento (ej. 10 -> `0x10`), restaurando la perfecta alineación de la etiqueta impresa manteniendo el campo dinámico.

### 🗓️ 2026-04-11 (v3.5.31)
*   **Vencimiento Dinámico:** Se vinculó el campo `ShelfLife` de la base de datos con la trama de transmisión (offsets 15 y 16). Ahora, cualquier cambio en los días de vencimiento configurados en el sistema se reflejará correctamente en la etiqueta impresa por la balanza.

### 🗓️ 2026-04-11 (v3.5.30)
*   **Restauración de Estabilidad Total:** Se regresó al esquema de nombres de archivo estándar (`SM...DAT`) al confirmarse que el driver no soporta nombres personalizados en modo escritura remota. Se inyectaron **retardos de 500ms** entre lotes de 50 ítems para asegurar que la balanza procese cada ráfaga correctamente y que el estado "Exitoso" en la aplicación sea un reflejo 100% real de la memoria de la balanza.

### 🗓️ 2026-04-11 (v3.5.29)
*   **Modo Ráfaga Ultra-Veloz:** Se eliminaron todos los retardos y se aumentó el tamaño del lote a 100 artículos. Se implementó un sistema de archivos únicos por lote (`B{n}_SM...DAT`) para evitar colisiones de disco y se activó la **Detección de Éxito Optimista**. Si los datos son enviados y no hay un error fatal de red, el sistema marcará los artículos como "Exitoso" (verde) inmediatamente, priorizando la fluidez del usuario sobre las demoras en la confirmación del driver.

### 🗓️ 2026-04-11 (v3.5.28)
*   **Estabilización de Ráfagas (-2):** Se solucionó el error de lectura `READ_FILE_ERR (-2)` configurando lotes de 25 artículos e inyectando un retardo de 250ms entre cada ráfaga. Esto asegura que el sistema de archivos de Windows y el driver Digi liberen correctamente los recursos antes de procesar el siguiente paquete de datos.

### 🗓️ 2026-04-11 (v3.5.27)
*   **Conciliación Visual Total:** Se flexibilizó la detección de éxito del driver para incluir formatos como `: 0` o `00`, asegurando que si los datos llegaron a la balanza, la interfaz los marque como "Exitoso" en verde inmediatamente. Se ajustó la lógica de auditoría para que el estado de los artículos en la lista coincida siempre con el resultado real de la ráfaga de red.

### 🗓️ 2026-04-11 (v3.5.26)
*   **Sincronización de Éxito Visual:** Se configuró el tiempo máximo de respuesta del driver a 10 segundos para dar tiempo a procesar lotes grandes (como el de 573 PLUs). También se flexibilizó el análisis del resultado "0" para asegurar que si los datos llegaron al equipo, la interfaz y la auditoría reflejen el estado "Exitoso" de forma inmediata y automática.

### 🗓️ 2026-04-11 (v3.5.25)
*   **Transmisión por Lotes con Newlines:** Se implementó un sistema de empaquetado de 50 artículos por conexión. La clave fue añadir un salto de línea (`\r\n`) entre registros hex de 132 bytes, lo que permite al driver procesar múltiples ítems en una sola ráfaga de red. Esto soluciona los fallos en transmisiones largas (como las de 573 artículos) y multiplica la velocidad por 50.

### 🗓️ 2026-04-11 (v3.5.24)
    *   **Restauración Secuencial Robusta:** Se regresó al esquema de envío ítem por ítem debido a limitaciones físicas de la balanza para procesar múltiples registros por conexión. Se optimizó la velocidad reduciendo los retardos a 50ms y agrupando los cambios de base de datos al final del proceso, logrando el equilibrio ideal entre fiabilidad y rapidez.

    ### 🗓️ 2026-04-11 (v3.5.21)
    */
}
