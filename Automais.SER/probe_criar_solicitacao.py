"""CRIA uma solicitação no SER a partir de uma existente como MODELO. Escrita de verdade.

Por que existe: a linha Cancelada do SER não oferece "Editar" nem nada parecido com "Reabrir"
(medido em 2749938: o menu tem só Visualizar, Historico da Solicitação e Registrar FollowUP).
Reenviar um cancelado, no SER, só pode significar **criar um pedido novo** com os mesmos dados.

O modelo é LIDO da tela, não transcrito: recurso, CID, classificação de risco, médico e os
campos dinâmicos saem do próprio SER. Assim o pedido novo é fiel ao original por construção, e
não pela minha digitação.

O caminho, todo lido da página (nada de `j_id` chumbado — é posicional e muda quando a SES-RJ
recompila):

  1. login + módulo ambulatorial
  2. modelo: Pesquisar por ID -> ação `Visualizar` -> extrai os dados
  3. aba Editar (criação): ramo "É AMBULATÓRIO ESTADUAL?" -> Tipo -> Recurso
     (a ordem importa: o ramo decide QUAIS recursos existem — docs/ser-criar-solicitacao.md §2.1)
  4. Pesquisar paciente por CPF (motor do próprio SER)
  5. Unidade de origem: o radio nasce em "Não", e o campo de texto livre que ele habilita é
     OBRIGATÓRIO. Com `--unidade` a sonda tenta o caminho identificado (radio "Sim" +
     autocomplete `suggUnidadeOrigem`) e só cai no texto livre se o SER não sugerir a unidade
     exata — inventar vínculo de unidade em pedido de produção é pior que declarar não-informada.
  6. Hipótese: `rich:suggestionbox` de CID — exige a AMARRAÇÃO em duas requisições A4J
     (fetch com `inputvalue` + onselect com o índice no hidden `_selection`, docs/ser.md §4.3)
     E o texto no campo. As duas coisas: o onselect responde `Ajax-Update-Ids` VAZIO — ele não
     re-renderiza nada, a escolha fica só na conversa Seam e quem escreve o texto visível é o
     JS do navegador. Gravar sem o texto devolve "Hipótese Diagnóstica é obrigatório".
  7. campos dinâmicos, casados por RÓTULO
  8. `<a title="Gravar">` -> lê o número devolvido

**A resposta de cada troca de combo é PARCIAL** (sem `<form>`), e é nela — e só nela — que vêm
o catálogo de recursos e o bloco dinâmico. Por isso esta sonda mantém um ESTADO acumulado, como
o DOM do navegador: a página completa é a base, e cada fragmento re-renderizado é mesclado por
cima. Ler só a página completa devolve combo vazio; ler só o fragmento perde os hidden do form.

Por padrão é ENSAIO: monta tudo, imprime o POST e PARA antes de gravar. Para criar de verdade
é preciso `--gravar`.

Uso (CPF/CNS é PII — não deixar número real neste arquivo):
  python probe_criar_solicitacao.py --modelo <id> --cpf <cpf ou cns>
  python probe_criar_solicitacao.py --modelo <id> --cpf <cpf ou cns> --gravar
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys

import httpx

RAIZ = pathlib.Path(__file__).parent
sys.path.insert(0, str(RAIZ))

from probe_editar_solicitacao import (  # noqa: E402
    BASE, CAMPO_ID, CAMPO_SITUACAO, CAP, UA, URL_SOLIC,
    Ser, campos_do_form, mensagens, sopa, viewstate,
)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAMPO_SISREG = "form0:comboSisReg"
CAMPO_TIPO = "form0:comboTipoRecurso"
CAMPO_RECURSO = "form0:comboRecurso"
CAMPO_RECURSO_SUGG = "form0:suggRecurso"
CAMPO_RISCO = "form0:classificacao_risco"
CAMPO_MEDICO = "form0:medicoResp"
CAMPO_CID = "form0:procedimento"
CAMPO_CPF_BUSCA = "form0:numeroCADSUS"
PAINEL_PACIENTE = "form0:painelDadosDoPaciente"

RADIO_UNIDADE = "form0:unidadeDeOrigemIdentificada_radio"
CAMPO_UNIDADE_SUGG = "form0:suggUnidadeOrigem"
CAMPO_UNIDADE_LIVRE = "form0:unidadeNaoIdentificada"
RADIO_MEDICO = "form0:booleanMedicoSolicitanteIdentificado_radio"


# ---------------------------------------------------------------- leitura de fragmento

def campos_de(html: str) -> tuple[dict[str, str], set[str]]:
    """(valores, nomes que são hidden) de um fragmento — com ou sem <form>.

    Espelha o navegador: campo `disabled` não é enviado (o SER trava a identidade do paciente
    assim e tem esses valores do lado dele), radio/checkbox só vai se marcado, botão não entra.
    """
    d = sopa(html)
    raiz = d.find("form", id="form0") or d
    valores: dict[str, str] = {}
    hiddens: set[str] = set()

    for i in raiz.find_all("input"):
        nome, tipo = i.get("name"), (i.get("type") or "text").lower()
        if not nome or i.has_attr("disabled"):
            continue
        if tipo in {"submit", "button", "image", "reset", "file"}:
            continue
        if tipo in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        valores[nome] = i.get("value") or ""
        if tipo == "hidden":
            hiddens.add(nome)

    for s in raiz.find_all("select"):
        if not s.get("name") or s.has_attr("disabled"):
            continue
        o = s.find("option", selected=True) or s.find("option")
        valores[s["name"]] = (o.get("value") if o else "") or ""

    for t in raiz.find_all("textarea"):
        if t.get("name") and not t.has_attr("disabled"):
            valores[t["name"]] = t.get_text() or ""

    return valores, hiddens


def evento_do_controle(html: str, nome: str) -> str | None:
    """Id do `a4j:support` do controle, tirado do `similarityGroupingId` da própria tag.

    Serve para `<select>` e para `<input type=radio>`: os radios do bloco fixo também disparam
    A4J, e é essa ida que troca os campos condicionais.
    """
    for padrao in (r'<select[^>]*name="' + re.escape(nome) + r'"[^>]*>',
                   r'<input[^>]*name="' + re.escape(nome) + r'"[^>]*>'):
        for m in re.finditer(padrao, html):
            if (g := re.search(r"'similarityGroupingId'\s*:\s*'([^']+)'", m.group(0))):
                return g.group(1)
    return None


def combo(html: str, campo: str) -> list[tuple[str, str]]:
    d = sopa(html)
    sel = d.find("select", attrs={"name": campo})
    if sel is None:
        return []
    fora = ("NoSelectionConverter", "null")
    return [(o.get("value") or "", " ".join(o.get_text(" ", strip=True).split()))
            for o in sel.find_all("option")
            if (o.get("value") or "").strip() and not any(f in o.get("value") for f in fora)]


def casar(opcoes: list[tuple[str, str]], alvo: str) -> tuple[str, str] | None:
    """(valor, rótulo) da opção que casa com o alvo — exato primeiro, depois por prefixo.

    O prefixo EXIGE rótulo não-vazio dos dois lados: `"prioridade 1".startswith("")` é sempre
    verdadeiro, e uma opção de rótulo vazio (o "Selecione..." de alguns combos) casaria com
    qualquer coisa — o pedido sairia com a classificação de risco errada, sem erro nenhum.
    """
    limpo = " ".join(alvo.split()).casefold()
    if not limpo:
        return None
    for v, t in opcoes:
        if " ".join(t.split()).casefold() == limpo:
            return v, t
    for v, t in opcoes:
        t2 = " ".join(t.split()).casefold()
        if t2 and (t2.startswith(limpo) or limpo.startswith(t2)):
            return v, t
    return None


def suggestion_box(html: str, campo: str) -> tuple[str, str] | None:
    """(boxId, onselectId) do `rich:suggestionbox` daquele campo — docs/ser.md §4.3."""
    box = re.search(r"RichFaces\.Suggestion\(\s*'[^']*'\s*,\s*'" + re.escape(campo)
                    + r"'\s*,\s*'([^']+)'", html)
    if not box:
        return None
    box_id = box.group(1)
    onselect = re.search(r"'(" + re.escape(box_id) + r":[A-Za-z_]\w*)'\s*:\s*'\1'", html)
    return (box_id, onselect.group(1)) if onselect else None


def linhas_de_sugestao(html: str, box_id: str) -> list[list[str]]:
    """As linhas da tabela de sugestões. O índice de uma linha é o que viaja no `_selection`."""
    d = sopa(html)
    tab = None
    for cand in d.find_all("table"):
        if (cand.get("id") or "").startswith(box_id):
            tab = cand
            break
    if tab is None and (el := d.find(id=box_id)) is not None:
        tab = el.find("table") or el
    if tab is None:
        return []
    saida = []
    for tr in tab.find_all("tr"):
        celulas = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all("td")]
        if any(celulas):
            saida.append(celulas)
    return saida


def dinamicos(html: str) -> dict[str, tuple[str, str, bool]]:
    """{rótulo: (name, valor atual, obrigatório)} do bloco dinâmico.

    O rótulo sai do container SEM os controles dentro: o `get_text` cru devolveria
    "Queixa Principal: <o que está digitado>" e nenhum rótulo casaria entre duas telas.
    E o `rich:calendar` embute um <script> com a localização inteira do calendário.
    """
    d = sopa(html)
    out = {}
    for cont in d.find_all(id=re.compile(r"^form0:container_dinamico_id_\d+$")):
        el = cont.find(["input", "select", "textarea"])
        if el is None or not el.get("name"):
            continue
        nome = el["name"]
        valor = el.get_text() if el.name == "textarea" else (el.get("value") or "")
        copia = sopa(str(cont))
        for lixo in copia.find_all(["input", "select", "textarea", "script"]):
            lixo.decompose()
        texto = " ".join(copia.get_text(" ", strip=True).split())
        out[texto.replace("*", "").strip(" :")] = (nome, valor, "*" in texto)
    return out


def botao_titulo(html: str, titulo: str, prefixo: str | None = None) -> str | None:
    """Id do <a title="..."> — opcionalmente só o que estiver dentro de `prefixo` (form0:)."""
    for m in re.finditer(r"<a[^>]*>", html):
        tag = m.group(0)
        if f'title="{titulo}"' not in tag:
            continue
        if (idm := re.search(r'id="([^"]+)"', tag)):
            if prefixo is None or idm.group(1).startswith(prefixo):
                return idm.group(1)
    return None


def regiao_do_botao(html: str, ident: str) -> str | None:
    """O containerId que o `onclick` do botão passa ao `A4J.AJAX.Submit` — é a REGIÃO A4J."""
    for m in re.finditer(r"<a[^>]*>", html):
        if f'id="{ident}"' not in m.group(0):
            continue
        if (g := re.search(r"A4J\.AJAX\.Submit\(\s*'([^']+)'", m.group(0))):
            return g.group(1)
    return None


def normal(s: str) -> str:
    return " ".join(s.split()).casefold()


# ---------------------------------------------------------------- o modelo

def ler_modelo(ser: Ser, id_solic: str) -> dict:
    """Abre a solicitação existente em Visualizar e devolve os dados dela."""
    tela = ser.c.get(URL_SOLIC).text
    d = sopa(tela)
    botao = d.find(attrs={"title": "Pesquisar"})
    acao = d.find("form", id="form0").get("action") or URL_SOLIC
    busca = ser.postar(acao, campos_do_form(d) | {
        "form0": "form0",
        botao.get("id"): botao.get("id"),
        CAMPO_ID: id_solic,
        CAMPO_SITUACAO: "CANCELADA",
        "AJAXREQUEST": "form0",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(tela),
    })

    db = sopa(busca)
    tab = db.find("table", id="form0:listagem")
    if tab is None:
        raise SystemExit("a busca do modelo não devolveu grade. " + (mensagens(busca) or ""))
    linhas = [tr for tr in tab.select("tbody tr")
              if any(re.fullmatch(r"\d{5,}", td.get_text(strip=True)) for td in tr.find_all("td"))]
    if len(linhas) != 1:
        raise SystemExit(f"esperava 1 linha para o modelo {id_solic}, vieram {len(linhas)}")

    idx = int(re.search(r"form0:listagem:(\d+):", str(linhas[0])).group(1))
    menu = db.find(id=re.compile(rf"^form0:listagem:{idx}:j_id\d+_menu$"))
    ver = next((a for a in menu.find_all("a")
                if a.get_text(strip=True).lower() == "visualizar"), None)
    if ver is None:
        raise SystemExit("a linha do modelo não oferece Visualizar")

    html = ser.postar(acao, campos_do_form(db) | {
        "form0": "form0",
        ver.get("id"): ver.get("id"),
        "AJAXREQUEST": "_viewRoot",
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": viewstate(busca),
    })
    (CAP / f"criar_modelo_{id_solic}.html").write_text(html, encoding="utf-8")

    dm = sopa(html)

    def valor(nome: str) -> str:
        el = dm.find(attrs={"name": nome})
        if el is None:
            return ""
        return (el.get_text() if el.name == "textarea" else (el.get("value") or "")).strip()

    # Na tela Visualizar o bloco de identificação do pedido repete o mesmo <label>
    # ("Classificação de Risco:") em cinco campos — o SER reaproveita a marcação. O que separa
    # um do outro é o NOME, não o rótulo.
    return {
        "cid": valor("form0:j_id80"),
        "risco": valor(CAMPO_RISCO),
        "tipo": valor("form0:j_id85"),
        "recurso": valor("form0:j_id87"),
        "medico": valor("form0:medicoResponsavel").strip(),
        "solicitante": valor("form0:j_id56").strip(),
        "dinamicos": {r: v for r, (_, v, _) in dinamicos(html).items()},
    }


# ---------------------------------------------------------------- a tela de criação

class Criar:
    """Aba Editar em modo criação, com o ESTADO acumulado como o DOM do navegador.

    `base` é a última página COMPLETA (dá o `action`, os scripts de init e o botão Gravar).
    `estado` são os valores dos campos, mesclados a cada fragmento A4J — é o que vai no POST.
    `frags` guarda os fragmentos do mais novo para o mais velho, para leitura de combos e do
    bloco dinâmico: o catálogo de recursos SÓ existe no fragmento, nunca na página completa.
    """

    def __init__(self, ser: Ser):
        self.ser = ser
        self.base = ""
        self.vs = ""
        self.act = URL_SOLIC
        self.estado: dict[str, str] = {}
        self.hiddens: set[str] = set()
        self.frags: list[str] = []

    # -- estado

    def _absorver(self, html: str) -> str:
        valores, hiddens = campos_de(html)
        self.estado |= valores
        self.hiddens |= hiddens
        if '<form id="form0"' in html:
            self.base = html
            d = sopa(html)
            if (f := d.find("form", id="form0")) and f.get("action"):
                self.act = f["action"]
        if (vs := viewstate(html)):
            self.vs = vs
        self.frags.insert(0, html)
        return html

    def ler(self, fn):
        """Aplica `fn` ao fragmento mais recente que devolver algo — depois à página base."""
        for frag in self.frags:
            if (r := fn(frag)):
                return r
        return fn(self.base) if self.base else None

    def _hidden_atual(self) -> dict[str, str]:
        """O que o A4J manda quando há `ajaxSingle`: os hidden do form + o controle."""
        return {k: v for k, v in self.estado.items() if k in self.hiddens}

    def _a4j(self, controle: str, valor: str, extras: dict[str, str] | None = None) -> str:
        evento = self.ler(lambda h: evento_do_controle(h, controle))
        if not evento:
            raise SystemExit(f"não achei o evento A4J do controle {controle!r} na tela")
        dados = self._hidden_atual() | {
            controle: valor,
            "AJAXREQUEST": "_viewRoot",
            evento: evento,
            "ajaxSingle": controle,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs,
        }
        html = self.ser.postar(self.act, dados | (extras or {}))
        self._absorver(html)
        # O fragmento re-renderiza o controle com a escolha marcada; se o SER ignorar a troca, o
        # estado guardaria o valor que ninguém aplicou. Fixar aqui é o que o DOM do navegador faz.
        self.estado[controle] = valor
        return html

    # -- passos

    def abrir(self) -> str:
        tela = self.ser.c.get(URL_SOLIC).text
        d = sopa(tela)
        acao = d.find("form", id="form0").get("action") or URL_SOLIC
        html = self.ser.postar(acao, campos_do_form(d) | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(tela),
        })
        if CAMPO_TIPO not in html:
            (CAP / "criar_aba_falhou.html").write_text(html, encoding="utf-8")
            raise SystemExit("a aba Editar não abriu em modo criação (sem comboTipoRecurso).")
        self.estado, self.hiddens, self.frags = {}, set(), []
        return self._absorver(html)

    def trocar(self, campo: str, valor: str) -> str:
        return self._a4j(campo, valor)

    def marcar_radio(self, nome: str, valor: str) -> str:
        return self._a4j(nome, valor)

    def pesquisar_paciente(self, documento: str) -> str:
        botao = self.ler(lambda h: botao_titulo(h, "Pesquisar", prefixo="form0:"))
        if not botao:
            raise SystemExit("não achei o botão Pesquisar do painel de paciente")
        return self._absorver(self.ser.postar(self.act, self._hidden_atual() | {
            CAMPO_CPF_BUSCA: documento,
            "AJAXREQUEST": "_viewRoot",
            botao: botao,
            "ajaxSingle": botao,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs,
        }))

    def amarrar(self, campo: str, termo: str, escolher) -> list[str] | None:
        """As DUAS requisições do autocomplete (docs/ser.md §4.3). Texto solto o SER descarta.

        `escolher(linhas)` devolve o índice da linha, ou None. Devolve as células da linha
        escolhida — é delas que sai o texto que o navegador escreveria no campo.
        """
        caixa = self.ler(lambda h: suggestion_box(h, campo))
        if caixa is None:
            raise SystemExit(f"não achei o script do autocomplete de {campo}")
        box_id, onselect = caixa

        fetch = self.ser.postar(self.act, self._hidden_atual() | {
            "AJAXREQUEST": "_viewRoot",
            "inputvalue": termo,
            box_id: box_id,
            "ajaxSingle": box_id,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs,
        })
        if (vs := viewstate(fetch)):
            self.vs = vs

        linhas = linhas_de_sugestao(fetch, box_id)
        if not linhas:
            return None
        indice = escolher(linhas)
        if indice is None:
            print(f"      (sugestões de {campo}: "
                  + "; ".join(" | ".join(c) for c in linhas[:5])[:220] + ")")
            return None

        select = self.ser.postar(self.act, self._hidden_atual() | {
            "AJAXREQUEST": "_viewRoot",
            onselect: onselect,
            "ajaxSingle": box_id,
            f"{box_id}_selection": str(indice),
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.vs,
        })
        # O efeito do onselect é INVISÍVEL: ele responde `Ajax-Update-Ids` VAZIO — não
        # re-renderiza nada, a escolha mora só na conversa Seam. Sem o envelope A4J a amarração
        # não aconteceu, e o pedido sairia sem o vínculo com cara de completo.
        if 'name="Ajax-Response"' not in select and '<form id="form0"' not in select:
            (CAP / f"criar_onselect_falhou_{campo.replace(':', '_')}.html").write_text(
                select, encoding="utf-8")
            raise SystemExit(f"o onselect de {campo} não devolveu envelope A4J — amarração falhou")
        self._absorver(select)
        return linhas[indice]


def numero_gerado(html: str) -> str | None:
    """O número que o SER devolve depois de gravar."""
    msg = mensagens(html)
    for padrao in (r"[Ss]olicita[çc][ãa]o\D{0,40}?(\d{6,})", r"n[ºo°]\s*(\d{6,})", r"\b(\d{7,})\b"):
        if (m := re.search(padrao, msg)):
            return m.group(1)
    d = sopa(html)
    el = d.find(attrs={"name": "form0:idSolicitacao"})
    if el is not None and (el.get("value") or "").strip():
        return el["value"].strip()
    return None


# ---------------------------------------------------------------- principal

def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--modelo", required=True, help="ID da solicitação usada como molde")
    p.add_argument("--cpf", required=True, help="CPF (ou CNS) do paciente do pedido NOVO")
    p.add_argument("--unidade", default=None,
                   help="unidade de origem. Sem isso, usa o Solicitante do modelo.")
    p.add_argument("--gravar", action="store_true", help="cria de verdade (sem isso é ensaio)")
    a = p.parse_args()

    with httpx.Client(base_url=BASE, timeout=180, follow_redirects=True,
                      headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"}) as c:
        ser = Ser(c)
        ser.login()

        print(f"\n=== MODELO {a.modelo} " + "=" * 52)
        m = ler_modelo(ser, a.modelo)
        for k in ("tipo", "recurso", "cid", "risco", "medico", "solicitante"):
            print(f"    {k:12}: {m[k]!r}")
        for r, v in m["dinamicos"].items():
            print(f"    dinâmico    : {r} = {v!r}")
        if not m["recurso"] or not m["tipo"]:
            raise SystemExit("o modelo não devolveu tipo/recurso — nada a replicar.")

        unidade = a.unidade or m["solicitante"]

        print(f"\n=== MONTANDO O PEDIDO NOVO (CPF {a.cpf}) " + "=" * 30)
        t = Criar(ser)
        t.abrir()

        # Ramo primeiro: "É AMBULATÓRIO ESTADUAL?" decide QUAIS recursos existem.
        ramos = combo(t.base, CAMPO_SISREG)
        escolhido = None
        for valor_ramo, rotulo_ramo in ramos:
            t.abrir()
            t.trocar(CAMPO_SISREG, valor_ramo)
            t.trocar(CAMPO_TIPO, m["tipo"])
            recursos = t.ler(lambda h: combo(h, CAMPO_RECURSO)) or []
            achado = casar(recursos, m["recurso"])
            print(f"    ramo {rotulo_ramo!r}: {len(recursos)} recursos — "
                  + (f"ACHOU ({achado[0]} = {achado[1]!r})" if achado else "não tem o recurso"))
            if achado:
                escolhido = (valor_ramo, rotulo_ramo, achado[0])
                break
        if escolhido is None:
            raise SystemExit(f"o recurso {m['recurso']!r} não existe em nenhum dos dois ramos.")

        valor_ramo, rotulo_ramo, valor_recurso = escolhido
        t.trocar(CAMPO_RECURSO, valor_recurso)
        print(f"    ramo={rotulo_ramo!r}  tipo={m['tipo']!r}  recurso={valor_recurso!r}")

        extras: dict[str, str] = {CAMPO_RECURSO: valor_recurso}

        # O Recurso tem DOIS controles ligados ao mesmo vínculo: o combo e o autocomplete
        # `suggRecurso`, que vem DEPOIS dele na árvore JSF. Escolher só pelo combo popula o
        # bloco dinâmico e ainda assim o Gravar responde "Consulta ou Exame é obrigatório" —
        # o texto vazio do autocomplete, processado por último, zera o que o combo amarrou.
        # Mesmo padrão do CID: amarração A4J **e** texto no campo.
        linha_rec = t.amarrar(
            CAMPO_RECURSO_SUGG, m["recurso"],
            lambda ls: next((i for i, c in enumerate(ls)
                             if any(normal(cel) == normal(m["recurso"]) for cel in c)), None))
        if not linha_rec:
            raise SystemExit(f"o autocomplete de Recurso não sugeriu {m['recurso']!r}")
        extras[CAMPO_RECURSO_SUGG] = linha_rec[0]
        print(f"    recurso  : autocomplete amarrado -> {linha_rec[0]!r}")

        # paciente
        t.pesquisar_paciente(re.sub(r"\D", "", a.cpf))
        painel = t.ler(lambda h: sopa(h).find(id=PAINEL_PACIENTE))
        if painel is None:
            (CAP / "criar_paciente_sem_painel.html").write_text(t.frags[0], encoding="utf-8")
            raise SystemExit("o painel do paciente não veio na resposta")
        el_nome = painel.find(attrs={"name": "form0:nome"})
        nome = (el_nome.get("value") or "") if el_nome is not None else ""
        if not nome.strip():
            print("    !! o SER não devolveu paciente para esse documento.")
            print("       painel:", " | ".join(list(painel.stripped_strings))[:300])
            raise SystemExit(1)
        print(f"    paciente : {nome}")

        # --- unidade de origem. O radio nasce em "Não" e o texto livre que ele habilita é
        # obrigatório: gravar sem nada devolve "Unidade de origem precisa ser informada".
        t.marcar_radio(RADIO_UNIDADE, "true")
        linha = t.amarrar(
            CAMPO_UNIDADE_SUGG, unidade,
            lambda ls: next((i for i, c in enumerate(ls)
                             if any(normal(cel) == normal(unidade) for cel in c)), None))
        if linha:
            extras[RADIO_UNIDADE] = "true"
            extras[CAMPO_UNIDADE_SUGG] = linha[0]
            print(f"    unidade  : identificada -> {linha[0]!r}")
        else:
            # Sem sugestão EXATA, o caminho honesto é declarar não-identificada com o texto:
            # amarrar uma unidade parecida gravaria vínculo errado em pedido de produção.
            t.marcar_radio(RADIO_UNIDADE, "false")
            extras[RADIO_UNIDADE] = "false"
            extras[CAMPO_UNIDADE_LIVRE] = unidade
            print(f"    unidade  : não identificada (texto livre) -> {unidade!r}")

        # --- CID: amarração + o texto no campo (as duas coisas)
        codigo = ""
        if (mc := re.match(r"\s*([A-Za-z]\d{2}(?:\.\d+)?)", m["cid"])):
            codigo = mc.group(1).upper()
        linha = t.amarrar(
            CAMPO_CID, codigo or m["cid"].split()[0],
            lambda ls: next((i for i, c in enumerate(ls)
                             if any(normal(cel) == codigo.casefold() for cel in c)), None))
        if not linha:
            raise SystemExit(f"o SER não sugeriu o CID {m['cid']!r}")
        extras[CAMPO_CID] = m["cid"]
        print(f"    CID      : {linha[1] if len(linha) > 1 else linha[0]!r} "
              f"-> campo = {m['cid']!r}")

        # --- combos restantes
        riscos = t.ler(lambda h: combo(h, CAMPO_RISCO)) or []
        if (achado := casar(riscos, m["risco"])):
            extras[CAMPO_RISCO] = achado[0]
            print(f"    risco    : {m['risco']!r} -> {achado[0]!r} ({achado[1]!r})")
        elif m["risco"]:
            print(f"    !! risco {m['risco']!r} não está no combo: "
                  + ", ".join(f"{v}={t2!r}" for v, t2 in riscos[:8]))

        medicos = t.ler(lambda h: combo(h, CAMPO_MEDICO)) or []
        achado = casar(medicos, m["medico"]) if m["medico"] else None
        if achado:
            extras[RADIO_MEDICO] = "true"
            extras[CAMPO_MEDICO] = achado[0]
            print(f"    médico   : {m['medico']!r} -> {achado[0]!r} ({achado[1]!r})")
        elif m["medico"]:
            print(f"    !! médico {m['medico']!r} não está no combo ({len(medicos)} opções)")

        # --- dinâmicos, casados por rótulo
        alvo_din = t.ler(lambda h: dinamicos(h)) or {}
        print(f"    bloco dinâmico: {len(alvo_din)} campo(s)")
        for rotulo, (nome_campo, _, obrigatorio) in alvo_din.items():
            valor_modelo = m["dinamicos"].get(rotulo)
            if valor_modelo is None:
                marca = "!! OBRIGATÓRIO e o modelo não tem" if obrigatorio else "sem valor no modelo"
                print(f"      - {rotulo[:40]:42} {marca}")
                continue
            extras[nome_campo] = valor_modelo
            print(f"      + {rotulo[:40]:42} {nome_campo} = {valor_modelo[:40]!r}")

        # ---- o POST de gravação
        gravar_id = t.ler(lambda h: botao_titulo(h, "Gravar", prefixo="form0:"))
        if not gravar_id:
            (CAP / "criar_sem_botao_gravar.html").write_text(t.base, encoding="utf-8")
            raise SystemExit("não achei o <a title='Gravar'> dentro de form0")

        # A REGIÃO SAI DO `onclick` DO BOTÃO, não de constante: o Gravar declara
        # `A4J.AJAX.Submit('form0', ...)`, e é `form0` que decide qual parte da árvore o A4J
        # processa. Mandar `_viewRoot` aqui não é "mais abrangente" — é outra região.
        regiao = t.ler(lambda h: regiao_do_botao(h, gravar_id)) or "form0"
        payload = dict(t.estado) | extras | {
            "form0": "form0",
            gravar_id: gravar_id,
            "AJAXREQUEST": regiao,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": t.vs,
        }

        print("\n=== POST DE GRAVAÇÃO " + "=" * 50)
        print(f"    botão Gravar : {gravar_id}")
        print(f"    campos       : {len(payload)}")
        for k in (CAMPO_SISREG, CAMPO_TIPO):
            print(f"      {k:36} = {payload.get(k)!r}")
        for k, v in extras.items():
            print(f"      {k:36} = {v[:52]!r}")

        (CAP / "criar_tela_antes_de_gravar.html").write_text(t.frags[0], encoding="utf-8")

        if not a.gravar:
            print("\n    ENSAIO — nada foi enviado ao SER.")
            print("    Rode de novo com --gravar para criar de verdade.")
            return 0

        html = ser.postar(t.act, payload)
        (CAP / "criar_resposta_gravar.html").write_text(html, encoding="utf-8")
        print(f"\n    resposta: {len(html)} bytes -> capturas/criar_resposta_gravar.html")
        if (msg := mensagens(html)):
            print(f"    mensagem do SER: {msg}")

        numero = numero_gerado(html)
        if numero:
            print(f"\n    >>> SOLICITAÇÃO CRIADA: {numero}")
            print("    (confira na tela do SER — 'gravou com sucesso' não é prova por si só)")
            return 0

        print("\n    !! o SER não devolveu número. Veja a mensagem acima e o HTML capturado.")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
