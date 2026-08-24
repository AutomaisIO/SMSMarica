"""Cliente C-STORE (envia DICOM gerado para o dcm4chee AE principal)."""

from __future__ import annotations

from pydicom.dataset import Dataset
from pynetdicom import AE


def enviar(
    host: str,
    port: int,
    calling_ae: str,
    called_ae: str,
    dataset: Dataset,
) -> tuple[bool, str]:
    """Envia um único dataset via C-STORE. Retorna (sucesso, mensagem).

    O presentation context é negociado pela SOPClassUID do próprio Dataset
    (pynetdicom aceita o UID string direto em add_requested_context, evitando
    importar nomes simbólicos que mudam entre versões da lib)."""

    sop_class_uid = dataset.SOPClassUID

    ae = AE(ae_title=calling_ae)
    ae.add_requested_context(sop_class_uid)

    assoc = ae.associate(host, port, ae_title=called_ae)
    if not assoc.is_established:
        return False, (
            f"Não foi possível associar com {called_ae}@{host}:{port}. "
            "Verifique se o servidor está respondendo e se o firewall libera a porta."
        )

    try:
        status = assoc.send_c_store(dataset)
        if status and hasattr(status, "Status"):
            if status.Status == 0x0000:
                return True, f"C-STORE OK. SOP Instance: {dataset.SOPInstanceUID}"
            return False, f"C-STORE retornou status 0x{status.Status:04X}."
        return False, "Sem resposta do servidor."
    finally:
        assoc.release()
