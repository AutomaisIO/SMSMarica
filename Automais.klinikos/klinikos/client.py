"""Cliente base do laboratório Klinikos (Eco Sistemas — `klinikosconde.smsmarica.online`).

Porta o MÉTODO dos laboratórios `Automais.SER/` e `Automais.SERNIT/` (sondas `probe_*.py`,
cliente httpx com Referer automático, trava de somente-leitura) para um alvo diferente: o
objetivo aqui NÃO é regulação, é **mapear os endpoints que a web do Klinikos usa** para
substituir a leitura direta do SQL Server (`KlinikosImportacaoStrategy`) por acesso "como
usuário". Nada abaixo é presumido: o que está escrito foi medido contra a instância real em
16/09/2026 (ver docs/APRENDIZADOS.md).

Stack medida sem autenticar:
- ASP.NET WebForms 4.0 (`X-AspNet-Version: 4.0.30319`), `__VIEWSTATE` + `__EVENTVALIDATION`,
  Telerik (`Telerik.Web.UI.WebResource.axd`, RadGrid/Tabstrip/RadSiteMap nos temas).
- Build `K.2024.09.2.1` (rodapé da tela de login). Fornecedor: ecosistemas.com.br.
- Sem cookie: qualquer `.aspx` protegido responde 302 → `Share/Erros/sessaoexpirada.aspx`;
  a raiz `/KlinikosNet/` responde 302 → `Login.aspx?ReturnUrl=...`.
- Cookie de sessão: `ASP.NET_SessionId` (HttpOnly, SameSite=Lax). O jar do httpx cuida.
- Form de login (`form1`): `LoginView1$lgAcesso$UserName`, `LoginView1$lgAcesso$Password`,
  botão `LoginView1$lgAcesso$LoginButton` (value `ENTRAR`) + os hidden do WebForms.
- Swagger/API REST: 34 caminhos óbvios respondem 404 sem login. `/KlinikosNet/WebServices/`
  existe como pasta (301). Verificar de novo LOGADO (probe_swagger.py).

Trava de somente-leitura (duas camadas, igual ao SER): nome de parâmetro de POST e valor de
`__EVENTTARGET` não podem conter verbo de escrita. Login e postbacks de pesquisa/relatório são
POST e são permitidos — não alteram dado.
"""

from __future__ import annotations

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

BASE = os.environ.get("KLINIKOS_BASE", "https://klinikosconde.smsmarica.online").rstrip("/")
# Raiz da aplicação: `/KlinikosNet` no Conde; `/UPA24H` na UPA Maricá e em Santa Rita
# (medido 16/09/2026 — `https://upa24h.smsmarica.online/` → 302 `/UPA24H/`).
APP = "/" + os.environ.get("KLINIKOS_APP", "/KlinikosNet").strip("/")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

URL_LOGIN = f"{APP}/Login.aspx"
URL_HOME = f"{APP}/Default.aspx"
URL_SESSAO_EXPIRADA = "sessaoexpirada.aspx"

# Verbos de escrita — recusados no NOME de qualquer parâmetro de POST e no VALOR de
# __EVENTTARGET, salvo os explicitamente liberados. Ampliar LIBERADOS com cautela.
ESCRITA = re.compile(
    r"(salvar|gravar|confirmar|inserir|incluir|excluir|remover|deletar|apagar|cancelar|"
    r"agendar|marcar|desmarcar|autorizar|devolver|encaminhar|executar|"
    r"efetivar|finalizar|aprovar|reprovar|transferir|submeter|registrar|"
    r"atualizar|editar)", re.I)
# NB: tokens ingleses curtos (save/novo/insert/update/delete) e alguns pt (reservar, negar,
# enviar, alterar, novo) foram retirados por casarem DENTRO de palavras pt-BR legítimas —
# "responSAVEl", "reNOVAr", "enVIARias", "inALTERAdo". A guarda real é o __EVENTTARGET (a ação
# do postback), verificado à parte; nomes de campo passivos não são escrita.
LIBERADOS: set[str] = set()


