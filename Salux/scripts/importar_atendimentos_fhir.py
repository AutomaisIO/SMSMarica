"""Importa o histórico clínico (BAA) dos pacientes já no hub FHIR.

Para cada Patient do hub:
  - resolve cd_paciente (identifier urn:salux:cd_paciente)
  - lê os últimos N BAA do Salux (INFOSAUDE.BAA), com CID e nome do médico
  - cria Encounter (class AMB/EMER) + Condition (CID-10)
  - cria MedicationRequest por item de prescrição (PRESC_BAA_OPC_PROD ⋈ MATMED)
  - cria DocumentReference (EDOC remontado em HTML) ligado ao Encounter
Tudo meta.source=salux. Idempotente: limpa os recursos do paciente antes de reimportar.

Importador roda ON-PREM (alcança o Oracle interno) e empurra pro hub público.
Somente SELECT no Oracle (read-only). NÃO commitar saída — pode ter PII.
"""
from __future__ import annotations

import base64
import html as _html
import json
import time
import urllib.request

from conexao import executar_json, executar_texto

HUB = "http://smsmarica.online:5081"
SRC = "https://smsmarica.saude.marica/source/salux"
S_BAA = "urn:salux:baa"
S_EDOC = "urn:salux:edoc"
S_MATMED = "urn:salux:matmed"  # catálogo de material/medicamento do Salux
SYS_CID = "http://hl7.org/fhir/sid/icd-10"
SYS_CLASS = "http://terminology.hl7.org/CodeSystem/v3-ActCode"
SYS_LOINC = "http://loinc.org"
SYS_UCUM = "http://unitsofmeasure.org"
SYS_OBS_CAT = "http://terminology.hl7.org/CodeSystem/observation-category"
S_RISCO = "urn:salux:classificacao-risco"  # cor da triagem (Manchester) do BAA
S_PAC = "urn:salux:cd_paciente"
# Sem teto: importa TODOS os BAAs e TODOS os eDocs (BAU) de cada paciente.


def s(v):
    v = (str(v).strip() if v is not None else "")
    return v or None


def dt(v):
    """Datas do Salux são horário de Brasília; FHIR exige offset em dateTime com hora."""
    v = s(v)
    return v + "-03:00" if v else None


def http(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    # Hub é HTTP público; reinício de deploy/blip de rede derruba um POST no meio.
    # Retry só em falha de conexão/timeout (URLError); HTTPError é resposta real.
    for tentativa in range(4):
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
        except urllib.error.URLError as e:
            if tentativa == 3:
                raise
            espera = 3 * (tentativa + 1)
            print(f"  .. {method} {path} falhou ({e.reason}); retry em {espera}s")
            time.sleep(espera)


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
    for tipo in ("Observation", "MedicationRequest", "DocumentReference", "Condition", "Encounter"):
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
            'risco_ds' VALUE (SELECT ds_classificacao_risco FROM infosaude.classificacao_risco cr
                              WHERE cr.cd_classificacao_risco = b.cd_classificacao_risco),
            'medico' VALUE (SELECT nm_medico FROM infosaude.medico WHERE cd_medico = b.cd_medico))
        FROM (
            SELECT * FROM infosaude.baa
            WHERE cd_paciente = {int(cd)}
            ORDER BY dt_atendimento DESC NULLS LAST
        ) b
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


def _q_prescricao(h, ano, nr, com_texto_livre):
    livre = (",\n            'horario' VALUE p.ds_horario,"
             "\n            'obs' VALUE p.observacao") if com_texto_livre else ""
    return f"""
        SELECT JSON_OBJECT(
            'cd_mat'  VALUE p.cd_material,
            'mat'     VALUE (SELECT ds_material FROM infosaude.matmed m WHERE m.cd_material = p.cd_material),
            'qt'      VALUE p.qt_material_prescrita,
            'urg'     VALUE p.in_urgencia,
            'medico'  VALUE (SELECT nm_medico FROM infosaude.medico me WHERE me.cd_medico = p.cd_medico){livre})
        FROM infosaude.presc_baa_opc_prod p
        WHERE p.cd_hospital = {int(h)} AND p.dt_ano_baa = {int(ano)} AND p.nr_baa = {int(nr)}
        ORDER BY p.nr_prescricao, p.seq_item
    """


