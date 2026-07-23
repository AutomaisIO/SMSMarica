#!/usr/bin/env bash
# Preparação do host para o Agente IA. Roda UMA VEZ, à mão, no servidor smsmarica.online.
#
# O GitHub Actions publica o código; isto cuida do que o Actions não pode fazer:
# instalar o Claude Code, clonar o repositório e fazer o login com a conta do usuário.
set -euo pipefail

REPO_DIR="${AIENGINE_REPO_DIR:-/srv/smsmarica-repo}"
REPO_SSH="git@github.com:AutomaisIO/SMSMarica.git"
KEY_PATH="/root/.ssh/smsmarica_repo_ed25519"

echo "=== 1/5 — Claude Code ==="
export PATH="/root/.local/bin:$PATH"
if ! command -v claude >/dev/null 2>&1; then
  echo "Instalando Claude Code..."
  curl -fsSL https://claude.ai/install.sh | bash
fi
claude --version

echo "=== 1b — ferramental de dev do agente (idempotente) ==="
# O agente trabalha código NO servidor: precisa buildar .NET, gerar/aplicar migrations EF,
# usar o GitHub CLI e falar com o Postgres de prod por Python. Instalado à mão porque o
# GitHub Actions não provisiona o host. Symlinks em /usr/local/bin porque é o que está no
# PATH do systemd (o serviço não lê .bashrc). Confirmado: dotnet-ef roda sem DOTNET_ROOT.
export DEBIAN_FRONTEND=noninteractive
apt-get update -y -q
apt-get install -y -q python3-psycopg2                       # acesso ao Postgres de prod
if ! command -v gh >/dev/null 2>&1; then                     # GitHub CLI
  apt-get install -y -q gh || {
    mkdir -p -m 755 /etc/apt/keyrings
    wget -qO- https://cli.github.com/packages/githubcli-archive-keyring.gpg > /etc/apt/keyrings/githubcli-archive-keyring.gpg
    chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" > /etc/apt/sources.list.d/github-cli.list
    apt-get update -y -q && apt-get install -y -q gh
  }
fi
if ! /usr/share/dotnet/dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then   # .NET SDK 10
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /usr/share/dotnet
fi
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
export PATH="/usr/share/dotnet:/root/.dotnet/tools:$PATH"
/root/.dotnet/tools/dotnet-ef --version >/dev/null 2>&1 || dotnet tool install --global dotnet-ef
ln -sf /root/.dotnet/tools/dotnet-ef /usr/local/bin/dotnet-ef
echo "ferramental: dotnet $(dotnet --version) / ef $(/usr/local/bin/dotnet-ef --version | tail -1) / $(gh --version | head -1)"

echo "=== 2/5 — chave SSH de deploy (push no monorepo) ==="
if [ ! -f "$KEY_PATH" ]; then
  ssh-keygen -t ed25519 -N "" -C "aiengine@smsmarica" -f "$KEY_PATH"
  echo
  echo ">>> AÇÃO MANUAL: cadastre a chave pública abaixo em"
  echo ">>> https://github.com/AutomaisIO/SMSMarica/settings/keys  (Add deploy key, MARCAR 'Allow write access')"
  echo
  cat "${KEY_PATH}.pub"
  echo
  read -r -p "Pressione ENTER depois de cadastrar a deploy key... " _
fi

grep -q "Host github.com" /root/.ssh/config 2>/dev/null || cat >> /root/.ssh/config <<EOF

Host github.com
  IdentityFile $KEY_PATH
  IdentitiesOnly yes
  StrictHostKeyChecking accept-new
EOF

echo "=== 3/5 — clone do repositório em $REPO_DIR ==="
# FORA de /opt de propósito: os deploys rodam `find $APP_DIR -delete`.
if [ -d "$REPO_DIR/.git" ]; then
  git -C "$REPO_DIR" remote set-url origin "$REPO_SSH"
  git -C "$REPO_DIR" fetch --all --prune
  echo "Repositório já clonado; remote e fetch atualizados."
else
  mkdir -p "$(dirname "$REPO_DIR")"
  git clone "$REPO_SSH" "$REPO_DIR"
fi
git -C "$REPO_DIR" config user.name  "SMSMarica Agente IA"
git -C "$REPO_DIR" config user.email "agente-ia@smsmarica.online"
git -C "$REPO_DIR" config core.autocrlf input

echo "=== 4/5 — marcar o workspace como confiável ==="
# Sem isto o CLI ignora as permissions.allow do .claude/settings.local.json do repo e
# imprime "this workspace has not been trusted". O diálogo é interativo e não existe sob systemd.
python3 - "$REPO_DIR" <<'PY'
import json, sys, pathlib
repo = sys.argv[1]
p = pathlib.Path("/root/.claude.json")
d = json.loads(p.read_text()) if p.exists() else {}
d.setdefault("projects", {}).setdefault(repo, {})["hasTrustDialogAccepted"] = True
p.write_text(json.dumps(d, indent=2))
print(f"trust aceito para {repo}")
PY

echo "=== 5/5 — token da assinatura ==="
if ! grep -q "^CLAUDE_CODE_OAUTH_TOKEN=" /etc/smsmarica-aiengine/env 2>/dev/null; then
  echo
  echo ">>> AÇÃO MANUAL: gere o token de 1 ano com a conta web:"
  echo ">>>   claude setup-token"
  echo ">>> Cole o valor no Secret CLAUDE_CODE_OAUTH_TOKEN do repositório GitHub"
  echo ">>> (o deploy escreve /etc/smsmarica-aiengine/env a partir dos Secrets)."
  echo
  echo ">>> ATENÇÃO: NÃO use --bare com o CLI. Essa flag pula a descoberta de config e o"
  echo ">>> token OAuth deixa de ser aplicado ('Not logged in') mesmo estando correto."
fi

echo
echo "=== Host preparado. Agora: systemctl restart smsmarica-aiengine ==="
