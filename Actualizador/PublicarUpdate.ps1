# ============================================================
#  PublicarUpdate.ps1 — BalanzaQ Update Publisher
#  Uso: Doble clic, o: .\PublicarUpdate.ps1 -Version "3.6.1" -Desc "Fix"
# ============================================================
param(
    [string]$Version = "",
    [string]$Desc    = "",
    [string]$CarpetaSalida = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRaiz    = Split-Path -Parent $ScriptDir
$ProyectoWeb = Join-Path $RepoRaiz "BalanzaQ.Web"
$PortableDir = Join-Path $RepoRaiz "BalanzaQ_Portable_Final"
$PublishTemp = Join-Path $RepoRaiz "publish_temp_update"

# ── Si no se pasaron parametros, pedir via formulario ────────
if ([string]::IsNullOrEmpty($Version)) {

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "BalanzaQ — Publicar Nueva Version"
    $form.Size = New-Object System.Drawing.Size(460, 320)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = "FixedDialog"
    $form.MaximizeBox = $false
    $form.TopMost = $true
    $form.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::Dpi

    $lbVer = New-Object System.Windows.Forms.Label
    $lbVer.Text = "Version nueva (ej: 3.6.1):"; $lbVer.Location = New-Object System.Drawing.Point(20,20); $lbVer.Size = New-Object System.Drawing.Size(250,22)
    $form.Controls.Add($lbVer)

    $txVer = New-Object System.Windows.Forms.TextBox
    $txVer.Location = New-Object System.Drawing.Point(20,45); $txVer.Size = New-Object System.Drawing.Size(400,26)
    $txVer.Text = "3.6.1"
    $form.Controls.Add($txVer)

    $lbDesc = New-Object System.Windows.Forms.Label
    $lbDesc.Text = "Descripcion del cambio:"; $lbDesc.Location = New-Object System.Drawing.Point(20,85); $lbDesc.Size = New-Object System.Drawing.Size(250,22)
    $form.Controls.Add($lbDesc)

    $txDesc = New-Object System.Windows.Forms.TextBox
    $txDesc.Location = New-Object System.Drawing.Point(20,110); $txDesc.Size = New-Object System.Drawing.Size(400,26)
    $txDesc.Text = "Actualizacion de sistema BalanzaQ"
    $form.Controls.Add($txDesc)

    $lbPath = New-Object System.Windows.Forms.Label
    $lbPath.Text = "Carpeta de salida (USB o local):"; $lbPath.Location = New-Object System.Drawing.Point(20,150); $lbPath.Size = New-Object System.Drawing.Size(250,22)
    $form.Controls.Add($lbPath)

    $txPath = New-Object System.Windows.Forms.TextBox
    $txPath.Location = New-Object System.Drawing.Point(20,175); $txPath.Size = New-Object System.Drawing.Size(320,26)
    $txPath.Text = "C:\Updates\BalanzaQ"
    $form.Controls.Add($txPath)

    $btnExaminar = New-Object System.Windows.Forms.Button
    $btnExaminar.Text = "..."
    $btnExaminar.Location = New-Object System.Drawing.Point(350,174); $btnExaminar.Size = New-Object System.Drawing.Size(70,28)
    $btnExaminar.Add_Click({
        $fbd = New-Object System.Windows.Forms.FolderBrowserDialog
        $fbd.Description = "Seleccionar carpeta de salida (USB, red, etc.)"
        $fbd.RootFolder = "MyComputer"
        if ($fbd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $txPath.Text = $fbd.SelectedPath
        }
    })
    $form.Controls.Add($btnExaminar)

    $btnOK = New-Object System.Windows.Forms.Button
    $btnOK.Text = "Publicar"
    $btnOK.Location = New-Object System.Drawing.Point(270,225); $btnOK.Size = New-Object System.Drawing.Size(100,32)
    $btnOK.BackColor = [System.Drawing.Color]::FromArgb(13,110,253)
    $btnOK.ForeColor = [System.Drawing.Color]::White
    $btnOK.FlatStyle = "Flat"
    $btnOK.DialogResult = [System.Windows.Forms.DialogResult]::OK
    $form.AcceptButton = $btnOK
    $form.Controls.Add($btnOK)

    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = "Cancelar"
    $btnCancel.Location = New-Object System.Drawing.Point(160,225); $btnCancel.Size = New-Object System.Drawing.Size(100,32)
    $btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $form.CancelButton = $btnCancel
    $form.Controls.Add($btnCancel)

    $result = $form.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }

    $Version       = $txVer.Text.Trim()
    $Desc          = $txDesc.Text.Trim()
    $CarpetaSalida = $txPath.Text.Trim()
}

