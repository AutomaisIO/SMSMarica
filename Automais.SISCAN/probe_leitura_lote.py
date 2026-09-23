"""Dá para ler MUITAS requisições seguidas sem pagar o menu a cada uma?

O menu do SISCAN custa de 14 a 32 s (medido). Se, para voltar da tela da
requisição à grade de resultados, fosse preciso reabrir o menu, ler 551
requisições levaria horas. A tela tem um `frm:botaoVoltar` — esta sonda mede
se ele devolve a grade COM os resultados, e quanto custa o ciclo
abrir → ler → voltar.

SOMENTE LEITURA: navega, lê e volta. Nunca toca em Salvar.

    python probe_leitura_lote.py [--quantas 3]
"""

from __future__ import annotations

import argparse
import re
import time

from siscan import exame
from siscan.client import SiscanClient

RE_NUM = re.compile(r"listaExamePaginada:(\d+):")


def voltar(c, doc, nome="voltar"):
    return c.sopa(c.post_form(doc, "frm", {"frm:botaoVoltar": "frm:botaoVoltar"}, nome))


def respostas(doc) -> dict[str, str]:
    """O que a requisição respondeu — radios marcados + textos preenchidos."""
    out: dict[str, str] = {}
    for i in doc.find_all("input"):
        nome, valor = i.get("name"), i.get("value")
        if not nome:
            continue
        if i.has_attr("checked"):
            out.setdefault(nome, "")
            out[nome] = (out[nome] + "," + (valor or "")).strip(",")
        elif i.get("type") == "text" and (valor or "").strip():
            out[nome] = valor.strip()
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--quantas", type=int, default=3)
    args = ap.parse_args()

    with SiscanClient(capturar=False) as c:
        t0 = time.time()
        c.login()
        doc = exame.abrir(c)
        print(f"login + menu: {time.time()-t0:.1f}s")

        t0 = time.time()
        doc = exame.pesquisar(c, exame.Filtro(
            mamografia=True, status="01",
            data_inicio="01/09/2026", data_fim="10/09/2026"), doc)
        cab, linhas = exame.grade(doc)
        print(f"pesquisa: {time.time()-t0:.1f}s · {len(linhas)} linha(s)")

        for n, l in enumerate(linhas[: args.quantas], 1):
            num = RE_NUM.search(list(l.acoes.values())[0]).group(1)
            t0 = time.time()
            req = exame.abrir_acao(c, doc, num, exame.ACAO_ALTERAR_REQUISICAO, f"req-{n}")
            t_abre = time.time() - t0
            r = respostas(req)
            t0 = time.time()
            doc = voltar(c, req, f"voltar-{n}")
            t_volta = time.time() - t0
            _, linhas2 = exame.grade(doc)
            print(f"  #{n} exame {num}: abrir {t_abre:.1f}s · voltar {t_volta:.1f}s · "
                  f"grade após voltar: {len(linhas2)} linha(s) · "
                  f"prontuário={r.get('frm:prontuario','(vazio)')!r} · "
                  f"campos lidos={len(r)}")
            if not linhas2:
                print("  !! a grade veio VAZIA — o Voltar não preserva a pesquisa")
                break
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
