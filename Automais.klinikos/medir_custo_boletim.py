"""Mede o custo do "deep" (camada profunda por boletim/paciente) pela tela de leitura
`Cadastro/ResumoProntuario.aspx` — Histórico do Paciente. SOMENTE LEITURA (nenhum verbo de
escrita; a trava do cliente recusa). Grava só AGREGADOS (requisições, segundos); nenhum dado de
paciente vai para o stdout ou para o repo (as capturas ficam em capturas/, gitignored).

Fluxo medido, por prontuário:
1. GET da tela (shell).
2. POST carregando o prontuário (o campo é readonly, mas o servidor lê o valor postado; tenta o
   __EVENTTARGET de pesquisa e o de árvore).
3. (se carregou) 1 postback de expansão da árvore, para ver se a narrativa/documento aparece.

Detecta, sem imprimir conteúdo: se vieram nome do paciente, CID (txtDescCID01..06), e se o
documento clínico (HTML embutido / nós da árvore) apareceu. Conta requisições e cronometra cada
etapa.

Uso:  python medir_custo_boletim.py [prontuario1 prontuario2 ...]
Sem argumentos, tenta extrair prontuários de um relatório 407 já capturado.
"""

from __future__ import annotations

import glob
import re
import sys
import time

from klinikos.client import KlinikosSession, sopa, campos_todos, action_do_form, CAP, LIBERADOS

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
URL = "/KlinikosNet/Cadastro/ResumoProntuario.aspx"
PREF = "ctl00$ctl00$contentCenter$contentCenterChild$"


def prontuarios_de_407() -> list[str]:
    import xlrd
    fs = sorted(glob.glob("capturas/*parRel_407*.xls"), key=lambda f: __import__("os").path.getsize(f), reverse=True)
    out = []
    if not fs:
        return out
    sh = xlrd.open_workbook(fs[0]).sheet_by_index(0)
    for i in range(sh.nrows):
        vals = [re.sub(r"\.0$", "", str(c).strip()) for c in sh.row_values(i)]
        bol = next((v for v in vals if re.fullmatch(r"\d{12}", v)), None)
        if not bol:
            continue
        # prontuário = inteiro 3–8 dígitos na mesma linha, != boletim
        pr = next((v for v in vals if re.fullmatch(r"\d{3,8}", v) and v != bol), None)
        if pr and pr not in out:
            out.append(pr)
        if len(out) >= 5:
            break
    return out


def carregou(html: str) -> dict:
    d = sopa(html)
    def val(idprefix):
        e = d.find("input", id=re.compile(idprefix + "$"))
        return (e.get("value") or "").strip() if e else ""
    nome = val("txtPaciente")
    cids = [val(f"txtDescCID0{i}") for i in range(1, 7)]
    cids = [c for c in cids if c]
    # documento clínico embutido (os hidden grandes com <table>/<style>) ou nós de árvore
    doc = bool(re.search(r"%3ctable|&lt;table|<table", html)) and "rtrvResumoProntuario_t0" in html
    return {"tem_nome": bool(nome), "n_cid": len(cids), "tem_doc": doc}


def medir(s: KlinikosSession, pront: str) -> dict:
    req = 0
    t0 = time.time()
    r = s.get(URL); req += 1
    html = r.text
    t_shell = time.time() - t0
    # tenta carregar o prontuário: seta o campo (readonly, mas posta) e dispara pesquisa/árvore
    dados = campos_todos(html)
    dados[PREF + "txtProntuarioPaciente"] = pront
    # alvos candidatos de carga
    alvos = [PREF + "txtProntuarioPaciente", "ctl00$ctl00$contentCenter$imbPesquisar",
             PREF + "rtrvResumoProntuario"]
    melhor = None
    for alvo in alvos:
        d2 = dict(dados); d2["__EVENTTARGET"] = alvo; d2["__EVENTARGUMENT"] = ""
        tt = time.time()
        try:
            r2 = s.post(action_do_form(html, str(r.url)), d2); req += 1
        except Exception:
            continue
        dt = time.time() - tt
        info = carregou(r2.text)
        if info["tem_nome"] or info["n_cid"]:
            melhor = {"alvo": alvo.split("$")[-1], "t_load": dt, **info}
            (CAP / f"deep_{pront}.html").write_text(r2.text, encoding="utf-8")
            break
        melhor = melhor or {"alvo": None, "t_load": dt, **info}
    return {"pront": pront, "req": req, "t_shell": round(t_shell, 1),
            "t_load": round(melhor.get("t_load", 0), 1), **{k: melhor[k] for k in ("alvo", "tem_nome", "n_cid", "tem_doc")}}


def main(args: list[str]) -> int:
    pronts = args or prontuarios_de_407()
    if not pronts:
        print("sem prontuários (passe como argumento ou capture um 407 antes)")
        return 2
    print(f"medindo {len(pronts)} prontuário(s) pela tela de leitura ResumoProntuario\n")
    s = KlinikosSession(); s.entrar()
    tot_req = tot_t = 0
    ok = 0
    for p in pronts:
        try:
            m = medir(s, p)
        except Exception as e:  # noqa: BLE001
            print(f"  prontuário {p[:2]}***: ERRO {type(e).__name__}: {str(e)[:60]}")
            continue
        tot_req += m["req"]; tot_t += m["t_shell"] + m["t_load"]
        ok += 1 if (m["tem_nome"] or m["n_cid"]) else 0
        print(f"  prontuário ***{p[-2:]}: {m['req']} req | shell {m['t_shell']}s + load {m['t_load']}s "
              f"| alvo={m['alvo']} nome={m['tem_nome']} cids={m['n_cid']} doc={m['tem_doc']}")
    n = len(pronts)
    print(f"\nAGREGADO: {n} prontuários, {ok} carregaram | média {tot_req/n:.1f} req e "
          f"{tot_t/n:.1f}s por prontuário")
    print("(cada prontuário pode ter vários boletins na árvore — custo é por PACIENTE, não por boletim)")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
