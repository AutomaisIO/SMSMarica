"""Importa ~10 pacientes do Salux (Oracle, read-only) para o hub FHIR — COMPLETO.

Mapeia o máximo de PACIENTE para campos FHIR R4 nativos (nomes, identifiers,
endereço, telecom, contatos/filiação, óbito) + uma extension urn:salux:extras
com os códigos internos do Salux (cor, religião, nacionalidade, peso/altura,
sangue/RH, etnia, naturalidade, prontuários) para não perder nada.

Upsert por CPF: se já existe no hub, deleta e recria completo.
"""
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
S_PIS = "urn:br:gov:pis-pasep"
S_PASS = "urn:passport"
S_RNE = "urn:br:gov:rne"
S_CERT = "urn:br:gov:certidao-nascimento"
S_SALUX = "urn:salux:cd_paciente"
S_SGH = "urn:sgh:prontuario"
S_CEM = "urn:cem:prontuario"
EXTRAS_URL = "urn:salux:extras"
V3 = "http://terminology.hl7.org/CodeSystem/v3-RoleCode"


UF_IBGE = {
    "11": "RO", "12": "AC", "13": "AM", "14": "RR", "15": "PA", "16": "AP", "17": "TO",
    "21": "MA", "22": "PI", "23": "CE", "24": "RN", "25": "PB", "26": "PE", "27": "AL",
    "28": "SE", "29": "BA", "31": "MG", "32": "ES", "33": "RJ", "35": "SP",
    "41": "PR", "42": "SC", "43": "RS", "50": "MS", "51": "MT", "52": "GO", "53": "DF",
}


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


def existente_id(cpf):
    try:
        _, b = http("GET", f"/fhir/Patient?identifier={S_CPF}|{cpf}")
        e = b.get("entry") or []
        return e[0]["resource"]["id"] if e else None
    except Exception:
        return None


def contato(rel_code, nome, fone=None):
    c = {"relationship": [{"coding": [{"system": V3, "code": rel_code}]}], "name": {"text": nome}}
    if fone:
        c["telecom"] = [{"system": "phone", "value": fone}]
    return c


