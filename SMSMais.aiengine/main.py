"""
Serviço Agente IA do SMSMais.

Expõe o Claude Code (rodando no servidor, com o clone do repositório) como API HTTP de
loopback na 5085. Quem consome é a API .NET (5080), que valida a permissão `AgenteIa`
antes de repassar. O nginx nunca expõe esta porta.

Autocontido de propósito: o SMSMais não tem `shared/python`, então nada aqui depende de
biblioteca interna — só stdlib, FastAPI e o SDK.
"""
import asyncio
import logging
import os
import sys
import time
from contextlib import asynccontextmanager
from pathlib import Path
from urllib.parse import unquote

import uvicorn
from fastapi import Body, FastAPI, Header, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

import config
from claude_runner import engine
from store import store

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# PRECEDÊNCIA DE CREDENCIAL — não remova sem entender o efeito na fatura.
#
# O Claude Code resolve credenciais nesta ordem: ANTHROPIC_AUTH_TOKEN -> ANTHROPIC_API_KEY
# -> apiKeyHelper -> CLAUDE_CODE_OAUTH_TOKEN. Se uma chave de API entrar no ambiente, ela
# VENCE o token da assinatura e o consumo passa a ser cobrado à parte, sem nenhum sinal.
# Isso aconteceu no Automais.IO (2026-07-18) e só apareceu quando o teto da chave estourou.
if os.environ.get("CLAUDE_CODE_OAUTH_TOKEN") and os.environ.pop("ANTHROPIC_API_KEY", None):
    logger.info("ANTHROPIC_API_KEY removida do ambiente: o agente usa a assinatura.")

if not config.INTERNAL_KEY:
    logger.error("AIENGINE_INTERNAL_KEY ausente — sem ela qualquer processo local chamaria o motor.")
    raise SystemExit(1)
if not (os.environ.get("CLAUDE_CODE_OAUTH_TOKEN") or os.environ.get("ANTHROPIC_API_KEY")):
    logger.error("Sem credencial do Claude (CLAUDE_CODE_OAUTH_TOKEN ou ANTHROPIC_API_KEY).")
    raise SystemExit(1)

# Estado do loop de manutenção. O SMSMais não tem o loop_supervisor do Automais, então
# guardamos o batimento aqui — um loop morto precisa aparecer no /health, senão o serviço
# fica "ativo" com a manutenção parada e ninguém percebe.
_sweep = {"last_beat": 0.0, "failures": 0, "last_error": ""}


async def sweep_loop() -> None:
    while True:
        _sweep["last_beat"] = time.time()
        try:
            # `sweep` também roda o watchdog de turno: é ele que interrompe um turno que
            # passou do tempo. Um loop parado deixa turnos eternos — daí o /health vigiar.
            reaped = await engine.sweep()
            if reaped:
                logger.info("Sweep descartou %d cliente(s) ocioso(s) (histórico preservado)", reaped)
            if config.HISTORY_RETENTION_DAYS > 0:
                store.prune_sessions(config.HISTORY_RETENTION_DAYS)
            _sweep["failures"] = 0
            _sweep["last_error"] = ""
        except Exception as exc:  # noqa: BLE001
            _sweep["failures"] += 1
            _sweep["last_error"] = str(exc)
            logger.exception("Falha no sweep")
        await asyncio.sleep(config.SESSION_SWEEP_INTERVAL_SEC)


