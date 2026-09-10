# ============================================================
#  ActualizadorBQ.ps1 — Cliente Actualizador BalanzaQ
#  Detecta un ZIP de update en USB/carpeta local y lo aplica
#  preservando datos y licencia del cliente.
# ============================================================

$ErrorActionPreference = "Stop"
$AppDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Archivos del cliente que NUNCA se tocan
$Protegidos = @(
    "balanzas.db","balanzas.db-shm","balanzas.db-wal","balanzaQ.db",
    "license.lic","license.lic.bak",
    "appsettings.json","appsettings.Development.json",
    "TEMPLATE.DAT","fatal_error.txt","error_log.txt",
    "version_local.json","web.config"
)

# Version local instalada
$VersionLocalPath = Join-Path $AppDir "version_local.json"
$VersionLocal = "0.0.0"
if (Test-Path $VersionLocalPath) {
    try {
        $vl = Get-Content $VersionLocalPath -Raw | ConvertFrom-Json
        $VersionLocal = $vl.version
    } catch { $VersionLocal = "0.0.0" }
}

# ── Buscar version.json en USB / carpeta local / escritorio ──
function Find-UpdateSource {
    param([string]$AppDir, [string]$VersionLocal)

    $desktop = [System.Environment]::GetFolderPath("Desktop")
    $buscar = [System.Collections.Generic.List[string]]::new()

    # Escritorio y subcarpeta BalanzaQ en escritorio
    $buscar.Add($desktop)
    $buscar.Add((Join-Path $desktop "BalanzaQ"))

    # Todas las unidades del sistema (busca en raiz y subcarpeta BalanzaQ)
    Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Root -and (Test-Path $_.Root) } | ForEach-Object {
        $buscar.Add($_.Root)
        $sub = Join-Path $_.Root "BalanzaQ"
        if (Test-Path $sub) { $buscar.Add($sub) }
    }

    foreach ($ruta in $buscar) {
        if (-not (Test-Path $ruta)) { continue }
        $vj = Join-Path $ruta "version.json"
        if (-not (Test-Path $vj)) { continue }

        # Ignorar el version.json de la propia carpeta de la app
        if ($ruta -eq $AppDir) { continue }

        try {
            $vdata = Get-Content $vj -Raw | ConvertFrom-Json
            # Solo si tiene un ZIP asociado
            if (-not [string]::IsNullOrEmpty($vdata.zip_nombre)) {
                $zipCandidate = Join-Path $ruta $vdata.zip_nombre
                if (Test-Path $zipCandidate) {
                    return @{ Path = $ruta; VersionJson = $vj; Data = $vdata }
                }
            }
        } catch { continue }
    }
    return $null
}

$fuente = Find-UpdateSource -AppDir $AppDir -VersionLocal $VersionLocal
if ($null -eq $fuente) { exit 0 }   # Sin update disponible, arrancar normal

$vjR = $fuente.Data
$VersionRemota = $vjR.version

# ── Comparar versiones ──
function Compare-Versions($a, $b) {
    try { return ([System.Version]::Parse($a)).CompareTo([System.Version]::Parse($b)) }
    catch { return [string]::Compare($a, $b) }
}

if ((Compare-Versions $VersionRemota $VersionLocal) -le 0) { exit 0 }

$zipPath = Join-Path $fuente.Path $vjR.zip_nombre

# ── Ventana de confirmacion ──
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$msgTexto = @"
Hay una actualizacion disponible para BalanzaQ.

   Version instalada : $VersionLocal
   Nueva version     : $VersionRemota

   $($vjR.descripcion)

Desea instalar la actualizacion ahora?

(Sus datos y licencia seran preservados)
"@

$respuesta = [System.Windows.Forms.MessageBox]::Show(
    $msgTexto,
    "BalanzaQ — Nueva Version Disponible",
    [System.Windows.Forms.MessageBoxButtons]::YesNo,
    [System.Windows.Forms.MessageBoxIcon]::Information
)
if ($respuesta -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }

# ── Detener app si esta corriendo ──
Get-Process -Name "BalanzaQ.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 1500

# ── Backup de archivos protegidos ──
$backupDir = Join-Path $AppDir ".backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

