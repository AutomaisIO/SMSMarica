"""Importa ~10 médicos do Salux (tabela MEDICO) para o hub FHIR como Practitioner."""
from __future__ import annotations

import json
import re
import urllib.request

from conexao import executar_json

HUB = "http://smsmarica.online:5081"
SRC = "https://smsmarica.saude.marica/source/salux"
S_CPF = "https://fhir.saude.gov.br/sid/cpf"
S_CNS = "https://fhir.saude.gov.br/sid/cns"
S_RG = "urn:br:gov:rg"
S_CRM = "urn:br:conselho:crm:"   # + UF
S_SALUX = "urn:salux:cd_medico"
EXTRAS = "urn:salux:extras"


def dig(v):
    return re.sub(r"\D", "", str(v)) if v else ""


def s(v):
    v = (str(v).strip() if v is not None else "")
    return v or None


def http(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(HUB + path, data=data, method=method)
    req.add_header("Accept", "application/fhir+json")
    if data:
        req.add_header("Content-Type", "application/fhir+json")
    with urllib.request.urlopen(req, timeout=20) as r:
        raw = r.read().decode()
        return r.status, (json.loads(raw) if raw.strip() else {})


def purgar():
    _, b = http("GET", "/fhir/Practitioner")
    ids = [e["resource"]["id"] for e in (b.get("entry") or [])]
    for i in ids:
        http("DELETE", f"/fhir/Practitioner/{i}")
    print(f"Hub limpo: {len(ids)} practitioners removidos.")


def construir(m):
    cpf = dig(m.get("cpf"))
    uf = (str(m.get("uf") or "").strip().upper() or "RJ")
    crm = dig(m.get("crm"))

    ident = []
    if cpf:
        ident.append({"system": S_CPF, "value": cpf})
    if dig(m.get("cns")):
        ident.append({"system": S_CNS, "value": dig(m.get("cns"))})
    if crm:
        ident.append({"system": S_CRM + uf, "value": crm})
    if s(m.get("rg")):
        rg = {"system": S_RG, "value": s(m.get("rg"))}
        if s(m.get("orgao")):
            rg["assigner"] = {"display": s(m.get("orgao"))}
        ident.append(rg)
    ident.append({"system": S_SALUX, "value": str(m.get("cd"))})

    p = {
        "resourceType": "Practitioner",
        "meta": {"source": SRC},
        "identifier": ident,
        "name": [{"use": "official", "text": s(m.get("nome"))}],
        "active": str(m.get("ativo") or "").upper() in ("S", "1", "A", "T"),
    }
    sexo = str(m.get("sexo") or "").strip().upper()
    p["gender"] = {"M": "male", "F": "female"}.get(sexo, "unknown")
    if s(m.get("nasc")):
        p["birthDate"] = m["nasc"]
    if s(m.get("email")):
        p["telecom"] = [{"system": "email", "value": s(m.get("email"))}]
    if crm:
        p["qualification"] = [{
            "identifier": [{"system": S_CRM + uf, "value": crm}],
            "code": {"text": f"CRM {uf}"},
        }]

    extras = {k: m.get(k) for k in ("mae", "pai", "orgao", "categoria", "cbo", "especialidade") if m.get(k) not in (None, "", "0")}
    if extras:
        p["extension"] = [{"url": EXTRAS, "valueString": json.dumps(extras, ensure_ascii=False)}]
    return p


def main():
    purgar()
    linhas = executar_json(
        """
        SELECT JSON_OBJECT(
            'cd' VALUE cd_medico, 'nome' VALUE nm_medico, 'crm' VALUE nr_crm, 'uf' VALUE uf_cd_uf,
            'cpf' VALUE cpf, 'cns' VALUE cns, 'rg' VALUE nr_rg, 'orgao' VALUE orgao_emissor,
            'nasc' VALUE TO_CHAR(dt_nascimento,'YYYY-MM-DD'), 'sexo' VALUE sexo, 'email' VALUE ds_email,
            'ativo' VALUE in_ativo, 'mae' VALUE nm_mae, 'pai' VALUE nm_pai,
            'categoria' VALUE id_categoria, 'cbo' VALUE cd_cbo_smm,
            'especialidade' VALUE (
                SELECT LISTAGG(e.ds_especialidade, ', ') WITHIN GROUP (ORDER BY e.ds_especialidade)
                FROM medico_especialidade me JOIN especialidade e ON e.cd_especialidade = me.cd_especialidade
                WHERE me.cd_medico = med.cd_medico))
        FROM (
            SELECT * FROM medico
            WHERE nr_crm IS NOT NULL AND nm_medico IS NOT NULL AND cpf IS NOT NULL
              AND dt_exclusao IS NULL
            ORDER BY cd_medico DESC
        ) med WHERE ROWNUM <= 10
        """,
        modo="supervisor",
    )
    print(f"Salux retornou {len(linhas)} médicos.")
    n = 0
    for m in linhas:
        if not s(m.get("nome")):
            continue
        st, body = http("POST", "/fhir/Practitioner", construir(m))
        print(f"  [{st}] {s(m.get('nome'))}  CRM {m.get('crm')}/{m.get('uf')} -> {body.get('id')}")
        n += 1
    print(f"Importados: {n}")


if __name__ == "__main__":
    raise SystemExit(main())
