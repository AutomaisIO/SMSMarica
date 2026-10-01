#!/usr/bin/env bash
# Publica a versão ATUAL da extensão (esta pasta, SMSMais.chrome) no repositório PÚBLICO de
# distribuição SMSMais/extensao-sisreg — de onde o atualizar-extensao.bat dos PCs faz git pull.
#
# A fonte de desenvolvimento é o monorepo; este script espelha os arquivos da extensão no repo
# público, preservando o README de distribuição e o .bat que vivem lá.
#
# Uso:  bash publicar.sh
set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
PUB="${TMPDIR:-/tmp}/smsmais-extensao-sisreg-pub"
REPO="https://github.com/SMSMais/extensao-sisreg.git"

if [ -d "$PUB/.git" ]; then
  git -C "$PUB" pull --ff-only
else
  git clone "$REPO" "$PUB"
fi

VER=$(grep -o '"version"[^,]*' "$AQUI/manifest.json" | head -1 | grep -o '[0-9][0-9.]*')
PUBVER=$(grep -o '"version"[^,]*' "$PUB/manifest.json" 2>/dev/null | head -1 | grep -o '[0-9][0-9.]*' || true)

# Nunca rebaixar: em 30/09/2026 o repo público estava na v0.9.0 (vinda de outro branch) e o main
# na v0.4.0 — publicar daqui apagaria o que os PCs da recepção usam.
if [ -n "$PUBVER" ] && [ "$(printf '%s\n%s\n' "$PUBVER" "$VER" | sort -V | tail -1)" != "$VER" ]; then
  echo "ABORTADO: o público está na v$PUBVER e esta pasta na v$VER (seria rebaixar)." >&2
  echo "          Junte as duas versões antes de publicar." >&2
  exit 1
fi

for f in manifest.json config.js endpoints.js background.js capture-hook.js content.js auth-content.js popup.html popup.js prime-agenda.js prime-agenda-main.js; do
  cp "$AQUI/$f" "$PUB/$f"
done
git -C "$PUB" add -A
if git -C "$PUB" commit -q -m "Extensão v$VER"; then
  git -C "$PUB" push
  echo "Publicado v$VER em https://github.com/SMSMais/extensao-sisreg"
else
  echo "Nada a publicar (já está igual)."
fi