def prescricao_do_baa(h, ano, nr):
    """Itens de prescrição (medicação) de um BAA: PRESC_BAA_OPC_PROD ⋈ MATMED.

    ds_horario/observacao são texto livre do charset legado e podem estourar
    JSON_OBJECT (ORA-40474); por isso há fallback só com os campos de catálogo.
    """
    try:
        return executar_json(_q_prescricao(h, ano, nr, True), modo="supervisor")
    except RuntimeError:
        return executar_json(_q_prescricao(h, ano, nr, False), modo="supervisor")


def texto_posologia(item):
    partes = []
    if s(item.get("qt")):
        partes.append(f"Qtd: {s(item.get('qt'))}")
    horario = s(item.get("horario"))
    if horario:
        partes.append(horario)
    obs = s(item.get("obs"))
    if obs:
        partes.append(obs)
    return " — ".join(partes) or None


def build_medication_request(item, patient_ref, enc_ref, authored_on):
    """MedicationRequest R4: status/intent/medication[x]/subject obrigatórios."""
    mat = s(item.get("mat")) or f"Material {item.get('cd_mat')}"
    mr = {
        "resourceType": "MedicationRequest",
        "meta": {"source": SRC},
        "status": "completed",  # prescrição histórica (atendimento encerrado)
        "intent": "order",
        "medicationCodeableConcept": {
            "coding": [{"system": S_MATMED, "code": str(item.get("cd_mat")), "display": mat}],
            "text": mat,
        },
        "subject": {"reference": patient_ref},
        "encounter": {"reference": enc_ref},
    }
    if authored_on:
        mr["authoredOn"] = authored_on
    if (str(item.get("urg") or "").upper() == "S"):
        mr["priority"] = "urgent"
    if s(item.get("medico")):
        mr["requester"] = {"display": s(item.get("medico"))}
    pos = texto_posologia(item)
    if pos:
        mr["dosageInstruction"] = [{"text": pos}]
    return mr


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
        ) mov
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
    """HTML semântico (classes .edoc-*) — rótulo/valor em campos legíveis.
    O front e a view de impressão estilizam essas classes (não usa Tailwind prose).
    """
    grupos = []  # [(rotulo, [resposta, ...])] — agrupa linhas multilinha do mesmo campo
    for it in itens:
        resp = s(it.get("resp"))
        if not resp:
            continue
        label = s(it.get("label")) or ""
        if grupos and grupos[-1][0] == label:
            grupos[-1][1].append(resp)
        else:
            grupos.append((label, [resp]))

    campos = []
    for label, valores in grupos:
        valor = "".join(f'<div class="edoc-linha">{_html.escape(v)}</div>' for v in valores)
        rotulo = f'<span class="edoc-rotulo">{_html.escape(label)}</span>' if label else ""
        campos.append(f'<div class="edoc-campo">{rotulo}<div class="edoc-valor">{valor}</div></div>')

    corpo = "\n".join(campos) or '<p class="edoc-vazio">(documento sem itens preenchidos)</p>'
    return (
        '<section class="edoc-doc">'
        f'<h3 class="edoc-titulo">{_html.escape(modelo or "Documento")}</h3>'
        f"{corpo}</section>"
    )


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


def _num(v):
    """Converte resposta numérica do Salux em float; None se vazio/zero/inválido."""
    v = s(v)
    if not v:
        return None
    try:
        f = float(v.replace(",", "."))
    except ValueError:
        return None
    return f if f != 0 else None  # 0 em sinal vital = não aferido