def sweep_healthy() -> bool:
    """Stale = não bate há mais de 3x o intervalo esperado."""
    age = time.time() - _sweep["last_beat"]
    return _sweep["last_beat"] > 0 and age < config.SESSION_SWEEP_INTERVAL_SEC * 3


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Iniciando SMSMais.aiengine (modelo=%s)", config.MODEL)

    mode = config.auth_mode()
    if mode == "subscription":
        logger.info("Cobrança: assinatura (CLAUDE_CODE_OAUTH_TOKEN).")
    else:
        logger.warning("ATENÇÃO — modo de cobrança: %s. O esperado é 'subscription'.", mode)

    orphans = store.recover_orphan_turns()
    if orphans:
        logger.warning("%d turno(s) órfão(s) marcados como interrompidos no startup", orphans)

    _, repo_available = config.resolve_cwd()
    if not repo_available:
        logger.warning(
            "MODO DEGRADADO: %s não existe. Sessões sobem em %s e o agente NÃO poderá "
            "alterar código. Rode deploy/setup-aiengine-host.sh.",
            config.REPO_DIR, config.FALLBACK_CWD)
    if not config.PROMPT_FILE.is_file():
        logger.error("Prompt de sistema não encontrado em %s", config.PROMPT_FILE)
        raise SystemExit(1)

    task = asyncio.create_task(sweep_loop())
    yield
    task.cancel()
    await engine.shutdown()
    logger.info("Serviço Agente IA encerrado")


app = FastAPI(
    title="SMSMais Agente IA",
    description="Motor do Agente IA — Claude Code no servidor",
    version="1.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware, allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"],
)


def require_internal_key(key: str | None) -> None:
    """Só a API .NET (que já validou a permissão AgenteIa) pode falar com este serviço."""
    if not key or key.strip() != config.INTERNAL_KEY.strip():
        raise HTTPException(status_code=403, detail="Chave interna inválida ou ausente.")


# Quem está do outro lado. A API .NET manda em todo request — é a única fonte de identidade
# que este serviço tem, e ela já autenticou o usuário. Ausente em chamada de diagnóstico
# feita direto no loopback (curl), daí ser opcional.
UsuarioId = Header(default=None, alias="X-SMSMarica-Usuario-Id")
UsuarioNome = Header(default=None, alias="X-SMSMarica-Usuario-Nome")


def _nome(valor: str | None) -> str | None:
    """Cabeçalho HTTP não carrega acento com segurança (latin-1), e nome brasileiro tem
    acento na maioria das vezes. A API .NET manda percent-encoded; aqui desfaz."""
    if not valor:
        return None
    return unquote(valor.strip()) or None


@app.get("/health")
async def health():
    ok = sweep_healthy()
    return JSONResponse(
        {
            "status": "ok" if ok else "degraded",
            "service": "smsmarica-aiengine",
            "model": config.MODEL,
            "authMode": config.auth_mode(),
            "repoDir": config.REPO_DIR,
            "repoAvailable": config.resolve_cwd()[1],
            "sessions": len(store.list_sessions()),
            "availableMemoryMb": config.available_memory_mb(),
            "sweep": {
                "alive": ok,
                "lastBeatAgeS": round(time.time() - _sweep["last_beat"], 1) if _sweep["last_beat"] else -1,
                "failures": _sweep["failures"],
                "lastError": _sweep["last_error"],
            },
        },
        status_code=200 if ok else 503,
    )


@app.post("/internal/ai/sessions", tags=["AI"])
async def create_session(payload: dict = Body(default={}),
                         x_smsmarica_internal_key: str | None = Header(default=None),
                         usuario_id: str | None = UsuarioId,
                         usuario_nome: str | None = UsuarioNome):
    require_internal_key(x_smsmarica_internal_key)
    payload = payload or {}
    try:
        record = await engine.create_session(
            title=payload.get("title", ""),
            ticket_numero=payload.get("ticketNumero"),
            ticket_titulo=payload.get("ticketTitulo"),
            usuario_id=usuario_id,
            usuario_nome=_nome(usuario_nome),
        )
    except MemoryError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    return {"sessionId": record["id"], "ticketNumero": record.get("ticket_numero"),
            "model": config.MODEL}


@app.get("/internal/ai/sessions", tags=["AI"])
async def list_sessions(include_archived: bool = Query(default=False),
                        x_smsmarica_internal_key: str | None = Header(default=None)):
    require_internal_key(x_smsmarica_internal_key)
    return {"sessions": engine.list_sessions(include_archived=include_archived)}


