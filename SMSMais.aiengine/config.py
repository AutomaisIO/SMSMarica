"""
Configuração do motor de Agente IA do SMSMais.

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

# ── Autorização de escrita por operador ────────────────────────────────────────────────
# REGRA DE NEGÓCIO, não preferência: SOMENTE os usuario_id abaixo (GUIDs de
# smsmarica.usuario, autenticados pela API .NET via X-SMSMarica-Usuario-Id) podem conduzir
# o agente em alteração de código, commit, deploy ou escrita no host. Qualquer outro
# operador sobe o processo do Claude SEM as ferramentas de escrita (Edit/Write/etc.) —
# a restrição é estrutural, não depende de prompt.
#
# O default é o Bernardo Almeida (dono do sistema). Para adicionar alguém, ajuste o
# Secret AIENGINE_ADMIN_USUARIO_IDS (lista separada por vírgula) — nunca edite o env
# do host à mão (o GitHub Actions o reescreve a cada deploy).
_DEFAULT_ADMIN_IDS = "019dc264-7de1-78cc-b6ff-0be0c0e8b714"  # Bernardo Almeida
ADMIN_USUARIO_IDS = {s.strip().lower()
                     for s in (os.getenv("AIENGINE_ADMIN_USUARIO_IDS", "").strip()
                               or _DEFAULT_ADMIN_IDS).split(",") if s.strip()}

# Ferramentas do operador NÃO autorizado: diagnóstico apenas. Sem Edit/Write/NotebookEdit
# (não altera arquivo) e sem Task (subagente herdaria capacidades fora deste controle).
# O Bash permanece para diagnóstico (logs, SELECT no banco, systemctl status) — os comandos
# de escrita/impacto são negados pela lista abaixo e pelo prompt.
_DEFAULT_READONLY_TOOLS = "Bash,Read,Glob,Grep,WebFetch,WebSearch,TodoWrite,Skill"
READONLY_TOOLS = [t.strip() for t in (os.getenv("AIENGINE_READONLY_TOOLS", "").strip()
                                      or _DEFAULT_READONLY_TOOLS).split(",") if t.strip()]

# Negações explícitas no modo somente-leitura (defesa em profundidade sobre o Bash).
READONLY_DISALLOWED_TOOLS = [
    "Edit", "Write", "NotebookEdit", "Task",
    "Bash(git commit*)", "Bash(git push*)", "Bash(git merge*)", "Bash(git rebase*)",
    "Bash(git reset*)", "Bash(git checkout*)", "Bash(git restore*)", "Bash(git stash*)",
    "Bash(systemctl restart*)", "Bash(systemctl stop*)", "Bash(systemctl start*)",
    "Bash(reboot*)", "Bash(shutdown*)", "Bash(rm *)", "Bash(mv *)", "Bash(cp *)",
    "Bash(dotnet ef*)", "Bash(psql*-c*INSERT*)", "Bash(psql*-c*UPDATE*)",
    "Bash(psql*-c*DELETE*)",
]


def operador_admin(usuario_id: str | None) -> bool:
    """True se o usuario_id (autenticado pela API .NET) pode conduzir escrita."""
    return bool(usuario_id) and usuario_id.strip().lower() in ADMIN_USUARIO_IDS

# ── Modo `dados` (menu IA: perguntas às bases) ─────────────────────────────────────────
# Sessão RESTRITA: sobe o Claude Code sem nenhuma ferramenta de host — só o tool MCP
# `consultar_base`, que executa SQL read-only na base da sessão via /proxy-sql. cwd isolado
# (nunca o repo), para não haver contato/vazamento com o código ou o sistema. Ver ADR-0023.
DADOS_TOOL_FQN = "mcp__dados__consultar_base"
DADOS_VIZ_TOOL_FQN = "mcp__dados__visualizar"
# As DUAS únicas ferramentas do modo dados. Ambas benignas: consultar (read-only via proxy) e
# visualizar (só declara um gráfico/mapa; quem renderiza é o painel). Nada de host/código.
DADOS_ALLOWED_TOOLS = [DADOS_TOOL_FQN, DADOS_VIZ_TOOL_FQN]
# Sandbox vazio e próprio. Fora de /opt (apagado a cada deploy) e fora do repo (para o agente
# não ter o código sequer no diretório de trabalho).
DADOS_CWD = os.getenv("AIENGINE_DADOS_CWD", "/var/lib/smsmarica-aiengine/dados-sandbox")
DADOS_PROMPT_FILE = Path(os.getenv(
    "AIENGINE_DADOS_PROMPT_FILE",
    str(Path(__file__).parent / "prompts" / "dados_system.md"),
))

# Proxy SQL interno da API .NET (loopback). O tool `consultar_base` fala SÓ com ele.
PROXYSQL_URL = os.getenv("AIENGINE_PROXYSQL_URL", "http://127.0.0.1:5091/proxy-sql")
PROXYSQL_TOKEN = os.getenv("AIENGINE_PROXYSQL_TOKEN", "")
PROXYSQL_TIMEOUT_SEC = int(os.getenv("AIENGINE_PROXYSQL_TIMEOUT_SEC", "60"))

MAX_TURNS = int(os.getenv("AIENGINE_MAX_TURNS", "60"))
TURN_TIMEOUT_SEC = int(os.getenv("AIENGINE_TURN_TIMEOUT_SEC", "900"))
# Depois de mandar interrupt(), quanto esperamos pelo ResultMessage antes de concluir que o
# processo travou e derrubar o cliente inteiro. Derrubar é o último recurso: enquanto o
# cliente vive, quem fecha o turno é o leitor, no ponto certo do stream.
TURN_GRACE_SEC = int(os.getenv("AIENGINE_TURN_GRACE_SEC", "60"))

# Deltas de texto (efeito máquina de escrever no painel). Não geram linha no SQLite — ficam
# só em memória, na sessão viva, e o painel lê como "cauda" do turno em andamento.
PARTIAL_MESSAGES = os.getenv("AIENGINE_PARTIAL_MESSAGES", "1").strip() not in ("0", "false", "")
# Teto da cauda em memória. Uma resposta gigante não pode virar um buffer sem fim: o texto
# completo chega logo depois como evento persistido.
PARTIAL_MAX_CHARS = int(os.getenv("AIENGINE_PARTIAL_MAX_CHARS", "20000"))

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
    str(Path(__file__).parent / "prompts" / "smsmais_system.md"),
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


def load_dados_prompt() -> str:
    """Prompt de sistema do modo `dados` (restrito a consultas ao banco)."""
    return DADOS_PROMPT_FILE.read_text(encoding="utf-8")
