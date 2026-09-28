# -*- coding: utf-8 -*-
"""Roda as 12 cenas reais do robô contra um endpoint OpenAI-compatível (llama-server) e grava
transcript + tempos em bench/resultados/<modelo>/<cena>.json.

Reproduz o contrato do RoboAtendimentoMotorApi: tool_choice obrigatório, máx. 6 chamadas,
resposta só vale se sair por responder_cidadao. Resultados de ferramenta são "enlatados"
(RESPOSTAS abaixo), fiéis aos textos que os comandos reais devolvem nas cenas da skill.

Uso:  python rodar_cena.py --base http://IP:8080/v1 --modelo qwen3-30b-a3b [--cenas S01,S03]
      (as cenas .txt vêm de gerar com a skill analisar-robo-cenarios-reais/scripts/montar_cenarios.py)
"""
import argparse, glob, json, os, re, sys, time, urllib.request

AQUI = os.path.dirname(os.path.abspath(__file__))
CENAS_DIR = os.environ.get("CENAS_DIR") or os.path.join(
    AQUI, "..", "..", ".claude", "skills", "analisar-robo-cenarios-reais", "scripts", "cenarios")

sys.path.insert(0, AQUI)
from tools_openai import ferramentas_da_cena  # noqa: E402

# assunto de cada cena (espelha montar_cenarios.py)
ASSUNTO = {"S01": "confirmacao", "S02": "saudacoes", "S03": "sintoma", "S04": "confirmacao",
           "S05": "padrao", "S06": "atendente", "S07": "padrao", "S08": "local",
           "S09": "saudacoes", "S10": "padrao", "S11": "padrao", "S12": "padrao"}

NAO_LOCALIZEI = ("NÃO localizei agendamento futuro NO NOSSO SISTEMA — o que NÃO quer dizer que não "
                 "exista: marcação feita agora pela equipe ou pela regulação pode ainda não ter "
                 "chegado aqui. NUNCA diga que a pessoa não tem nada agendado. Diga que não conseguiu "
                 "localizar por aqui, peça para ela conferir a guia no posto onde é atendida, e "
                 "encaminhe para um atendente confirmar.")
UNIDADES = ("UPA 24H Inoã — Rod. Amaral Peixoto, km 30, Inoã. | Hospital Municipal Conde Modesto "
            "Leal — R. Clímaco Pereira 241, Centro. | PS Santa Rita (PA 24h) — Av. Carlos Mariguella "
            "s/n, Jardim Atlântico, Itaipuaçu.")

# resultado enlatado por (cena, ferramenta, nº da chamada). Default cobre o resto.
RESPOSTAS = {
    ("S01", "confirmar_presenca", 1): (
        "Identidade confirmada: VALDERI RODRIGUES DE OLIVEIRA. Agendamento localizado: OCI AVALIAÇÃO "
        "DIAGNÓSTICA EM ORTOPEDIA — 17/09/2026 às 09:20 — CDT DR ALBERTO LUIS M. BORGES. Confirme o "
        "NOME com a pessoa e chame de novo com confirmado=true para registrar a presença."),
    ("S01", "confirmar_presenca", 2): "Presença confirmada com sucesso.",
    ("S04", "confirmar_presenca", 1): (
        "Identidade confirmada: ADOLPHO FELIX DOS SANTOS FILHO. Agendamento localizado: RADIOGRAFIA "
        "DE TORAX (PA E PERFIL) — 16/09/2026 às 13:00. Confirme o NOME com a pessoa e chame de novo "
        "com confirmado=true."),
    ("S04", "confirmar_presenca", 2): "Presença confirmada com sucesso.",
}
DEFAULT = {
    "consultar_cadastro": "Identidade confirmada: MARIA APARECIDA DA SILVA.",
    "consultar_agendamentos": NAO_LOCALIZEI,
    "consultar_unidades": UNIDADES,
    "confirmar_presenca": ("Identidade confirmada: MARIA APARECIDA DA SILVA. Agendamento localizado: "
                           "MAMOGRAFIA BILATERAL — 30/09/2026 às 10:00. Confirme o NOME com a pessoa "
                           "e chame de novo com confirmado=true."),
    "encaminhar_para_humano": "Conversa marcada para atendimento humano.",
}


def parse_cena(path):
    txt = open(path, encoding="utf-8").read()
    system = re.search(r"<system>\n(.*?)\n</system>", txt, re.S).group(1)
    conversa = re.search(r"<conversa>\n(.*?)\n</conversa>", txt, re.S).group(1)
    msgs, papel, buf = [], None, []
    for linha in conversa.splitlines():
        m = re.match(r"(user|assistant): ?(.*)", linha)
        if m:
            if papel:
                msgs.append({"role": papel, "content": "\n".join(buf)})
            papel, buf = m.group(1), [m.group(2)]
        else:
            buf.append(linha)
    if papel:
        msgs.append({"role": papel, "content": "\n".join(buf)})
    return system, msgs


