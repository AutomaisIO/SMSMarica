"""
Testes do canal WhatsApp do Agente IA — sem rede, sem Claude.

    python tests/test_whatsapp.py

Reaproveita o dublê do SDK de `test_turnos.py` (importá-lo só monta o ambiente; os testes de
lá não rodam). O que se guarda aqui:

- a sessão do telefone é UMA e não expira: a poda de histórico nunca apaga kind 'whatsapp';
- "reiniciar" arquiva a viva e a próxima mensagem abre outra;
- o canal muda só o MEIO: o bloco de forma do WhatsApp entra no prompt, mas a autorização
  continua sendo a de ADMIN_USUARIO_IDS — operador comum segue sem Edit/Write;
- os endpoints HTTP que a API .NET chama (409 levando o id do turno em andamento).
"""
import asyncio
import os
import sys
import time
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import test_turnos as base  # noqa: E402  (monta env + dublê do SDK)

from test_turnos import checar, esperar_fim, falhas  # noqa: E402

claude_runner = base.claude_runner
engine = base.engine
store = base.store


async def test_prune_poupa_whatsapp() -> None:
    print("\n[W1] a poda de histórico nunca apaga sessão de WhatsApp")
    agente = await engine.create_session(title="velha do painel")
    viva = await engine.create_session(title="whats viva", kind="whatsapp", canal_ref="5521990000001")
    arquivada = await engine.create_session(title="whats arquivada", kind="whatsapp",
                                            canal_ref="5521990000001")
    store.archive_session(arquivada["id"])

    antigo = time.time() - 400 * 86400
    for sid in (agente["id"], viva["id"], arquivada["id"]):
        store._write("UPDATE sessions SET last_used_at=? WHERE id=?", (antigo, sid))

    store.prune_sessions(30)
    checar(store.get_session(agente["id"]) is None, "sessão velha do painel é podada (como antes)")
    checar(store.get_session(viva["id"]) is not None, "sessão viva do WhatsApp sobrevive à poda")
    checar(store.get_session(arquivada["id"]) is not None,
           "sessão ARQUIVADA do WhatsApp também sobrevive (é histórico)")


async def test_sessao_unica_e_reiniciar() -> None:
    print("\n[W2] uma sessão por telefone; 'reiniciar' arquiva e a próxima abre outra")
    fone = "5521990000002"
    admin_id = next(iter(claude_runner.config.ADMIN_USUARIO_IDS))

    t1, nova1 = await engine.whatsapp_turn(fone, "primeira", admin_id, "Bernardo")
    await esperar_fim(t1["id"])
    t2, nova2 = await engine.whatsapp_turn(fone, "segunda", admin_id, "Bernardo")
    await esperar_fim(t2["id"])
    checar(nova1 and not nova2, f"só a primeira mensagem cria sessão (veio {nova1}, {nova2})")
    checar(t1["session_id"] == t2["session_id"], "as duas mensagens caem na MESMA sessão")

    achada = store.find_active_whatsapp(fone)
    checar(achada is not None and achada["id"] == t1["session_id"],
           "find_active_whatsapp devolve a sessão viva do telefone")
    checar(store.find_active_whatsapp("5521990000999") is None,
           "outro telefone não enxerga essa sessão")
    checar(achada["kind"] == "whatsapp" and achada["title"].endswith("0002"),
           f"kind e título do canal (veio {achada['kind']!r}, {achada['title']!r})")

    estado = engine.whatsapp_estado(fone)
    checar(estado["turnos"] == 2 and not estado["running"],
           f"estado conta os turnos e não há turno rodando (veio {estado})")

    arquivada = await engine.whatsapp_reiniciar(fone)
    checar(arquivada == t1["session_id"], "reiniciar devolve o id da sessão arquivada")
    checar(store.find_active_whatsapp(fone) is None, "depois de reiniciar não há sessão viva")
    checar(store.get_session(arquivada)["archived_at"] is not None, "a sessão ficou arquivada")
    checar(await engine.whatsapp_reiniciar(fone) is None, "reiniciar sem sessão viva devolve None")

    t3, nova3 = await engine.whatsapp_turn(fone, "terceira", admin_id, "Bernardo")
    await esperar_fim(t3["id"])
    checar(nova3 and t3["session_id"] != arquivada, "a mensagem seguinte abre sessão nova")


async def test_reiniciar_com_turno_rodando() -> None:
    print("\n[W3] reiniciar com turno em andamento encerra o turno junto")
    fone = "5521990000003"
    turno, _ = await engine.whatsapp_turn(fone, "SOPARCIAL")  # fica parado esperando
    await asyncio.sleep(0.05)
    checar(engine.whatsapp_estado(fone)["currentTurnId"] == turno["id"],
           "estado expõe o turno em andamento")
    try:
        await engine.whatsapp_turn(fone, "outra")
        checar(False, "segunda mensagem com turno rodando deveria dar RuntimeError")
    except RuntimeError:
        checar(True, "segunda mensagem com turno rodando dá RuntimeError (o .NET enfileira)")

    await engine.whatsapp_reiniciar(fone)
    fim = await esperar_fim(turno["id"])
    checar(fim["status"] != "running", f"o turno não fica 'running' para sempre (veio {fim['status']})")


