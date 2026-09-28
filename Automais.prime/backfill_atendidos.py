"""BACKFILL do clínico do Prime para o hub: varre dia × unidade e escreve NDJSON. SOMENTE LEITURA.

É a via que NÃO depende da extensão estar no PC — puxa o mesmo que o operador puxaria pelo menu de
relatórios, com a sessão de leitura do laboratório.

Uso:
  python backfill_atendidos.py 01/09/2026 22/09/2026 --seco        # só o PLANO (nada é pedido)
  python backfill_atendidos.py 01/09/2026 22/09/2026               # todas as unidades do gate
  python backfill_atendidos.py 22/09/2026 22/09/2026 --unidade GUID

Saída: `capturas/backfill/<unidade>_<AAAAMMDD>.ndjson` (um atendimento por linha) + um
`_resumo.csv`. **Tem PII** — `capturas/` é ignorada pelo git, e assim deve continuar.

Retomável: dia/unidade cujo .ndjson já existe é pulado (`--refazer` força). Uma varredura longa
cai no meio (sessão única do Prime derruba a outra estação) e não pode recomeçar do zero.

Custo medido (23/09/2026): ~248 ms e ~135 KB por dia × unidade. Com 35 unidades, um mês são
1.085 requisições — por isso o `--pausa` (padrão 0,4 s) e por isso o `--seco` existe: confira o
tamanho da varredura ANTES de metralhar o Prime.
"""

from __future__ import annotations

import csv
import json
import sys
import time
from datetime import date, datetime, timedelta

from bs4 import BeautifulSoup

from prime.atendidos import do_csv, baixar
from prime.client import CAP, PrimeSession

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = CAP / "backfill"


def unidades_do_gate() -> dict[str, str]:
    """As 35 unidades vêm do `LoginView1$ddlUnidade` da tela pós-login já capturada. `unidades=`
    do relatório aceita qualquer GUID dessa lista SEM trocar a unidade da sessão (medido 16/09)."""
    html = (CAP / "pos_login.html").read_text(encoding="utf-8")
    sel = BeautifulSoup(html, "html.parser").find("select", attrs={"name": "LoginView1$ddlUnidade"})
    if not sel:
        raise SystemExit("capturas/pos_login.html sem o seletor de unidades — rode probe_login.py")
    return {o["value"]: o.get_text(strip=True) for o in sel.find_all("option") if o.get("value") not in (None, "-1")}


def dias(ini: str, fim: str) -> list[date]:
    a = datetime.strptime(ini, "%d/%m/%Y").date()
    b = datetime.strptime(fim, "%d/%m/%Y").date()
    if b < a:
        raise SystemExit("data final anterior à inicial")
    return [a + timedelta(days=i) for i in range((b - a).days + 1)]


