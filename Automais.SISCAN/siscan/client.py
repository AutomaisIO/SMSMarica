"""Cliente HTTP do SISCAN (Sistema de Informação do Câncer — DATASUS).

Mesmo stack do SER: JSF 1.2 + RichFaces 3.3.3.Final. Valem os mesmos aprendizados:

  * SEMPRE postar na URL do atributo `action` do `<form>` lido, nunca num caminho
    constante — no SER, postar em URL fixa devolvia listagem diferente e incompleta,
    sem erro e sem aviso.
  * O `javax.faces.ViewState` tem de vir do form daquela resposta.
  * Requisição A4J exige o par `AJAXREQUEST` + o nome do componente disparador;
    sem isso a ação falha em silêncio com HTTP 200.

SOMENTE LEITURA. Ver `somente_leitura` abaixo.

NOTA (22/09/2026): este arquivo foi RECONSTRUÍDO. Os .py do laboratório foram
perdidos (sobrou só o `__pycache__`); a reconstrução saiu do bytecode + de
docs/APRENDIZADOS.html e foi reconferida contra o SISCAN real.
"""

from __future__ import annotations

import copy
import hashlib
import os
import re
import urllib.parse
from pathlib import Path

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

RAIZ = Path(__file__).resolve().parent.parent
CAPTURAS = RAIZ / "capturas"
load_dotenv(RAIZ / ".env")

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

# Trava de escrita em duas camadas: nome do parâmetro e rótulo visível do componente.
PALAVRAS_ESCRITA = (
    "gravar", "salvar", "excluir", "remover", "cancelar solicit", "confirmar",
    "incluir", "alterar", "editar", "encerrar", "finalizar", "liberar",
    "aprovar", "autorizar", "laudar", "assinar", "enviar", "novo",
)


class BloqueioEscrita(RuntimeError):
    """Disparado quando uma submissão parece alterar dado no SISCAN."""


