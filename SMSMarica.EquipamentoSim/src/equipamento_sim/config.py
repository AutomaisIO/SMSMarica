"""Configurações default — sobrescritíveis por variáveis EQSIM_* ou flags da CLI."""

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    """Endereço de rede e AE titles do dcm4chee.

    O dcm4chee tem múltiplos AEs com papéis distintos:
    - PACS-CDT: Storage SCP (recebe C-STORE) — AE de imagens do CDT
    - WORK-CDT: MWL/UPS SCP (responde C-FIND MWL e expõe UPS-RS) — worklist do CDT
    Não é correto usar PACS-CDT como Called AE para C-FIND MWL — falha com
    `cannot associate`. (DCM4CHEE/WORKLIST seguem ativos como alias legado.)
    """

    host: str
    port: int

    calling_ae: str
    """Calling AE Title (esse simulador). Default é MAMO-SIM. Se o dcm4chee
    rejeitar a Association com 'Calling AE not recognized', cadastre o AE via
    UI Arc Light → Configuration → Devices → Add AE."""

    called_ae_store: str
    """AE de destino para C-STORE (PACS-CDT — onde os studies vão parar)."""

    called_ae_mwl: str
    """AE de destino para C-FIND MWL (WORK-CDT). O AE PACS-CDT NÃO responde MWL."""


def carregar() -> Config:
    return Config(
        host=os.environ.get("EQSIM_HOST", "pacs.marica.automais.cloud"),
        port=int(os.environ.get("EQSIM_PORT", "11112")),
        calling_ae=os.environ.get("EQSIM_CALLING_AE", "MAMO-SIM"),
        called_ae_store=os.environ.get("EQSIM_CALLED_AE_STORE", "PACS-CDT"),
        called_ae_mwl=os.environ.get("EQSIM_CALLED_AE_MWL", "WORK-CDT"),
    )
