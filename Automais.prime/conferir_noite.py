"""CONFERENCIA DA NOITE: o dia fechou direito? Roda sem ninguem olhando. SOMENTE LEITURA nos dois lados.

Cruza duas fontes independentes:
  VERDADE   - o relatorio *Pacientes Atendidos* do Prime (1 GET por unidade). Cobre TODA a rede,
              instalada a extensao ou nao. E por isso que ele e a espinha e a extensao e o bonus.
  COBERTURA - o que a extensao depositou em `smsmarica.sisreg_captura_navegador` naquele dia.

Por que as duas: a extensao da a NARRATIVA (o SOAP que o medico escreveu, as telas), mas so do PC
onde esta instalada; o relatorio da o ESTRUTURADO de todo mundo, mas nunca a narrativa. Uma nao
cobre a outra, e a conferencia serve justamente para ver o buraco entre elas.

Uso:
  python conferir_noite.py                          # ontem
  python conferir_noite.py 22/09/2026
  python conferir_noite.py 22/09/2026 --sem-banco   # so a verdade (sem tocar no hub)

Saida: relatorio em texto no stdout (para e-mail/log) + `capturas/backfill/_conferencia_<dia>.txt`.
Codigo de saida != 0 quando ha ALERTA - para o agendador da noite reclamar sozinho.
"""

from __future__ import annotations

import sys
from datetime import date, datetime, timedelta

from bs4 import BeautifulSoup

from prime.atendidos import baixar, do_csv
from prime.client import CAP, PrimeSession

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SAIDA = CAP / "backfill"

# Unidades que operam no Prime TODO DIA. As USFs vivem no Klinikos e vem zeradas por natureza.
# Medido no backfill de 23/09/2026 (01/01/2024 a hoje, 243.330 atendimentos): so 10 das 36 do gate
# tem historico, e destas CEREST (17 atendimentos em um ano) e ODONTOMOVEL (139) sao esporadicas -
# ficam FORA desta lista de proposito, senao alertariam quase toda noite sem nada errado.
# Zero numa das 8 abaixo e alerta; nas outras nao.
COM_MOVIMENTO = {
    "44d338e6-d84f-4cab-bd9e-5b30a160bc45",  # AMBULATORIO PERICLES SIQUEIRA FERREIRA
    "2b89b351-f048-492e-b28b-75c498fd04bc",  # CDT DR ALBERTO LUIS MACHADO BORGES
    "c381396d-6ef0-4307-b1a4-0738b7baa58d",  # CENTRO DE REABILITACAO AMBULATORIAL E DOMICILIAR
    "ed60e0c4-046a-488b-8836-af717be01eeb",  # CENTRO MATERNO INFANTIL
    "f449877a-aec5-4fed-8e79-2f05d7954764",  # CEO BOQUEIRAO
    "8ae301da-c9e2-4ea5-9048-c19143b9453c",  # CEO ITAIPUACU
    "643c58d7-95d8-4486-b5f9-9220a24680d4",  # MELHOR EM CASA MARICA
    "32fc8be5-0054-4a66-a4a6-deb2f5d4c7de",  # SAE SERVICO DE ATENDIMENTO ESPECIALIZADO
}


def unidades_do_gate() -> dict[str, str]:
    html = (CAP / "pos_login.html").read_text(encoding="utf-8")
    sel = BeautifulSoup(html, "html.parser").find("select", attrs={"name": "LoginView1$ddlUnidade"})
    return {o["value"]: o.get_text(strip=True) for o in sel.find_all("option")
            if o.get("value") not in (None, "-1")}


def verdade(dia_br: str) -> tuple[list[dict], list[str]]:
    """Uma linha por unidade + os alertas do lado do Prime."""
    dia_iso = datetime.strptime(dia_br, "%d/%m/%Y").date().isoformat()
    linhas: list[dict] = []
    alertas: list[str] = []
    with PrimeSession() as s:
        s.entrar()
        for guid, nome in unidades_do_gate().items():
            try:
                ats, aferido = do_csv(baixar(s, guid, dia_br), guid, nome)
            except Exception as e:  # noqa: BLE001 - uma unidade ruim nao derruba a conferencia
                alertas.append(f"unidade {nome}: falhou ao baixar ({e})")
                continue
            if not aferido["ok"]:
                alertas.append(f"unidade {nome}: CONFERENCIA DO PARSER FALHOU - nao importar "
                               f"({aferido}); o CSV do Prime e malformado e desalinha em silencio")
            if not ats:
                if guid in COM_MOVIMENTO:
                    alertas.append(f"unidade {nome}: ZERO atendimentos, mas ela costuma ter movimento")
                continue
            consultas = [a for a in ats if not a["acolhimento"]]
            sem_cid = [a for a in consultas if not a["cids"]]
            sem_hora = [a for a in ats if not a["fim"]]
            # O relatorio filtra pelo DIA DO ATENDIMENTO, mas `fim` e o fechamento do registro:
            # o profissional que deixa o atendimento aberto e fecha na manha seguinte produz um
            # `fim` no dia seguinte (visto em 22/09/2026 no Reabilitacao: fechado 23/09 08:33).
            # Nao e erro - e o "esta la ate agora". Importar pela CHAVE, nunca pela data do fim.
            fora = [a for a in ats if a["fim"] and a["fim"][:10] != dia_iso]
            if sem_cid:
                alertas.append(f"unidade {nome}: {len(sem_cid)}/{len(consultas)} consultas SEM CID")
            if sem_hora:
                alertas.append(f"unidade {nome}: {len(sem_hora)} atendimentos sem horario de termino")
            if fora:
                alertas.append(f"unidade {nome}: {len(fora)} atendimento(s) fechado(s) em OUTRO dia "
                               f"(o profissional deixou aberto) - ex.: {fora[0]['fim']}")
            linhas.append({
                "unidade": nome, "unidade_id": guid, "atendimentos": len(ats),
                "consultas": len(consultas), "acolhimentos": len(ats) - len(consultas),
                "com_cid": sum(1 for a in consultas if a["cids"]),
                "com_exame": sum(1 for a in ats if a["exames_solicitados"]),
                "com_medicamento": sum(1 for a in ats if a["medicamentos"]),
                "fora_do_dia": len(fora),
                "primeiro_fim": min((a["fim"] for a in ats if a["fim"] and a not in fora),
                                    default=""),
                "ultimo_fim": max((a["fim"] for a in ats if a["fim"] and a not in fora), default=""),
            })
    return linhas, alertas