foreach ($f in $Protegidos) {
    $src = Join-Path $AppDir $f
    if (Test-Path $src) {
        $dst = Join-Path $backupDir $f
        $dstParent = Split-Path $dst -Parent
        if (-not (Test-Path $dstParent)) { New-Item -ItemType Directory $dstParent -Force | Out-Null }
        Copy-Item $src $dst -Force
    }
}

# Backup DAT de balanzas Digi
$digiSrc    = Join-Path $AppDir "Digi"
$digiBackup = Join-Path $backupDir "Digi"
if (Test-Path $digiSrc) {
    New-Item -ItemType Directory $digiBackup -Force | Out-Null
    Get-ChildItem $digiSrc -Filter "SM*.DAT" -ErrorAction SilentlyContinue | Copy-Item -Destination $digiBackup -Force
    $rf = Join-Path $digiSrc "result"
    if (Test-Path $rf) { Copy-Item $rf $digiBackup -Force }
}

# ── Barra de progreso ──
$form = New-Object System.Windows.Forms.Form
$form.Text = "BalanzaQ — Actualizando..."
$form.Size = New-Object System.Drawing.Size(440, 125)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.ControlBox = $false
$form.TopMost = $true

$lbl = New-Object System.Windows.Forms.Label
$lbl.Text = "Aplicando actualizacion v$VersionRemota... Por favor espere."
$lbl.Location = New-Object System.Drawing.Point(20, 18)
$lbl.Size = New-Object System.Drawing.Size(390, 22)
$lbl.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$form.Controls.Add($lbl)

$bar = New-Object System.Windows.Forms.ProgressBar
$bar.Location = New-Object System.Drawing.Point(20, 50)
$bar.Size = New-Object System.Drawing.Size(390, 22)
$bar.Style = "Marquee"
$bar.MarqueeAnimationSpeed = 25
$form.Controls.Add($bar)
$form.Show(); $form.Refresh()

# ── Extraer ZIP y copiar archivos ──
try {
    $tmpDir = Join-Path $env:TEMP "BQ_update_extract"
    if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tmpDir)

    Get-ChildItem $tmpDir -Recurse -File | ForEach-Object {
        $relPath = $_.FullName.Substring($tmpDir.Length).TrimStart("\", "/")
        $baseName = Split-Path $relPath -Leaf

        # Verificar si esta protegido
        $protegido = ($Protegidos -contains $baseName) `
                  -or ($relPath -match "^Digi[/\\]SM.*\.DAT$") `
                  -or ($relPath -match "^Digi[/\\]result$")

        if (-not $protegido) {
            $destino = Join-Path $AppDir $relPath
            $destinoDir = Split-Path $destino -Parent
            if (-not (Test-Path $destinoDir)) { New-Item -ItemType Directory $destinoDir -Force | Out-Null }
            Copy-Item $_.FullName $destino -Force
        }
    }
    Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    $form.Close()
    # Rollback
    foreach ($f in $Protegidos) {
        $bs = Join-Path $backupDir $f
        if (Test-Path $bs) { Copy-Item $bs (Join-Path $AppDir $f) -Force -ErrorAction SilentlyContinue }
    }
    [System.Windows.Forms.MessageBox]::Show(
        "Error al aplicar la actualizacion:`n$_`n`nSus datos fueron restaurados.",
        "Error — BalanzaQ Actualizador",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}

# ── Restaurar datos protegidos del backup ──
foreach ($f in $Protegidos) {
    $bs = Join-Path $backupDir $f
    if (Test-Path $bs) { Copy-Item $bs (Join-Path $AppDir $f) -Force -ErrorAction SilentlyContinue }
}
if (Test-Path $digiBackup) {
    Get-ChildItem $digiBackup -ErrorAction SilentlyContinue | Copy-Item -Destination $digiSrc -Force -ErrorAction SilentlyContinue
}

# ── Guardar nueva version local ──
@{ version = $VersionRemota; fecha_update = (Get-Date -Format "yyyy-MM-dd HH:mm") } `
    | ConvertTo-Json | Set-Content $VersionLocalPath -Encoding UTF8

$form.Close()

[System.Windows.Forms.MessageBox]::Show(
    "BalanzaQ actualizado exitosamente a la version $VersionRemota.`n`nLa aplicacion se iniciara ahora.",
    "Actualizacion Completada",
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information
) | Out-Null

exit 0