def construir(p):
    cpf = dig(p.get("cpf"))
    nome = s(p.get("nome"))

    ident = [{"system": S_CPF, "value": cpf}]
    if dig(p.get("cns")):
        ident.append({"system": S_CNS, "value": dig(p.get("cns"))})
    if s(p.get("rg")):
        rg = {"system": S_RG, "value": s(p.get("rg"))}
        if s(p.get("orgao")):
            rg["assigner"] = {"display": s(p.get("orgao"))}
        ident.append(rg)
    for val, sysu in [(p.get("pis"), S_PIS), (p.get("passaporte"), S_PASS), (p.get("rne"), S_RNE),
                      (p.get("certidao"), S_CERT), (p.get("sgh"), S_SGH), (p.get("cem"), S_CEM)]:
        if s(val) and s(val) not in ("0", "None"):
            ident.append({"system": sysu, "value": str(val).strip()})
    ident.append({"system": S_SALUX, "value": str(p.get("cd"))})

    nomes = [{"use": "official", "text": nome}]
    if s(p.get("social")) and str(p.get("flag_social") or "").upper() in ("S", "1", "T"):
        nomes.append({"use": "nickname", "text": s(p.get("social"))})

    pat = {
        "resourceType": "Patient",
        "meta": {"source": SRC},
        "identifier": ident,
        "name": nomes,
        "active": str(p.get("ativo") or "").upper() in ("S", "1", "A", "T"),
    }
    sexo = (str(p.get("sexo") or "").strip().upper())
    pat["gender"] = {"M": "male", "F": "female"}.get(sexo, "unknown")
    if s(p.get("nasc")):
        pat["birthDate"] = p["nasc"]
    if s(p.get("obito")):
        pat["deceasedDateTime"] = p["obito"]

    # Endereço
    linha = " ".join(x for x in [s(p.get("logr")), str(p["nr_logr"]) if p.get("nr_logr") else None] if x)
    end = {}
    if linha:
        end["line"] = [linha] + ([s(p.get("compl"))] if s(p.get("compl")) else [])
    if s(p.get("bairro")):
        end["district"] = s(p.get("bairro"))
    if dig(p.get("cep")):
        end["postalCode"] = dig(p.get("cep"))
    if s(p.get("ref")):
        end["text"] = "Ref: " + s(p.get("ref"))
    if s(p.get("cidade")):
        end["city"] = s(p.get("cidade"))
    if s(p.get("uf_sigla")):
        uf = str(p.get("uf_sigla")).strip()
        end["state"] = UF_IBGE.get(uf, uf)
    if end:
        end["use"] = "home"
        pat["address"] = [end]

    if s(p.get("estado_civil_ds")):
        pat["maritalStatus"] = {"text": s(p.get("estado_civil_ds"))}

    # Telecom
    tel = []
    fone = (dig(p.get("ddd")) + dig(p.get("fone"))) if p.get("fone") else ""
    if fone:
        tel.append({"system": "phone", "value": fone, "use": "home"})
    if s(p.get("email")):
        tel.append({"system": "email", "value": s(p.get("email"))})
    if tel:
        pat["telecom"] = tel

    # Contatos / filiação
    contatos = []
    if s(p.get("mae")):
        contatos.append(contato("MTH", s(p.get("mae"))))
    if s(p.get("pai")):
        contatos.append(contato("FTH", s(p.get("pai"))))
    if s(p.get("conjuge")):
        contatos.append(contato("SPS", s(p.get("conjuge"))))
    if s(p.get("responsavel")):
        foneresp = (dig(p.get("ddd_resp")) + dig(p.get("fone_resp"))) if p.get("fone_resp") else None
        contatos.append(contato("GUARD", s(p.get("responsavel")), foneresp))
    if contatos:
        pat["contact"] = contatos

    # Extras Salux (códigos internos preservados — viram recursos/lookups depois)
    extras = {k: p.get(k) for k in (
        "cd_cor", "cd_nacionalidade", "pais", "profissao", "ocupacao",
        "peso", "altura", "sangue", "rh", "etnia", "grau_parentesco", "entrada_pais")
        if p.get(k) not in (None, "", "0")}
    # Nomes resolvidos via lookups do Salux.
    for src, dst in (("estado_civil_ds", "estado_civil"), ("instrucao_ds", "escolaridade"),
                     ("religiao_ds", "religiao"), ("barreira_ds", "barreira_comunicacao")):
        if s(p.get(src)):
            extras[dst] = s(p.get(src))
    if "religiao" not in extras and s(p.get("religiao")):
        extras["religiao"] = s(p.get("religiao"))
    if extras:
        pat.setdefault("extension", []).append(
            {"url": EXTRAS_URL, "valueString": json.dumps(extras, ensure_ascii=False)})

    return pat


def purgar_hub():
    """Remove todos os Patient do hub (são só dados de teste)."""
    _, b = http("GET", "/fhir/Patient")
    ids = [e["resource"]["id"] for e in (b.get("entry") or [])]
    for i in ids:
        http("DELETE", f"/fhir/Patient/{i}")
    print(f"Hub limpo: {len(ids)} pacientes removidos.")


