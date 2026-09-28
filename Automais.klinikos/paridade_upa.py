"""Teste de PARIDADE web × hub para uma UPA. SOMENTE LEITURA (não escreve em lugar nenhum).

Puxa a espinha web (407+667) de um dia da UPA e compara com o que o conector SQL já colocou no
hub FHIR para os MESMOS boletins: existência, chegada (period.start, tolerância de 1 min) e cor.
É o critério de aceite antes de confiar no caminho web (o Conde não tem SQL para comparar).

Uso:  python paridade_upa.py [DD/MM/AAAA]   (padrão 15/09/2026)
Requer no .env: KLINIKOS_BASE=upa24h, KLINIKOS_APP=/UPA24H, credencial que loga na UPA.
"""

from __future__ import annotations

import collections
import datetime as dt
import glob
import os
import re
import sys

import xlrd

from klinikos.client import KlinikosSession

sys.path.insert(0, os.path.join("..", "Aprendizados e Scratchpads", "ferramentas"))
from db import conn  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
UNID = "0006"                          # UPA Maricá
SLUG_HUB = "upa24h-marica-sqlserver"   # meta.source do conector SQL no hub
COR = re.compile(r"(Vermelh\w*|Amarelo|Verde|Laranja|Azul)", re.I)


def serial_para_dt(v: str) -> dt.datetime | None:
    try:
        f = float(v)
    except ValueError:
        return None
    return dt.datetime(1899, 12, 30) + dt.timedelta(days=f)


def norm_bol(v: str) -> str | None:
    v = re.sub(r"\.0$", "", str(v).strip())
    return v.zfill(12) if re.fullmatch(r"\d{9,12}", v) else None


def linhas(b: bytes):
    sh = xlrd.open_workbook(file_contents=b).sheet_by_index(0)
    g = [[str(c).strip() for c in sh.row_values(i)] for i in range(sh.nrows)]
    hi = next((i for i, r in enumerate(g) if any("Boletim" in c for c in r)), 0)
    return g[hi], g[hi + 1:]


def col(cab, *nomes):
    return next((j for j, c in enumerate(cab) if any(n.lower() in c.lower() for n in nomes)), None)


import time

BIFF = b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1"


def _xls(s: KlinikosSession, url: str, tentativas: int = 5) -> bytes:
    """GET de relatório robusto: retry no OOM do Crystal ('Not enough memory') — transitório no
    servidor de relatório deles; re-seleciona o local se caiu no gate."""
    for i in range(tentativas):
        r = s.get(url)
        if r.content[:8] == BIFF:
            return r.content
        txt = r.text
        if re.search(r"Not enough memory|OutOfMemory|COMException|Gateway Time-out", txt, re.I):
            time.sleep(4)  # OOM/timeout do Crystal — tenta de novo
            continue
        if "GravaCookie" in str(r.url):
            s.selecionar_local()
            continue
        break
    raise SystemExit(f"relatório não veio como XLS após {tentativas} tentativas (url {r.url}).")


def gate_porta_local(s: KlinikosSession):
    """Gate da build 2025 (UPA/Santa Rita): porta com AutoPostBack → carrega local físico → salva.
    Seta _ID_VINCULO (que os relatórios precisam); o administrativo do Conde 2024 não basta aqui."""
    from klinikos.client import sopa, campos_todos, action_do_form, URL_HOME, LIBERADOS
    pref = "ctl00$ctl00$contentCenter$contentCenterChild$"
    r = s.get(URL_HOME); html = r.text; url = str(r.url)
    if "GravaCookie" not in url:
        return
    d = sopa(html); sel = d.find("select", attrs={"name": lambda n: n and n.endswith("ddlPortadeEntrada")})
    porta = next((o.get("value") for o in sel.find_all("option") if o.get("value") not in (None, "", "-1")), None)
    r = s.postback(url, html, target=pref + "ddlPortadeEntrada", extra={pref + "ddlPortadeEntrada": porta}); html = r.text; url = str(r.url)
    d = sopa(html); locf = d.find("select", attrs={"name": lambda n: n and n.endswith("ddlLocalFisico")})
    loc = next((o.get("value") for o in (locf.find_all("option") if locf else []) if o.get("value") not in (None, "", "-1")), None)
    dd = campos_todos(html); dd[pref + "ddlPortadeEntrada"] = porta
    if loc:
        dd[pref + "ddlLocalFisico"] = loc
    dd[pref + "imbSalvar.x"] = "10"; dd[pref + "imbSalvar.y"] = "10"
    LIBERADOS.update({pref + "imbSalvar.x", pref + "imbSalvar.y"})
    s.post(action_do_form(html, url), dd)