@app.patch("/internal/ai/sessions/{session_id}", tags=["AI"])
async def rename_session(session_id: str, payload: dict = Body(...),
                         x_smsmarica_internal_key: str | None = Header(default=None)):
    require_internal_key(x_smsmarica_internal_key)
    titulo = (payload or {}).get("title", "").strip()
    if not titulo:
        raise HTTPException(status_code=400, detail="Campo 'title' é obrigatório.")
    if not store.rename_session(session_id, titulo[:120]):
        raise HTTPException(status_code=404, detail="Sessão não encontrada.")
    return {"renamed": True}


@app.post("/internal/ai/sessions/{session_id}/unarchive", tags=["AI"])
async def unarchive_session(session_id: str,
                            x_smsmarica_internal_key: str | None = Header(default=None)):
    require_internal_key(x_smsmarica_internal_key)
    if not store.unarchive_session(session_id):
        raise HTTPException(status_code=404, detail="Sessão arquivada não encontrada.")
    return {"unarchived": True}


@app.get("/internal/ai/sessions/{session_id}", tags=["AI"])
async def session_detail(session_id: str,
                         x_smsmarica_internal_key: str | None = Header(default=None)):
    """Histórico completo — é o que o painel replica ao reabrir."""
    require_internal_key(x_smsmarica_internal_key)
    detail = engine.session_detail(session_id)
    if detail is None:
        raise HTTPException(status_code=404, detail="Sessão não encontrada.")
    return detail


@app.delete("/internal/ai/sessions/{session_id}", tags=["AI"])
async def archive_session(session_id: str,
                          x_smsmarica_internal_key: str | None = Header(default=None)):
    require_internal_key(x_smsmarica_internal_key)
    if not await engine.archive_session(session_id):
        raise HTTPException(status_code=404, detail="Sessão não encontrada.")
    return {"archived": True}


@app.post("/internal/ai/sessions/{session_id}/turns", tags=["AI"])
async def create_turn(session_id: str, payload: dict = Body(...),
                      x_smsmarica_internal_key: str | None = Header(default=None),
                      usuario_id: str | None = UsuarioId,
                      usuario_nome: str | None = UsuarioNome):
    require_internal_key(x_smsmarica_internal_key)
    prompt = (payload or {}).get("prompt", "").strip()
    if not prompt:
        raise HTTPException(status_code=400, detail="Campo 'prompt' é obrigatório.")
    try:
        record = await engine.start_turn(session_id, prompt, usuario_id, _nome(usuario_nome))
    except KeyError as exc:
        raise HTTPException(status_code=404, detail="Sessão não encontrada.") from exc
    except MemoryError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    return {"turnId": record["id"], "sessionId": session_id, "status": record["status"]}


@app.get("/internal/ai/turns/{turn_id}", tags=["AI"])
async def get_turn(turn_id: str, cursor: int = Query(default=0, ge=0),
                   x_smsmarica_internal_key: str | None = Header(default=None)):
    """Polling incremental: devolve só os eventos a partir de 'cursor'."""
    require_internal_key(x_smsmarica_internal_key)
    view = engine.turn_view(turn_id, cursor)
    if view is None:
        raise HTTPException(status_code=404, detail="Turno não encontrado.")
    return view


@app.post("/internal/ai/turns/{turn_id}/cancel", tags=["AI"])
async def cancel_turn(turn_id: str,
                      x_smsmarica_internal_key: str | None = Header(default=None)):
    require_internal_key(x_smsmarica_internal_key)
    if not await engine.cancel_turn(turn_id):
        raise HTTPException(status_code=409, detail="Turno não está em execução.")
    return {"cancelled": True}


# ─────────────────────────── Modo `dados` (menu IA: perguntas às bases) ───────────────────
# Sessões RESTRITAS (sandbox, só consultam banco), SEPARADAS das do Agente IA (kind='dados')
# e POR USUÁRIO. O polling/cancelamento de turno reusa /internal/ai/turns/* (id é UUID opaco).

