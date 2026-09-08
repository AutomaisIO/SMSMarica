"""Cliente base do laboratório SERNIT (SER de Niterói).

Porta o método do `Automais.SER/` (SES-RJ), medido contra o SER de Niterói real em
25/08/2026. Mesma stack (JSF 1.2 + RichFaces/A4J 3.3.3 + Seam / WildFly 10), mas a
INSTÂNCIA é outra e o build é mais antigo (`2024-01-01-NITEROI`) — então os ids `j_idNN`
são diferentes dos do SER-RJ e **nunca** devem ser chumbados: resolva sempre por rótulo,
por `<select>` que oferece o valor esperado, ou lendo o script da própria página.

O que difere do SER-RJ e está medido (ver docs/APRENDIZADOS.md):

1. Cookie de balanceador é **`UCID`** (no SER-RJ é `SERVERID`). O jar do httpx cuida sozinho.
2. **O form de login não é servido direto.** `/ser/login` responde uma página "AGUARDE..."
   com meta-refresh para `logout.jsp`, que reflete de volta para `login` — um loop. O form
   real só aparece em `/ser/login` quando o **Referer** é `logout.jsp`. A sequência que
   funciona (a mesma do navegador): página protegida → `logout.jsp` → `login` → FORM.
   Por isso o cliente manda **Referer automático** (= última URL), como um navegador.
3. Botão *Entrar* tem id volátil (`login:j_id17` em 25/08) — lido do form, nunca fixado.
4. Só o módulo **`ambulatorial`** aparece para a credencial de Maricá.

Trava de somente-leitura: duas camadas, igual ao SER-RJ (nome do parâmetro + rótulo do
componente). Login e telas de *pesquisa* são POST e são permitidos (não alteram dado).
"""

from __future__ import annotations

import os
import pathlib
import re

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

RAIZ = pathlib.Path(__file__).resolve().parent.parent
load_dotenv(RAIZ / ".env")

CAP = RAIZ / "capturas"
CAP.mkdir(exist_ok=True)

