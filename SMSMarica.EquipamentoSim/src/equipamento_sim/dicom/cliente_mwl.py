"""Cliente C-FIND para Modality Worklist (puxa worklist do dcm4chee AE WORKLIST)."""

from __future__ import annotations

from dataclasses import dataclass

from pydicom.dataset import Dataset
from pynetdicom import AE, build_role
from pynetdicom.sop_class import ModalityWorklistInformationFind


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
    """Scheduled Procedure Step ID — usado para identificar a etapa dentro do procedimento."""

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
    """Faz C-FIND no AE WORKLIST do dcm4chee. Filtros opcionais por modalidade
    ou AccessionNumber. Retorna a lista de items recebidos."""

    ae = AE(ae_title=calling_ae)
    ae.add_requested_context(ModalityWorklistInformationFind)

    query = _montar_query(modalidade=modalidade, accession=accession)

    items: list[ItemWorklist] = []
    assoc = ae.associate(host, port, ae_title=called_ae)
    if not assoc.is_established:
        raise RuntimeError(
            f"Não foi possível associar com {called_ae}@{host}:{port}. "
            "Verifique se o AE Title está cadastrado no dcm4chee."
        )

    try:
        responses = assoc.send_c_find(query, ModalityWorklistInformationFind)
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
    q = Dataset()
    # Campos que queremos receber preenchidos (universal matching = string vazia).
    q.AccessionNumber = accession if accession else ""
    q.PatientName = ""
    q.PatientID = ""
    q.PatientBirthDate = ""
    q.PatientSex = ""
    q.StudyInstanceUID = ""

    sps = Dataset()
    sps.Modality = modalidade if modalidade else ""
    sps.ScheduledStationAETitle = ""
    sps.ScheduledProcedureStepStartDate = ""
    sps.ScheduledProcedureStepStartTime = ""
    sps.ScheduledProcedureStepDescription = ""
    sps.ScheduledProcedureStepID = ""
    q.ScheduledProcedureStepSequence = [sps]

    q.RequestedProcedureDescription = ""
    q.RequestedProcedureID = ""

    return q


def _para_item(ds: Dataset) -> ItemWorklist:
    sps_seq = getattr(ds, "ScheduledProcedureStepSequence", []) or []
    sps0 = sps_seq[0] if sps_seq else Dataset()
    data = str(getattr(sps0, "ScheduledProcedureStepStartDate", "") or "")
    hora = str(getattr(sps0, "ScheduledProcedureStepStartTime", "") or "")
    scheduled = _fmt_datetime(data, hora)

    return ItemWorklist(
        accession_number=str(getattr(ds, "AccessionNumber", "") or ""),
        patient_name=str(getattr(ds, "PatientName", "") or ""),
        patient_id=str(getattr(ds, "PatientID", "") or ""),
        patient_birth_date=str(getattr(ds, "PatientBirthDate", "") or ""),
        patient_sex=str(getattr(ds, "PatientSex", "") or ""),
        study_instance_uid=str(getattr(ds, "StudyInstanceUID", "") or ""),
        modalidade=str(getattr(sps0, "Modality", "") or ""),
        descricao=str(
            getattr(sps0, "ScheduledProcedureStepDescription", "")
            or getattr(ds, "RequestedProcedureDescription", "")
            or ""
        ),
        scheduled_datetime=scheduled,
        sps_id=str(getattr(sps0, "ScheduledProcedureStepID", "") or ""),
        raw=ds,
    )


def _fmt_datetime(data: str, hora: str) -> str:
    if not data and not hora:
        return ""
    d = f"{data[0:4]}-{data[4:6]}-{data[6:8]}" if len(data) == 8 else data
    h = f"{hora[0:2]}:{hora[2:4]}" if len(hora) >= 4 else hora
    return f"{d} {h}".strip()
