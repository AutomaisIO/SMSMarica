"""Importa atendimentos (BAA) dos pacientes já no hub FHIR -> Encounter + Condition.

Fatia 1 do histórico clínico. Para cada Patient do hub:
  - resolve cd_paciente (identifier urn:salux:cd_paciente)
  - lê os últimos N BAA do Salux (INFOSAUDE.BAA), com CID e nome do médico
  - cria Encounter (class AMB/EMER) + Condition (CID-10) no hub, meta.source=salux
Idempotente: limpa Encounter/Condition do paciente antes de reimportar.

Importador roda ON-PREM (alcança o Oracle interno) e empurra pro hub público.
Somente SELECT no Oracle (read-only). NÃO commitar saída — pode ter PII.
"""
from __future__ import annotations

import json
import urllib.request

from conexao import executar_json

HUB = "http://smsmarica.online:5081"
SRC = "https://smsmarica.saude.marica/source/salux"
S_BAA = "urn:salux:baa"
SYS_CID = "http://hl7.org/fhir/sid/icd-10"
SYS_CLASS = "http://terminology.hl7.org/CodeSystem/v3-ActCode"
S_PAC = "urn:salux:cd_paciente"
LIMITE_POR_PACIENTE = 30


def s(v):
    v = (str(v).strip() if v is not None else "")
    return v or None


def dt(v):
    """Datas do Salux são horário de Brasília; FHIR exige offset em dateTime com hora."""
    v = s(v)
    return v + "-03:00" if v else None


def http(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(HUB + path, data=data, method=method)
    req.add_header("Accept", "application/fhir+json")
    if data:
        req.add_header("Content-Type", "application/fhir+json")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw.strip() else {})
    except urllib.error.HTTPError as e:
        corpo = e.read().decode(errors="replace")
        print(f"  !! {method} {path} -> HTTP {e.code}: {corpo[:500]}")
        raise


def pacientes_do_hub():
    """[(fhir_id, cd_paciente)] dos Patients no hub que têm identifier do Salux."""
    _, b = http("GET", "/fhir/Patient")
    out = []
    for e in (b.get("entry") or []):
        r = e["resource"]
        cd = next((i.get("value") for i in r.get("identifier", []) if i.get("system") == S_PAC), None)
        if cd:
            out.append((r["id"], cd))
    return out


def purgar_paciente(fhir_id):
    for tipo in ("Encounter", "Condition"):
        _, b = http("GET", f"/fhir/{tipo}?patient={fhir_id}")
        for e in (b.get("entry") or []):
            http("DELETE", f"/fhir/{tipo}/{e['resource']['id']}")


def baa_do_paciente(cd):
    return executar_json(
        f"""
        SELECT JSON_OBJECT(
            'h' VALUE cd_hospital, 'ano' VALUE dt_ano_baa, 'nr' VALUE nr_baa,
            'dt_cheg'  VALUE TO_CHAR(dt_chegada,    'YYYY-MM-DD"T"HH24:MI:SS'),
            'dt_atend' VALUE TO_CHAR(dt_atendimento,'YYYY-MM-DD"T"HH24:MI:SS'),
            'dt_saida' VALUE TO_CHAR(dt_saida,      'YYYY-MM-DD"T"HH24:MI:SS'),
            'cid' VALUE cd_cid,
            'cid_ds' VALUE (SELECT ds_cid FROM infosaude.cid WHERE cd_cid = b.cd_cid),
            'emerg' VALUE in_emergencia,
            'medico' VALUE (SELECT nm_medico FROM infosaude.medico WHERE cd_medico = b.cd_medico))
        FROM (
            SELECT * FROM infosaude.baa
            WHERE cd_paciente = {int(cd)}
            ORDER BY dt_atendimento DESC NULLS LAST
        ) b WHERE ROWNUM <= {LIMITE_POR_PACIENTE}
        """,
        modo="supervisor",
    )


def build_encounter(b, patient_ref):
    emerg = (str(b.get("emerg") or "").upper() == "S")
    start = dt(b.get("dt_cheg")) or dt(b.get("dt_atend"))
    enc = {
        "resourceType": "Encounter",
        "meta": {"source": SRC},
        "status": "finished",
        "class": {
            "system": SYS_CLASS,
            "code": "EMER" if emerg else "AMB",
            "display": "emergency" if emerg else "ambulatory",
        },
        "subject": {"reference": patient_ref},
        "identifier": [{"system": S_BAA, "value": f"{b['h']}-{b['ano']}-{b['nr']}"}],
    }
    if start:
        enc["period"] = {"start": start}
        if dt(b.get("dt_saida")):
            enc["period"]["end"] = dt(b.get("dt_saida"))
    if s(b.get("medico")):
        enc["participant"] = [{"individual": {"display": s(b.get("medico"))}}]
    return enc


def build_condition(b, patient_ref, enc_ref):
    cid, ds = s(b.get("cid")), s(b.get("cid_ds"))
    if not cid:
        return None
    return {
        "resourceType": "Condition",
        "meta": {"source": SRC},
        "subject": {"reference": patient_ref},
        "encounter": {"reference": enc_ref},
        "code": {
            "coding": [{"system": SYS_CID, "code": cid, "display": ds}],
            "text": ds or cid,
        },
    }


def main():
    pacientes = pacientes_do_hub()
    print(f"Pacientes no hub: {len(pacientes)}")
    tot_enc = tot_cond = 0
    for fhir_id, cd in pacientes:
        purgar_paciente(fhir_id)
        patient_ref = f"Patient/{fhir_id}"
        linhas = baa_do_paciente(cd)
        n_enc = n_cond = 0
        for b in linhas:
            _, criado = http("POST", "/fhir/Encounter", build_encounter(b, patient_ref))
            enc_ref = f"Encounter/{criado.get('id')}"
            n_enc += 1
            cond = build_condition(b, patient_ref, enc_ref)
            if cond:
                http("POST", "/fhir/Condition", cond)
                n_cond += 1
        tot_enc += n_enc
        tot_cond += n_cond
        print(f"  cd_paciente={cd}: {n_enc} atendimentos, {n_cond} diagnósticos")
    print(f"\nTotal: {tot_enc} Encounters, {tot_cond} Conditions")


if __name__ == "__main__":
    raise SystemExit(main())