def cobertura(dia: date) -> tuple[list[dict], list[str]]:
    """O que a extensao mandou naquele dia, por instalacao. Le o hub - SOMENTE SELECT."""
    sys.path.insert(0, str(CAP.parent.parent / "Aprendizados e Scratchpads" / "ferramentas"))
    from db import conn  # noqa: PLC0415 - so quando o banco e mesmo consultado

    sql = """
      select install_id, versao,
             count(*) capturas,
             count(distinct payload->>'busca')
                 filter (where caminho like '/Prime/Pep/%%') atendimentos_vistos,
             count(*) filter (where caminho like '/Prime/Pep/%%') capturas_pep,
             min(ocorrido_em at time zone 'America/Sao_Paulo')::time primeira,
             max(ocorrido_em at time zone 'America/Sao_Paulo')::time ultima
        from smsmarica.sisreg_captura_navegador
       where payload->>'sitio' = 'ecosistemas'
         and (ocorrido_em at time zone 'America/Sao_Paulo')::date = %s
       group by 1, 2 order by 3 desc"""
    with conn() as c, c.cursor() as cur:
        cur.execute(sql, (dia,))
        cols = [x[0] for x in cur.description]
        linhas = [dict(zip(cols, r)) for r in cur.fetchall()]

    alertas: list[str] = []
    if not linhas:
        alertas.append("NENHUMA captura da extensao no dia - nenhum PC mandou nada")
    for l in linhas:
        if l["capturas_pep"] == 0:
            alertas.append(f"instalacao {l['install_id'][:8]} mandou {l['capturas']} capturas mas "
                           f"NENHUMA do PEP - e um PC de recepcao/agenda, nao de consultorio")
    return linhas, alertas


def main(argv: list[str]) -> int:
    pos = [a for a in argv if not a.startswith("--")]
    dia = (datetime.strptime(pos[0], "%d/%m/%Y").date() if pos
           else date.today() - timedelta(days=1))
    dia_br = dia.strftime("%d/%m/%Y")

    saida: list[str] = [f"CONFERENCIA DO PRIME - {dia_br}", "=" * 78, ""]
    uni, alertas = verdade(dia_br)
    tot = sum(u["atendimentos"] for u in uni)
    cons = sum(u["consultas"] for u in uni)
    saida.append(f"VERDADE (relatorio Pacientes Atendidos) - {tot} atendimentos, {cons} consultas")
    saida.append(f"{'unidade':<40}{'atend':>6}{'consul':>7}{'CID':>6}{'exame':>6}{'med':>5}"
                 f"   termino 1o/ultimo")
    for u in sorted(uni, key=lambda x: -x["atendimentos"]):
        saida.append(f"{u['unidade'][:39]:<40}{u['atendimentos']:>6}{u['consultas']:>7}"
                     f"{u['com_cid']:>6}{u['com_exame']:>6}{u['com_medicamento']:>5}"
                     f"   {u['primeiro_fim'][11:16]}-{u['ultimo_fim'][11:16]}"
                     + (f"  (+{u['fora_do_dia']} fechado depois)" if u['fora_do_dia'] else ""))

    if "--sem-banco" not in argv:
        saida += ["", "COBERTURA (extensao)"]
        try:
            cob, mais = cobertura(dia)
            alertas += mais
            saida.append(f"{'instalacao':<14}{'versao':>8}{'capturas':>10}{'no PEP':>8}"
                         f"{'atend.':>8}   janela")
            for c in cob:
                saida.append(f"{c['install_id'][:12]:<14}{c['versao']:>8}{c['capturas']:>10}"
                             f"{c['capturas_pep']:>8}{c['atendimentos_vistos']:>8}"
                             f"   {str(c['primeira'])[:5]}-{str(c['ultima'])[:5]}")
            vistos = sum(c["atendimentos_vistos"] for c in cob)
            pct = (vistos / tot * 100) if tot else 0
            saida.append(f"\nnarrativa capturada em {vistos} de {tot} atendimentos ({pct:.1f}%) - "
                         f"o resto tem so o estruturado do relatorio, que e o esperado enquanto a "
                         f"extensao nao esta em todo PC")
        except Exception as e:  # noqa: BLE001 - sem banco a conferencia do Prime ainda vale
            alertas.append(f"nao consegui ler o hub: {e}")

    saida += ["", f"ALERTAS ({len(alertas)})"]
    saida += [f"  ! {a}" for a in alertas] or ["  nenhum"]
    texto = "\n".join(saida)
    print(texto)
    SAIDA.mkdir(parents=True, exist_ok=True)
    (SAIDA / f"_conferencia_{dia:%Y%m%d}.txt").write_text(texto, encoding="utf-8")
    return 1 if alertas else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
