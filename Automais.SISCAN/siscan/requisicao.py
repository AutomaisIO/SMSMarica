"""Fluxo de CRIAR uma requisição nova no SISCAN (mamografia).

    EXAME -> GERENCIAR EXAME -> [Novo Exame] -> /visao/exame/novoExame.jsf
      etapa 1: Cartão SUS (o CADSUS preenche o resto) + tipo de exame + unidade
      [Avançar] -> /visao/exame/requisicao/mamografia/salvarRequisicaoMamografia.jsf
      etapa 2: a requisição (anamnese) + Responsável -> [Salvar]

Medido contra o SISCAN real em 22/09/2026 (V2.19.1-RC08). NADA aqui clica em
Salvar — a trava de `SiscanClient` barra "salvar"/"encerrar"/"confirmar". A
criação de verdade exige OK explícito por ação, e mais: a decisão de negócio de
quem pode ser o Responsável (ver ORDEM OBRIGATÓRIA abaixo).

ORDEM OBRIGATÓRIA, medida — não é preferência, é o que a tela impõe:

    1. Cartão SUS (A4J do onblur)     -> traz paciente do CADSUS + histórico
    2. Tipo de exame (A4J)            -> só então Unidade Requisitante tem opções
    3. Avançar                        -> etapa 2
    4. Tipo de mamografia (A4J)       -> só então Responsável tem opções,
                                         e a LISTA MUDA conforme diagnóstica × rastreamento
    5. Responsável (A4J)              -> deriva o Conselho (vira disabled)
"""

from __future__ import annotations

from datetime import date

from bs4 import BeautifulSoup

from .client import SiscanClient, aplicar_a4j
from .inspecao import _a4j_de, gatilhos

# Só o que NAVEGA. "salvar"/"encerrar"/"confirmar"/"liberar" seguem travados.
NAVEGACAO = ("novo",)

TIPO_EXAME = {"01": "Mamografia", "02": "Cito de Colo", "03": "Cito de Mama",
              "04": "Histo de Colo", "05": "Histo de Mama"}
TIPO_MAMOGRAFIA = {"01": "Diagnóstica", "02": "Rastreamento"}
RASTREAMENTO = {"01": "População alvo",
                "02": "População de risco elevado (história familiar)",
                "03": "Paciente já tratado de câncer de mama"}


def tipo_mamografia_por_idade(nascimento: date, referencia: date | None = None) -> str:
    """Régua do CDT Maricá (dada pelo Bernardo em 22/09/2026):

        idade >= 36  ->  02 Rastreamento
        idade  < 36  ->  01 Diagnóstica

    Não é o critério do INCA (50–69 para rastreamento populacional): é a régua
    operacional daqui, e é ela que vale. Não é adivinhação a partir do CID — o CID
    da ficha do SISREG pode sugerir, mas quem decide é a idade.

    Consequência prática: **a idade também decide quem pode assinar**, porque a
    lista do combo Responsável muda entre diagnóstica e rastreamento.
    """
    hoje = referencia or date.today()
    idade = hoje.year - nascimento.year - (
        (hoje.month, hoje.day) < (nascimento.month, nascimento.day))
    return "02" if idade >= 36 else "01"


def _a4j_do_elemento(el) -> dict[str, str]:
    for ev in ("onclick", "onchange", "onblur"):
        p = _a4j_de(el.get(ev) or "")
        if p:
            return p
    raise RuntimeError(f"elemento {el.get('name')!r} não tem parâmetros A4J")


# ------------------------------------------------------------------ etapa 1
def novo_exame(c: SiscanClient, doc: BeautifulSoup, nome: str = "novo-exame") -> BeautifulSoup:
    """Clica [Novo Exame] na tela GERENCIAR EXAME. É A4J e navega de tela."""
    alvo = next((g for g in gatilhos(doc)
                 if g["tipo"] == "a4j" and g["rotulo"].upper() == "NOVO EXAME"), None)
    if not alvo:
        raise RuntimeError("botão 'Novo Exame' não encontrado")
    params = {k: v for k, v in alvo["parametros"].items() if k != "similarityGroupingId"}
    return c.sopa(c.post_a4j(doc, alvo["form"], {}, params, nome, permitir=NAVEGACAO))


def digitar_cartao_sus(c: SiscanClient, doc: BeautifulSoup, cns: str,
                       nome: str = "cns") -> BeautifulSoup:
    """Digita o CNS e dispara o A4J do campo — é ele que traz o paciente.

    Medido: só o `onblur` basta. Não é preciso clicar a lupa `frm:btnPesquisarCadSUS`;
    o servidor já devolve nome, nascimento, nacionalidade, sexo, mãe, raça/cor e o
    endereço inteiro (todos DISABLED, derivados do CADSUS), mais o CPF e a lista de
    exames anteriores do paciente.
    """
    campo = doc.find("input", {"name": "frm:cartaoSUS"})
    if campo is None:
        raise RuntimeError("campo 'Cartão SUS' ausente — a tela não é a de Novo Exame")
    r = c.post_a4j(doc, "frm", {"frm:cartaoSUS": cns}, _a4j_do_elemento(campo), nome)
    return aplicar_a4j(doc, c.sopa(r))


