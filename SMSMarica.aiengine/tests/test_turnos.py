"""
Testes do ciclo de vida de turno — sem rede, sem Claude, sem pytest.

    python tests/test_turnos.py

O que estes testes guardam é a regressão de 2026-07-22: até então cada turno abria o seu
próprio `receive_response()`, que para no PRIMEIRO `ResultMessage` do stream, sem filtrar por
turno. Cancelar ou estourar o timeout matava o consumidor no meio e as mensagens que sobravam
eram herdadas pelo turno seguinte — a conversa ficava permanentemente um turno atrasada.

O SDK é substituído por um cliente falso: o que está sob teste é o motor, não o Claude.
"""
import asyncio
import os
import sys
import tempfile
import types
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(RAIZ))

os.environ.setdefault("AIENGINE_INTERNAL_KEY", "teste")
os.environ["AIENGINE_DB_PATH"] = str(Path(tempfile.mkdtemp()) / "teste.db")
os.environ["AIENGINE_REPO_DIR"] = str(RAIZ)  # existe: evita o modo degradado
os.environ["AIENGINE_TURN_GRACE_SEC"] = "1"


# --------------------------------------------------------------------- dublê do SDK

class TextBlock:
    def __init__(self, text: str) -> None:
        self.text = text


class AssistantMessage:
    def __init__(self, blocos: list) -> None:
        self.content = blocos


class ResultMessage:
    def __init__(self, result: str = "", is_error: bool = False) -> None:
        self.result = result
        self.is_error = is_error
        self.num_turns = 1
        self.duration_ms = 1
        self.total_cost_usd = 0.0
        self.usage = None


class StreamEvent:
    def __init__(self, texto: str) -> None:
        self.event = {"type": "content_block_delta",
                      "delta": {"type": "text_delta", "text": texto}}


class ClaudeAgentOptions:
    """Aceita qualquer campo: o teste não valida a configuração do SDK."""

    def __init__(self, **kwargs) -> None:
        self.__dict__.update(kwargs)


class ClienteFalso:
    """Emula o transporte: uma fila única de mensagens, como o stream real.

    O ponto do dublê é justamente a fila ser compartilhada entre turnos — é dela que nascia
    a defasagem quando alguém parava de ler no meio.
    """

    def __init__(self, options=None) -> None:
        self.options = options
        self.fila: asyncio.Queue = asyncio.Queue()
        self.desconectado = False
        self.interrompido = 0
        self._pendentes: list[asyncio.Task] = []

    async def connect(self) -> None:
        return None

    async def disconnect(self) -> None:
        self.desconectado = True
        for t in self._pendentes:
            t.cancel()

    async def query(self, prompt: str) -> None:
        """Responde em partes, com atraso — dá tempo de cancelar no meio.

        Com o prefixo SOPARCIAL emite só deltas e para: assim o teste da cauda ao vivo
        observa um estado parado, em vez de correr atrás do relógio.
        """
        async def emitir() -> None:
            if prompt.startswith("SOPARCIAL"):
                for i in range(3):
                    await self.fila.put(StreamEvent(f"pedaco{i} "))
                return
            for i in range(4):
                await asyncio.sleep(0.05)
                await self.fila.put(StreamEvent(f"{prompt}:{i} "))
                await self.fila.put(AssistantMessage([TextBlock(f"{prompt}:{i}")]))
            await self.fila.put(ResultMessage(result=f"fim de {prompt}"))

        self._pendentes.append(asyncio.create_task(emitir()))

    async def interrupt(self) -> None:
        """Como o CLI real: para de produzir e fecha o turno com um ResultMessage."""
        self.interrompido += 1
        for t in self._pendentes:
            t.cancel()
        self._pendentes.clear()
        await self.fila.put(ResultMessage(result="interrompido", is_error=False))

    async def receive_messages(self):
        while True:
            yield await self.fila.get()


sdk = types.ModuleType("claude_agent_sdk")
sdk.ClaudeAgentOptions = ClaudeAgentOptions
sdk.ClaudeSDKClient = ClienteFalso
# dados_tool importa estes dois no nível do módulo; o teste não exercita o modo dados,
# então stubs inertes bastam para o import de claude_runner não quebrar.
sdk.create_sdk_mcp_server = lambda **kwargs: object()
sdk.tool = lambda *a, **k: (lambda fn: fn)
sys.modules["claude_agent_sdk"] = sdk

