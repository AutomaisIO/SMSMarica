#!/usr/bin/env bash
# Gera o PACOTE da extensão para publicar na plataforma (painel → Extensão Chrome → Versões →
# Publicar). É isto que separa o desenvolvimento do que chega aos PCs: mexer nesta pasta não
# entrega nada a ninguém; só o pacote publicado (primeiro em teste, depois promovido) chega.
#
# Entram os arquivos que a extensão usa (.js, .json, .html, .css, imagens); ficam de fora
# README, scripts, PDFs e pastas ocultas. Antes de fechar o pacote, confere o que o atualizador
# dos PCs também confere: manifest válido e todo arquivo que ele manda carregar presente.
#
# Uso:  bash empacotar.sh        → ../SMSMais.atualizador/dist/extensao-<versão>.zip
set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
DESTINO="$AQUI/../SMSMais.atualizador/dist"
mkdir -p "$DESTINO"

python - "$AQUI" "$DESTINO" <<'EOF'
import json, os, sys, zipfile

aqui, destino = sys.argv[1], sys.argv[2]
EXTENSOES = {'.js', '.json', '.html', '.css', '.png', '.svg', '.ico', '.jpg', '.jpeg', '.webp', '.woff2'}
FORA = {'readme.html', 'package.json', 'package-lock.json'}

arquivos = []
for pasta, subpastas, nomes in os.walk(aqui):
    subpastas[:] = [s for s in subpastas if not s.startswith('.') and s != 'node_modules']
    for nome in nomes:
        relativo = os.path.relpath(os.path.join(pasta, nome), aqui).replace(os.sep, '/')
        if nome.startswith('.') or nome.lower() in FORA or os.path.splitext(nome)[1].lower() not in EXTENSOES:
            continue
        arquivos.append(relativo)
arquivos.sort()

if 'manifest.json' not in arquivos:
    sys.exit('ABORTADO: não há manifest.json nesta pasta.')
manifesto = json.load(open(os.path.join(aqui, 'manifest.json'), encoding='utf-8'))
versao = manifesto.get('version')
if not versao:
    sys.exit('ABORTADO: o manifest.json não tem "version".')

pedidos = [manifesto.get('background', {}).get('service_worker'), manifesto.get('action', {}).get('default_popup')]
for cs in manifesto.get('content_scripts', []):
    pedidos += cs.get('js', []) + cs.get('css', [])
faltando = sorted({p for p in pedidos if p and p.lstrip('./') not in arquivos})
if faltando:
    sys.exit('ABORTADO: o manifest manda carregar e não existe: ' + ', '.join(faltando))

saida = os.path.join(destino, 'extensao-%s.zip' % versao)
with zipfile.ZipFile(saida, 'w', zipfile.ZIP_DEFLATED) as z:
    for relativo in arquivos:
        z.write(os.path.join(aqui, relativo), relativo)
print('Pacote da extensão v%s (%d arquivos, %d KB):' % (versao, len(arquivos), os.path.getsize(saida) // 1024))
for relativo in arquivos:
    print('   ' + relativo)
print(os.path.normpath(saida))
EOF
