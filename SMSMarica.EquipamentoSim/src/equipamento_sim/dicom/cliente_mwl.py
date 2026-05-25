"""Cliente C-FIND para Unified Worklist (UPS).

O backend SMSMarica grava items de worklist via UPS-RS (POST /workitems no AE
WORKLIST). Isso fica disponível em DICOM como **UPS Pull** SOP Class, NÃO
como Modality Worklist clássica (MWL). Por isso usamos UPS C-FIND aqui — o
dcm4chee não converte UPS→MWL automaticamente.
"""

from __future__ import annotations

from dataclasses import dataclass

from pydicom.dataset import Dataset
from pynetdicom import AE


# SOP Class — Unified Procedure Step - Pull (PS3.4 CC.2)
UPS_PULL_SOP_CLASS_UID = "1.2.840.10008.5.1.4.34.6.1"


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
    """Procedure Step Label (UPS) — texto humano da etapa."""

    raw: Dataset
    """Dataset original (útil para herdar todos os campos no DICOM gerado)."""


def consultar_worklist(
    host: str,
    port: int,
    calling_ae: str,
    called_ae: str,
    modalidade: str | None = None,
    accession: str | None = None,
) -> list[ItemWorklist]:
    """C-FIND UPS no AE WORKLIST do dcm4chee. Filtros opcionais por modalidade
    ou AccessionNumber."""

    ae = AE(ae_title=calling_ae)
    ae.add_requested_context(UPS_PULL_SOP_CLASS_UID)

    query = _montar_query(modalidade=modalidade, accession=accession)

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
            if status is None:
                continue
            # 0xFF00 = Pending (com dados). 0x0000 = Success (sem dados).
            if status.Status in (0xFF00, 0xFF01) and identifier is not None:
                items.append(_para_item(identifier))
    finally:
        assoc.release()

    return items


def _montar_query(*, modalidade: str | None, accession: str | None) -> Dataset:
    """Query UPS Pull (PS3.4 CC.2.2). Campos top-level + Referenced Request
    Sequence (onde fica o AccessionNumber e o RequestedProcedureDescription)."""
    q = Dataset()

    # Filtro por estado — SCHEDULED é o que interessa pro equipamento puxar.
    q.ProcedureStepState = "SCHEDULED"

    # Campos do paciente (universal matching = string vazia).
    q.PatientName = ""
    q.PatientID = ""
    q.PatientBirthDate = ""
    q.PatientSex = ""

    # Study UID — vem direto no top do UPS.
    q.StudyInstanceUID = ""

    # ScheduledProcedureStepStartDateTime
    q.ScheduledProcedureStepStartDateTime = ""
    q.ProcedureStepLabel = ""
    q.InputReadinessState = ""

    # Referenced Request Sequence — contém AccessionNumber e RequestedProcedureDescription
    rrs = Dataset()
    rrs.AccessionNumber = accession if accession else ""
    rrs.RequestedProcedureDescription = ""
    rrs.RequestedProcedureID = ""
    q.ReferencedRequestSequence = [rrs]

    # Scheduled Station Name Code Sequence (modalidade + nome da estação)
    if modalidade:
        ssncs = Dataset()
        ssncs.CodeValue = ""
        ssncs.CodingSchemeDesignator = ""
        ssncs.CodeMeaning = ""
        q.ScheduledStationNameCodeSequence = [ssncs]

        # Filtro de modalidade vai pelo ScheduledProcessingApplicationsCodeSequence
        spacs = Dataset()
        spacs.CodeValue = modalidade
        spacs.CodingSchemeDesignator = "DCM"
        spacs.CodeMeaning = ""
        q.ScheduledProcessingParametersSequence = [spacs]

    return q


def _para_item(ds: Dataset) -> ItemWorklist:
    """Extrai os campos relevantes do UPS Pull response."""

    # Referenced Request Sequence — AccessionNumber + descrição
    rrs_seq = getattr(ds, "ReferencedRequestSequence", []) or []
    rrs0 = rrs_seq[0] if rrs_seq else Dataset()
    accession = str(getattr(rrs0, "AccessionNumber", "") or "")
    descricao_rrs = str(getattr(rrs0, "RequestedProcedureDescription", "") or "")

    # Scheduled DateTime — campo único no UPS (em vez de Date+Time separados).
    scheduled = _fmt_datetime_ups(str(getattr(ds, "ScheduledProcedureStepStartDateTime", "") or ""))

    # Modalidade — fica em ScheduledProcessingParametersSequence ou inferimos.
    modalidade = _extrair_modalidade(ds)

    return ItemWorklist(
        accession_number=accession,
        patient_name=str(getattr(ds, "PatientName", "") or ""),
        patient_id=str(getattr(ds, "PatientID", "") or ""),
        patient_birth_date=str(getattr(ds, "PatientBirthDate", "") or ""),
        patient_sex=str(getattr(ds, "PatientSex", "") or ""),
        study_instance_uid=str(getattr(ds, "StudyInstanceUID", "") or ""),
        modalidade=modalidade,
        descricao=descricao_rrs or str(getattr(ds, "ProcedureStepLabel", "") or ""),
        scheduled_datetime=scheduled,
        sps_id=str(getattr(ds, "ProcedureStepLabel", "") or ""),
        raw=ds,
    )


def _extrair_modalidade(ds: Dataset) -> str:
    """Tenta achar a modalidade nos sequences do UPS. Fallback: vazio."""
    for nome in ("ScheduledProcessingParametersSequence", "ScheduledWorkitemCodeSequence"):
        seq = getattr(ds, nome, None)
        if seq:
            item0 = seq[0]
            v = str(getattr(item0, "CodeValue", "") or "")
            if v:
                return v
    return ""


def _fmt_datetime_ups(dt: str) -> str:
    """UPS usa DT (YYYYMMDDHHMMSS) num único campo."""
    if not dt:
        return ""
    if len(dt) >= 12:
        return f"{dt[0:4]}-{dt[4:6]}-{dt[6:8]} {dt[8:10]}:{dt[10:12]}"
    if len(dt) == 8:
        return f"{dt[0:4]}-{dt[4:6]}-{dt[6:8]}"
    return dt
