"""Laboratório de integração com o SISREG III (sisregiii.saude.gov.br).

Objetivo: mapear/documentar login, sessão, menus e telas para depois portar
o motor para o SMSMarica.server (.NET, HttpClient). Isolado de propósito para
não depender de deploy durante a fase de aprendizado.
"""

from .client import SisregClient, SisregLoginError

__all__ = ["SisregClient", "SisregLoginError"]
