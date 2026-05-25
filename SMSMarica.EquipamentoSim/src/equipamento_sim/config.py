"""Configurações default — sobrescritíveis por variáveis EQSIM_* ou flags da CLI."""

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Config:
    """Endereço de rede e AE titles do dcm4chee."""

    host: str
    port: int
    calling_ae: str
    """Calling AE Title (esse simulador). Precisa estar cadastrado no dcm4chee."""

    called_ae_store: str
    """AE de destino para C-STORE (default DCM4CHEE — onde os studies vão parar)."""

    called_ae_mwl: str
    """AE de destino para C-FIND MWL (default WORKLIST)."""


def carregar() -> Config:
    return Config(
        host=os.environ.get("EQSIM_HOST", "pacs.marica.automais.cloud"),
        port=int(os.environ.get("EQSIM_PORT", "11112")),
        calling_ae=os.environ.get("EQSIM_CALLING_AE", "MAMO-SIM"),
        called_ae_store=os.environ.get("EQSIM_CALLED_AE_STORE", "DCM4CHEE"),
        called_ae_mwl=os.environ.get("EQSIM_CALLED_AE_MWL", "WORKLIST"),
    )