def marcar_tipo_exame(c: SiscanClient, doc: BeautifulSoup, codigo: str = "01",
                      nome: str = "tipo-exame") -> BeautifulSoup:
    """Marca o tipo de exame. ANTES disso, Unidade Requisitante só tem 'Selecionar';
    depois, vêm as 37 unidades do município (valor = ÍNDICE, texto = 'CNES - NOME')."""
    radio = next((i for i in doc.find_all("input", {"name": "frm:tipoExame"})
                  if i.get("value") == codigo), None)
    if radio is None:
        raise RuntimeError(f"tipo de exame {codigo!r} ausente")
    r = c.post_a4j(doc, "frm", {"frm:tipoExame": codigo}, _a4j_do_elemento(radio), nome)
    return aplicar_a4j(doc, c.sopa(r))


def unidades(doc: BeautifulSoup) -> list[tuple[str, str]]:
    return opcoes(doc, "frm:unidadeSaude2")


def unidade_por_cnes(doc: BeautifulSoup, cnes: str) -> str:
    """Índice do `<option>` da unidade com esse CNES.

    O `value` do option é POSICIONAL e não é estável entre renderizações — quem
    guardar o índice guarda lixo. O texto é `CNES - NOME`, então o CNES é a chave
    honesta, a mesma ponte que já usamos para unidade em todo o resto do sistema.
    """
    for v, t in unidades(doc):
        if t.split(" - ", 1)[0].strip() == str(cnes).strip():
            return v
    raise RuntimeError(f"CNES {cnes!r} não está entre as unidades requisitantes do SISCAN")


def responsavel_por_cns(doc: BeautifulSoup, cns: str) -> str | None:
    """Índice do Responsável com esse CNS — mesma razão de `unidade_por_cnes`."""
    for v, t in responsaveis(doc):
        if t.rsplit(" - ", 1)[-1].strip() == str(cns).strip():
            return v
    return None


def avancar(c: SiscanClient, doc: BeautifulSoup, unidade: str, tipo: str = "01",
            prestador: str = "0", nome: str = "avancar") -> BeautifulSoup:
    """[Avançar] para a etapa 2. NÃO grava: a requisição só nasce no Salvar.

    ARMADILHA (22/09/2026): `frm:tipoExame` TEM de ir no POST completo, mesmo já
    tendo ido pro bean pelo A4J. O parcial do A4J não devolve o radio marcado,
    então quem reposta só o que leu da tela manda o radio vazio e recebe
    "O campo Tipo de Exame deve ser informado" — e, para completar a confusão, a
    resposta já mostra o radio marcado. No navegador não acontece porque a marca
    fica no DOM do cliente.
    """
    extras = {"frm:tipoExame": tipo,
              "frm:prestadorServico2": prestador,
              "frm:unidadeSaude2": unidade,
              "frm:botaoAvancar": "frm:botaoAvancar"}
    return c.sopa(c.post_form(doc, "frm", extras, nome, permitir=NAVEGACAO))


# ------------------------------------------------------------------ etapa 2
def marcar_tipo_mamografia(c: SiscanClient, doc: BeautifulSoup, codigo: str,
                           nome: str = "tipo-mamografia") -> BeautifulSoup:
    """01 Diagnóstica | 02 Rastreamento.

    É este A4J que POPULA o combo Responsável — antes dele o `<select>` vem
    literalmente vazio, e a lista muda conforme a escolha (medido na USF Elenir:
    11 profissionais na diagnóstica, 14 no rastreamento). Um A4J qualquer não
    serve: o de risco elevado (`frm:j_id91`) deixa o combo vazio.
    """
    radio = next((i for i in doc.find_all("input", {"name": "frm:j_id242"})
                  if i.get("value") == codigo), None)
    if radio is None:
        raise RuntimeError("radio 'Tipo de Mamografia' ausente — etapa 2 não carregou")
    r = c.post_a4j(doc, "frm", {"frm:j_id242": codigo}, _a4j_do_elemento(radio), nome)
    return aplicar_a4j(doc, c.sopa(r))


def responsaveis(doc: BeautifulSoup) -> list[tuple[str, str]]:
    """[(índice, 'NOME - CNS')] — profissionais da unidade requisitante."""
    return [(v, t) for v, t in opcoes(doc, "frm:responsavelColeta") if v != "0"]


def escolher_responsavel(c: SiscanClient, doc: BeautifulSoup, indice: str,
                         nome: str = "responsavel") -> BeautifulSoup:
    """Escolhe o Responsável; o servidor DERIVA o Conselho (ex.: 'CRM - 1255134')
    e o deixa disabled — ou seja, conselho não se digita nem se reposta."""
    sel = doc.find("select", {"name": "frm:responsavelColeta"})
    if sel is None:
        raise RuntimeError("combo 'Responsável' ausente")
    r = c.post_a4j(doc, "frm", {"frm:responsavelColeta": indice}, _a4j_do_elemento(sel), nome)
    return aplicar_a4j(doc, c.sopa(r))


def opcoes(doc: BeautifulSoup, name: str) -> list[tuple[str, str]]:
    sel = doc.find("select", {"name": name})
    return [] if sel is None else [(o.get("value"), o.get_text(" ", strip=True))
                                   for o in sel.find_all("option")]