import claude_runner  # noqa: E402
from claude_runner import engine  # noqa: E402
from store import store  # noqa: E402


# --------------------------------------------------------------------- utilidades

falhas: list[str] = []


def checar(condicao: bool, descricao: str) -> None:
    print(("  ok   " if condicao else "  FALHA") + " " + descricao)
    if not condicao:
        falhas.append(descricao)


def textos(tid: str) -> str:
    return " ".join(e.get("text", "") for e in store.events(tid) if e.get("type") == "text")


async def esperar_fim(tid: str, limite: float = 5.0) -> dict:
    fim = asyncio.get_event_loop().time() + limite
    while asyncio.get_event_loop().time() < fim:
        turno = store.get_turn(tid)
        if turno and turno["status"] != "running":
            return turno
        await asyncio.sleep(0.02)
    return store.get_turn(tid)


# --------------------------------------------------------------------- testes

async def test_cancelamento_nao_contamina_o_proximo_turno() -> None:
    print("\n[1] cancelar um turno não pode vazar eventos para o seguinte")
    sessao = await engine.create_session(title="cancelamento")
    sid = sessao["id"]

    a = await engine.start_turn(sid, "PRIMEIRO", usuario_nome="Bernardo")
    await asyncio.sleep(0.12)  # deixa o turno A produzir parte das mensagens
    checar(await engine.cancel_turn(a["id"]), "cancel_turn devolve True")
    turno_a = await esperar_fim(a["id"])
    checar(turno_a["status"] == "cancelled", f"turno A fica 'cancelled' (veio {turno_a['status']})")

    b = await engine.start_turn(sid, "SEGUNDO", usuario_nome="Ana")
    turno_b = await esperar_fim(b["id"])
    checar(turno_b["status"] == "done", f"turno B fica 'done' (veio {turno_b['status']})")

    conteudo_b = textos(b["id"])
    checar("PRIMEIRO" not in conteudo_b,
           "turno B NÃO contém eventos do turno A  <- a regressão da defasagem")
    checar("SEGUNDO" in conteudo_b, "turno B contém a resposta do próprio prompt")


async def test_participantes_e_autoria() -> None:
    print("\n[2] autoria: a sessão mostra todo mundo que interagiu")
    sessao = await engine.create_session(title="autoria", usuario_nome="Bernardo")
    sid = sessao["id"]
    await esperar_fim((await engine.start_turn(sid, "oi", usuario_nome="Bernardo"))["id"])
    await esperar_fim((await engine.start_turn(sid, "tchau", usuario_nome="Ana"))["id"])

    detalhe = engine.session_detail(sid)
    checar(detalhe["usuario_nome"] == "Bernardo", "autor da sessão é quem a abriu")
    checar(detalhe["participantes"] == ["Bernardo", "Ana"],
           f"participantes na ordem de entrada (veio {detalhe['participantes']})")


async def test_texto_ao_vivo() -> None:
    print("\n[3] texto ao vivo aparece antes do bloco fechar e some depois")
    sessao = await engine.create_session(title="parcial")
    sid = sessao["id"]
    turno = await engine.start_turn(sid, "SOPARCIAL")
    await asyncio.sleep(0.1)  # os 3 deltas já foram; nada mais vem

    view = engine.turn_view(turno["id"])
    checar(view["partial"] == "pedaco0 pedaco1 pedaco2 ",
           f"a cauda junta os deltas na ordem (veio {view['partial']!r})")
    checar(store.event_count(turno["id"]) == 0,
           "delta NÃO vira linha no SQLite — a cauda é só exibição")

    # Fecha o turno como o CLI faria; a cauda tem que sumir junto.
    await engine._live[sid].client.fila.put(ResultMessage(result="fim"))
    await esperar_fim(turno["id"])
    checar(engine.turn_view(turno["id"])["partial"] is None,
           "a cauda some quando o turno termina (o texto vira evento persistido)")


