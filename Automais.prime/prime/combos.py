"""Resolvedor de CÓDIGO INTERNO dos combos do Prime, via os mesmos .asmx que a tela usa.

Por que existe (APRENDIZADOS §15): o RadComboBox valida pelo `value` do ClientState, que é um
código INTERNO do Prime (não o texto, não o IBGE) — gerado quando o combo carrega as opções.
`rcboMunicipioNascimento` = "RIO DE JANEIRO" tem value "007043". O servidor acumula o objeto do
paciente por esse código e o `cvMunicipioNascimento` cobra ele no Salvar.

Cada combo tem seu serviço e seu context (medido no cadastro manual capturado pela extensão):
    ComboUFService.asmx/GetData          {Text}                          -> value = sigla
    ComboMunicipioService.asmx/GetData   {Text, UF, CODIGOIBGE:"COM"}     -> value = cód município
    ComboRaca.asmx/GetData               {Text}                          -> value = cód raça
    ComboBairroService.asmx/GetData      {Text, Municipio:<cód>}          -> value = cód bairro
    ComboLogradouroService.asmx/GetData  {Text, Municipio:<cód>, Bairro}  -> value = cód logradouro

Cascata: bairro precisa do código do município; logradouro precisa de município + bairro.
Só LEITURA — .asmx de combo não escreve nada.
"""

from __future__ import annotations

import unicodedata

from .client import APP, PrimeSession

_REF = "https://marica.ecosistemas.com.br" + APP + "/Paciente/CadastroPaciente.aspx"


def _norm(s: str) -> str:
    """Compara texto ignorando acento e caixa (o combo devolve 'ANTÔNIO', a ficha tem 'ANTONIO')."""
    s = unicodedata.normalize("NFKD", s or "").encode("ascii", "ignore").decode()
    return " ".join(s.upper().split())


def _chamar(sess: PrimeSession, servico: str, context: dict) -> list[dict]:
    r = sess.c.post(
        f"{APP}/Services/{servico}",
        json={"context": {"NumberOfItems": 0, **context}},
        headers={"Content-Type": "application/json; charset=UTF-8",
                 "X-Requested-With": "XMLHttpRequest", "Referer": _REF},
    )
    if r.status_code != 200:
        raise RuntimeError(f"{servico} devolveu {r.status_code}: {r.text[:120]}")
    d = r.json().get("d", {})
    return d.get("Items", []) if isinstance(d, dict) else (d or [])


def _casar(itens: list[dict], texto: str) -> str | None:
    """Value do item cujo Text bate com `texto` (exato, sem acento/caixa)."""
    alvo = _norm(texto)
    for it in itens:
        if _norm(it.get("Text", "")) == alvo:
            return it.get("Value")
    return None


def uf(sess: PrimeSession, sigla: str) -> str:
    """A UF já é a própria sigla no value; confirmamos que existe na lista."""
    itens = _chamar(sess, "ComboUFService.asmx/GetData", {"Text": ""})
    v = _casar(itens, sigla)
    return v or sigla


def municipio(sess: PrimeSession, sigla_uf: str, nome: str) -> str | None:
    itens = _chamar(sess, "ComboMunicipioService.asmx/GetData",
                    {"Text": nome[:4].lower(), "UF": sigla_uf, "CODIGOIBGE": "COM"})
    return _casar(itens, nome)


def raca(sess: PrimeSession, nome: str) -> str | None:
    itens = _chamar(sess, "ComboRaca.asmx/GetData", {"Text": None})
    return _casar(itens, nome)


def bairro(sess: PrimeSession, cod_municipio: str, nome: str) -> str | None:
    itens = _chamar(sess, "ComboBairroService.asmx/GetData",
                    {"Text": nome[:4].lower(), "Municipio": cod_municipio, "UnidadeId": ""})
    return _casar(itens, nome)


def logradouro(sess: PrimeSession, cod_municipio: str, bairro_nome: str, nome: str) -> str | None:
    itens = _chamar(sess, "ComboLogradouroService.asmx/GetData",
                    {"Text": nome[:4].lower(), "Municipio": cod_municipio, "Bairro": bairro_nome})
    return _casar(itens, nome)
