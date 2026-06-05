"""Verifica se os BAAs dos pacientes JÁ no hub têm prescrição/medicação preenchida.

Read-only (modo supervisor). Não decide nada — só relata cobertura:
  - quantos BAAs (atendimentos) cada paciente tem
  - quantos desses BAAs têm ao menos 1 item em PRESC_BAA_OPC_PROD (medicação)
  - amostra de materiais (ds_material) pra inspeção visual

PRESC_BAA_OPC_PROD liga ao BAA por (cd_hospital, dt_ano_baa, nr_baa) e ao
catálogo por cd_material -> MATMED.ds_material. Não commitar saída (PII).
"""
from __future__ import annotations

import json
import urllib.request

from conexao import executar_json

HUB = "http://smsmarica.online:5081"
S_PAC = "urn:salux:cd_paciente"
LIMITE_POR_PACIENTE = 30  # mesmo recorte do importador


def pacientes_do_hub():
    req = urllib.request.Request(HUB + "/fhir/Patient")
    req.add_header("Accept", "application/fhir+json")
    with urllib.request.urlopen(req, timeout=30) as r:
        b = json.loads(r.read().decode())
    out = []
    for e in (b.get("entry") or []):
        r = e["resource"]
        cd = next((i.get("value") for i in r.get("identifier", []) if i.get("system") == S_PAC), None)
        nome = (r.get("name") or [{}])[0].get("text") or "?"
        if cd:
            out.append((cd, nome))
    return out


def cobertura_paciente(cd):
    """Por BAA recente do paciente: nº de itens de prescrição (medicação)."""
    return executar_json(
        f"""
        SELECT JSON_OBJECT(
            'baa' VALUE b.cd_hospital||'-'||b.dt_ano_baa||'-'||b.nr_baa,
            'dt'  VALUE TO_CHAR(b.dt_atendimento,'YYYY-MM-DD'),
            'itens' VALUE (
                SELECT COUNT(*) FROM infosaude.presc_baa_opc_prod p
                WHERE p.cd_hospital = b.cd_hospital
                  AND p.dt_ano_baa  = b.dt_ano_baa
                  AND p.nr_baa      = b.nr_baa))
        FROM (
            SELECT * FROM infosaude.baa
            WHERE cd_paciente = {int(cd)}
            ORDER BY dt_atendimento DESC NULLS LAST
        ) b WHERE ROWNUM <= {LIMITE_POR_PACIENTE}
        """,
        modo="supervisor",
    )


def amostra_materiais(cd, limite=8):
    """Alguns materiais prescritos do paciente (qualquer BAA recente)."""
    return executar_json(
        f"""
        SELECT JSON_OBJECT(
            'mat' VALUE NVL(m.ds_material, TO_CHAR(p.cd_material)),
            'qt'  VALUE p.qt_material_prescrita,
            'um'  VALUE p.cd_unidade_medida)
        FROM infosaude.presc_baa_opc_prod p
        JOIN infosaude.baa b
          ON b.cd_hospital = p.cd_hospital AND b.dt_ano_baa = p.dt_ano_baa AND b.nr_baa = p.nr_baa
        LEFT JOIN infosaude.matmed m ON m.cd_material = p.cd_material
        WHERE b.cd_paciente = {int(cd)}
          AND ROWNUM <= {int(limite)}
        """,
        modo="supervisor",
    )


def main():
    pacientes = pacientes_do_hub()
    print(f"Pacientes no hub: {len(pacientes)}\n")
    tot_baa = tot_com = 0
    for cd, nome in pacientes:
        linhas = cobertura_paciente(cd)
        n_baa = len(linhas)
        n_com = sum(1 for l in linhas if (l.get("itens") or 0) > 0)
        tot_itens = sum((l.get("itens") or 0) for l in linhas)
        tot_baa += n_baa
        tot_com += n_com
        print(f"cd={cd} {nome[:30]:30} | {n_baa:2} BAA | {n_com:2} c/ medicacao | {tot_itens} itens")
        if n_com:
            for m in amostra_materiais(cd):
                print(f"      - {m.get('mat')}  (qt={m.get('qt')} {m.get('um') or ''})")
    print(f"\nTotal: {tot_baa} BAAs, {tot_com} com medicacao preenchida")


if __name__ == "__main__":
    raise SystemExit(main())