BASE = os.environ.get("SERNIT_BASE", "https://regulacao.niteroi.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

# Uma tela protegida qualquer serve para "acender" a conversa Seam antes do logout/login.
URL_PROTEGIDA = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"
URL_SOLIC = "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam"

# Verbos de escrita — recusados no corpo de qualquer POST, salvo os explicitamente liberados.
ESCRITA = re.compile(
    r"(salvar|gravar|confirmar|inserir|incluir|excluir|remover|deletar|apagar|cancelar|"
    r"agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|executar|"
    r"efetivar|finalizar|aprovar|reprovar|transferir|solicitar|enviar|submeter|"
    r"registrar|followup|pendenciar)", re.I)
# Ids "falantes" que contêm verbo mas são de leitura (pesquisar/editar-aba). Ampliar com cautela.
LIBERADOS: set[str] = set()


def guardar(dados: dict) -> dict:
    """Trava de somente-leitura camada 1: recusa parâmetro com verbo de escrita no nome."""
    for k in dados:
        if k not in LIBERADOS and ESCRITA.search(k):
            raise SystemExit(f"TRAVA: POST recusado — parâmetro de escrita: {k!r}")
    return dados


# --------------------------------------------------------------------------- parsers

def sopa(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def viewstate(html: str) -> str | None:
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def hidden_do_form(html: str, form_id: str = "form0") -> dict:
    d = sopa(html)
    f = d.find("form", id=form_id)
    out: dict[str, str] = {}
    if f:
        for i in f.find_all("input", attrs={"type": "hidden"}):
            if i.get("name"):
                out[i["name"]] = i.get("value") or ""
    return out


def campos_todos(doc: BeautifulSoup, form_id: str = "form0") -> dict:
    """Todos os campos submissíveis de um form (inputs/selects/textareas), como o navegador."""
    f = doc.find("form", id=form_id)
    d: dict[str, str] = {}
    if not f:
        return d
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset"}:
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        d[n] = i.get("value") or ""
    for s in f.find_all("select"):
        if s.get("name"):
            o = s.find("option", selected=True) or s.find("option")
            d[s["name"]] = (o.get("value") if o else "") or ""
    for t in f.find_all("textarea"):
        if t.get("name"):
            d[t["name"]] = t.get_text() or ""
    return d


def seguir_redirect_a4j(sess: "SernitSession", html: str) -> str:
    """Se a resposta A4J embute <meta name="Location">, segue e devolve a página de destino."""
    m = re.search(r'<meta name="Location" content="([^"]+)"', html)
    if m:
        return sess.get(m.group(1).replace("&amp;", "&")).text
    return html


# ----------------------------------------------------------------------------- sessão

class SernitSession:
    """httpx com Referer automático (= última URL), como um navegador.

    O Referer é o que destrava o form de login do SERNIT (ver docstring do módulo).
    """

    def __init__(self, timeout: float = 60.0):
        self.c = httpx.Client(
            base_url=BASE,
            headers={
                "User-Agent": UA,
                "Accept-Language": "pt-BR,pt;q=0.9",
                "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            },
            timeout=timeout,
            follow_redirects=True,
        )
        self.ref: str | None = None
        self.logado = False

    def get(self, url: str, **kw) -> httpx.Response:
        h = kw.pop("headers", {}) or {}
        if self.ref:
            h.setdefault("Referer", self.ref)
        r = self.c.get(url, headers=h, **kw)
        self.ref = str(r.url)
        return r

    def post(self, url: str, data: dict, *, ajax: bool = False, **kw) -> httpx.Response:
        h = kw.pop("headers", {}) or {}
        if self.ref:
            h.setdefault("Referer", self.ref)
        if ajax:
            h.setdefault("X-Requested-With", "XMLHttpRequest")
        r = self.c.post(url, data=guardar(data), headers=h, **kw)
        self.ref = str(r.url)
        return r

    def post_escrita(self, url: str, data: dict, *, operacao: str, ajax: bool = False, **kw) -> httpx.Response:
        """Porta SEPARADA de escrita (espelho do SubmeterEscritaAsync do SER-RJ).

        NÃO passa pela trava de leitura `guardar()` — quem chama assume a responsabilidade e
        aplica a *trava invertida* (comparar o POST com o que a tela renderizou). Loga em
        destaque para que toda escrita seja rastreável por um grep no output.
        """
        print(f"  *** ESCRITA SERNIT: {operacao} *** (porta separada, sem trava de leitura)")
        h = kw.pop("headers", {}) or {}
        if self.ref:
            h.setdefault("Referer", self.ref)
        if ajax:
            h.setdefault("X-Requested-With", "XMLHttpRequest")
        r = self.c.post(url, data=data, headers=h, **kw)
        self.ref = str(r.url)
        return r

    def close(self) -> None:
        self.c.close()

    def __enter__(self) -> "SernitSession":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # ------------------------------------------------------------------ login

    def login(self) -> None:
        """Sequência medida: protegida → logout.jsp → login (com Referer) → POST login."""
        usuario = os.environ.get("SERNIT_USUARIO")
        senha = os.environ.get("SERNIT_SENHA")
        if not usuario or not senha:
            raise SystemExit("Defina SERNIT_USUARIO e SERNIT_SENHA no .env (gitignored).")

        self.get(URL_PROTEGIDA)      # acende a conversa Seam
        self.get("/ser/logout.jsp")  # invalida — vira o Referer que destrava o form
        lg = self.get("/ser/login").text
        d = sopa(lg)
        f = d.find("form", id="login")
        if not f or "login:username" not in lg:
            (CAP / "login_aguarde.html").write_text(lg, encoding="utf-8")
            raise SystemExit("form de login não veio (AGUARDE?) — ver capturas/login_aguarde.html")

        btn = f.find("input", attrs={"type": "submit"})
        dados = campos_todos(d, "login") | {
            "login": "login",
            "login:username": usuario,
            "login:password": senha,
            (btn.get("name") if btn else "login:entrar"): (btn.get("value") if btn else "Entrar"),
            "javax.faces.ViewState": viewstate(lg) or "j_id1",
        }
        r = self.post(f.get("action") or "/ser/login", dados)
        if 'id="login:username"' in r.text:
            (CAP / "login_falhou.html").write_text(r.text, encoding="utf-8")
            raise SystemExit("LOGIN FALHOU — ver capturas/login_falhou.html")
        self.logado = True

    # --------------------------------------------------------------- módulo

    def ativar_modulo(self, modulo: str = "ambulatorial") -> None:
        """Replica goModulo(): POST /ser/home com param1=<modulo> + AJAXREQUEST=<form> e segue o Location."""
        home = self.get("/ser/home.seam").text
        m = re.search(r'<form id="(j_id\d+)"[^>]*action="/ser/home"', home)
        if not m:
            raise SystemExit("form de módulo não encontrado na home")
        fid = m.group(1)
        dh = sopa(home)
        # o parâmetro do goModulo é o similarityGroupingId lido do próprio script (…:goModulo)
        gm = re.search(rf"'({re.escape(fid)}:goModulo)'", home)
        chave_gm = gm.group(1) if gm else f"{fid}:goModulo"
        dados = campos_todos(dh, fid) | {
            fid: fid,
            chave_gm: chave_gm,
            "param1": modulo,
            "AJAXREQUEST": fid,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": viewstate(str(dh.find("form", id=fid))) or "",
        }
        r = self.post("/ser/home", dados, ajax=True)
        loc = r.headers.get("location")
        if not loc:
            loc_meta = re.search(r'<meta name="Location" content="([^"]+)"', r.text)
            if loc_meta:
                loc = loc_meta.group(1).replace("&amp;", "&")
        if not loc:
            raise SystemExit(f"módulo {modulo!r} não ativou (sem redirect)")
        self.get(loc)  # ativa o módulo na sessão


def login_e_modulo(modulo: str = "ambulatorial", timeout: float = 60.0) -> SernitSession:
    """Atalho: sessão logada com o módulo já ativo, pronta para abrir telas."""
    s = SernitSession(timeout=timeout)
    s.login()
    s.ativar_modulo(modulo)
    return s
