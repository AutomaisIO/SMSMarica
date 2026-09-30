"""Cliente base do laboratório e-SUS São Gonçalo ("Novo Esus").

Medido contra o sistema real em 30/09/2026 (versão 2.8.4 do front). Ver docs/APRENDIZADOS.md.

Arquitetura do alvo (três origens, todas em saogoncalo.esusmais.com.br):

| porta | o que é                                   | autenticação                                   |
|-------|-------------------------------------------|------------------------------------------------|
| 8000  | SPA Vue 3 + element-plus (só estático)    | —                                              |
| 8001  | backend novo (Node): REST + `/graphql`     | `authorization: <token>` + `unithealth: <id>`  |
| 9001  | backend legado (PHP 7.4): `/<mod>/controller-<x>/<acao>` | `authorization: <token legado>` + `unithealth` |

O que o login faz (a mesma sequência do navegador):

1. `POST :8001/access-control/login-light` com `{namespaces, client, username, password}`.
   `client` é o código do cliente em minúsculas (`sgo`); `namespaces` é a lista de permissões
   que o front quer conhecer (guardada em `namespaces.json`, capturada do front). Devolve o
   token (UUID, **sem** prefixo `Bearer`), o usuário, a unidade padrão e a árvore de permissões.
2. `POST :9001/niveisacesso/login-sem-permissoes` com `{usuario, senha, cliente}` — dá o token
   do backend legado. As telas de fila/agendados ainda moram no legado.

A sessão vale 3600 s (`loginInfo.expireTime`). O front renova com
`GET :8001/access-control/refresh-token` + `GET :9001/niveisacesso/renovar-sessao`.
Logoff: `DELETE :8001/access-control/logoff`.

Trava de somente-leitura (três portas, três regras):

- **GraphQL**: só `query`. Documento com `mutation`/`subscription` é recusado.
- **Legado PHP**: o último segmento do caminho é a ação (`buscar`, `listar`, `salvar`...).
  Só passa ação que case `LEITURA_LEGADO`; qualquer outra é recusada.
- **REST novo**: GET livre; POST/PUT/DELETE só nos caminhos de `REST_LIBERADOS`.
"""

from __future__ import annotations

import json
import os
import pathlib
import re
from typing import Any

import httpx
from dotenv import load_dotenv

RAIZ = pathlib.Path(__file__).resolve().parent.parent
load_dotenv(RAIZ / ".env")

CAP = RAIZ / "capturas"
CAP.mkdir(exist_ok=True)

API = os.environ.get("ESUS_API", "https://saogoncalo.esusmais.com.br:8001").rstrip("/")
LEGADO = os.environ.get("ESUS_LEGADO", "https://saogoncalo.esusmais.com.br:9001").rstrip("/")
FRONT = "https://saogoncalo.esusmais.com.br:8000"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

NAMESPACES = json.loads((pathlib.Path(__file__).parent / "namespaces.json").read_text(encoding="utf-8"))

# Ações do legado que só leem. O legado usa POST para tudo, então a trava é pelo NOME da ação.
LEITURA_LEGADO = re.compile(
    r"^(buscar|listar|pesquisar|consultar|obter|carregar|exibir|visualizar|verificar|combo|combobox|"
    r"renovar-sessao|login-sem-permissoes)([-_a-z0-9]*)$", re.I)
# Mesmo começando com verbo de leitura, recusa se o nome embutir escrita (ex.: buscar-e-agendar).
# `comprovante`: gerar o comprovante pode carimbar "comprovante impresso = SIM" no agendamento
# (a tela de agendados filtra por isso) — efeito colateral no SG, então fica fora.
ESCRITA = re.compile(
    r"(salvar|gravar|incluir|inserir|alterar|editar|atualizar|excluir|remover|deletar|apagar|"
    r"cancelar|agendar|desagendar|marcar|desmarcar|transferir|efetivar|confirmar|regular|"
    r"mudar|resolver|cadastrar|enviar|imprimir|unificar|inativar|ativar|habilitar|comprovante|"
    r"importar|exportar|gerar)", re.I)

# REST novo (:8001): métodos de escrita só nestes caminhos (não alteram dado clínico).
REST_LIBERADOS = {
    ("POST", "/access-control/login-light"),
    ("POST", "/access-control/load-module-permissions-by-unit-id"),
    ("DELETE", "/access-control/logoff"),
}


class TravaLeitura(SystemExit):
    """Chamada recusada pela trava de somente-leitura."""


class ErroEsus(RuntimeError):
    """O e-SUS respondeu erro (HTTP ou `status:false` / `errors` no corpo)."""


def _guardar_legado(caminho: str) -> None:
    acao = caminho.rstrip("/").rsplit("/", 1)[-1]
    if not LEITURA_LEGADO.match(acao) or ESCRITA.search(acao):
        raise TravaLeitura(f"TRAVA: ação do legado recusada (não é leitura): {caminho!r}")