def main(argv: list[str]) -> int:
    pos = [a for a in argv if not a.startswith("--")]
    if len(pos) < 2:
        raise SystemExit(__doc__)
    ini, fim = pos[0], pos[1]
    seco = "--seco" in argv
    refazer = "--refazer" in argv
    pausa = float(argv[argv.index("--pausa") + 1]) if "--pausa" in argv else 0.4
    todas = unidades_do_gate()
    alvo = ({argv[argv.index("--unidade") + 1]: todas.get(argv[argv.index("--unidade") + 1], "?")}
            if "--unidade" in argv else todas)

    grade = dias(ini, fim)
    print(f"{len(grade)} dia(s) × {len(alvo)} unidade(s) = {len(grade)*len(alvo)} requisições "
          f"(~{len(grade)*len(alvo)*0.25:.0f}s de rede, ~{len(grade)*len(alvo)*135/1024:.0f} MB)")
    if seco:
        for g, nome in alvo.items():
            print(f"   {g}  {nome}")
        print("\n--seco: nada foi pedido ao Prime.")
        return 0

    SAIDA.mkdir(parents=True, exist_ok=True)
    linhas_resumo, total, falhas = [], 0, 0

    if "--de-uma-vez" in argv:
        # O relatório aceita JANELA LARGA: 1 requisição cobre o intervalo inteiro de uma unidade
        # (medido 23/09/2026: ano de 2026 do CDT = 3.942 atendimentos em 3,2 s). É assim que o
        # backfill HISTÓRICO se faz — dia a dia só serve para a conferência diária.
        with PrimeSession() as s:
            s.entrar()
            for guid, nome in alvo.items():
                destino = SAIDA / f"{guid}_{grade[0]:%Y%m%d}-{grade[-1]:%Y%m%d}.ndjson"
                if destino.exists() and not refazer:
                    continue
                try:
                    texto = baixar(s, guid, grade[0].strftime("%d/%m/%Y"),
                                   grade[-1].strftime("%d/%m/%Y"))
                    ats, aferido = do_csv(texto, guid, nome)
                except Exception as e:  # noqa: BLE001
                    falhas += 1
                    print(f"   FALHA {nome}: {e}")
                    continue
                with destino.open("w", encoding="utf-8") as f:
                    for a in ats:
                        f.write(json.dumps(a, ensure_ascii=False) + "\n")
                total += len(ats)
                consultas = sum(1 for a in ats if not a["acolhimento"])
                dias_vistos = len({(a["fim"] or "")[:10] for a in ats if a["fim"]})
                print(f"   {nome[:38]:<38} {len(ats):>6} atend. ({consultas} consulta) "
                      f"em {dias_vistos} dia(s)"
                      + ("" if aferido["ok"] else "   ATENÇÃO: conferência do parser falhou"))
                time.sleep(pausa)
        print(f"\n{total} atendimentos gravados em {SAIDA}   (falhas: {falhas})")
        return 1 if falhas else 0

    with PrimeSession() as s:
        s.entrar()
        for dia in grade:
            dia_br = dia.strftime("%d/%m/%Y")
            for guid, nome in alvo.items():
                destino = SAIDA / f"{guid}_{dia:%Y%m%d}.ndjson"
                if destino.exists() and not refazer:
                    continue
                try:
                    texto = baixar(s, guid, dia_br)
                    ats, aferido = do_csv(texto, guid, nome)
                except Exception as e:  # noqa: BLE001 — uma unidade ruim não derruba a varredura
                    falhas += 1
                    print(f"   FALHA {dia_br} {nome}: {e}")
                    linhas_resumo.append({"dia": dia.isoformat(), "unidade_id": guid,
                                          "unidade": nome, "atendimentos": "", "consultas": "",
                                          "parser_ok": "", "erro": str(e)[:120]})
                    continue
                with destino.open("w", encoding="utf-8") as f:
                    for a in ats:
                        f.write(json.dumps(a, ensure_ascii=False) + "\n")
                consultas = sum(1 for a in ats if not a["acolhimento"])
                total += len(ats)
                linhas_resumo.append({"dia": dia.isoformat(), "unidade_id": guid, "unidade": nome,
                                      "atendimentos": len(ats), "consultas": consultas,
                                      "parser_ok": aferido["ok"], "erro": ""})
                if ats:
                    print(f"   {dia_br}  {nome[:34]:<34} {len(ats):>4} atend. ({consultas} consulta)"
                          + ("" if aferido["ok"] else "   ATENÇÃO: conferência do parser falhou"))
                time.sleep(pausa)

    resumo = SAIDA / "_resumo.csv"
    novo = not resumo.exists()
    with resumo.open("a", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["dia", "unidade_id", "unidade", "atendimentos",
                                          "consultas", "parser_ok", "erro"])
        if novo:
            w.writeheader()
        w.writerows(linhas_resumo)
    print(f"\n{total} atendimentos gravados em {SAIDA}   (falhas: {falhas})")
    print(f"resumo: {resumo}")
    return 1 if falhas else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