def sinais_vitais_do_baa(h, ano, nr, id_tipo="B"):
    """Aferição de TRIAGEM de um BAA (a mais antiga) — tabela colunada SINAIS_VITAIS.

    id_tipo: 'B' (BAA/ambulatório) ou 'F' (FIA/internação). Chave liga por
    (cd_hospital, dt_ano_fia_baa, nr_fia_baa). Retorna 0 ou 1 linha.
    """
    return executar_json(
        f"""
        SELECT JSON_OBJECT(
            'dthr'  VALUE TO_CHAR(dthr_visita,'YYYY-MM-DD"T"HH24:MI:SS'),
            'pa_alta'  VALUE vl_pa_alta,
            'pa_baixa' VALUE vl_pa_baixa,
            'fc'    VALUE vl_freq_cardio,
            'fr'    VALUE vl_respiracao,
            'temp'  VALUE vl_temp_aux,
            'spo2'  VALUE vl_saturacao_oxigenio)
        FROM (
            SELECT * FROM infosaude.sinais_vitais
            WHERE id_tipo = '{id_tipo}' AND cd_hospital = {int(h)}
              AND dt_ano_fia_baa = {int(ano)} AND nr_fia_baa = {int(nr)}
            ORDER BY dthr_visita ASC NULLS LAST
        ) WHERE ROWNUM <= 1
        """,
        modo="supervisor",
    )


def _obs_skeleton(patient_ref, enc_ref, effective, categoria):
    o = {
        "resourceType": "Observation",
        "meta": {"source": SRC},
        "status": "final",
        "category": [{"coding": [{"system": SYS_OBS_CAT, "code": categoria}]}],
        "subject": {"reference": patient_ref},
        "encounter": {"reference": enc_ref},
    }
    if effective:
        o["effectiveDateTime"] = effective
    return o


def build_obs_quantity(patient_ref, enc_ref, effective, loinc, display, valor, unidade, ucum):
    o = _obs_skeleton(patient_ref, enc_ref, effective, "vital-signs")
    o["code"] = {"coding": [{"system": SYS_LOINC, "code": loinc, "display": display}], "text": display}
    o["valueQuantity"] = {"value": valor, "unit": unidade, "system": SYS_UCUM, "code": ucum}
    return o


def build_obs_pressao(patient_ref, enc_ref, effective, sist, diast):
    """Pressão arterial como painel (85354-9) com componentes sistólica + diastólica."""
    o = _obs_skeleton(patient_ref, enc_ref, effective, "vital-signs")
    o["code"] = {"coding": [{"system": SYS_LOINC, "code": "85354-9", "display": "Pressão arterial"}],
                 "text": "Pressão arterial"}
    comps = []
    if sist is not None:
        comps.append({"code": {"coding": [{"system": SYS_LOINC, "code": "8480-6", "display": "Pressão sistólica"}]},
                      "valueQuantity": {"value": sist, "unit": "mmHg", "system": SYS_UCUM, "code": "mm[Hg]"}})
    if diast is not None:
        comps.append({"code": {"coding": [{"system": SYS_LOINC, "code": "8462-4", "display": "Pressão diastólica"}]},
                      "valueQuantity": {"value": diast, "unit": "mmHg", "system": SYS_UCUM, "code": "mm[Hg]"}})
    o["component"] = comps
    return o


def build_obs_risco(patient_ref, enc_ref, effective, cor):
    """Classificação de risco (cor da triagem) como Observation survey + valueCodeableConcept."""
    o = _obs_skeleton(patient_ref, enc_ref, effective, "survey")
    o["code"] = {"coding": [{"system": S_RISCO, "code": "classificacao-risco",
                             "display": "Classificação de risco"}], "text": "Classificação de risco"}
    o["valueCodeableConcept"] = {"coding": [{"system": S_RISCO, "display": cor}], "text": cor}
    return o


# Rótulos de vitais no eDoc (acolhimento/triagem) -> tipo interno. Casados por
# substring minúscula; cobre variações "(Acolhimento)", "(mmHg)", etc.
_MATCHERS_VITAL = [
    ("pressão arterial", "pa"),
    ("pulso", "fc"),
    ("frequência cardíaca", "fc"),
    ("freq. cardíaca", "fc"),
    ("frequência respiratória", "fr"),
    ("sat o2", "spo2"),
    ("saturação", "spo2"),
    ("temperatura", "temp"),
]


