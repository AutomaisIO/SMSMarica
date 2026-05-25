"""Geração de DICOM Datasets fantasmas a partir de itens da worklist."""

from __future__ import annotations

import datetime
import uuid
from pathlib import Path

import numpy as np
from PIL import Image
from pydicom.dataset import Dataset, FileMetaDataset
from pydicom.uid import ExplicitVRLittleEndian, generate_uid

from .cliente_mwl import ItemWorklist


# SOP Class UIDs em string — independente de nomes simbólicos da lib (que
# variam entre versões do pydicom/pynetdicom). Fonte: DICOM PS3.6 Annex A.
SOP_CLASS_UID_POR_MODALIDADE: dict[str, str] = {
    "MG": "1.2.840.10008.5.1.4.1.1.1.2",   # Digital Mammography X-Ray Image Storage - For Presentation
    "DX": "1.2.840.10008.5.1.4.1.1.1.1",   # Digital X-Ray Image Storage - For Presentation
    "CR": "1.2.840.10008.5.1.4.1.1.1",     # Computed Radiography Image Storage
    "US": "1.2.840.10008.5.1.4.1.1.6.1",   # Ultrasound Image Storage
    "CT": "1.2.840.10008.5.1.4.1.1.2",     # CT Image Storage
    "MR": "1.2.840.10008.5.1.4.1.1.4",     # MR Image Storage
}

# Prefixo de UID do nosso simulador (DICOM PS3.5 B.2 — "2.25." + UUID inteiro).
UID_ROOT = "2.25."


def _gerar_uid() -> str:
    return UID_ROOT + str(uuid.uuid4().int)


def gerar_imagem_fantasma(largura: int = 1024, altura: int = 1024) -> np.ndarray:
    """Gradiente diagonal monocromo 16-bit, suficiente para o PACS aceitar."""
    x = np.linspace(0, 65535, largura, dtype=np.uint16)
    y = np.linspace(0, 65535, altura, dtype=np.uint16)
    xv, yv = np.meshgrid(x, y)
    return ((xv.astype(np.uint32) + yv.astype(np.uint32)) // 2).astype(np.uint16)


def carregar_imagem_arquivo(caminho: Path) -> np.ndarray:
    img = Image.open(caminho).convert("L")  # grayscale
    return np.array(img, dtype=np.uint8).astype(np.uint16) * 257  # 8-bit → 16-bit


def montar_dataset_a_partir_do_worklist(
    item: ItemWorklist,
    pixels: np.ndarray,
) -> Dataset:
    """Cria um Dataset DICOM completo herdando demograficos do worklist item.
    Pixel Data vem do array passado (uint16, monocromo)."""

    modalidade = (item.modalidade or "OT").upper()
    sop_class_uid = SOP_CLASS_UID_POR_MODALIDADE.get(
        modalidade, SOP_CLASS_UID_POR_MODALIDADE["MG"]
    )
    sop_instance_uid = _gerar_uid()
    series_instance_uid = _gerar_uid()
    study_instance_uid = item.study_instance_uid or _gerar_uid()

    agora = datetime.datetime.now(datetime.timezone.utc)
    data_str = agora.strftime("%Y%m%d")
    hora_str = agora.strftime("%H%M%S")

    # File meta (cabeçalho)
    file_meta = FileMetaDataset()
    file_meta.MediaStorageSOPClassUID = sop_class_uid
    file_meta.MediaStorageSOPInstanceUID = sop_instance_uid
    file_meta.TransferSyntaxUID = ExplicitVRLittleEndian
    file_meta.ImplementationClassUID = generate_uid()
    file_meta.ImplementationVersionName = "EQSIM-0.1"

    ds = Dataset()
    ds.file_meta = file_meta
    ds.is_little_endian = True
    ds.is_implicit_VR = False

    # SOP comum
    ds.SOPClassUID = sop_class_uid
    ds.SOPInstanceUID = sop_instance_uid

    # Paciente (herdado do worklist)
    ds.PatientName = item.patient_name or "ANONIMO"
    ds.PatientID = item.patient_id or "ANON-ID"
    ds.PatientBirthDate = item.patient_birth_date or ""
    ds.PatientSex = item.patient_sex or "O"

    # Study (herdado do worklist — fundamental para o backend amarrar)
    ds.StudyInstanceUID = study_instance_uid
    ds.SeriesInstanceUID = series_instance_uid
    ds.StudyDate = data_str
    ds.StudyTime = hora_str
    ds.AccessionNumber = item.accession_number or ""
    ds.StudyDescription = item.descricao or ""
    ds.StudyID = item.sps_id or ""

    # Series + Equipamento
    ds.Modality = modalidade
    ds.Manufacturer = "SMS MARICA"
    ds.ManufacturerModelName = "EQUIPAMENTO-SIM"
    ds.SeriesNumber = 1
    ds.InstanceNumber = 1

    # Image — pixel data
    pixels = pixels.astype(np.uint16)
    rows, cols = pixels.shape
    ds.Rows = int(rows)
    ds.Columns = int(cols)
    ds.BitsAllocated = 16
    ds.BitsStored = 16
    ds.HighBit = 15
    ds.PixelRepresentation = 0  # unsigned
    ds.SamplesPerPixel = 1
    ds.PhotometricInterpretation = "MONOCHROME2"
    ds.PixelSpacing = [0.05, 0.05]  # 50 µm — padrão mamografia
    ds.PixelData = pixels.tobytes()

    return ds