async def test_watchdog_encerra_turno_estourado() -> None:
    print("\n[4] turno que passa do tempo é interrompido, não abandonado")
    claude_runner.config.TURN_TIMEOUT_SEC = 0  # tudo já nasce estourado
    try:
        sessao = await engine.create_session(title="watchdog")
        sid = sessao["id"]
        turno = await engine.start_turn(sid, "LONGO")
        await engine.watchdog()
        fim = await esperar_fim(turno["id"])
        checar(fim["status"] == "error", f"turno estourado vira 'error' (veio {fim['status']})")
        checar("excedeu" in (fim["error"] or ""), "o erro explica que foi o tempo")
    finally:
        claude_runner.config.TURN_TIMEOUT_SEC = 900

    # E o mais importante: a sessão continua utilizável, sem sobra no stream.
    seguinte = await engine.start_turn(sid, "DEPOIS")
    fim = await esperar_fim(seguinte["id"])
    checar(fim["status"] == "done", "a sessão segue utilizável depois do timeout")
    checar("LONGO" not in textos(seguinte["id"]),
           "o turno seguinte não herda nada do turno estourado")


async def test_autorizacao_por_operador() -> None:
    print("\n[5] autorização: só admin sobe com ferramentas de escrita")
    admin_id = next(iter(claude_runner.config.ADMIN_USUARIO_IDS))

    sessao = await engine.create_session(title="autorizacao", usuario_nome="Bernardo")
    sid = sessao["id"]

    # Turno do admin: conjunto completo.
    await esperar_fim((await engine.start_turn(
        sid, "oi", usuario_id=admin_id, usuario_nome="Bernardo"))["id"])
    opcoes_admin = engine._live[sid].client.options
    checar("Edit" in opcoes_admin.allowed_tools and "Write" in opcoes_admin.allowed_tools,
           "operador admin tem Edit/Write")
    checar("MODO SOMENTE LEITURA" not in opcoes_admin.system_prompt.upper()
           or "COMPLETA" in opcoes_admin.system_prompt,
           "system prompt do admin declara autorização completa")
    cliente_admin = engine._live[sid].client

    # Outro operador continua a MESMA sessão: cliente é reconstruído sem escrita.
    await esperar_fim((await engine.start_turn(
        sid, "oi de novo", usuario_id=str(__import__("uuid").uuid4()),
        usuario_nome="Ana"))["id"])
    live = engine._live[sid]
    checar(live.client is not cliente_admin,
           "trocar de operador reconstrói o cliente  <- sem herdar ferramentas do admin")
    opcoes_ana = live.client.options
    checar("Edit" not in opcoes_ana.allowed_tools and "Write" not in opcoes_ana.allowed_tools
           and "Task" not in opcoes_ana.allowed_tools,
           f"operador comum NÃO tem Edit/Write/Task (veio {opcoes_ana.allowed_tools})")
    checar("SOMENTE LEITURA" in opcoes_ana.system_prompt,
           "system prompt do operador comum declara o modo somente leitura")
    checar(getattr(opcoes_ana, "disallowed_tools", None) ==
           claude_runner.config.READONLY_DISALLOWED_TOOLS,
           "negações extras (git commit/push, systemctl...) aplicadas ao operador comum")
    checar("criar-ticket" in opcoes_ana.system_prompt,
           "o caminho oficial (abrir ticket) está no prompt do operador comum")

    # Turno sem identidade (curl de diagnóstico): menor privilégio.
    await esperar_fim((await engine.start_turn(sid, "anonimo"))["id"])
    opcoes_anon = engine._live[sid].client.options
    checar("Edit" not in opcoes_anon.allowed_tools,
           "turno sem usuario_id cai no somente leitura (menor privilégio)")


async def main() -> int:
    for teste in (test_cancelamento_nao_contamina_o_proximo_turno,
                  test_participantes_e_autoria,
                  test_texto_ao_vivo,
                  test_watchdog_encerra_turno_estourado,
                  test_autorizacao_por_operador):
        await teste()
    await engine.shutdown()

    print("\n" + ("-" * 60))
    if falhas:
        print(f"{len(falhas)} verificação(ões) falharam:")
        for f in falhas:
            print(f"  - {f}")
        return 1
    print("Tudo certo.")
    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
