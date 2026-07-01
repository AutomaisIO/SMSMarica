"""Cliente HTTP do SISREG III.

Descobertas do recon (ver docs/APRENDIZADOS.md):
- Login é um POST simples para "/" (form ``formLogin``), sem widget de captcha.
- A senha é hasheada no CLIENTE: ``senha_256 = sha256(senha.toUpperCase())`` (hex),
  e o campo ``senha`` vai vazio. Reproduzimos isso com ``hashlib``.
- Campos do form: usuario, senha (vazio), senha_256, etapa=ACESSO, logout (vazio).
- Cookies de sessão: SESSION, ID, TS01... (WAF/BIG-IP). O httpx.Client mantém tudo.
"""

from __future__ import annotations

import hashlib
import json
import pathlib
from dataclasses import dataclass, field

import httpx

# User-Agent de navegador comum — alguns WAFs recusam clients "vazios".
_DEFAULT_UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
)


class SisregLoginError(RuntimeError):
    """Falha ao autenticar no SISREG (credencial inválida, captcha, etc.)."""


def hash_senha(senha: str) -> str:
    """Reproduz ``hex_sha256(senha.toUpperCase())`` do JS do SISREG.

    O JS faz ``senha.value.toUpperCase()`` antes do sha256. Uppercase em pt-BR
    aqui é o ``str.upper()`` padrão; senhas do SISREG costumam ser alfanuméricas.
    """
    return hashlib.sha256(senha.upper().encode("utf-8")).hexdigest()


@dataclass
class SisregClient:
    base_url: str = "https://sisregiii.saude.gov.br"
    timeout: float = 30.0
    _client: httpx.Client = field(init=False, repr=False)

    def __post_init__(self) -> None:
        self._client = httpx.Client(
            base_url=self.base_url,
            timeout=self.timeout,
            follow_redirects=True,
            headers={"User-Agent": _DEFAULT_UA},
        )

    # -- ciclo de vida -----------------------------------------------------
    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> "SisregClient":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # -- login -------------------------------------------------------------
    def priming(self) -> httpx.Response:
        """GET inicial para receber os cookies de sessão antes do POST."""
        return self._client.get("/")

    def login(self, usuario: str, senha: str, *, uppercase_usuario: bool = True) -> httpx.Response:
        """Autentica e retorna a resposta pós-login.

        Levanta ``SisregLoginError`` se a resposta parecer a própria tela de login
        (heurística: presença do form ``formLogin`` / campo ``senha_256``).
        """
        self.priming()

        user = usuario.upper() if uppercase_usuario else usuario
        data = {
            "usuario": user,
            "senha": "",
            "senha_256": hash_senha(senha),
            "etapa": "ACESSO",
            "logout": "",
        }
        resp = self._client.post("/", data=data)

        if not self._login_ok(resp):
            msg = self._extrair_mensagem(resp.text)
            raise SisregLoginError(
                f"Login não efetivado. Mensagem do SISREG: {msg!r}"
                if msg
                else "Login não efetivado — resposta ainda parece a tela de login "
                "(credencial inválida, bloqueio de WAF, ou captcha exigido)."
            )
        return resp

    def logout(self) -> httpx.Response:
        return self._client.post("/", data={"logout": "1", "etapa": "ACESSO"})

    # -- reuso de sessão (SISREG é sessão ÚNICA por operador) ---------------
    def esta_logado(self) -> bool:
        """Valida a sessão atual com um GET barato à home logada.

        Se a sessão expirou/foi derrubada, o SISREG redireciona para a tela de
        login em ``/``; ``_login_ok`` diferencia os dois casos.
        """
        try:
            r = self._client.get("/cgi-bin/index")
        except httpx.HTTPError:
            return False
        return self._login_ok(r)

    def dump_cookies(self) -> list[dict]:
        return [
            {"name": c.name, "value": c.value, "domain": c.domain, "path": c.path}
            for c in self._client.cookies.jar
        ]

    def load_cookies(self, cookies: list[dict]) -> None:
        for c in cookies:
            self._client.cookies.set(c["name"], c["value"], domain=c.get("domain", ""), path=c.get("path", "/"))

    def salvar_sessao(self, caminho: str | pathlib.Path) -> None:
        pathlib.Path(caminho).write_text(json.dumps(self.dump_cookies()), encoding="utf-8")

    def carregar_sessao(self, caminho: str | pathlib.Path) -> bool:
        p = pathlib.Path(caminho)
        if not p.exists():
            return False
        try:
            self.load_cookies(json.loads(p.read_text(encoding="utf-8")))
            return True
        except (json.JSONDecodeError, KeyError, OSError):
            return False

    def conectar(
        self,
        usuario: str,
        senha: str,
        *,
        session_file: str | pathlib.Path = "capturas/.sessao.json",
    ) -> str:
        """Garante sessão ativa REUSANDO o token salvo sempre que possível.

        Evita múltiplos ``login()`` (SISREG derruba a sessão anterior a cada
        novo login). Retorna 'reaproveitada' ou 'novo_login'.
        """
        if self.carregar_sessao(session_file) and self.esta_logado():
            return "reaproveitada"
        self.login(usuario, senha)
        self.salvar_sessao(session_file)
        return "novo_login"

    # -- helpers -----------------------------------------------------------
    def get(self, path: str, **kwargs) -> httpx.Response:
        return self._client.get(path, **kwargs)

    def post(self, path: str, **kwargs) -> httpx.Response:
        return self._client.post(path, **kwargs)

    @property
    def cookies(self) -> httpx.Cookies:
        return self._client.cookies

    @staticmethod
    def _login_ok(resp: httpx.Response) -> bool:
        """True se a resposta é o SISREG já logado.

        Sinais de sucesso (a tela logada é servida em ``/cgi-bin/index`` e traz o
        iframe principal ``f_main`` + a barra "Operador:/Perfil:/Unidade:"). A tela
        de login com falha volta em ``/`` com o ``<div id="mensagem">`` preenchido.
        """
        if "/cgi-bin/index" in str(resp.url):
            return True
        html = resp.text
        return 'id="f_main"' in html or 'href="?logout=1"' in html

    @staticmethod
    def _extrair_mensagem(html: str) -> str | None:
        """Extrai o texto do ``<div id="mensagem">`` (ex.: 'Login ou senha incorreto(s).')."""
        import re

        m = re.search(r'id="mensagem"[^>]*>(.*?)</DIV>', html, re.IGNORECASE | re.DOTALL)
        if not m:
            return None
        texto = re.sub(r"<[^>]+>", " ", m.group(1))
        texto = re.sub(r"\s+", " ", texto).strip()
        return texto or None