def main():
    purgar_hub()
    linhas = executar_json(
        """
        SELECT JSON_OBJECT(
            'cd' VALUE cd_paciente, 'nome' VALUE nm_paciente, 'social' VALUE nm_paciente_social,
            'flag_social' VALUE in_flag_social, 'nasc' VALUE TO_CHAR(dt_nascimento,'YYYY-MM-DD'),
            'sexo' VALUE sexo, 'cpf' VALUE cpf_paciente, 'cns' VALUE cns, 'rg' VALUE rg_paciente,
            'orgao' VALUE sc_orgao_emissor, 'pis' VALUE nr_pis_pasep, 'passaporte' VALUE sc_passaporte,
            'rne' VALUE sc_rne, 'certidao' VALUE nr_certidao_nascimento, 'sgh' VALUE cd_pront_sgh,
            'cem' VALUE cd_pront_cem, 'obito' VALUE TO_CHAR(dt_obito,'YYYY-MM-DD'), 'ativo' VALUE in_ativo,
            'estado_civil' VALUE estado_civil, 'logr' VALUE nm_logradouro, 'nr_logr' VALUE nr_logradouro,
            'compl' VALUE compl_logradouro, 'bairro' VALUE bairro, 'cep' VALUE cep,
            'ref' VALUE sc_ponto_referencia, 'ddd' VALUE nr_ddd_fone, 'fone' VALUE nr_fone,
            'ddd_resp' VALUE nr_ddd_fone_resp, 'fone_resp' VALUE nr_fone_resp, 'email' VALUE email,
            'mae' VALUE nm_mae, 'pai' VALUE nm_pai, 'conjuge' VALUE nm_conjuge,
            'responsavel' VALUE nm_responsavel, 'grau_parentesco' VALUE ds_grau_parentesco,
            'cd_cor' VALUE cd_cor, 'cd_nacionalidade' VALUE cd_nacionalidade, 'pais' VALUE sc_pais,
            'religiao' VALUE religiao, 'profissao' VALUE profissao, 'ocupacao' VALUE ocupacao,
            'instrucao' VALUE id_instrucao, 'peso' VALUE peso, 'altura' VALUE altura,
            'sangue' VALUE id_sangue, 'rh' VALUE id_fator_rh, 'etnia' VALUE tu_cd_etnia,
            'entrada_pais' VALUE TO_CHAR(dt_entrada_pais,'YYYY-MM-DD'),
            'cidade' VALUE (SELECT ci.ds_cidade FROM cidade ci WHERE ci.cd_uf=pac.cd_uf AND ci.cd_cidade=pac.cd_cidade AND ROWNUM=1),
            'uf_sigla' VALUE pac.cd_uf,
            'estado_civil_ds' VALUE (SELECT ec.ds_est_civil FROM estado_civil ec WHERE TO_CHAR(ec.cd_est_civil)=TRIM(pac.estado_civil) AND ROWNUM=1),
            'instrucao_ds' VALUE (SELECT gi.ds_grau_instrucao FROM grau_instrucao gi WHERE TO_CHAR(gi.cd_grau_instrucao)=TO_CHAR(pac.id_instrucao) AND ROWNUM=1),
            'religiao_ds' VALUE (SELECT r.ds_religiao FROM religiao r WHERE TO_CHAR(r.cd_religiao)=TO_CHAR(pac.cd_religiao) AND ROWNUM=1),
            'barreira_ds' VALUE (SELECT bc.ds_barreira_comunicacao FROM barreira_comunicacao bc WHERE TO_CHAR(bc.cd_barreira_comunicacao)=TO_CHAR(pac.cd_barreira_comunicacao) AND ROWNUM=1))
        FROM (
            SELECT * FROM paciente
            WHERE cpf_paciente IS NOT NULL AND nm_paciente IS NOT NULL
              AND dt_nascimento IS NOT NULL AND nm_logradouro IS NOT NULL AND nm_mae IS NOT NULL
            ORDER BY cd_paciente DESC
        ) pac WHERE ROWNUM <= 10
        """,
        modo="supervisor",
    )
    print(f"Salux retornou {len(linhas)} pacientes.")

    n = 0
    for p in linhas:
        cpf = dig(p.get("cpf"))
        if not cpf or not s(p.get("nome")):
            continue
        antigo = existente_id(cpf)
        if antigo:
            http("DELETE", f"/fhir/Patient/{antigo}")
        st, body = http("POST", "/fhir/Patient", construir(p))
        campos = [k for k in ("address", "telecom", "contact", "extension") if k in construir(p)]
        print(f"  [{st}] {s(p.get('nome'))} -> {body.get('id')}  ({', '.join(campos)})")
        n += 1
    print(f"Importados (completo): {n}")


if __name__ == "__main__":
    raise SystemExit(main())
