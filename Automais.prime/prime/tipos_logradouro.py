"""De-para de tipo de logradouro: CadWeb -> Prime, e limpeza do nome.

Por que existe: os dois sistemas usam tabelas DIFERENTES para a mesma coisa. O CadWeb manda
`key_endereco_codigoTipoLogradouro` na tabela do CADSUS/DNE (3 dígitos, chega a 081, 090…); o
Prime tem uma tabela curta e própria (001 a 010, servida por
`ComboTipoLogradouroService.asmx/GetData`). Mandar o código do CadWeb direto grava o tipo errado
— `008` é AVENIDA no CadWeb e VILA no Prime.

Como foi levantado (21/09/2026): pareando, no acervo da extensão, o código que o CadWeb entregou
na entrada do cadastro com o tipo que o OPERADOR escolheu à mão antes de salvar. 16 cadastros
gravados, 39 entradas observadas. Não é dedução: é o que a recepção decidiu, caso a caso.
"""

from __future__ import annotations

import re

# CadWeb -> texto EXATO do combo do Prime (o campo posta o texto, não o código).
DE_PARA: dict[str, str] = {
    "081": "001 - RUA",      # 12 confirmações
    "008": "009 - AVENIDA",  # 2 confirmações (MAYSA, PADRE CICERO ROMAO BATISTA)
    "090": "003 - RODOVIA",  # 1 confirmação (ERNANI DO AMARAL PEIXOTO)
}

# Vistos na entrada, mas ainda SEM um cadastro salvo que confirme a escolha humana.
# Preencher só quando houver o par observado — chutar aqui grava endereço errado em massa.
PENDENTES: dict[str, str] = {
    "031": "",  # ANTONIO CALLADO, ITAIPUACU — provável ESTRADA, não confirmado
    "017": "",  # DA GLORIA — não confirmado
}

PADRAO = "001 - RUA"  # 30 das 39 entradas observadas eram 081

# O CadWeb decora o nome do logradouro, e QUEM LIMPA É O OPERADOR, À MÃO. O Prime não
# transforma nada: ele copia o nome sujo para o campo e fica assim até alguém apagar.
# Rastreado postback a postback (21/09/2026):
#   15:06:40  CadWeb manda    'MAYSA 1/99998'
#   15:06:49  a tela carrega  'MAYSA 1/99998'   <- sujo
#   15:07:05  ainda           'MAYSA 1/99998'   <- sujo
#   15:07:18  vira            'MAYSA'           <- o operador digitou
# Idem 'GOVERNADOR LEONEL BRIZOLA QUADRA' -> 'GOVERNADOR LEONEL BRIZOLA' (17:28:52 -> 17:29:05).
#
# Duas consequências: (a) a carga em massa TEM que limpar, porque ninguém limpa por ela; e
# (b) quando o operador não limpa, o Prime grava o nome sujo — deve haver endereços assim
# na base, e isso não é problema da automação, é do preenchimento manual de hoje.
SUFIXO_NUMERICO = re.compile(r"\s+\d+/\d+\s*$")
SUFIXO_QUADRA = re.compile(r"\s+QUADRA\s*$", re.I)
PREFIXO_TIPO = re.compile(r"^(AV|AVENIDA|R|RUA|TV|TRAVESSA|ROD|RODOVIA|EST|ESTRADA|PC|PRACA|"
                          r"AL|ALAMEDA|VL|VILA|LARGO|BECO)\.?\s+", re.I)

PREFIXO_PARA_TIPO = {
    "AV": "009 - AVENIDA", "AVENIDA": "009 - AVENIDA",
    "R": "001 - RUA", "RUA": "001 - RUA",
    "TV": "002 - TRAVESSA", "TRAVESSA": "002 - TRAVESSA",
    "ROD": "003 - RODOVIA", "RODOVIA": "003 - RODOVIA",
    "EST": "010 - ESTRADA", "ESTRADA": "010 - ESTRADA",
    "PC": "007 - PRACA", "PRACA": "007 - PRACA",
    "AL": "005 - ALAMEDA", "ALAMEDA": "005 - ALAMEDA",
    "VL": "008 - VILA", "VILA": "008 - VILA",
}


def traduzir(codigo_cadweb: str | None, logradouro: str | None = None) -> tuple[str, str]:
    """(tipo do Prime, motivo). O nome do logradouro, quando informado, tem prioridade sobre o
    código — ele é evidência direta, o código é tabela de outro sistema."""
    nome = (logradouro or "").strip()
    m = PREFIXO_TIPO.match(nome)
    if m:
        chave = m.group(1).upper().rstrip(".")
        if chave in PREFIXO_PARA_TIPO:
            return PREFIXO_PARA_TIPO[chave], f"prefixo {chave!r} no nome do logradouro"

    cod = (codigo_cadweb or "").strip()
    if cod in DE_PARA:
        return DE_PARA[cod], f"de-para CadWeb {cod}"
    if cod in PENDENTES:
        return PADRAO, f"código CadWeb {cod} NÃO MAPEADO — usando o padrão, CONFERIR"
    return PADRAO, f"código CadWeb {cod!r} desconhecido — usando o padrão, CONFERIR"


def limpar_logradouro(nome: str | None) -> str:
    """Tira as decorações do CadWeb que o Prime não guarda."""
    n = (nome or "").strip()
    n = PREFIXO_TIPO.sub("", n)
    n = SUFIXO_NUMERICO.sub("", n)
    n = SUFIXO_QUADRA.sub("", n)
    return re.sub(r"\s{2,}", " ", n).strip()
