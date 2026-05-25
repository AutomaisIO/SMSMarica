"""Cliente C-FIND para Unified Worklist (UPS).

O backend SMSMarica grava items de worklist via UPS-RS (POST /workitems no AE
WORKLIST). Isso fica disponível em DICOM como **UPS Pull** SOP Class, NÃO
como Modality Worklist clássica (MWL). Por isso usamos UPS C-FIND aqui — o
dcm4chee não converte UPS→MWL automaticamente.
"""

from __future__ import annotations

import os
import sys
from dataclasses import dataclass

from pydicom.dataset import Dataset
from pynetdicom import AE


# SOP Class — Unified Procedure Step - Pull (PS3.4 CC.2)
UPS_PULL_SOP_CLASS_UID = "1.2.840.10008.5.1.4.34.6.1"

# Verbose: set EQSIM_DEBUG=1 para ver cada status devolvido pelo dcm4chee.
_DEBUG = os.environ.get("EQSIM_DEBUG") == "1"


@dataclass
class ItemWorklist:
    accession_number: str
    patient_name: str
    patient_id: str
    patient_birth_date: str
    patient_sex: str
    study_instance_uid: str
    modalidade: str
    descricao: str
    scheduled_datetime: str
    sps_id: str

    raw: Dataset


def consultar_worklist(
    host: str,
    port: int,
    calling_ae: str,
    called_ae: str,
    modalidade: str | None = None,
    accession: str | None = None,
) -> list[ItemWorklist]:
    """C-FIND UPS Pull. Filtros opcionais aplicados pós-resposta (a query
    enviada ao servidor é mínima — alguns SCPs rejeitam queries com sequences
    não-padrão)."""

    ae = AE(ae_title=calling_ae)
    ae.add_requested_context(UPS_PULL_SOP_CLASS_UID)

    query = _montar_query_minima()

    if _DEBUG:
        print(f"[DEBUG] Query enviada:\n{query}", file=sys.stderr)

    items: list[ItemWorklist] = []
    assoc = ae.associate(host, port, ae_title=called_ae)
    if not assoc.is_established:
        raise RuntimeError(
            f"Não foi possível associar com {called_ae}@{host}:{port}. "
            "Verifique se o AE Title está cadastrado no dcm4chee."
        )

    try:
        responses = assoc.send_c_find(query, UPS_PULL_SOP_CLASS_UID)
        for (status, identifier) in responses:
            if _DEBUG:
                s = f"0x{status.Status:04X}" if status else "None"
                print(f"[DEBUG] status={s} identifier={'yes' if identifier else 'no'}", file=sys.stderr)
            if status is None:
                continue
            # 0xFF00 = Pending (com dados). 0xFF01 = Pending with warning.
            if status.Status in (0xFF00, 0xFF01) and identifier is not None:
                items.append(_para_item(identifier))
    finally:
        assoc.release()

    # Filtros aplicados em memória (mais robusto que enviar tudo no query).
    if accession:
        items = [it for it in items if it.accession_number == accession.strip()]
    if modalidade:
        m = modalidade.strip().upper()
        items = [it for it in items if it.modalidade.upper() == m]

    return items


def _montar_query_minima() -> Dataset:
    """Query mínima: filtra só por ProcedureStepState=SCHEDULED. Nenhum outro
    campo — o dcm4chee devolve o workitem inteiro de qualquer jeito (Comp.
    Statement) e a gente extrai o que precisa do raw."""
    q = Dataset()
    q.ProcedureStepState = "SCHEDULED"
    return q


def _para_item(ds: Dataset) -> ItemWorklist:
    """Extrai os campos relevantes do UPS Pull response."""

    rrs_seq = getattr(ds, "ReferencedRequestSequence", []) or []
    rrs0 = rrs_seq[0] if rrs_seq else Dataset()
    accession = str(getattr(rrs0, "AccessionNumber", "") or "")
    descricao_rrs = str(getattr(rrs0, "RequestedProcedureDescription", "") or "")

    scheduled = _fmt_datetime_ups(
        str(getattr(ds, "ScheduledProcedureStepStartDateTime", "") or "")
    )

    return ItemWorklist(
        accession_number=accession,
        patient_name=str(getattr(ds, "PatientName", "") or ""),
        patient_id=str(getattr(ds, "PatientID", "") or ""),
        patient_birth_date=str(getattr(ds, "PatientBirthDate", "") or ""),
        patient_sex=str(getattr(ds, "PatientSex", "") or ""),
        study_instance_uid=str(getattr(ds, "StudyInstanceUID", "") or ""),
        modalidade=_extrair_modalidade(ds),
        descricao=descricao_rrs or str(getattr(ds, "ProcedureStepLabel", "") or ""),
        scheduled_datetime=scheduled,
        sps_id=str(getattr(ds, "ProcedureStepLabel", "") or ""),
        raw=ds,
    )


def _extrair_modalidade(ds: Dataset) -> str:
    for nome in ("ScheduledProcessingParametersSequence", "ScheduledWorkitemCodeSequence"):
        seq = getattr(ds, nome, None)
        if seq:
            item0 = seq[0]
            v = str(getattr(item0, "CodeValue", "") or "")
            if v:
                return v
    return ""


def _fmt_datetime_ups(dt: str) -> str:
    if not dt:
        return ""
    if len(dt) >= 12:
        return f"{dt[0:4]}-{dt[4:6]}-{dt[6:8]} {dt[8:10]}:{dt[10:12]}"
    if len(dt) == 8:
        return f"{dt[0:4]}-{dt[4:6]}-{dt[6:8]}"
    return dt
