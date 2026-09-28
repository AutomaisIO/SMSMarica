"""Dry-run da ESPINHA do conector web do Conde. SOMENTE LEITURA — não escreve no hub.

Puxa, de um dia, os dois relatórios leves do Conde e monta, por boletim (`spa_codigo`), o registro
que a via rápida gravaria como Encounter + identidade:
  - 407 (Pacientes Registrados no Dia): Nº Boletim, Prontuário, Paciente, Nasc/Idade, Clínica
  - 667 (Nominal por Classificação de Risco): Nº Boletim, Data/Hora Entrada (chegada), COR, Origem

Faz o JOIN por Nº Boletim, mostra COBERTURA (quantos têm cor, chegada, prontuário), a distribuição
por cor (o que o painel mostraria) e uma amostra mascarada. Nenhum dado de paciente vai ao stdout
além de agregados; a amostra mascara nome. Não monta o recurso FHIR final (isso é do .NET com o
`UpsertCanonicoPep`), mas prova que os campos necessários se montam a partir dos relatórios baratos.

Uso:  python dryrun_conde_espinha.py [DD/MM/AAAA]   (padrão: 15/09/2026)
"""

from __future__ import annotations

import collections
import re
import sys
import time

from klinikos.client import KlinikosSession
import xlrd

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
UNID = "0005"  # Conde


def baixa_xls(s: KlinikosSession, url: str) -> bytes:
    r = s.get(url)
    if r.content[:8] != b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1":
        raise SystemExit(f"esperava XLS, veio {r.headers.get('content-type')} — {url}")
    return r.content


def linhas(b: bytes) -> tuple[list[str], list[list[str]]]:
    sh = xlrd.open_workbook(file_contents=b).sheet_by_index(0)
    grade = [[str(c).strip() for c in sh.row_values(i)] for i in range(sh.nrows)]
    hi = next((i for i, r in enumerate(grade) if any("Boletim" in c for c in r)), 0)
    return grade[hi], grade[hi + 1:]


def col(cab: list[str], *nomes: str) -> int | None:
    for j, c in enumerate(cab):
        if any(n.lower() in c.lower() for n in nomes):
            return j
    return None


def num_boletim(v: str) -> str | None:
    v = re.sub(r"\.0$", "", v.strip())
    # 407 vem 11 díg (Excel come o zero), 667 vem 12 — normaliza para 12 (zero-pad), senão o JOIN falha.
    return v.zfill(12) if re.fullmatch(r"\d{9,12}", v) else None


def main(args: list[str]) -> int:
    dia = args[0] if args else "15/09/2026"
    s = KlinikosSession(); s.entrar()
    t0 = time.time()

    # 407 — registrados no dia
    cab7, dados7 = linhas(baixa_xls(s, f"/KlinikosNet/Relatorios/rptviewXls.aspx?parRel=407&parNum=4&par1={UNID}&par2={dia}&par3={dia}&par4=1&Modulo=UPA"))
    jb7 = col(cab7, "Boletim"); jpront = col(cab7, "Prontu"); jpac = col(cab7, "Paciente"); jclin = col(cab7, "Clinic", "Clínic")
    reg: dict[str, dict] = {}
    for r in dados7:
        b = num_boletim(r[jb7]) if jb7 is not None and jb7 < len(r) else None
        if not b:
            continue
        reg.setdefault(b, {})["clinica"] = (r[jclin] if jclin is not None and jclin < len(r) else "") or reg.get(b, {}).get("clinica", "")
        reg[b]["tem407"] = True

    # 667 — nominal por classificação (chegada + cor)
    cab6, dados6 = linhas(baixa_xls(s, f"/KlinikosNet/Relatorios/rptviewXls.aspx?parNomeMaquina=&parRel=667&parNum=6&par1={dia}&par2={dia}&par3=&par4={UNID}&par5=PAR&par6="))
    jb6 = col(cab6, "Boletim"); jentrada = col(cab6, "Entrada", "Data/Hora"); jorigem = col(cab6, "Origem")
    # a cor no 667 é cabeçalho de GRUPO: linha com "Status da Classificação:" + a cor numa célula.
    # Rastreia a cor corrente e normaliza para a cor-base (Verde/Amarelo/… sem o sufixo Obs/Consult).
    CORES = re.compile(r"(Vermelh\w*|Amarelo|Verde|Laranja|Azul)", re.I)
    def cor_base(txt: str) -> str:
        m = CORES.search(txt); return m.group(1).capitalize() if m else txt
    cor_atual = None
    for r in dados6:
        celulas = [c for c in r if c]
        if any("Status da Classifica" in c for c in celulas):
            achou = next((c for c in celulas if CORES.search(c) and "Status" not in c), None)
            if achou:
                cor_atual = cor_base(achou)
            continue
        b = num_boletim(r[jb6]) if jb6 is not None and jb6 < len(r) else None
        if not b:
            continue
        d = reg.setdefault(b, {})
        d["tem667"] = True
        d["cor"] = cor_atual or d.get("cor")
        if jentrada is not None and jentrada < len(r):
            d["chegada"] = r[jentrada]

    dt = time.time() - t0
    # ---- relatório do dry-run (só agregados) ----
    n = len(reg)
    com_cor = sum(1 for d in reg.values() if d.get("cor"))
    com_chegada = sum(1 for d in reg.values() if d.get("chegada"))
    so407 = sum(1 for d in reg.values() if d.get("tem407") and not d.get("tem667"))
    so667 = sum(1 for d in reg.values() if d.get("tem667") and not d.get("tem407"))
    ambos = sum(1 for d in reg.values() if d.get("tem407") and d.get("tem667"))
    print(f"\n=== DRY-RUN espinha Conde {dia} (nada escrito) — {dt:.0f}s ===")
    print(f"boletins montados: {n}")
    print(f"  com cor (667): {com_cor}  | com chegada: {com_chegada}")
    print(f"  em 407+667: {ambos} | só 407: {so407} | só 667: {so667}")
    print("distribuição por cor (o que o painel mostraria):")
    for cor, q in collections.Counter(d.get("cor") or "(sem cor)" for d in reg.values()).most_common():
        print(f"   {cor}: {q}")
    print("amostra (3, mascarada):")
    for b, d in list(reg.items())[:3]:
        print(f"   boletim ***{b[-4:]}: cor={d.get('cor')} chegada={d.get('chegada')} clinica={d.get('clinica','')[:24]}")
    print("\nEncounter que a via rápida gravaria: identifier urn:klinikos:boletim = "
          f"'klinikosconde-{UNID}:<spa>', period.start=chegada, class EMER, +Condition(CID via 526 noturno).")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