def puxar_espinha_web(s: KlinikosSession, dia: str) -> dict[str, dict]:
    """Espinha web da build 2025: relatório 751 (Nominal), modo par8=1 — boletim + chegada + cor +
    clínica + cadastro básico. Params CONFIRMADOS no recon (docs/espinha-upa-build2025.md §2):
    par1=unid, par2/par3=dd/mm/aaaa, par4=5 (Urgência), par6=0023 (dia inteiro), par8=1.

    NOTA: o 667 do Conde NÃO existe na UPA (build 2025) — usar 751 aqui. Os NOMES das colunas do 751
    ainda dependem de uma extração bem-sucedida para travar; por isso a detecção é por cabeçalho
    (col por palavra-chave) e a cor tem duas rotas: coluna própria OU cabeçalho de grupo (como no 667).
    """
    esp: dict[str, dict] = {}
    cab, dados = linhas(_xls(
        s,
        f"/UPA24H/Relatorios/rptviewXls.aspx?parNomeMaquina=&parRel=751&parNum=8"
        f"&par1={UNID}&par2={dia}&par3={dia}&par4=5&par5=&par6=0023&par7=&par8=1"))
    jb = col(cab, "Boletim")
    jent = col(cab, "Entrada", "Chegada", "Data/Hora")
    jcor = col(cab, "Classifica", "Risco", "Cor")          # cor como coluna, se existir
    cor_atual = None
    for r in dados:
        cel = [c for c in r if c]
        # Rota 2 da cor: linha de cabeçalho de grupo "Status da Classificação: <cor>".
        if any("Classifica" in c for c in cel) and any("Status" in c for c in cel):
            achou = next((c for c in cel if COR.search(c) and "Status" not in c), None)
            if achou:
                cor_atual = COR.search(achou).group(1).capitalize()
            continue
        b = norm_bol(r[jb]) if jb is not None and jb < len(r) else None
        if not b:
            continue
        d = esp.setdefault(b, {})
        # Rota 1 da cor: valor na própria coluna; senão herda o cabeçalho de grupo corrente.
        cor_cel = (COR.search(r[jcor]).group(1).capitalize()
                   if jcor is not None and jcor < len(r) and r[jcor] and COR.search(r[jcor]) else None)
        d["cor"] = cor_cel or cor_atual
        if jent is not None and jent < len(r):
            d["chegada"] = serial_para_dt(r[jent])
    return esp


def hub_do_dia(dia_iso: str) -> dict[str, dt.datetime]:
    c = conn(); cur = c.cursor(); cur.execute("SET statement_timeout='40s'")
    cur.execute("""
        select i->>'value', (content->'period'->>'start')::timestamptz
        from fhir.encounter, jsonb_array_elements(content->'identifier') i
        where content->'meta'->>'source' = %s
          and i->>'system' = 'urn:klinikos:boletim'
          and (content->'period'->>'start')::date = %s
    """, (f"https://smsmarica.saude.marica/source/klinikos/{SLUG_HUB}", dia_iso))
    out = {}
    for val, start in cur.fetchall():
        spa = val.split(":", 1)[1] if ":" in val else val
        out[spa.zfill(12)] = start
    c.close()
    return out


def main(argv):
    dia = argv[0] if argv else "15/09/2026"
    dia_iso = f"{dia[6:10]}-{dia[3:5]}-{dia[0:2]}"
    s = KlinikosSession(); s.login(); gate_porta_local(s)
    web = puxar_espinha_web(s, dia)
    s.close()
    hub = hub_do_dia(dia_iso)

    sweb, shub = set(web), set(hub)
    ambos = sweb & shub
    # paridade da chegada (period.start) na tolerância de 1 min
    ok_cheg = dif_cheg = sem_cheg = 0
    for b in ambos:
        cw = web[b].get("chegada"); ch = hub[b]
        if cw is None:
            sem_cheg += 1; continue
        ch_naive = ch.replace(tzinfo=None)
        if abs((cw - ch_naive).total_seconds()) <= 90:
            ok_cheg += 1
        else:
            dif_cheg += 1

    print(f"\n=== PARIDADE UPA {dia} (web × hub-SQL) — leitura, nada escrito ===")
    print(f"boletins web (407+667): {len(sweb)}")
    print(f"boletins hub (SQL, chegada no dia): {len(shub)}")
    print(f"em AMBOS: {len(ambos)}  |  só web: {len(sweb - shub)}  |  só hub: {len(shub - sweb)}")
    print(f"chegada (period.start) nos {len(ambos)} comuns: igual(±90s)={ok_cheg} | diferente={dif_cheg} | web sem chegada={sem_cheg}")
    print("cor (web) distribuição:",
          dict(collections.Counter(web[b].get("cor") or "(sem)" for b in web)))
    # amostra de divergência de chegada (mascarada: só os deltas)
    ex = [(b[-4:], (web[b]["chegada"] - hub[b].replace(tzinfo=None)).total_seconds())
          for b in ambos if web[b].get("chegada") and abs((web[b]["chegada"] - hub[b].replace(tzinfo=None)).total_seconds()) > 90][:5]
    if ex:
        print("exemplos de delta de chegada (boletim***, segundos):", ex)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
