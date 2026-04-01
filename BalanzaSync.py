import os
import binascii
import subprocess
import shutil

# Rutas de los archivos
PATH_BASE = r"D:\Antigravity Proyectos\trasmisionBalanzaQ"
PATH_DATA = os.path.join(PATH_BASE, "data.dat")
IP_BALANZA = "192.168.157.200"
FILE_DAT = f"SM{IP_BALANZA}F37.DAT"
PATH_DAT = os.path.join(PATH_BASE, FILE_DAT)
PATH_TEMPLATE = os.path.join(PATH_BASE, "TEMPLATE.DAT")
PATH_DIGI = os.path.join(PATH_BASE, "Digi", "digiwtcp.exe")

def bcd_encode(val, length):
    """Convierte un entero a su representacion BCD (Hex) empaquetado"""
    s = str(int(val)).zfill(length*2)
    return binascii.unhexlify(s)

def main():
    print(f"=== Sincronizador DIGI SM-300 P ===")
    
    # 1. Asegurar template base (solo la primera vez copiamos el existente)
    if not os.path.exists(PATH_TEMPLATE):
        if not os.path.exists(PATH_DAT):
            print(f"ERROR: Falta el archivo base {FILE_DAT} para usar como plantilla.")
            return
        shutil.copyfile(PATH_DAT, PATH_TEMPLATE)
        print("-> Plantilla copiada.")
    
    # 2. Leer la plantilla en bytes
    with open(PATH_TEMPLATE, 'r', encoding='ascii') as f:
        template_hex = f.read().strip()
        # Si tiene el E2 final de cuando se descargaba, lo ignoramos para la decodificación
        if template_hex.endswith("E2"):
            template_hex = template_hex[:-2]
        template_bytes = binascii.unhexlify(template_hex)
        
    # Extraer el bloque dinámicamente usando el separador de nombre (03 07)
    idx_0307 = template_bytes.find(b'\x03\x07')
    if idx_0307 == -1:
        print("ERROR: Formato del archivo F37 no reconocido (plantilla invalida).")
        return
        
    name_len = template_bytes[idx_0307 + 2]
    before_name = bytearray(template_bytes[:idx_0307 + 3])
    after_name = bytearray(template_bytes[idx_0307 + 3 + name_len:])
    
    # 3. Leer y procesar linea por linea desde data.dat
    if not os.path.exists(PATH_DATA):
        print(f"ERROR: No se encuentra el archivo {PATH_DATA}")
        return
        
    with open(PATH_DATA, 'r', encoding='utf-8') as f:
        lineas = [x.strip() for x in f.readlines() if x.strip()]
        
    out_payload = bytearray()
    
    print(f"-> Procesando {len(lineas)} productos...")
    procesados = 0
    
    for linea in lineas:
        partes = linea.split(';')
        if len(partes) < 14:
            continue
            
        plu_id = int(partes[0])
        nombre_str = partes[3] 
        # Formatear el precio usando float por seguridad, pasando a centavos. Eje: "14099.00" -> 1409900
        try:
            precio_centavos = int(float(partes[6]) * 100)
        except ValueError:
            continue
            
        # Truncar nombre a maximo tamaño permitido, ej 24 (el template original era 23)
        # Tomaremos la longitud real en caracteres Ascii
        nombre_bytes = nombre_str.encode('ascii', errors='ignore')[:30] # Limitar a 30 caracteres
        
        # Copiar plantilla "antes del nombre"
        cur_before = bytearray(before_name)
        
        # Modificar PLU (Bytes 0 a 3)
        cur_before[0:4] = bcd_encode(plu_id, 4)
        
        # Modificar Precio (Bytes 11 a 14) 
        cur_before[11:15] = bcd_encode(precio_centavos, 4)
        
        # Modificar Longitud de Nombre
        cur_before[-1] = len(nombre_bytes)
        
        # Agregar al gran archivo hexadecimal
        plu_record = cur_before + nombre_bytes + after_name
        out_payload.extend(plu_record)
        procesados += 1
        
    # 4. Guardar archivo final SM{IP}F37.DAT (Sin byte E2, solo hex)
    hex_str = binascii.hexlify(out_payload).decode('ascii').upper()
    with open(PATH_DAT, 'w', encoding='ascii') as f:
        f.write(hex_str)
        
    print(f"-> Archivo generado exitosamente con {procesados} PLUs ({len(hex_str)} caracteres hex).")
    
    # 5. Enviar a la Balanza ejecutando digiwtcp.exe WR 37 IP
    print(f"-> Conectando a balanza Digi en {IP_BALANZA}...")
    cwd = os.path.dirname(PATH_DIGI)
    
    # IMPORTANTE: El digiwtcp busca el archivo en el CWD, así que debemos asegurar que el DAT esté donde está digiwtcp.exe.
    # Vamos a copiar el DAT generado a la carpeta Digi para que lo tome.
    PATH_DAT_DIGI = os.path.join(cwd, FILE_DAT)
    shutil.copyfile(PATH_DAT, PATH_DAT_DIGI)
    
    comando = f'"{PATH_DIGI}" WR 37 {IP_BALANZA}'
    try:
        subprocess.run(comando, cwd=cwd, shell=True, check=True)
        # Leer RESULT
        path_result = os.path.join(cwd, "RESULT")
        if os.path.exists(path_result):
            with open(path_result, "r") as res_file:
                codigo = res_file.read().strip()
            print(f"-> TRANSFERENCIA COMPLETADA. Codigo de retorno: [{codigo}] (0 = Exitoso)")
        else:
            print("-> Transferencia completada, pero no se genero el archivo RESULT.")
    except Exception as e:
        print(f"ERROR al intentar subir archivo a balanza: {e}")

if __name__ == '__main__':
    main()