def chamar(base, modelo, payload):
    req = urllib.request.Request(base.rstrip("/") + "/chat/completions",
                                 json.dumps(payload).encode(), {"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=1800) as r:
        d = json.load(r)
    return d, time.time() - t0


def rodar(base, modelo, cena_path, cid):
    system, historico = parse_cena(cena_path)
    tools = ferramentas_da_cena(ASSUNTO[cid])
    msgs = [{"role": "system", "content": system}] + historico
    reg = {"cena": cid, "modelo": modelo, "chamadas": [], "resposta_final": None,
           "violacoes": [], "iniciado_em": time.strftime("%Y-%m-%dT%H:%M:%S")}
    contagem = {}
    tool_choice = "required"
    for i in range(6):
        payload = {"model": modelo, "messages": msgs, "tools": tools,
                   "tool_choice": tool_choice, "max_tokens": 1024}
        try:
            d, dt = chamar(base, modelo, payload)
        except Exception as e:  # noqa: BLE001
            if tool_choice == "required":
                # template de alguns modelos não aceita required — cai p/ auto e registra
                tool_choice = "auto"
                reg["violacoes"].append("fallback_tool_choice_auto")
                try:
                    payload["tool_choice"] = "auto"
                    d, dt = chamar(base, modelo, payload)
                except Exception as e2:  # noqa: BLE001
                    reg["violacoes"].append(f"erro_http: {e2}")
                    break
            else:
                reg["violacoes"].append(f"erro_http: {e}")
                break
        ch = d["choices"][0]["message"]
        uso = d.get("usage", {})
        calls = ch.get("tool_calls") or []
        reg["chamadas"].append({"n": i + 1, "latencia_s": round(dt, 2), "usage": uso,
                                "texto_fora_de_tool": ch.get("content"),
                                "tools": [{"nome": c["function"]["name"],
                                           "args": c["function"]["arguments"]} for c in calls]})
        if not calls:
            reg["violacoes"].append("nenhuma_tool_chamada (tool_choice=required ignorado)")
            break
        msgs.append(ch)
        fim = False
        for c in calls:
            nome = c["function"]["name"]
            try:
                args = json.loads(c["function"]["arguments"] or "{}")
            except ValueError:
                args = None
                reg["violacoes"].append(f"argumentos_invalidos:{nome}")
            if nome == "responder_cidadao":
                reg["resposta_final"] = args
                fim = True  # contrato do motor: responder_cidadao encerra e ignora o resto
                break
            contagem[nome] = contagem.get(nome, 0) + 1
            res = RESPOSTAS.get((cid, nome, contagem[nome])) or DEFAULT.get(nome, "ok")
            msgs.append({"role": "tool", "tool_call_id": c["id"], "content": res})
        if fim:
            break
    else:
        reg["violacoes"].append("estourou_6_iteracoes")
    if reg["resposta_final"] is None and "estourou_6_iteracoes" not in reg["violacoes"]:
        if not any(v.startswith("erro_http") for v in reg["violacoes"]):
            reg["violacoes"].append("terminou_sem_responder_cidadao")
    reg["latencia_total_s"] = round(sum(c["latencia_s"] for c in reg["chamadas"]), 2)
    return reg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--modelo", required=True)
    ap.add_argument("--cenas", default="")
    ap.add_argument("--repeticoes", type=int, default=1)
    a = ap.parse_args()
    quer = set(a.cenas.split(",")) if a.cenas else None
    outdir = os.path.join(AQUI, "resultados", a.modelo)
    os.makedirs(outdir, exist_ok=True)
    for path in sorted(glob.glob(os.path.join(CENAS_DIR, "S*.txt"))):
        cid = os.path.basename(path)[:-4]
        if quer and cid not in quer:
            continue
        for rep in range(1, a.repeticoes + 1):
            reg = rodar(a.base, a.modelo, path, cid)
            suf = f"-r{rep}" if a.repeticoes > 1 else ""
            out = os.path.join(outdir, f"{cid}{suf}.json")
            json.dump(reg, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            rf = (reg["resposta_final"] or {}).get("texto", "—")
            print(f"{cid}{suf}: {reg['latencia_total_s']}s, {len(reg['chamadas'])} chamada(s), "
                  f"violacoes={reg['violacoes']}\n  -> {str(rf)[:140]}")


if __name__ == "__main__":
    main()