def _guardar_gql(query: str) -> None:
    # tira comentários e strings para não se enganar com "mutation" dentro de um literal
    limpo = re.sub(r'#[^\n]*|"(?:\\.|[^"\\])*"', "", query)
    if re.search(r"^\s*(mutation|subscription)\b", limpo, re.M | re.I):
        raise TravaLeitura("TRAVA: documento GraphQL com mutation/subscription recusado")


def _guardar_rest(metodo: str, caminho: str) -> None:
    if metodo.upper() == "GET":
        return
    if (metodo.upper(), caminho.split("?", 1)[0]) not in REST_LIBERADOS:
        raise TravaLeitura(f"TRAVA: {metodo} {caminho} recusado no REST novo")


class EsusSession:
    """Sessão logada nos dois backends do e-SUS de São Gonçalo."""

    def __init__(self, timeout: float = 120.0):
        cab = {
            "User-Agent": UA,
            "Accept": "application/json, text/plain, */*",
            "Content-Type": "application/json",
            "Origin": FRONT,
            "Referer": FRONT + "/",
        }
        self.api = httpx.Client(base_url=API, headers=cab, timeout=timeout)
        self.leg = httpx.Client(base_url=LEGADO, headers=cab, timeout=timeout)
        self.token: str | None = None
        self.token_legado: str | None = None
        self.unidade: int | None = None
        self.login_info: dict[str, Any] = {}

    # ------------------------------------------------------------------ infraestrutura

    def _cab(self, legado: bool = False) -> dict[str, str]:
        tok = self.token_legado if legado else self.token
        h: dict[str, str] = {}
        if tok:
            h["authorization"] = tok
        if self.unidade is not None:
            h["unithealth"] = str(self.unidade)
        return h

    def rest(self, metodo: str, caminho: str, *, json_body: Any = None, params: dict | None = None) -> Any:
        """Chamada ao REST novo (:8001). Devolve o JSON."""
        _guardar_rest(metodo, caminho)
        r = self.api.request(metodo, caminho, json=json_body, params=params, headers=self._cab())
        if r.status_code >= 400:
            raise ErroEsus(f"{metodo} {caminho} -> HTTP {r.status_code}: {r.text[:300]}")
        return r.json() if r.content else None

    def gql(self, query: str, variables: dict | None = None, operation_name: str | None = None) -> dict:
        """Query GraphQL (:8001/graphql). Devolve `data`; levanta ErroEsus se vier `errors`."""
        _guardar_gql(query)
        corpo: dict[str, Any] = {"query": query, "variables": variables or {}}
        if operation_name:
            corpo["operationName"] = operation_name
        r = self.api.post("/graphql", json=corpo, headers=self._cab())
        if r.status_code >= 400:
            raise ErroEsus(f"graphql {operation_name or ''} -> HTTP {r.status_code}: {r.text[:300]}")
        d = r.json()
        if d.get("errors"):
            raise ErroEsus(f"graphql {operation_name or ''}: {json.dumps(d['errors'], ensure_ascii=False)[:500]}")
        return d.get("data") or {}

    def legado(self, caminho: str, form: dict | None = None, **extra: Any) -> Any:
        """POST no legado PHP (:9001) no formato do front: `{"arrFormData": form, ...extra}`.

        O legado responde `{"status": bool, "dados": ..., "trace": ..., "meta": {...}}`.
        Devolve `dados`; `status:false` vira ErroEsus.
        """
        _guardar_legado(caminho)
        corpo: dict[str, Any] = {"arrFormData": form} if form is not None else {}
        corpo.update(extra)
        r = self.leg.post(caminho, json=corpo, headers=self._cab(legado=True))
        if r.status_code >= 400:
            raise ErroEsus(f"legado {caminho} -> HTTP {r.status_code}: {r.text[:300]}")
        d = r.json()
        if isinstance(d, dict) and d.get("status") is False:
            raise ErroEsus(f"legado {caminho}: {json.dumps(d, ensure_ascii=False)[:500]}")
        return d.get("dados") if isinstance(d, dict) and "dados" in d else d

    # --------------------------------------------------------------------------- login

    def login(self) -> dict:
        cliente = os.environ.get("ESUS_CLIENTE", "SGO")
        usuario = os.environ.get("ESUS_USUARIO")
        senha = os.environ.get("ESUS_SENHA")
        if not usuario or not senha:
            raise SystemExit("Defina ESUS_USUARIO e ESUS_SENHA no .env (gitignored).")

        r = self.api.post("/access-control/login-light", json={
            "namespaces": NAMESPACES,
            "client": cliente.lower(),
            "username": usuario,
            "password": senha,
        })
        corpo = r.json() if r.content else {}
        dados = (corpo or {}).get("data") or {}
        if r.status_code >= 400 or not dados.get("token"):
            (CAP / "login_falhou.json").write_text(r.text, encoding="utf-8")
            raise SystemExit(f"LOGIN FALHOU (HTTP {r.status_code}) — ver capturas/login_falhou.json")
        self.token = dados["token"]
        self.login_info = dados
        self.unidade = (dados.get("user") or {}).get("usu_id_unidades_saude_padrao")

        rl = self.leg.post("/niveisacesso/login-sem-permissoes",
                           json={"usuario": usuario, "senha": senha, "cliente": cliente.lower()})
        cl = rl.json() if rl.content else {}
        tok = cl.get("token") if isinstance(cl, dict) else None
        if not tok and isinstance(cl, dict) and isinstance(cl.get("dados"), dict):
            tok = cl["dados"].get("token")
        if not tok:
            raise SystemExit(f"login no legado não devolveu token (HTTP {rl.status_code})")
        self.token_legado = tok
        return dados

    def renovar(self) -> None:
        """Keep-alive dos dois backends (o front faz isso a cada navegação)."""
        self.api.get("/access-control/refresh-token", headers=self._cab())
        self.leg.get("/niveisacesso/renovar-sessao", headers=self._cab(legado=True))

    def trocar_unidade(self, uns_id: int) -> None:
        """Só muda o cabeçalho `unithealth` local. A unidade precisa estar em `unitHealths`."""
        if uns_id not in (self.login_info.get("unitHealths") or []):
            raise SystemExit(f"unidade {uns_id} não está entre as do usuário")
        self.unidade = uns_id

    def logoff(self) -> None:
        if self.token:
            try:
                self.rest("DELETE", "/access-control/logoff")
            except ErroEsus:
                pass
        self.token = self.token_legado = None

    def close(self) -> None:
        self.logoff()
        self.api.close()
        self.leg.close()

    def __enter__(self) -> "EsusSession":
        return self

    def __exit__(self, *exc) -> None:
        self.close()