def _classificar_vital(label):
    l = (label or "").lower()
    for chave, tipo in _MATCHERS_VITAL:
        if chave in l:
            return tipo
    return None


def _parse_pa(txt):
    """'106 / 66' (ou '106x66') -> (106.0, 66.0)."""
    t = (s(txt) or "").replace("x", "/").replace("X", "/")
    partes = [p for p in t.split("/")]
    if len(partes) >= 2:
        return _num(partes[0]), _num(partes[1])
    return _num(t), None


def observations_de_edoc(itens, patient_ref, enc_ref, effective):
    """Sinais vitais do formulário de acolhimento (eDoc) -> Observation (vital-signs).

    Fonte real dos vitais da triagem (SINAIS_VITAIS é incompleto). Dedup por tipo,
    preferindo o item rotulado '(Acolhimento)'.
    """
    por_tipo = {}
    for it in sorted(itens, key=lambda x: 0 if "acolhimento" in (x.get("label") or "").lower() else 1):
        tipo = _classificar_vital(it.get("label"))
        resp = s(it.get("resp"))
        if tipo and resp and tipo not in por_tipo:
            por_tipo[tipo] = resp

    obs = []
    if "pa" in por_tipo:
        sist, diast = _parse_pa(por_tipo["pa"])
        if sist is not None or diast is not None:
            obs.append(build_obs_pressao(patient_ref, enc_ref, effective, sist, diast))
    if "fc" in por_tipo and _num(por_tipo["fc"]) is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, effective, "8867-4", "Frequência cardíaca", _num(por_tipo["fc"]), "bpm", "/min"))
    if "fr" in por_tipo and _num(por_tipo["fr"]) is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, effective, "9279-1", "Frequência respiratória", _num(por_tipo["fr"]), "irpm", "/min"))
    if "temp" in por_tipo and _num(por_tipo["temp"]) is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, effective, "8310-5", "Temperatura", _num(por_tipo["temp"]), "°C", "Cel"))
    if "spo2" in por_tipo and _num(por_tipo["spo2"]) is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, effective, "2708-6", "Saturação de O₂", _num(por_tipo["spo2"]), "%", "%"))
    return obs


def observations_de_vitais(v, patient_ref, enc_ref):
    """[Legado] Vitais a partir de SINAIS_VITAIS (tabela colunada, incompleta).

    Mantido como fallback; a fonte primária passou a ser o eDoc de acolhimento
    (ver observations_de_edoc), que é onde os vitais da triagem realmente vivem.
    """
    eff = dt(v.get("dthr"))
    obs = []
    pa_s, pa_d = _num(v.get("pa_alta")), _num(v.get("pa_baixa"))
    if pa_s is not None or pa_d is not None:
        obs.append(build_obs_pressao(patient_ref, enc_ref, eff, pa_s, pa_d))
    fc = _num(v.get("fc"))
    if fc is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, eff, "8867-4", "Frequência cardíaca", fc, "bpm", "/min"))
    fr = _num(v.get("fr"))
    if fr is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, eff, "9279-1", "Frequência respiratória", fr, "irpm", "/min"))
    temp = _num(v.get("temp"))
    if temp is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, eff, "8310-5", "Temperatura", temp, "°C", "Cel"))
    spo2 = _num(v.get("spo2"))
    if spo2 is not None:
        obs.append(build_obs_quantity(patient_ref, enc_ref, eff, "2708-6", "Saturação de O₂", spo2, "%", "%"))
    return obs