async def test_prompt_e_autorizacao() -> None:
    print("\n[W4] o canal muda a forma, não a autorização")
    admin_id = next(iter(claude_runner.config.ADMIN_USUARIO_IDS))

    t_admin, _ = await engine.whatsapp_turn("5521990000004", "oi", admin_id, "Bernardo")
    await esperar_fim(t_admin["id"])
    op_admin = engine._live[t_admin["session_id"]].client.options
    checar("## Canal: WhatsApp" in op_admin.system_prompt, "bloco do WhatsApp entra no prompt")
    checar("Sessão livre" not in op_admin.system_prompt, "e substitui o 'Sessão livre' do painel")
    checar("Aviso citado" in op_admin.system_prompt and "resolver-erro" in op_admin.system_prompt,
           "o prompt trata o aviso citado como dado e aponta a skill resolver-erro")
    checar("Edit" in op_admin.allowed_tools and "Write" in op_admin.allowed_tools,
           "admin pelo WhatsApp tem Edit/Write")

    t_ana, _ = await engine.whatsapp_turn("5521990000005", "oi", str(uuid.uuid4()), "Ana")
    await esperar_fim(t_ana["id"])
    op_ana = engine._live[t_ana["session_id"]].client.options
    checar("Edit" not in op_ana.allowed_tools and "Write" not in op_ana.allowed_tools
           and "Task" not in op_ana.allowed_tools,
           f"operador comum pelo WhatsApp continua SEM Edit/Write/Task (veio {op_ana.allowed_tools})")
    checar("SOMENTE LEITURA" in op_ana.system_prompt and "## Canal: WhatsApp" in op_ana.system_prompt,
           "operador comum recebe somente-leitura E o bloco do canal")


def test_endpoints_http() -> None:
    print("\n[W5] endpoints que a API .NET chama")
    try:
        from fastapi.testclient import TestClient
    except Exception as exc:  # noqa: BLE001
        print(f"  pulado: fastapi.testclient indisponível ({exc})")
        return
    os.environ.setdefault("CLAUDE_CODE_OAUTH_TOKEN", "teste")
    import main  # noqa: E402  (exige chave interna e credencial no ambiente)

    h = {"x-smsmarica-internal-key": os.environ["AIENGINE_INTERNAL_KEY"]}
    fone = "5521990000006"
    # Sem o `with`: o lifespan subiria o sweep e derrubaria o engine no fim — não é o que se
    # testa aqui, e o engine é compartilhado com os testes acima.
    c = TestClient(main.app)

    checar(c.post("/internal/ai/whatsapp/turns", json={"telefone": fone}, headers=h).status_code == 400,
           "sem prompt → 400")
    checar(c.post("/internal/ai/whatsapp/turns", json={"telefone": fone, "prompt": "x"}).status_code == 403,
           "sem chave interna → 403")

    r = c.post("/internal/ai/whatsapp/turns", json={"telefone": fone, "prompt": "SOPARCIAL"}, headers=h)
    corpo = r.json()
    checar(r.status_code == 200 and corpo.get("novaSessao") is True and corpo.get("turnId"),
           f"primeira mensagem → 200 com turno e novaSessao (veio {r.status_code} {corpo})")

    r2 = c.post("/internal/ai/whatsapp/turns", json={"telefone": fone, "prompt": "outra"}, headers=h)
    checar(r2.status_code == 409 and r2.json().get("currentTurnId") == corpo.get("turnId"),
           f"turno rodando → 409 com o id do turno em andamento (veio {r2.status_code} {r2.json()})")

    est = c.get("/internal/ai/whatsapp/estado", params={"telefone": fone}, headers=h).json()
    checar(est.get("running") is True and est.get("sessionId") == corpo.get("sessionId"),
           f"estado mostra a sessão e o turno rodando (veio {est})")

    lista = c.get("/internal/ai/sessions", params={"kind": "whatsapp"}, headers=h).json()["sessions"]
    checar(any(s["id"] == corpo["sessionId"] for s in lista), "lista kind=whatsapp traz a sessão")
    checar(all(s["kind"] == "whatsapp" for s in lista), "lista kind=whatsapp só traz whatsapp")
    painel = c.get("/internal/ai/sessions", headers=h).json()["sessions"]
    checar(all(s["kind"] == "agente" for s in painel), "lista padrão continua só 'agente'")
    checar(c.get("/internal/ai/sessions", params={"kind": "dados"}, headers=h).status_code == 400,
           "kind=dados pela lista geral → 400 (dados tem rota própria com guarda de dono)")

    det = c.get(f"/internal/ai/sessions/{corpo['sessionId']}", headers=h)
    checar(det.status_code == 200 and det.json().get("canal_ref") == fone,
           "detalhe da sessão whatsapp funciona pela rota comum")

    rr = c.post("/internal/ai/whatsapp/reiniciar", json={"telefone": fone}, headers=h).json()
    checar(rr.get("arquivada") == corpo["sessionId"], f"reiniciar arquiva a sessão (veio {rr})")
    est2 = c.get("/internal/ai/whatsapp/estado", params={"telefone": fone}, headers=h).json()
    checar(est2.get("sessionId") is None, "depois de reiniciar o estado não tem sessão")


async def main() -> int:
    for teste in (test_prune_poupa_whatsapp,
                  test_sessao_unica_e_reiniciar,
                  test_reiniciar_com_turno_rodando,
                  test_prompt_e_autorizacao):
        await teste()
    # TestClient roda o app no próprio loop de fundo dele; fora do nosso loop.
    await asyncio.to_thread(test_endpoints_http)
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
