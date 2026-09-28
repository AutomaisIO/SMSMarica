"""De-para unidade do Prime -> unidade nossa (CNES). ADR-0039: o CNES e a ponte entre PEPs.

O relatorio do Prime entrega GUID + nome da unidade, e nada mais. Quem amarra ao nosso cadastro
e o **CNES**, entao esta tabela e obrigatoria antes de qualquer ingestao: sem ela o atendimento
entra no hub sem saber a que unidade pertence, e a unidade e o eixo durave do ADR-0039.

Levantado em 23/09/2026 cruzando as 10 unidades com historico no Prime contra as 51 do
`smsmarica.unidade`. 6 bateram por nome; 2 (os CEO) por deducao CONFIRMADA pelo Bernardo; e 2 nao existiam e foram
CRIADAS em 23/09/2026. Um `cnes: None` aqui nunca e descuido - significa "nao ingerir ainda".
"""

from __future__ import annotations

# guid do Prime -> (nome no Prime, CNES nosso, confianca)
#   "nome"      = bateu pelo nome, sem duvida
#   "confirmado"= o nome nao bate, o CNES foi deduzido e uma PESSOA confirmou
#   None        = nao existe no `smsmarica.unidade`; precisa ser cadastrada antes
DE_PARA: dict[str, tuple[str, str | None, str]] = {
    "44d338e6-d84f-4cab-bd9e-5b30a160bc45": ("AMBULATORIO PERICLES SIQUEIRA FERREIRA", "2266741", "nome"),
    "2b89b351-f048-492e-b28b-75c498fd04bc": ("CDT DR ALBERTO LUIS MACHADO BORGES", "3132358", "nome"),
    "c381396d-6ef0-4307-b1a4-0738b7baa58d": ("CENTRO DE REABILITACAO AMBULATORIAL E DOMICILIAR", "4256387", "nome"),
    "ed60e0c4-046a-488b-8836-af717be01eeb": ("CENTRO MATERNO INFANTIL", "2930242", "nome"),
    "643c58d7-95d8-4486-b5f9-9220a24680d4": ("MELHOR EM CASA MARICA", "5613175", "nome"),
    "32fc8be5-0054-4a66-a4a6-deb2f5d4c7de": ("SAE SERVICO DE ATENDIMENTO ESPECIALIZADO", "6633641", "nome"),
    # Nomes diferentes dos nossos, CONFIRMADOS pelo Bernardo em 23/09/2026:
    #   "CEO ITAIPUACU"  -> "CENTRO DE ESPECIALIDADES ODONTOLOGICAS DE ITAIPUACU"
    #   "CEO BOQUEIRAO"  -> o nosso "CEO" sem bairro (era deducao por eliminacao; confirmada)
    "8ae301da-c9e2-4ea5-9048-c19143b9453c": ("CEO ITAIPUACU", "4936183", "confirmado"),
    "f449877a-aec5-4fed-8e79-2f05d7954764": ("CEO BOQUEIRAO", "5874211", "confirmado"),
    # Nao existiam no nosso cadastro; CRIADAS em 23/09/2026 por `criar_unidades_faltantes.py`
    # (o cadastro foi de 51 para 53 unidades). CNES e endereco vieram do cadastro nacional do
    # DataSUS, nao de palpite. O `0209724` PRECISA do zero a esquerda: a API devolve `209724`
    # porque serializa como inteiro, e todas as nossas sao de 7 digitos.
    "a5388cc2-8760-470c-8dbd-c569ea097d22": ("CEREST", "6893430", "criada"),
    "2016fabd-4c5b-44c8-b888-14ea57a44247": ("ODONTOMOVEL MARICA", "0209724", "criada"),
}


def resolver(guid: str) -> tuple[str | None, str]:
    """(cnes, confianca). `cnes=None` significa: NAO ingerir ainda."""
    achado = DE_PARA.get(guid)
    return (achado[1], achado[2]) if achado else (None, "desconhecida")


def pendencias() -> list[tuple[str, str, str]]:
    """(guid, nome, motivo) do que um humano precisa resolver antes da carga."""
    return [(g, nome, conf) for g, (nome, cnes, conf) in DE_PARA.items()
            if cnes is None or conf == "inferido"]