def main():
    pacientes = pacientes_do_hub()
    print(f"Pacientes no hub: {len(pacientes)}")
    inicio = time.time()
    tot_enc = tot_cond = tot_doc = tot_med = tot_obs = 0
    falhas = []
    for fhir_id, cd in pacientes:
        tp = time.time()
        try:
            n_enc, n_cond, n_doc, n_med, n_obs = _importar_paciente(fhir_id, cd, tp)
        except Exception as exc:  # noqa: BLE001 — benchmark não pode abortar por 1 paciente
            falhas.append((cd, str(exc).splitlines()[0][:160]))
            print(f"  cd_paciente={cd}: FALHOU ({time.time()-tp:.1f}s) — {str(exc).splitlines()[0][:160]}")
            continue
        tot_enc += n_enc
        tot_cond += n_cond
        tot_doc += n_doc
        tot_med += n_med
        tot_obs += n_obs
    dur = time.time() - inicio
    n = len(pacientes)
    print(f"\nTotal: {tot_enc} Encounters, {tot_cond} Conditions, "
          f"{tot_doc} DocumentReferences, {tot_med} MedicationRequests, {tot_obs} Observations")
    print(f"Tempo: {dur:.1f}s para {n} pacientes "
          f"({dur/max(n,1):.1f}s/paciente; {dur/max(tot_enc,1):.2f}s/BAA importado)")
    if falhas:
        print(f"\nFalhas ({len(falhas)}):")
        for cd, msg in falhas:
            print(f"  cd_paciente={cd}: {msg}")


def _importar_paciente(fhir_id, cd, tp):
    """Importa um paciente e retorna (n_enc, n_cond, n_doc, n_med, n_obs)."""
    purgar_paciente(fhir_id)
    patient_ref = f"Patient/{fhir_id}"
    enc_por_baa = {}
    n_enc = n_cond = n_doc = n_med = n_obs = 0
    for b in baa_do_paciente(cd):
        _, criado = http("POST", "/fhir/Encounter", build_encounter(b, patient_ref))
        enc_ref = f"Encounter/{criado.get('id')}"
        enc_por_baa[f"{b['h']}-{b['ano']}-{b['nr']}"] = enc_ref
        n_enc += 1
        cond = build_condition(b, patient_ref, enc_ref)
        if cond:
            http("POST", "/fhir/Condition", cond)
            n_cond += 1
        # Prescrição/medicação do atendimento -> MedicationRequest (1 por item).
        authored = dt(b.get("dt_atend")) or dt(b.get("dt_cheg"))
        for item in prescricao_do_baa(b["h"], b["ano"], b["nr"]):
            http("POST", "/fhir/MedicationRequest",
                 build_medication_request(item, patient_ref, enc_ref, authored))
            n_med += 1
        # Classificação de risco (cor da triagem) -> Observation (survey).
        # Os SINAIS VITAIS vêm do eDoc de acolhimento (ver loop de documentos
        # abaixo), não de SINAIS_VITAIS — que é incompleto.
        cor = s(b.get("risco_ds"))
        if cor:
            http("POST", "/fhir/Observation",
                 build_obs_risco(patient_ref, enc_ref, dt(b.get("dt_cheg")) or dt(b.get("dt_atend")), cor))
            n_obs += 1
    for doc in edoc_do_paciente(cd):
        enc_ref = enc_por_baa.get(s(doc.get("baa")))
        if not enc_ref:
            continue  # documento de um BAA fora dos atendimentos importados
        itens = itens_do_documento(doc["h"], doc["ano"], doc["idm"])
        html_str = montar_html(doc.get("modelo"), itens)
        http("POST", "/fhir/DocumentReference", build_docref(doc, html_str, patient_ref, enc_ref))
        n_doc += 1
        # Sinais vitais do acolhimento (eDoc) -> Observation (vital-signs).
        for o in observations_de_edoc(itens, patient_ref, enc_ref, dt(doc.get("dt"))):
            http("POST", "/fhir/Observation", o)
            n_obs += 1
    print(f"  cd_paciente={cd}: {n_enc} atend., {n_cond} diag., {n_doc} docs, "
          f"{n_med} medic., {n_obs} obs. ({time.time()-tp:.1f}s)")
    return (n_enc, n_cond, n_doc, n_med, n_obs)


if __name__ == "__main__":
    raise SystemExit(main())
