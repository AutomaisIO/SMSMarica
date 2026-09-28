"""Cliente base do laboratório Prime Saúde Mental (Eco Sistemas — `marica.ecosistemas.com.br/SaudeMental`).

Cópia do cliente de `Automais.prime/` (que porta o método de `Automais.klinikos/`) — mesmo fornecedor, mesma família WebForms:
sondas `probe_*.py`, cliente httpx com Referer automático, trava de somente-leitura, sessão
persistida em `capturas/` para não relogar (sessão única por usuário derruba a outra estação).

Medido sem autenticar em 16/09/2026:
- nginx/1.18.0 (Ubuntu) na frente, `X-AspNet-Version: 4.0.30319` — ASP.NET WebForms 4.0.
- Hospedado pela Eco (`marica.ecosistemas.com.br`), NÃO em `*.smsmarica.online` como o Klinikos.
- Raiz da aplicação `/SaudeMental`; build `2025.08.0.18`. Cookie de auth `.Eco.SL.AtencaoBasica_ASPXAUTH`
  (sem o sufixo `_niteroi` do Prime). Gate pós-login só com `ddlUnidade` (3 CAPS).
- Form de login `formLogin` (Klinikos: `form1`) com os MESMOS campos:
  `LoginView1$lgAcesso$UserName`, `LoginView1$lgAcesso$Password`,
  `LoginView1$lgAcesso$LoginButton=ENTRAR` + hidden do WebForms + `hidEnderecoPortalAcesso`.

Trava de somente-leitura: nome de parâmetro de POST e valor de `__EVENTTARGET` não podem ter
verbo de escrita, salvo os LIBERADOS pontualmente (login, confirmação de sessão, gate de local).
"""

from __future__ import annotations

import json
import os
import pathlib
import re
from urllib.parse import urljoin

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

RAIZ = pathlib.Path(__file__).resolve().parent.parent
load_dotenv(RAIZ / ".env")

CAP = RAIZ / "capturas"
CAP.mkdir(exist_ok=True)

BASE = os.environ.get("SAUDEMENTAL_BASE", "https://marica.ecosistemas.com.br").rstrip("/")
APP = "/" + os.environ.get("SAUDEMENTAL_APP", "/SaudeMental").strip("/")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

URL_LOGIN = f"{APP}/login.aspx"
URL_HOME = f"{APP}/AtencaoBasica/Default.aspx"

ESCRITA = re.compile(
    r"(salvar|gravar|confirmar|inserir|incluir|excluir|remover|deletar|apagar|cancelar|"
    r"agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|executar|"
    r"efetivar|finalizar|aprovar|reprovar|transferir|enviar|submeter|registrar|"
    r"alterar|atualizar|editar|novo|delete|update|insert|save)", re.I)
LIBERADOS: set[str] = set()


class TravaEscrita(SystemExit):
    pass


def guardar(dados: dict) -> dict:
    for k, v in dados.items():
        if k in LIBERADOS:
            continue
        # Hidden `*_ClientState` vazio do Telerik (ex. `rwinCookie_C_rbSalvar_ClientState`, do
        # master page) só guarda estado visual; o navegador sempre o manda. Não é ação.
        if k.endswith("_ClientState") and not v:
            continue
        if ESCRITA.search(k):
            raise TravaEscrita(f"TRAVA: POST recusado — parâmetro de escrita: {k!r}")
        if k == "__EVENTTARGET" and v and ESCRITA.search(str(v)):
            raise TravaEscrita(f"TRAVA: POST recusado — __EVENTTARGET de escrita: {v!r}")
    return dados


# --------------------------------------------------------------------------- parsers

