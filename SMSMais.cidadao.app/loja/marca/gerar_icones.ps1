# Gera os ícones e a abertura do app (Android e iOS) e o ícone da Play a partir do símbolo "MA RI CÁ".
# Rode da pasta SMSMais.cidadao.app:  pwsh loja/marca/gerar_icones.ps1
#
# simbolo-512.png        quadrado arredondado #FF2A00 com as letras brancas (o símbolo como é visto)
# simbolo-letras-512.png só as letras brancas, com alfa, na mesma posição e escala
# Desenhado sobre um quadrado do mesmo laranja, os cantos arredondados do símbolo somem.

Add-Type -AssemblyName System.Drawing
$aqui = $PSScriptRoot
$app = Resolve-Path (Join-Path $aqui '..\..')
$laranja = [System.Drawing.Color]::FromArgb(255, 255, 42, 0)
$simbolo = [System.Drawing.Bitmap]::FromFile((Join-Path $aqui 'simbolo-512.png'))
$letras = [System.Drawing.Bitmap]::FromFile((Join-Path $aqui 'simbolo-letras-512.png'))

function Desenhar($origem, $lado, $escala, $destino, [switch]$Transparente) {
    $fmt = if ($Transparente) { [System.Drawing.Imaging.PixelFormat]::Format32bppArgb } else { [System.Drawing.Imaging.PixelFormat]::Format24bppRgb }
    $bmp = New-Object System.Drawing.Bitmap $lado, $lado, $fmt
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    if ($Transparente) { $g.Clear([System.Drawing.Color]::Transparent) } else { $g.Clear($laranja) }
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $tam = $lado * $escala; $x = ($lado - $tam) / 2
    $g.DrawImage($origem, (New-Object System.Drawing.RectangleF ([single]$x), ([single]$x), ([single]$tam), ([single]$tam)))
    $g.Dispose(); $bmp.Save($destino, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

# Android. Ícone legado (48–192): o símbolo com os cantos arredondados. Ícone adaptativo: primeiro
# plano só com as letras, a 74% (cabem na área segura de 66dp de 108dp), sobre o @color/laranja_marca.
# Abertura antes do Android 12 (190dp): o símbolo sobre o laranja.
$res = Join-Path $app 'android\app\src\main\res'
$dens = [ordered]@{ mdpi = 1.0; hdpi = 1.5; xhdpi = 2.0; xxhdpi = 3.0; xxxhdpi = 4.0 }
foreach ($d in $dens.Keys) {
    $f = $dens[$d]
    Desenhar $simbolo ([int](48 * $f)) 1.0 (Join-Path $res "mipmap-$d\ic_launcher.png") -Transparente
    Desenhar $letras ([int](108 * $f)) 0.74 (Join-Path $res "mipmap-$d\ic_launcher_foreground.png") -Transparente
    Desenhar $simbolo ([Math]::Min(512, [int](190 * $f))) 1.0 (Join-Path $res "drawable-$d\splash_logo.png")
}

# iOS: ícones (quadrado cheio, sem transparência; o iOS arredonda) e a imagem da abertura (190pt).
$ios = Join-Path $app 'ios\Runner\Assets.xcassets'
$icones = @{ 'Icon-App-20x20@1x' = 20; 'Icon-App-20x20@2x' = 40; 'Icon-App-20x20@3x' = 60; 'Icon-App-29x29@1x' = 29;
  'Icon-App-29x29@2x' = 58; 'Icon-App-29x29@3x' = 87; 'Icon-App-40x40@1x' = 40; 'Icon-App-40x40@2x' = 80;
  'Icon-App-40x40@3x' = 120; 'Icon-App-60x60@2x' = 120; 'Icon-App-60x60@3x' = 180; 'Icon-App-76x76@1x' = 76;
  'Icon-App-76x76@2x' = 152; 'Icon-App-83.5x83.5@2x' = 167; 'Icon-App-1024x1024@1x' = 1024 }
foreach ($n in $icones.Keys) { Desenhar $simbolo $icones[$n] 1.0 (Join-Path $ios "AppIcon.appiconset\$n.png") }
Desenhar $simbolo 190 1.0 (Join-Path $ios 'LaunchImage.imageset\LaunchImage.png')
Desenhar $simbolo 380 1.0 (Join-Path $ios 'LaunchImage.imageset\LaunchImage@2x.png')
Desenhar $simbolo 512 1.0 (Join-Path $ios 'LaunchImage.imageset\LaunchImage@3x.png')

# Ícone da Play (512, quadrado cheio; a Play arredonda).
Desenhar $simbolo 512 1.0 (Join-Path $app 'loja\play\icone-512.png')

# PWA (SMSMais.cidadao.pwa/public): os mesmos quadrados cheios. O maskable leva o símbolo a 80%, dentro
# da área segura (círculo de 80%) do recorte do Android.
$pwa = Join-Path $app '..\SMSMais.cidadao.pwa\public'
if (Test-Path $pwa) {
    Desenhar $simbolo 48 1.0 (Join-Path $pwa 'favicon.png')
    Desenhar $simbolo 180 1.0 (Join-Path $pwa 'apple-touch-icon.png')
    Desenhar $simbolo 192 1.0 (Join-Path $pwa 'icon-192.png')
    Desenhar $simbolo 512 1.0 (Join-Path $pwa 'icon-512.png')
    Desenhar $simbolo 512 0.8 (Join-Path $pwa 'icon-512-maskable.png')
}

$simbolo.Dispose(); $letras.Dispose()
'ícones gerados'