class TravaEscrita(SystemExit):
    pass


def guardar(dados: dict) -> dict:
    """Trava camada 1: recusa parâmetro (ou __EVENTTARGET) com verbo de escrita."""
    for k, v in dados.items():
        if k in LIBERADOS:
            continue
        if ESCRITA.search(k):
            raise TravaEscrita(f"TRAVA: POST recusado — parâmetro de escrita: {k!r}")
        if k == "__EVENTTARGET" and v and ESCRITA.search(str(v)):
            raise TravaEscrita(f"TRAVA: POST recusado — __EVENTTARGET de escrita: {v!r}")
    return dados


# --------------------------------------------------------------------------- parsers

def sopa(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def hidden_do_form(html: str, form_id: str = "form1") -> dict:
    """Todos os hidden do form (`__VIEWSTATE`, `__EVENTVALIDATION`, ...)."""
    d = sopa(html)
    f = d.find("form", id=form_id) or d.find("form")
    out: dict[str, str] = {}
    if f:
        for i in f.find_all("input", attrs={"type": "hidden"}):
            if i.get("name"):
                out[i["name"]] = i.get("value") or ""
    return out


def campos_todos(html: str, form_id: str = "form1") -> dict:
    """Todos os campos submissíveis de um form, como o navegador (sem os botões submit)."""
    d = sopa(html)
    f = d.find("form", id=form_id) or d.find("form")
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
        # Select SEM opção não vai no POST (o navegador também não manda): mandar "" dispara
        # "Invalid postback or callback argument" da validação de eventos (medido 16/09/2026).
        o = s.find("option", selected=True) or s.find("option")
        if s.get("name") and o is not None:
            out[s["name"]] = o.get("value") or ""
    for t in f.find_all("textarea"):
        if t.get("name"):
            out[t["name"]] = t.get_text() or ""
    return out


def action_do_form(html: str, base_url: str, form_id: str = "form1") -> str:
    d = sopa(html)
    f = d.find("form", id=form_id) or d.find("form")
    return urljoin(base_url, (f.get("action") if f else None) or base_url)


_RE_ALVO = re.compile(r"""["'(]([^"'()\s]+\.(?:aspx|ashx|asmx|svc|axd)[^"'()\s]*)""", re.I)


def links_aspx(html: str, base_url: str) -> list[str]:
    """Todos os alvos `.aspx`/`.ashx`/`.asmx`/`.svc` referenciados na página (href, src, JS)."""
    achados = set(_RE_ALVO.findall(html))
    return sorted(urljoin(base_url, a.replace("&amp;", "&")) for a in achados)


def eh_sessao_expirada(r: httpx.Response) -> bool:
    u = str(r.url).lower()
    return URL_SESSAO_EXPIRADA in u or "/login.aspx" in u


# ----------------------------------------------------------------------------- sessão

class KlinikosSession:
    """httpx com Referer automático (= última URL), como um navegador."""

    def __init__(self, timeout: float = 90.0):
        self.c = httpx.Client(
            base_url=BASE,
            headers={
                "User-Agent": UA,
                "Accept-Language": "pt-BR,pt;q=0.9",
                "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            },
            timeout=timeout,
            follow_redirects=True,
            verify=True,
        )
        self.ref: str | None = None
        self.logado = False

    # -- transporte ------------------------------------------------------------------
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
        """Replica um `__doPostBack(target, argument)` a partir da página renderizada."""
        dados = campos_todos(html)
        dados["__EVENTTARGET"] = target
        dados["__EVENTARGUMENT"] = argument
        if extra:
            dados.update(extra)
        return self.post(action_do_form(html, url), dados, ajax=ajax)

    def close(self) -> None:
        self.c.close()

    def __enter__(self) -> "KlinikosSession":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # -- login ----------------------------------------------------------------------
    def login(self) -> httpx.Response:
        """Entra na sessão. Se `KLINIKOS_COOKIE` estiver no .env, ADOTA a sessão do navegador
        do operador (valor de `ASP.NET_SessionId`, copiado do DevTools → Application → Cookies)
        em vez de logar de novo — evita derrubar a janela dele. Senão, POST no form de login."""
        cookie = (os.environ.get("KLINIKOS_COOKIE") or "").strip()
        if cookie:
            self.c.cookies.set("ASP.NET_SessionId", cookie, domain=BASE.split("://", 1)[1], path="/")
            r = self.get(URL_HOME)
            (CAP / "login_resposta.html").write_text(r.text, encoding="utf-8")
            if eh_sessao_expirada(r):
                raise SystemExit("KLINIKOS_COOKIE não vale mais (caiu em sessão expirada/login) — "
                                 "copie de novo o ASP.NET_SessionId do navegador.")
            self.logado = True
            return r

        usuario = os.environ.get("KLINIKOS_USUARIO")
        senha = os.environ.get("KLINIKOS_SENHA")
        if not usuario or not senha:
            raise SystemExit("Defina KLINIKOS_USUARIO e KLINIKOS_SENHA no .env (gitignored).")

        lg = self.get(URL_LOGIN, params={"ReturnUrl": f"{APP}/"})
        html = lg.text
        (CAP / "login_form.html").write_text(html, encoding="utf-8")
        if "LoginView1$lgAcesso$UserName" not in html:
            raise SystemExit("form de login não encontrado — ver capturas/login_form.html")

        dados = hidden_do_form(html)
        dados.update({
            "LoginView1$lgAcesso$UserName": usuario,
            "LoginView1$lgAcesso$Password": senha,
            "LoginView1$lgAcesso$LoginButton": "ENTRAR",
        })
        LIBERADOS.add("LoginView1$lgAcesso$LoginButton")
        r = self.post(action_do_form(html, str(lg.url)), dados)
        (CAP / "login_resposta.html").write_text(r.text, encoding="utf-8")

        # Medido em 16/09/2026: se o MESMO usuário já está logado em outra estação, o Klinikos
        # devolve a própria Login.aspx com "O seu login está autenticado em outra estação...
        # Confirma o login nesta estação?" e dois submits: btnConfirmarLogin=CONFIRMA /
        # btnCancelarLogin=CANCELA. Confirmar DERRUBA a outra sessão (é o que o operador
        # autorizou ao dar a credencial do laboratório). Por isso a sessão é persistida em
        # capturas/sessao.json — para logar uma vez só, não a cada sonda.
        # Senha errada: o Klinikos devolve a própria Login com "Usuário ou Senha Inválido".
        # (Medido: o `123` vale no Conde e NÃO nas UPAs.) Detectar ANTES de qualquer confirm.
        if re.search(r"Usu.rio ou Senha Inv.lid", r.text, re.I):
            raise SystemExit("login FALHOU: usuário ou senha inválido nesta instância.")

        # Sessão em outra estação: detectar pela MENSAGEM visível, não pelo botão — o markup do
        # btnConfirmarLogin está SEMPRE na página (painel oculto). Usar a presença do botão como
        # sinal fazia o cliente "confirmar" um login que na verdade falhou e cair no Erro.aspx.
        if re.search(r"autenticado em outra esta|Confirma o login nesta esta", r.text, re.I):
            print("  (login: usuário autenticado em outra estação — confirmando nesta; a outra cai)")
            conf = hidden_do_form(r.text)
            conf["btnConfirmarLogin"] = "CONFIRMA"
            LIBERADOS.add("btnConfirmarLogin")
            r = self.post(action_do_form(r.text, str(r.url)), conf)
            (CAP / "login_resposta.html").write_text(r.text, encoding="utf-8")

        if "/login.aspx" in str(r.url).lower() or "LoginView1$lgAcesso$Password" in r.text:
            raise SystemExit(f"login FALHOU (ficou em {r.url}) — ver capturas/login_resposta.html")
        self.logado = True
        self._salvar_sessao()
        return r

    # -- sessão persistida --------------------------------------------------------------
    # Uma sessão persistida POR INSTÂNCIA (Conde, UPA, Santa Rita são hosts diferentes).
    ARQ_SESSAO = CAP / f"sessao_{BASE.split('://', 1)[1].split('.')[0]}.json"

    def _salvar_sessao(self) -> None:
        import json
        cookies = {c.name: c.value for c in self.c.cookies.jar}
        self.ARQ_SESSAO.write_text(json.dumps(cookies), encoding="utf-8")

    def retomar_sessao(self) -> bool:
        """Reaproveita os cookies gravados pela última sonda; True se a sessão ainda vale."""
        import json
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

    def entrar(self, local: bool = True) -> None:
        """Retoma a sessão gravada; só faz login de verdade se ela expirou. Depois passa pelo
        gate do local de atendimento (GravaCookie.aspx) se ainda não passou."""
        if self.retomar_sessao():
            print("  (sessão retomada de capturas/sessao.json — sem novo login)")
        else:
            self.login()
        if local:
            self.selecionar_local()

    # -- gate do local de atendimento --------------------------------------------------
    URL_GRAVA_COOKIE = f"{APP}/UPA/GravaCookie.aspx"

    def selecionar_local(self, porta: str | None = None, administrativo: bool = True) -> None:
        """Medido em 16/09/2026: depois do login, TODA tela protegida redireciona para
        `UPA/GravaCookie.aspx` até o usuário escolher o local de atendimento. O form tem
        `ddlPortadeEntrada` (GUIDs: SUTURA, URGÊNCIA E EMERGÊNCIA, MATERNIDADE, PEDIATRIA,
        INTERNAÇÃO...), `ddlLocalFisico` (carregado por postback em cascata) e o checkbox
        `ckbAcessoAdministrativo`; o botão é o ImageButton `imbSalvar`.

        Não é escrita de dado clínico — grava o cookie `_codigo_unidade`/vínculo do local na
        sessão, exatamente o que o operador faz ao entrar. Por isso `imbSalvar` é liberado
        da trava SÓ aqui. Default: acesso administrativo (o rodapé da sessão do operador
        mostrava "ACESSO ADMINISTRATIVO")."""
        r = self.get(URL_HOME)
        if "GravaCookie.aspx" not in str(r.url):
            return
        html = r.text
        url = str(r.url)
        pref = "ctl00$ctl00$contentCenter$contentCenterChild$"
        # Checkbox e porta de entrada têm AutoPostBack (onclick/onchange → __doPostBack). O
        # navegador faz dois POSTs: 1) o postback do controle, 2) o Salvar. Replicar igual.
        if administrativo:
            r = self.postback(url, html, target=pref + "ckbAcessoAdministrativo",
                              extra={pref + "ckbAcessoAdministrativo": "on"})
        elif porta:
            r = self.postback(url, html, target=pref + "ddlPortadeEntrada",
                              extra={pref + "ddlPortadeEntrada": porta})
        html, url = r.text, str(r.url)
        (CAP / "grava_cookie_passo1.html").write_text(html, encoding="utf-8")
        dados = campos_todos(html)
        if administrativo:
            dados[pref + "ckbAcessoAdministrativo"] = "on"
        dados[pref + "imbSalvar.x"] = "10"
        dados[pref + "imbSalvar.y"] = "10"
        LIBERADOS.update({pref + "imbSalvar.x", pref + "imbSalvar.y"})
        r = self.post(action_do_form(html, url), dados)
        (CAP / "grava_cookie_resposta.html").write_text(r.text, encoding="utf-8")
        r2 = self.get(URL_HOME)
        if "GravaCookie.aspx" in str(r2.url):
            raise SystemExit("gate do local de atendimento não passou — ver capturas/grava_cookie_resposta.html")
        print(f"  (local de atendimento definido; cookies: {sorted(self.c.cookies.keys())})")
        self._salvar_sessao()