def sopa(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def _form(d: BeautifulSoup, form_id: str | None):
    return (d.find("form", id=form_id) if form_id else None) or d.find("form")


def hidden_do_form(html: str, form_id: str | None = None) -> dict:
    f = _form(sopa(html), form_id)
    out: dict[str, str] = {}
    if f:
        for i in f.find_all("input", attrs={"type": "hidden"}):
            if i.get("name"):
                out[i["name"]] = i.get("value") or ""
    return out


def campos_todos(html: str, form_id: str | None = None) -> dict:
    """Campos submissíveis como o navegador (sem botões; select sem opção não vai)."""
    f = _form(sopa(html), form_id)
    out: dict[str, str] = {}
    if not f:
        return out
    for i in f.find_all("input"):
        n, t = i.get("name"), (i.get("type") or "text").lower()
        if not n or t in {"submit", "button", "image", "reset", "file"}:
            continue
        if t in {"checkbox", "radio"} and not i.has_attr("checked"):
            continue
        out[n] = i.get("value") or ""
    for s in f.find_all("select"):
        o = s.find("option", selected=True) or s.find("option")
        if s.get("name") and o is not None:
            out[s["name"]] = o.get("value") or ""
    for t in f.find_all("textarea"):
        if t.get("name"):
            out[t["name"]] = t.get_text() or ""
    return out


def action_do_form(html: str, base_url: str, form_id: str | None = None) -> str:
    f = _form(sopa(html), form_id)
    return urljoin(base_url, (f.get("action") if f else None) or base_url)


_RE_ALVO = re.compile(r"""["'(]([^"'()\s]+\.(?:aspx|ashx|asmx|svc|axd)[^"'()\s]*)""", re.I)


def links_aspx(html: str, base_url: str) -> list[str]:
    achados = set(_RE_ALVO.findall(html))
    return sorted(urljoin(base_url, a.replace("&amp;", "&")) for a in achados)


def eh_sessao_expirada(r: httpx.Response) -> bool:
    u = str(r.url).lower()
    return "sessaoexpirada" in u or "/login.aspx" in u


# ----------------------------------------------------------------------------- sessão

class SaudeMentalSession:
    """httpx com Referer automático (= última URL), como um navegador."""

    ARQ_SESSAO = CAP / "sessao.json"

    def __init__(self, timeout: float = 90.0):
        self.c = httpx.Client(
            base_url=BASE,
            headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9",
                     "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"},
            timeout=timeout, follow_redirects=True, verify=True)
        self.ref: str | None = None
        self.logado = False

    def get(self, url: str, **kw) -> httpx.Response:
        h = kw.pop("headers", {}) or {}
        if self.ref:
            h.setdefault("Referer", self.ref)
        r = self.c.get(url, headers=h, **kw)
        self.ref = str(r.url)
        return r

    def post(self, url: str, data: dict | None = None, *, json=None, ajax: bool = False, **kw) -> httpx.Response:
        h = kw.pop("headers", {}) or {}
        if self.ref:
            h.setdefault("Referer", self.ref)
        if ajax:
            h.setdefault("X-Requested-With", "XMLHttpRequest")
        if data is not None:
            data = guardar(data)
        r = self.c.post(url, data=data, json=json, headers=h, **kw)
        self.ref = str(r.url)
        return r

    def postback(self, url: str, html: str, *, target: str = "", argument: str = "",
                 extra: dict | None = None, ajax: bool = False) -> httpx.Response:
        dados = campos_todos(html)
        dados["__EVENTTARGET"] = target
        dados["__EVENTARGUMENT"] = argument
        if extra:
            dados.update(extra)
        return self.post(action_do_form(html, url), dados, ajax=ajax)

    def close(self) -> None:
        self.c.close()

    def __enter__(self) -> "SaudeMentalSession":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # -- login ----------------------------------------------------------------------
    def login(self) -> httpx.Response:
        usuario = os.environ.get("SAUDEMENTAL_USUARIO")
        senha = os.environ.get("SAUDEMENTAL_SENHA")
        if not usuario or not senha:
            raise SystemExit("Defina SAUDEMENTAL_USUARIO e SAUDEMENTAL_SENHA no .env (gitignored).")

        lg = self.get(URL_LOGIN)
        html = lg.text
        (CAP / "login_form.html").write_text(html, encoding="utf-8")
        if "LoginView1$lgAcesso$UserName" not in html:
            raise SystemExit("form de login não encontrado — ver capturas/login_form.html")

        dados = hidden_do_form(html, "formLogin")
        dados.update({
            "LoginView1$lgAcesso$UserName": usuario,
            "LoginView1$lgAcesso$Password": senha,
            "LoginView1$lgAcesso$LoginButton": "ENTRAR",
        })
        LIBERADOS.add("LoginView1$lgAcesso$LoginButton")
        r = self.post(action_do_form(html, str(lg.url), "formLogin"), dados)
        (CAP / "login_resposta.html").write_text(r.text, encoding="utf-8")

        # Klinikos (mesmo fornecedor): usuário logado em outra estação → a própria login.aspx
        # volta com btnConfirmarLogin/btnCancelarLogin. Confirmar derruba a outra sessão.
        if "btnConfirmarLogin" in r.text:
            print("  (login: usuário autenticado em outra estação — confirmando nesta; a outra cai)")
            conf = hidden_do_form(r.text)
            conf["btnConfirmarLogin"] = "CONFIRMA"
            LIBERADOS.add("btnConfirmarLogin")
            r = self.post(action_do_form(r.text, str(r.url)), conf)
            (CAP / "login_resposta.html").write_text(r.text, encoding="utf-8")

        if "LoginView1$lgAcesso$Password" in r.text:
            raise SystemExit(f"login FALHOU (ficou em {r.url}) — ver capturas/login_resposta.html")
        self.logado = True
        self._salvar_sessao()
        return r

    # -- sessão persistida --------------------------------------------------------------
    def _salvar_sessao(self) -> None:
        cookies = {c.name: c.value for c in self.c.cookies.jar}
        self.ARQ_SESSAO.write_text(json.dumps(cookies), encoding="utf-8")

    def retomar_sessao(self) -> bool:
        if not self.ARQ_SESSAO.exists():
            return False
        try:
            cookies = json.loads(self.ARQ_SESSAO.read_text(encoding="utf-8"))
        except Exception:
            return False
        dominio = BASE.split("://", 1)[1]
        for k, v in cookies.items():
            self.c.cookies.set(k, v, domain=dominio, path="/")
        r = self.get(URL_HOME)
        if eh_sessao_expirada(r) or "LoginView1$lgAcesso$Password" in r.text:
            self.c.cookies.clear()
            return False
        self.logado = True
        return True

    def entrar(self) -> None:
        if self.retomar_sessao():
            print("  (sessão retomada de capturas/sessao.json — sem novo login)")
        else:
            r = self.login()
            self.selecionar_unidade(r)

    # -- gate de perfil + unidade ---------------------------------------------------------
    def selecionar_unidade(self, r: httpx.Response, perfil: str | None = None,
                           unidade: str | None = None) -> httpx.Response:
        """Medido 16/09/2026: o login devolve a própria Login.aspx com `LoginView1$ddlPerfil`
        (CBO do perfil, ex. 131210 Gerente de serviços de saúde) e `LoginView1$ddlUnidade`
        (GUIDs das 35 unidades), ambos AutoPostBack. O navegador faz 2 POSTs em cascata.
        Não grava dado clínico — só o contexto da sessão. `SAUDEMENTAL_PERFIL`/`SAUDEMENTAL_UNIDADE` no
        .env escolhem; sem eles, o primeiro de cada lista."""
        html, url = r.text, str(r.url)
        if "LoginView1$ddlPerfil" not in html and "LoginView1$ddlUnidade" not in html:
            return r
        d = sopa(html)

        def opcoes(nome: str) -> list[tuple[str, str]]:
            s = d.find("select", attrs={"name": nome})
            return [(o.get("value"), o.get_text(strip=True)) for o in s.find_all("option")
                    if o.get("value") not in (None, "-1")] if s else []

        # Medido: com um perfil só, `pnlPerfil` vem com display:none e o perfil já está resolvido
        # no servidor. Mandar `ddlPerfil=-1` (ou postback do perfil) faz a tela voltar igual,
        # sem erro. O que passa é UM postback de `ddlUnidade` SEM o campo ddlPerfil no corpo
        # → 302 para `AtencaoBasica/Default.aspx`.
        # Saúde Mental (medido 16/09/2026): não há ddlPerfil nenhum, só ddlUnidade (3 CAPS).
        perfil_visivel = ("LoginView1$ddlPerfil" in html
                          and "$('#LoginView1_pnlPerfil').css('display', 'none')" not in html)
        dados = campos_todos(html, "formLogin")
        if perfil_visivel:
            perfil = perfil or os.environ.get("SAUDEMENTAL_PERFIL") or (opcoes("LoginView1$ddlPerfil") or [("", "")])[0][0]
            r = self.postback(url, html, target="LoginView1$ddlPerfil", extra={"LoginView1$ddlPerfil": perfil})
            html, url, d = r.text, str(r.url), sopa(r.text)
            (CAP / "gate_passo1.html").write_text(html, encoding="utf-8")
            dados = campos_todos(html, "formLogin")
        else:
            dados.pop("LoginView1$ddlPerfil", None)
        uns = opcoes("LoginView1$ddlUnidade")
        unidade = unidade or os.environ.get("SAUDEMENTAL_UNIDADE") or uns[0][0]
        print(f"  (gate: unidade {dict(uns).get(unidade, unidade)})")
        dados.update({"__EVENTTARGET": "LoginView1$ddlUnidade", "__EVENTARGUMENT": "",
                      "LoginView1$ddlUnidade": unidade})
        r = self.post(action_do_form(html, url, "formLogin"), dados)
        (CAP / "gate_resposta.html").write_text(r.text, encoding="utf-8")
        if "LoginView1$ddlUnidade" in r.text:
            raise SystemExit("gate de perfil/unidade não passou — ver capturas/gate_resposta.html")
        self._salvar_sessao()
        return r
