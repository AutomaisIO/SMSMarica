"""
Configuração do motor de Agente IA do SMSMarica.

Diferente do Automais.IO, aqui NÃO existe um arquivo de env central nem `shared/python`.
Cada serviço tem o seu `/etc/<serviço>/env`, escrito pelo GitHub Actions a cada deploy.
Este módulo lê direto de `os.environ` e não depende de nada fora da stdlib.
"""
import os
from pathlib import Path

# HTTP em loopback. A API .NET (5080) fala com este serviço; o nginx nunca o expõe.
# PORTA VERIFICADA NO HOST, não deduzida da sequência. Este servidor hospeda OUTRO produto
# (CentralIA/Falarmais) em 5083 e 5084 — deduzir 'a próxima livre' colocou o proxy da API
# conversando com a API do outro produto (2026-07-19). Conferir com `ss -tlnH` antes de mudar.
HTTP_PORT = int(os.getenv("AIENGINE_HTTP_PORT", "5085"))
HTTP_HOST = os.getenv("AIENGINE_HTTP_HOST", "127.0.0.1")

# Chave que a API .NET envia no header X-SMSMarica-Internal-Key.
INTERNAL_KEY = os.getenv("AIENGINE_INTERNAL_KEY", "")

# Clone do monorepo onde o agente trabalha código-fonte.
#
# FICA FORA DE /opt DE PROPÓSITO: todo deploy roda `find $APP_DIR -mindepth 1 -delete`
# antes de extrair o artefato. Um clone dentro de um diretório de deploy seria apagado
# na próxima publicação, sem aviso.
REPO_DIR = os.getenv("AIENGINE_REPO_DIR", "/srv/smsmarica-repo")
FALLBACK_CWD = os.getenv("AIENGINE_FALLBACK_CWD", "/tmp")

MODEL = os.getenv("AIENGINE_MODEL", "claude-opus-4-8")

# NÃO use bypassPermissions: o CLI recusa rodando como root ("--dangerously-skip-permissions
# cannot be used with root/sudo privileges") e nenhuma sessão sobe. 'dontAsk' nega o que não
# estiver na allowlist em vez de perguntar — correto sem TTY.
PERMISSION_MODE = os.getenv("AIENGINE_PERMISSION_MODE", "dontAsk")
_DEFAULT_TOOLS = "Bash,Read,Edit,Write,Glob,Grep,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit,Skill"
ALLOWED_TOOLS = [t.strip() for t in (os.getenv("AIENGINE_ALLOWED_TOOLS", "").strip()
                                     or _DEFAULT_TOOLS).split(",") if t.strip()]

MAX_TURNS = int(os.getenv("AIENGINE_MAX_TURNS", "60"))
TURN_TIMEOUT_SEC = int(os.getenv("AIENGINE_TURN_TIMEOUT_SEC", "900"))

# Orçamento de memória: cada sessão segura um processo do Claude Code de 80–200 MB.
# O host roda 3 serviços .NET + nginx + WireGuard; recusar sessão é melhor do que
# empurrar a máquina para swap e derrubar a API de produção.
SESSION_IDLE_TTL_SEC = int(os.getenv("AIENGINE_SESSION_IDLE_TTL_SEC", "1800"))
SESSION_SWEEP_INTERVAL_SEC = int(os.getenv("AIENGINE_SESSION_SWEEP_INTERVAL_SEC", "60"))
MAX_SESSIONS = int(os.getenv("AIENGINE_MAX_SESSIONS", "3"))
MIN_AVAILABLE_MB = int(os.getenv("AIENGINE_MIN_AVAILABLE_MB", "700"))

HISTORY_RETENTION_DAYS = int(os.getenv("AIENGINE_HISTORY_RETENTION_DAYS", "30"))

PROMPT_FILE = Path(os.getenv(
    "AIENGINE_PROMPT_FILE",
    str(Path(__file__).parent / "prompts" / "smsmarica_system.md"),
))


def auth_mode() -> str:
    """Qual credencial o processo filho `claude` vai realmente usar.

    O Claude Code resolve nesta ordem: ANTHROPIC_AUTH_TOKEN -> ANTHROPIC_API_KEY ->
    apiKeyHelper -> CLAUDE_CODE_OAUTH_TOKEN. Se uma chave de API vazar para o ambiente,
    ela VENCE o token da assinatura e o consumo passa a ser cobrado à parte — silenciosamente.
    Aconteceu no Automais.IO em 2026-07-18; aqui o modo fica visível no /health desde o dia um.
    """
    if os.environ.get("ANTHROPIC_AUTH_TOKEN"):
        return "auth-token"
    if os.environ.get("ANTHROPIC_API_KEY"):
        return "api-key (COBRADO SEPARADO — precedência vence o token da assinatura)"
    if os.environ.get("CLAUDE_CODE_OAUTH_TOKEN"):
        return "subscription"
    return "none"


def available_memory_mb() -> int:
    """MemAvailable do /proc/meminfo, em MB. -1 se não conseguir ler (não bloqueia)."""
    try:
        with open("/proc/meminfo", encoding="utf-8") as fh:
            for line in fh:
                if line.startswith("MemAvailable:"):
                    return int(line.split()[1]) // 1024
    except OSError:
        pass
    return -1


def resolve_cwd() -> tuple[str, bool]:
    """Devolve (cwd, repo_disponivel)."""
    if Path(REPO_DIR).is_dir():
        return REPO_DIR, True
    return FALLBACK_CWD, False


def load_system_prompt() -> str:
    """Lido do disco a cada sessão nova — permite ajustar sem reiniciar o serviço."""
    return PROMPT_FILE.read_text(encoding="utf-8").replace("{REPO_DIR}", REPO_DIR)