class SiscanClient:
    def __init__(self, somente_leitura: bool = True, capturar: bool = True) -> None:
        self.base = os.environ.get("SISCAN_BASE_URL", "https://siscan.saude.gov.br").rstrip("/")
        self.somente_leitura = somente_leitura
        self.capturar = capturar
        self._seq = 0
        self.http = httpx.Client(
            verify=False,
            follow_redirects=True,
            timeout=60,
            headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"},
        )

    # ---------------------------------------------------------------- captura
    def _dump(self, nome: str, texto: str) -> None:
        if not self.capturar:
            return
        self._seq += 1
        CAPTURAS.mkdir(exist_ok=True)
        alvo = CAPTURAS / f"{self._seq:02d}-{nome}.html"
        alvo.write_text(texto, encoding="utf-8")

    def _url(self, caminho: str) -> str:
        return urllib.parse.urljoin(self.base + "/", caminho.lstrip("/"))

    # ------------------------------------------------------------------ HTML
    @staticmethod
    def sopa(resp) -> BeautifulSoup:
        return BeautifulSoup(resp.text, "html.parser")

    @staticmethod
    def campos(doc: BeautifulSoup, form_id: str) -> dict[str, str]:
        """Colhe os campos submissíveis do form, como o navegador faria."""
        f = doc.find("form", id=form_id)
        d: dict[str, str] = {}
        if not f:
            return d
        for i in f.find_all("input"):
            n, t = i.get("name"), (i.get("type") or "text").lower()
            if not n or t in {"submit", "reset", "button", "image"}:
                continue
            if i.has_attr("disabled"):          # campo disabled o navegador NÃO posta
                continue
            if t in {"checkbox", "radio"} and not i.has_attr("checked"):
                continue
            d[n] = i.get("value") or ""
        for s in f.find_all("select"):
            n = s.get("name")
            if not n or s.has_attr("disabled"):
                continue
            o = s.find("option", selected=True) or s.find("option")
            d[n] = (o.get("value") if o else "") or ""
        for t in f.find_all("textarea"):
            if not t.get("name") or t.has_attr("disabled"):
                continue
            d[t["name"]] = t.text or ""
        return d

    @staticmethod
    def viewstate(doc: BeautifulSoup) -> str | None:
        """`javax.faces.ViewState` da resposta — inclusive de resposta A4J parcial.

        Depois de um A4J, o servidor emite um ViewState NOVO (no bloco
        `ajax-view-state`). Gravar com o ViewState anterior faz o JSF restaurar a
        view velha e **descartar em silêncio** o que o A4J tinha alterado: a
        resposta volta sem erro e o dado simplesmente não muda.
        """
        e = doc.find("input", {"name": "javax.faces.ViewState"})
        return e.get("value") if e else None

    @staticmethod
    def action(doc: BeautifulSoup, form_id: str) -> str | None:
        """URL de submissão REAL do form. Nunca chutar um caminho constante."""
        f = doc.find("form", id=form_id)
        return f.get("action") if f else None

    # ------------------------------------------------------------------ trava
    def _guarda(self, doc: BeautifulSoup | None, dados: dict[str, str],
                permitir: tuple[str, ...] = ()) -> None:
        """Trava de escrita. `permitir` libera palavras UMA A UMA, para navegação.

        Ex.: o botão "Novo Exame" só RENDERIZA o formulário de requisição — não
        grava nada —, mas cai na palavra "novo". Quem precisa dele passa
        `permitir=("novo",)`, e todo o resto (salvar/encerrar/...) segue travado.
        """
        if not self.somente_leitura:
            return
        proibidas = tuple(p for p in PALAVRAS_ESCRITA if p not in permitir)
        for chave in dados:
            if any(p in chave.lower() for p in proibidas):
                raise BloqueioEscrita(f"parâmetro suspeito de escrita: {chave}")
            if doc is None:
                continue
            el = doc.find(attrs={"name": chave}) or doc.find(id=chave)
            if el is None:
                continue
            rotulo = " ".join(filter(None, [
                el.get("value", ""), el.get("title", ""), el.get_text(" ", strip=True),
            ])).lower()
            if any(p in rotulo for p in proibidas):
                raise BloqueioEscrita(f"componente '{chave}' tem rótulo de escrita: {rotulo[:80]!r}")

    # ------------------------------------------------------------- transporte
    def get(self, caminho: str, nome: str = "get"):
        r = self.http.get(self._url(caminho))
        self._dump(nome, r.text)
        return r

    def post_form(self, doc: BeautifulSoup, form_id: str, extras: dict[str, str],
                  nome: str = "post", referer: str | None = None,
                  permitir: tuple[str, ...] = ()):
        dados = self.campos(doc, form_id)
        dados.update(extras)
        self._guarda(doc, extras, permitir)
        act = self.action(doc, form_id)
        if not act:
            raise RuntimeError(f"form '{form_id}' não encontrado na resposta")
        h = {"Referer": referer} if referer else {}
        r = self.http.post(self._url(act), data=dados, headers=h)
        self._dump(nome, r.text)
        return r

    def login(self):
        usuario = os.environ["SISCAN_USUARIO"]
        # A tela tem onsubmit="cifrar()", que faz SHA-256 hex da senha. Senha crua
        # devolve "e-mail ou senha incorretos" — idêntico a credencial errada.
        senha = hashlib.sha256(os.environ["SISCAN_SENHA"].encode("utf-8")).hexdigest()
        r = self.get("/login.jsf", "login")
        doc = self.sopa(r)
        submit = doc.find("input", attrs={"type": "submit"}, id=lambda _: True)
        f = doc.find("form", id="formLogin")
        submit = f.find("input", attrs={"type": "submit"}) if f else submit
        nome_submit = submit.get("name") if submit else "j_id34"
        return self.post_form(
            doc, "formLogin",
            {"email": usuario, "senha": senha,
             nome_submit: submit.get("value", "Acessar")},
            "pos-login", referer=self._url("/login.jsf"),
        )

    def post_a4j(self, doc: BeautifulSoup, form_id: str, extras: dict[str, str],
                 parametros: dict[str, str], nome: str = "a4j",
                 permitir: tuple[str, ...] = ()):
        """Reproduz `A4J.AJAX.Submit` — submit parcial do RichFaces.

        Necessário mesmo quando o submit completo carrega o mesmo campo: em
        JSF 1.2 a fase de validação roda ANTES do Update Model, então um
        validador cruzado (ex.: data final × data inicial) lê no bean o valor
        ANTIGO. No navegador isso passa despercebido porque o `onblur` já fez
        este round-trip antes do clique em Pesquisar.

        `ajaxSingle` restringe o processamento ao componente indicado.
        """
        dados = self.campos(doc, form_id)
        dados.update(extras)
        dados.update(parametros)
        dados["AJAXREQUEST"] = "_viewRoot"
        self._guarda(doc, extras, permitir)
        act = self.action(doc, form_id)
        r = self.http.post(
            self._url(act), data=dados,
            headers={"X-Requested-With": "XMLHttpRequest"},
        )
        self._dump(nome, r.text)
        return r

    def navegar(self, doc: BeautifulSoup, rotulo: str, nome: str = "tela"):
        """Abre uma tela CLICANDO no item de menu.

        NUNCA trocar isto por um GET na URL da tela. Medido em 07/08/2026: o GET
        direto em `/visao/exame/pesquisarExame.jsf` devolve HTTP 200 com a tela
        *meio inicializada* — sem o checkbox de tipo de exame e com o combo UF
        vazio. Postar nela os ids colhidos da tela boa gera erro 500 no servidor.
        """
        alvo = None
        for span in doc.find_all("span", class_="rich-menu-item-label"):
            if span.get_text(" ", strip=True).upper() == rotulo.upper():
                alvo = span.get("id", "").removesuffix(":anchor")
                break
        if not alvo:
            raise RuntimeError(f"item de menu '{rotulo}' não encontrado")
        form = doc.find("span", id=f"{alvo}:anchor").find_parent("form")
        fid = form.get("id")
        return self.post_form(
            doc, fid,
            {f"{fid}:_link_hidden_": alvo, f"{alvo}:hidden": alvo},
            nome,
        )

    def close(self) -> None:
        self.http.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def aplicar_a4j(base: BeautifulSoup, parcial: BeautifulSoup) -> BeautifulSoup:
    """Aplica a resposta A4J sobre o documento da tela, como o RichFaces faz.

    Medido em 22/09/2026, na tela de Novo Exame: a resposta de um `A4J.AJAX.Submit`
    NÃO é a tela inteira — é só o que está listado em
    `<meta name="Ajax-Update-Ids" content="j_id42,frm:pnlExame,...">`. Quem lê a
    resposta crua não encontra `<form id="frm">` e conclui, errado, que a ação não
    funcionou. Pior: postar a resposta crua como se fosse a tela perde todos os
    campos fora da região atualizada.

    Aqui: cada região citada é substituída dentro do documento base, e o
    `javax.faces.ViewState` passa a ser o NOVO (bloco `ajax-view-state`) — usar o
    anterior faz o JSF restaurar a view velha e descartar em silêncio o que o A4J
    mudou.
    """
    ids: list[str] = []
    for m in parcial.find_all("meta", attrs={"name": "Ajax-Update-Ids"}):
        ids += [i.strip() for i in (m.get("content") or "").split(",") if i.strip()]
    for i in dict.fromkeys(ids):
        novo = parcial.find(id=i)
        antigo = base.find(id=i)
        if novo is None or antigo is None:
            continue
        antigo.replace_with(copy.copy(novo))
    vs = parcial.find("span", id="ajax-view-state")
    valor = None
    if vs is not None:
        campo = vs.find("input", {"name": "javax.faces.ViewState"})
        valor = campo.get("value") if campo else None
    valor = valor or (parcial.find("input", {"name": "javax.faces.ViewState"}) or {}).get("value")
    if valor:
        for e in base.find_all("input", {"name": "javax.faces.ViewState"}):
            e["value"] = valor
    return base


def mensagens(doc: BeautifulSoup) -> list[str]:
    """Mensagens do JSF (erro/aviso) — no SER é quem denuncia truncamento."""
    out: list[str] = []
    for cls in ("rich-messages", "rich-message", "mensagem", "erro", "alert"):
        for e in doc.find_all(class_=re.compile(cls)):
            t = e.get_text(" ", strip=True)
            if t:
                out.append(t)
    return out
