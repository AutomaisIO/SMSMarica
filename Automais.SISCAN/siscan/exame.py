"""Tela EXAME -> GERENCIAR EXAME (`/visao/exame/pesquisarExame.jsf`).

Códigos medidos contra o SISCAN real (não deduzidos):

  frm:j_id47      checkbox tipo de exame — "01" = Mamografia
  frm:statusExame "01" Requisitado | "02" Com Resultado | "03" Liberado
  frm:unidadeRequisitante2  índice do <option>, NÃO o CNES

O botão Pesquisar é um commandLink Mojarra: `jsfcljs` injeta o par
`frm:botaoPesquisarExame=frm:botaoPesquisarExame`.

NOTA (22/09/2026): arquivo RECONSTRUÍDO a partir do bytecode — ver client.py.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from bs4 import BeautifulSoup

from .client import SiscanClient

CAMINHO = "/visao/exame/pesquisarExame.jsf"

MAMOGRAFIA = "01"
REQUISITADO, COM_RESULTADO, LIBERADO = "01", "02", "03"
STATUS_ROTULO = {REQUISITADO: "Requisitado", COM_RESULTADO: "Com Resultado", LIBERADO: "Liberado"}


@dataclass
class Filtro:
    mamografia: bool = True
    status: str | None = None
    data_inicio: str | None = None
    data_fim: str | None = None
    nome_paciente: str | None = None
    cartao_sus: str | None = None
    numero_exame: str | None = None
    numero_protocolo: str | None = None
    numero_prontuario: str | None = None
    unidade_requisitante: str | None = None

    def para_campos(self) -> dict[str, str]:
        d: dict[str, str] = {}
        if self.mamografia:
            d["frm:j_id47"] = MAMOGRAFIA
        if self.status:
            d["frm:statusExame"] = self.status
        if self.data_inicio:
            d["frm:dataRequisicaoInputDate"] = self.data_inicio
        if self.data_fim:
            d["frm:dataRequisicaoFinalInputDate"] = self.data_fim
        if self.nome_paciente:
            d["frm:nomePaciente"] = self.nome_paciente
        if self.cartao_sus:
            d["frm:cartaoSUS"] = self.cartao_sus
        if self.numero_exame:
            d["frm:numeroExame"] = self.numero_exame
        if self.numero_protocolo:
            d["frm:numeroProtocolo"] = self.numero_protocolo
        if self.numero_prontuario:
            d["frm:numeroProntuario"] = self.numero_prontuario
        if self.unidade_requisitante:
            d["frm:unidadeRequisitante2"] = self.unidade_requisitante
        return d


@dataclass
class Linha:
    colunas: list[str] = field(default_factory=list)
    acoes: dict[str, str] = field(default_factory=dict)


def abrir(c: SiscanClient) -> BeautifulSoup:
    """Abre a tela pelo MENU. GET direto na URL devolve tela meio-inicializada."""
    doc = c.sopa(c.get("/visao/index.jsf", "index"))
    return c.sopa(c.navegar(doc, "GERENCIAR EXAME", "gerenciar-exame"))


def campo_por_rotulo(doc: BeautifulSoup, rotulo: str) -> str | None:
    """Resolve o `name` de um campo pelo rótulo visível.

    Obrigatório para os ids auto-gerados (`frm:j_id47` = Mamografia): o número
    muda conforme o caminho de renderização, então fixá-lo no código quebra em
    silêncio — ou pior, acerta o campo errado.
    """
    f = doc.find("form", id="frm") or doc
    for lb in f.find_all("label"):
        if lb.get_text(" ", strip=True).upper().strip(": ") == rotulo.upper():
            alvo = lb.get("for")
            if alvo:
                el = doc.find(id=alvo)
                if el is not None:
                    return el.get("name")
        inp = lb.find("input")
        if inp is not None and lb.get_text(" ", strip=True).upper().strip(": ") == rotulo.upper():
            return inp.get("name")
    return None


def commit_data_inicial(c: SiscanClient, doc: BeautifulSoup, data: str) -> BeautifulSoup:
    """Empurra a data inicial pro bean via A4J, como faz o `onblur` da tela.

    Sem isto, Pesquisar responde "Data para comparação não informada" — ver
    `SiscanClient.post_a4j`. O par disparador (`frm:j_idNN`) é auto-gerado, então
    é lido do próprio `onblur` em vez de fixado.
    """
    campo = doc.find("input", {"name": "frm:dataRequisicaoInputDate"})
    onblur = campo.get("onblur") if campo else None
    m = re.search(r"'parameters':\{'([^']+)':'([^']+)','ajaxSingle':'([^']+)'", onblur or "")
    if not m:
        raise RuntimeError("não consegui ler os parâmetros A4J do campo de data")
    disparador, valor, single = m.groups()
    r = c.post_a4j(
        doc, "frm",
        {"frm:dataRequisicaoInputDate": data},
        {disparador: valor, "ajaxSingle": single},
        "a4j-data",
    )
    return c.sopa(r)


def pesquisar(c: SiscanClient, filtro: Filtro, doc: BeautifulSoup | None = None) -> BeautifulSoup:
    doc = doc or abrir(c)
    if filtro.data_inicio:
        commit_data_inicial(c, doc, filtro.data_inicio)
    extras = filtro.para_campos()
    if filtro.mamografia:
        extras.pop("frm:j_id47", None)
        nome = campo_por_rotulo(doc, "Mamografia")
        if not nome:
            raise RuntimeError("checkbox 'Mamografia' ausente — tela não inicializada?")
        extras[nome] = MAMOGRAFIA
    extras["frm:botaoPesquisarExame"] = "frm:botaoPesquisarExame"
    return c.sopa(c.post_form(doc, "frm", extras, "resultado-pesquisa",
                              referer=c._url(CAMINHO)))


ACAO_VER_REQUISICAO = "Visualizar Requisição do Exame"
ACAO_ALTERAR_REQUISICAO = "Alterar/Visualizar Requisição do Exame"
ACAO_INCLUIR_RESULTADO = "Incluir Resultado do Exame"


def acoes_da_linha(tr) -> dict[str, tuple[str, str]]:
    """Mapeia `title` -> par jsfcljs do link, por linha da grade.

    O id embute o nº do exame (`frm:listaExamePaginada:139257485:j_id173`), e o
    sufixo `j_idNNN` é auto-gerado — por isso a ação é localizada pelo TÍTULO.
    """
    out: dict[str, tuple[str, str]] = {}
    for a in tr.find_all("a"):
        titulo = (a.get("title") or "").strip()
        if not titulo:
            continue
        m = re.search(r"jsfcljs\(document\.getElementById\('frm'\),\{'([^']+)':'([^']+)'\}",
                      a.get("onclick") or "")
        if m:
            out[titulo] = (m.group(1), m.group(2))
    return out


def abrir_acao(c: SiscanClient, doc: BeautifulSoup, exame_id: str, titulo: str,
               nome: str = "acao") -> BeautifulSoup:
    """Clica a ação `titulo` da linha do exame `exame_id`. NÃO grava nada:
    apenas navega para a tela (visualizar / alterar / incluir resultado)."""
    tb = doc.find("tbody", id="frm:listaExamePaginada:tb")
    for tr in tb.find_all("tr", recursive=False):
        acoes = acoes_da_linha(tr)
        par = next((v for t, v in acoes.items() if t == titulo), None)
        if par and any(exame_id in k for k in (par[0],)):
            return c.sopa(c.post_form(doc, "frm", {par[0]: par[1]}, nome,
                                      permitir=("alterar", "incluir")))
    raise RuntimeError(f"ação '{titulo}' não encontrada para o exame {exame_id}")


def grade(doc: BeautifulSoup) -> tuple[list[str], list[Linha]]:
    """Cabeçalho + linhas da tabela de resultados (rich-table)."""
    t = doc.find("table", id="frm:listaExamePaginada")
    if not t:
        return [], []
    cab = [th.get_text(" ", strip=True) for th in (t.find("thead").find_all("th") if t.find("thead") else [])]
    tb = t.find("tbody", id="frm:listaExamePaginada:tb")
    linhas: list[Linha] = []
    for tr in (tb.find_all("tr", recursive=False) if tb else []):
        l = Linha(colunas=[td.get_text(" ", strip=True) for td in tr.find_all("td", recursive=False)])
        l.acoes = {k: v[0] for k, v in acoes_da_linha(tr).items()}
        linhas.append(l)
    return cab, linhas