def _guard_dados(session_id: str, usuario_id: str | None) -> dict:
    """Carrega a sessão e garante que é 'dados' E do próprio usuário. Sem isto, um id de
    outra pessoa (ou uma sessão do Agente IA) seria acessível pelo menu de dados."""
    record = store.get_session(session_id)
    if record is None or (record.get("kind") or "agente") != "dados":
        raise HTTPException(status_code=404, detail="Sessão de dados não encontrada.")
    if usuario_id and record.get("usuario_id") and record["usuario_id"] != usuario_id:
        raise HTTPException(status_code=404, detail="Sessão de dados não encontrada.")
    return record


@app.post("/internal/ai/dados/sessions", tags=["AI-Dados"])
async def dados_create_session(payload: dict = Body(default={}),
                               x_smsmarica_internal_key: str | None = Header(default=None),
                               usuario_id: str | None = UsuarioId,
                               usuario_nome: str | None = UsuarioNome):
    require_internal_key(x_smsmarica_internal_key)
    payload = payload or {}
    base_slug = (payload.get("baseSlug") or "").strip()
    if not base_slug:
        raise HTTPException(status_code=400, detail="Campo 'baseSlug' é obrigatório.")
    try:
        record = await engine.create_session(
            title=payload.get("title", ""),
            usuario_id=usuario_id,
            usuario_nome=_nome(usuario_nome),
            kind="dados",
            base_slug=base_slug,
        )
    except MemoryError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    return {"sessionId": record["id"], "baseSlug": record.get("base_slug"), "model": config.MODEL}


@app.get("/internal/ai/dados/sessions", tags=["AI-Dados"])
async def dados_list_sessions(include_archived: bool = Query(default=False),
                              x_smsmarica_internal_key: str | None = Header(default=None),
                              usuario_id: str | None = UsuarioId):
    require_internal_key(x_smsmarica_internal_key)
    return {"sessions": engine.list_sessions(
        include_archived=include_archived, kind="dados", usuario_id=usuario_id)}


@app.get("/internal/ai/dados/sessions/{session_id}", tags=["AI-Dados"])
async def dados_session_detail(session_id: str,
                               x_smsmarica_internal_key: str | None = Header(default=None),
                               usuario_id: str | None = UsuarioId):
    require_internal_key(x_smsmarica_internal_key)
    _guard_dados(session_id, usuario_id)
    detail = engine.session_detail(session_id)
    if detail is None:
        raise HTTPException(status_code=404, detail="Sessão de dados não encontrada.")
    return detail


@app.delete("/internal/ai/dados/sessions/{session_id}", tags=["AI-Dados"])
async def dados_archive_session(session_id: str,
                                x_smsmarica_internal_key: str | None = Header(default=None),
                                usuario_id: str | None = UsuarioId):
    require_internal_key(x_smsmarica_internal_key)
    _guard_dados(session_id, usuario_id)
    if not await engine.archive_session(session_id):
        raise HTTPException(status_code=404, detail="Sessão de dados não encontrada.")
    return {"archived": True}


@app.post("/internal/ai/dados/sessions/{session_id}/turns", tags=["AI-Dados"])
async def dados_create_turn(session_id: str, payload: dict = Body(...),
                            x_smsmarica_internal_key: str | None = Header(default=None),
                            usuario_id: str | None = UsuarioId,
                            usuario_nome: str | None = UsuarioNome):
    require_internal_key(x_smsmarica_internal_key)
    _guard_dados(session_id, usuario_id)
    prompt = (payload or {}).get("prompt", "").strip()
    if not prompt:
        raise HTTPException(status_code=400, detail="Campo 'prompt' é obrigatório.")
    try:
        record = await engine.start_turn(session_id, prompt, usuario_id, _nome(usuario_nome))
    except KeyError as exc:
        raise HTTPException(status_code=404, detail="Sessão não encontrada.") from exc
    except MemoryError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    return {"turnId": record["id"], "sessionId": session_id, "status": record["status"]}


if __name__ == "__main__":
    uvicorn.run(app, host=config.HTTP_HOST, port=config.HTTP_PORT)