def sessao(timeout: float = 120.0) -> EsusSession:
    """Atalho: sessão já logada nos dois backends."""
    s = EsusSession(timeout=timeout)
    s.login()
    return s


def linhas_e_total(dados: Any) -> tuple[list[dict], int]:
    """Normaliza as duas formas de busca paginada do legado (medidas em 30/09/2026):

    - fila (`controller-fila-*/buscar`):                `[[linhas...], "total"]`
    - agendados (`controller-paciente-agendado-*/buscar`): `{"recordSet": [...], "total": "N"}`

    Forma desconhecida levanta erro — devolver `[], 0` escondeu 61 agendados na 1ª medição.
    """
    if isinstance(dados, list) and len(dados) == 2 and isinstance(dados[0], list):
        return dados[0], int(dados[1] or 0)
    if isinstance(dados, dict) and isinstance(dados.get("recordSet"), list):
        return dados["recordSet"], int(dados.get("total") or 0)
    raise ErroEsus(f"forma de resposta paginada desconhecida: {type(dados).__name__} "
                   f"{list(dados)[:5] if isinstance(dados, dict) else ''}")


def paginar(s: EsusSession, caminho: str, form: dict, *, pagina: int = 1000,
            chave=("fil_id",)) -> tuple[list[dict], int]:
    """Busca paginada completa no legado: `limiteInicio` = offset, `limiteFim` = tamanho da página.

    SEMÂNTICA MEDIDA (30/09/2026): o `total` declarado conta registros ÚNICOS, mas o offset/limite
    conta linhas BRUTAS — e a lista de agendados repete linhas (2019: 1.405 brutas, 1.352 únicas =
    declarado). Então: paginar até uma página vir INCOMPLETA (nunca até offset ≥ total — isso perdeu
    3 agendamentos de mar/2019) e conferir ÚNICOS (pela `chave`) = declarado. Página grande (1000):
    a fila inteira vem numa requisição.

    `chave` = campos que identificam o registro: `("fil_id",)` na fila; na lista de agendados um
    pedido tem várias sessões, então `("fil_id", "eap_id", "data_hora_formatada")`.
    """
    vistos: dict[tuple, dict] = {}
    total = 0
    inicio = 0
    for _ in range(50):
        dados = s.legado(caminho, form | {"limiteInicio": inicio, "limiteFim": pagina},
                         toPrint=False, toCsv=False, toExcel=False)
        linhas, total = linhas_e_total(dados)
        for ln in linhas:
            vistos.setdefault(tuple(str(ln.get(c)) for c in chave), ln)
        inicio += pagina
        if len(linhas) < pagina:
            break
    if len(vistos) != total:
        raise ErroEsus(f"{caminho}: {len(vistos)} únicos ≠ declarado {total} — paginação não fechou")
    return list(vistos.values()), total
