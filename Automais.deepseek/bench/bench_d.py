# -*- coding: utf-8 -*-
"""Bench D — throughput paralelo (cenário "robô 100% LLM").

Produção mediu: pico de 144 tarefas/hora COM LLM hoje, mas 3.649 msgs recebidas/hora no pico
total (rajadas de campanha). Este bench dispara N conversas curtas simultâneas (turno típico de
clique de botão/cortesia + um turno cheio) e mede p50/p90 por nível de concorrência, para achar
o teto de msgs/hora da VM mantendo p90 < 30 s.

Uso: python bench_d.py --base http://IP:8080/v1 --modelo qwen3-30b-a3b --concorrencias 1,2,4,8
"""
import argparse, concurrent.futures as cf, json, os, statistics, time, urllib.request

AQUI = os.path.dirname(os.path.abspath(__file__))
SYSTEM_CURTO = ("Você é um atendente virtual da Secretaria de Saúde no WhatsApp. Responda com "
                "cordialidade e brevidade, em pt-BR.")
TURNOS = ["Confirmo", "Ok obrigada", "Bom dia", "Sim pode confirmar", "Obrigado pela atenção",
          "Quero saber do meu exame", "Onde fica o posto de Inoã?", "Preciso remarcar minha consulta"]


def uma(base, modelo, texto):
    msgs = [{"role": "system", "content": SYSTEM_CURTO}, {"role": "user", "content": texto}]
    req = urllib.request.Request(base.rstrip("/") + "/chat/completions",
                                 json.dumps({"model": modelo, "messages": msgs,
                                             "max_tokens": 200}).encode(),
                                 {"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=1800) as r:
        json.load(r)
    return time.time() - t0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--modelo", required=True)
    ap.add_argument("--concorrencias", default="1,2,4")
    ap.add_argument("--por_nivel", type=int, default=16)
    a = ap.parse_args()
    outdir = os.path.join(AQUI, "resultados", a.modelo)
    os.makedirs(outdir, exist_ok=True)
    res = []
    for n in [int(x) for x in a.concorrencias.split(",")]:
        lats, t0 = [], time.time()
        with cf.ThreadPoolExecutor(max_workers=n) as ex:
            futs = [ex.submit(uma, a.base, a.modelo, TURNOS[i % len(TURNOS)])
                    for i in range(a.por_nivel)]
            for f in cf.as_completed(futs):
                lats.append(f.result())
        total = time.time() - t0
        lats.sort()
        item = {"concorrencia": n, "reqs": a.por_nivel,
                "p50_s": round(statistics.median(lats), 1),
                "p90_s": round(lats[int(0.9 * len(lats)) - 1], 1),
                "max_s": round(lats[-1], 1),
                "vazao_msgs_por_hora": round(a.por_nivel / total * 3600)}
        res.append(item)
        print(item)
    json.dump(res, open(os.path.join(outdir, "bench_d.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