if ([string]::IsNullOrEmpty($Version)) {
    [System.Windows.Forms.MessageBox]::Show("Debes ingresar un numero de version.","Error",[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
    exit 1
}

$ZipNombre = "BalanzaQ_update_$Version.zip"

# ── Progreso ─────────────────────────────────────────────────
$prog = New-Object System.Windows.Forms.Form
$prog.Text = "BalanzaQ — Publicando v$Version"
$prog.Size = New-Object System.Drawing.Size(460,150)
$prog.StartPosition = "CenterScreen"
$prog.FormBorderStyle = "FixedDialog"
$prog.ControlBox = $false
$prog.TopMost = $true
$prog.Font = New-Object System.Drawing.Font("Segoe UI",10)

$lblProg = New-Object System.Windows.Forms.Label
$lblProg.Location = New-Object System.Drawing.Point(20,20); $lblProg.Size = New-Object System.Drawing.Size(410,22)
$lblProg.Text = "Compilando proyecto..."
$prog.Controls.Add($lblProg)

$barProg = New-Object System.Windows.Forms.ProgressBar
$barProg.Location = New-Object System.Drawing.Point(20,55); $barProg.Size = New-Object System.Drawing.Size(410,22)
$barProg.Style = "Marquee"; $barProg.MarqueeAnimationSpeed = 25
$prog.Controls.Add($barProg)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Location = New-Object System.Drawing.Point(20,85); $lblSub.Size = New-Object System.Drawing.Size(410,22)
$lblSub.ForeColor = [System.Drawing.Color]::Gray; $lblSub.Text = ""
$prog.Controls.Add($lblSub)

$prog.Show(); $prog.Refresh()

function Set-Paso($txt, $sub="") {
    $lblProg.Text = $txt; $lblSub.Text = $sub; $prog.Refresh()
}

try {
    # PASO 1: Crear carpeta de salida
    if (-not (Test-Path $CarpetaSalida)) {
        New-Item -ItemType Directory -Path $CarpetaSalida -Force | Out-Null
    }

    # PASO 2: Compilar
    Set-Paso "Compilando proyecto (Release, self-contained)..." "Esto puede tardar 1-2 minutos"
    if (Test-Path $PublishTemp) { Remove-Item $PublishTemp -Recurse -Force }

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "dotnet"
    $pinfo.Arguments = "publish `"$ProyectoWeb`" --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true --output `"$PublishTemp`" --nologo"
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($pinfo)
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) {
        $err = $p.StandardError.ReadToEnd()
        throw "Error de compilacion: $err"
    }

    # PASO 3: Copiar extras
    Set-Paso "Copiando archivos del portable..." ""
    $digiDest = Join-Path $PublishTemp "Digi"
    if (-not (Test-Path $digiDest)) { New-Item -ItemType Directory $digiDest | Out-Null }
    $digiExe = Join-Path $PortableDir "Digi\digiwtcp.exe"
    if (Test-Path $digiExe) { Copy-Item $digiExe $digiDest -Force }

    $jdateOrig = Join-Path $PortableDir "Jdate"
    if (Test-Path $jdateOrig) { Copy-Item $jdateOrig (Join-Path $PublishTemp "Jdate") -Recurse -Force }

    foreach ($f in @("ActualizadorBQ.ps1","ActualizadorBQ.bat","iniciar.bat")) {
        $src = Join-Path $PortableDir $f
        if (Test-Path $src) { Copy-Item $src (Join-Path $PublishTemp $f) -Force }
    }

    # version_local.json con la nueva version
    @{ version=$Version; fecha_update=(Get-Date -Format "yyyy-MM-dd HH:mm") } |
        ConvertTo-Json | Set-Content (Join-Path $PublishTemp "version_local.json") -Encoding UTF8

    # PASO 4: Checksum
    Set-Paso "Calculando checksum..." ""
    $ms = [System.IO.MemoryStream]::new()
    Get-ChildItem $PublishTemp -Recurse -File | Sort-Object FullName | ForEach-Object {
        $b = [System.IO.File]::ReadAllBytes($_.FullName); $ms.Write($b,0,$b.Length)
    }
    $sha  = [System.Security.Cryptography.SHA256]::Create()
    $hash = ([System.BitConverter]::ToString($sha.ComputeHash($ms.ToArray()))).Replace("-","").ToLower()

    # PASO 5: Crear ZIP
    Set-Paso "Empaquetando ZIP..." "$ZipNombre"
    $zipPath = Join-Path $CarpetaSalida $ZipNombre
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PublishTemp, $zipPath)
    $mb = [math]::Round((Get-Item $zipPath).Length/1MB,1)

    # PASO 6: version.json
    Set-Paso "Generando manifiesto version.json..." ""
    $vj = @{
        version             = $Version
        fecha               = (Get-Date -Format "yyyy-MM-dd")
        descripcion         = $Desc
        zip_nombre          = $ZipNombre
        checksum_sha256     = $hash
        archivos_protegidos = @("balanzas.db","license.lic","appsettings.json","TEMPLATE.DAT","version_local.json")
    } | ConvertTo-Json -Depth 4
    $vj | Set-Content (Join-Path $CarpetaSalida "version.json") -Encoding UTF8
    $vj | Set-Content (Join-Path $ScriptDir "version.json") -Encoding UTF8

    # PASO 7: Limpiar temp
    Set-Paso "Limpiando archivos temporales..." ""
    Remove-Item $PublishTemp -Recurse -Force

    $prog.Close()

    [System.Windows.Forms.MessageBox]::Show(
        "Version $Version publicada exitosamente!`n`nArchivos generados en:`n$CarpetaSalida`n`n  - $ZipNombre  ($mb MB)`n  - version.json`n`nCopiar esta carpeta al USB del cliente.",
        "Publicacion Exitosa",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null

} catch {
    $prog.Close()
    [System.Windows.Forms.MessageBox]::Show(
        "Error durante la publicacion:`n`n$_",
        "Error",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    if (Test-Path $PublishTemp) { Remove-Item $PublishTemp -Recurse -Force -ErrorAction SilentlyContinue }
    exit 1
}
