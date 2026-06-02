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

import base64
import html as _html
import json
import urllib.request

from conexao import executar_json, executar_texto

HUB = "http://smsmarica.online:5081"
SRC = "https://smsmarica.saude.marica/source/salux"
S_BAA = "urn:salux:baa"
S_EDOC = "urn:salux:edoc"
SYS_CID = "http://hl7.org/fhir/sid/icd-10"
SYS_CLASS = "http://terminology.hl7.org/CodeSystem/v3-ActCode"
S_PAC = "urn:salux:cd_paciente"
LIMITE_POR_PACIENTE = 30
LIMITE_DOCS = 15


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
    for tipo in ("DocumentReference", "Condition", "Encounter"):
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


def edoc_do_paciente(cd):
    """Documentos EDOC do paciente ligados a um BAA (os mais recentes)."""
    return executar_json(
        f"""
        SELECT JSON_OBJECT(
            'h' VALUE cd_hospital, 'ano' VALUE ano_movimento, 'idm' VALUE id_movimento,
            'modelo' VALUE (SELECT ds_modelo FROM infosaude.edoc_modelo m WHERE m.cd_modelo = mov.cd_modelo),
            'dt' VALUE TO_CHAR(dt_episodio,'YYYY-MM-DD"T"HH24:MI:SS'),
            'baa' VALUE COALESCE(baa_cd_hospital, cd_hospital)||'-'||dt_ano_baa||'-'||nr_baa)
        FROM (
            SELECT * FROM infosaude.edoc_movimento
            WHERE cd_paciente = {int(cd)} AND nr_baa IS NOT NULL
            ORDER BY dt_episodio DESC NULLS LAST
        ) mov WHERE ROWNUM <= {LIMITE_DOCS}
        """,
        modo="supervisor",
    )


_ROW = "@@ROW@@"  # sentinela de linha lógica (não aparece em dados clínicos)
_FLD = "@@FLD@@"  # sentinela de campo


def itens_do_documento(h, ano, idm):
    # Uma única coluna concatenada + sentinelas: executar_select (COLSEP) quebra
    # com 2 colunas de texto largo (sqlplus joga cada coluna em linha física
    # separada -> resp vazio) e JSON_OBJECT estoura com ORA-40474 no charset
    # legado. executar_texto rejunta linhas físicas; fatiamos pelos sentinelas.
    txt = executar_texto(
        f"""
        SELECT '{_ROW}' || it.ds_item || '{_FLD}' || i.ds_resposta
        FROM infosaude.edoc_movimento_item i
        JOIN infosaude.edoc_item it ON it.cd_item = i.cd_item
        WHERE i.cd_hospital = {int(h)} AND i.ano_movimento = {int(ano)} AND i.id_movimento = {int(idm)}
          AND i.ds_resposta IS NOT NULL
        ORDER BY i.seq_docto, i.cd_item_grupo, i.cd_item
        """,
        modo="supervisor",
    )
    itens = []
    for chunk in txt.split(_ROW):
        if not chunk:
            continue
        label, _, resp = chunk.partition(_FLD)
        itens.append({"label": label, "resp": resp})
    return itens


def montar_html(modelo, itens):
    linhas, atual = [], None
    for it in itens:
        resp = s(it.get("resp"))
        if not resp:
            continue
        label = s(it.get("label")) or ""
        if label == atual:  # campo multilinha: continua o anterior
            linhas.append(f"<div>{_html.escape(resp)}</div>")
        else:
            linhas.append(f"<p><strong>{_html.escape(label)}:</strong> {_html.escape(resp)}</p>")
            atual = label
    corpo = "\n".join(linhas) or "<p><em>(documento sem itens preenchidos)</em></p>"
    return f"<section><h3>{_html.escape(modelo or 'Documento')}</h3>{corpo}</section>"


def build_docref(doc, html_str, patient_ref, enc_ref):
    data64 = base64.b64encode(html_str.encode("utf-8")).decode("ascii")
    titulo = s(doc.get("modelo")) or "Documento"
    d = {
        "resourceType": "DocumentReference",
        "meta": {"source": SRC},
        "status": "current",
        "type": {"text": titulo},
        "subject": {"reference": patient_ref},
        "identifier": [{"system": S_EDOC, "value": f"{doc['h']}-{doc['ano']}-{doc['idm']}"}],
        "content": [{"attachment": {"contentType": "text/html", "data": data64, "title": titulo}}],
        "context": {"encounter": [{"reference": enc_ref}]},
    }
    if dt(doc.get("dt")):
        d["date"] = dt(doc.get("dt"))
    return d


def main():
    pacientes = pacientes_do_hub()
    print(f"Pacientes no hub: {len(pacientes)}")
    tot_enc = tot_cond = tot_doc = 0
    for fhir_id, cd in pacientes:
        purgar_paciente(fhir_id)
        patient_ref = f"Patient/{fhir_id}"
        enc_por_baa = {}
        n_enc = n_cond = n_doc = 0
        for b in baa_do_paciente(cd):
            _, criado = http("POST", "/fhir/Encounter", build_encounter(b, patient_ref))
            enc_ref = f"Encounter/{criado.get('id')}"
            enc_por_baa[f"{b['h']}-{b['ano']}-{b['nr']}"] = enc_ref
            n_enc += 1
            cond = build_condition(b, patient_ref, enc_ref)
            if cond:
                http("POST", "/fhir/Condition", cond)
                n_cond += 1
        for doc in edoc_do_paciente(cd):
            enc_ref = enc_por_baa.get(s(doc.get("baa")))
            if not enc_ref:
                continue  # documento de um BAA fora dos atendimentos importados
            itens = itens_do_documento(doc["h"], doc["ano"], doc["idm"])
            html_str = montar_html(doc.get("modelo"), itens)
            http("POST", "/fhir/DocumentReference", build_docref(doc, html_str, patient_ref, enc_ref))
            n_doc += 1
        tot_enc += n_enc
        tot_cond += n_cond
        tot_doc += n_doc
        print(f"  cd_paciente={cd}: {n_enc} atend., {n_cond} diag., {n_doc} docs")
    print(f"\nTotal: {tot_enc} Encounters, {tot_cond} Conditions, {tot_doc} DocumentReferences")


if __name__ == "__main__":
    raise SystemExit(main())
