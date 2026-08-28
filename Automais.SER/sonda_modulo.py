"""Por que o SER parou de ativar o módulo 'ambulatorial'?

O motor do servidor passou a falhar com "O SER não redirecionou após escolher o módulo
'ambulatorial'" — o POST do goModulo volta sem o header Location e sem redirect no corpo. Toda
consulta de cadastro pelo SER morre aí.

Esta sonda reproduz EXATAMENTE o que o servidor faz (mesmos campos, mesmo header) e mostra a
resposta crua: status, headers de redirecionamento, tamanho e as marcas do corpo. É leitura pura —
login e um POST de navegação, nada de escrita.

Uso:  python sonda_modulo.py
"""
from __future__ import annotations

import os
import pathlib
import re
import sys

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")

BASE = os.environ.get("SER_BASE_URL", "https://ser.saude.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/126.0 Safari/537.36")


def sopa(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def campos_do_form(doc: BeautifulSoup, form_id: str) -> dict[str, str]:
    """Todos os inputs do form — como o navegador enviaria (sem os disabled)."""
    form = doc.find("form", id=form_id)
    if form is None:
        return {}
    out: dict[str, str] = {}
    for el in form.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or el.has_attr("disabled"):
            continue
        if (el.get("type") or "").lower() in ("submit", "button", "image", "reset"):
            continue
        out[nome] = el.get("value") or ""
    return out


def viewstate(html: str) -> str | None:
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def marcas(corpo: str) -> str:
    baixo = corpo.lower()
    achados = []
    for marca in ("login:username", "sessão expirada", "sessao expirada", "recaptcha",
                  "<meta name=\"location\"", "ajax-response", "erro", "exception",
                  "acesso negado", "senha", "bloquead"):
        if marca in baixo:
            achados.append(marca)
    return ", ".join(achados) or "(nenhuma marca conhecida)"


def main() -> int:
    usuario, senha = os.environ.get("SER_USUARIO", ""), os.environ.get("SER_SENHA", "")
    if not usuario or not senha:
        print("!! SER_USUARIO/SER_SENHA ausentes no .env", file=sys.stderr)
        return 2

    with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                      follow_redirects=True, verify=False) as c:
        # 1) login
        pag = c.get("/ser/login").text
        d = sopa(pag)
        form_login = d.find("form", id="login")
        if form_login is None:
            print("!! a tela de login não trouxe o form 'login' — o SER está fora do ar?")
            print("   marcas:", marcas(pag))
            return 1

        r = c.post(form_login.get("action"), data=campos_do_form(d, "login") | {
            "login": "login",
            "login:username": usuario,
            "login:password": senha,
            "login:entrar": "Entrar",
            "javax.faces.ViewState": viewstate(str(form_login)) or "j_id1",
        })
        if 'id="login:username"' in r.text:
            print("!! LOGIN FALHOU (a tela de login voltou). Credencial do .env desatualizada?")
            print("   marcas:", marcas(r.text))
            return 1
        print(f"[1] login OK como {usuario}")

        # 2) home + o form de módulo
        home = c.get("/ser/home.seam").text

        # A MESMA regra do motor corrigido: o form sai do <script id="{form}:goModulo"> que a
        # página declara, nunca do "primeiro form j_idNN com action /ser/home" — com aviso
        # pendente, esse primeiro form é a TABELA DE AVISOS, e o POST vai para o alvo errado.
        m = re.search(r'<script[^>]*\bid="([^":]+):goModulo"', home)
        if not m:
            print("!! a home não declara goModulo.")
            if "Marcar Lida" in home:
                print("   >> CAUSA: há AVISO PENDENTE. O SER esconde os módulos até alguém ler o")
                print("      aviso na home e clicar em 'Marcar Lida'. Não é o layout, nem o protocolo.")
            print("   tamanho da home:", len(home), "| marcas:", marcas(home))
            (RAIZ / "capturas" / "sonda_modulo_home.html").write_text(home, encoding="utf-8")
            return 1
        fid = m.group(1)
        acao = re.search(rf'<form id="{re.escape(fid)}"[^>]*action="([^"]*)"', home)
        action = acao.group(1) if acao else "/ser/home"
        print(f"[2] home OK — form de módulo id={fid} (achado pelo goModulo) action={action}")

        dh = sopa(home)
        dados = campos_do_form(dh, fid) | {
            fid: fid,
            f"{fid}:goModulo": f"{fid}:goModulo",
            "param1": "ambulatorial",
            "AJAXREQUEST": fid,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": viewstate(str(dh.find("form", id=fid))) or "",
        }

        # 3) o POST que está falhando no servidor.
        # follow_redirects=False AQUI: o A4J responde 200 + header Location, e o que se quer medir
        # é justamente a presença desse header — segui-lo apagaria a evidência.
        r = c.post(action, data=dados, headers={"X-Requested-With": "XMLHttpRequest"},
                   follow_redirects=False)

        corpo = r.text
        print(f"\n[3] POST goModulo -> HTTP {r.status_code}")
        print(f"    header Location : {r.headers.get('location') or '(AUSENTE)'}")
        meta = re.search(r'<meta name="Location" content="([^"]+)"', corpo)
        print(f"    <meta> Location : {meta.group(1) if meta else '(AUSENTE)'}")
        print(f"    Content-Type    : {r.headers.get('content-type')}")
        print(f"    tamanho do corpo: {len(corpo)} bytes")
        print(f"    marcas          : {marcas(corpo)}")

        cap = RAIZ / "capturas"
        cap.mkdir(exist_ok=True)
        (cap / "sonda_modulo_resposta.html").write_text(corpo, encoding="utf-8")
        print(f"\n    corpo salvo em capturas/sonda_modulo_resposta.html")

        destino = r.headers.get("location") or (meta.group(1) if meta else None)
        if not destino:
            print("\n>> O SER não devolveu redirect — o módulo NÃO ativou.")
            print("   Primeiros 400 caracteres do corpo:")
            print("   " + " ".join(corpo[:400].split()))
            return 3

        print(f"\n[4] módulo ativou -> seguindo {destino[:80]}")
        c.get(destino.replace("&amp;", "&"))

        url_tela = ("/ser/pages/consultas-exames/solicitacao/"
                    "solicitar-consulta-pesquisar.seam")
        tela = c.get(url_tela).text
        print(f"[5] tela de Solicitação: {len(tela)} bytes"
              f" | form0={'form0' in tela}"
              f" | botão Pesquisar={'title=\"Pesquisar\"' in tela}")

        # O campo CNS/CPF do paciente vive na aba EDITAR, não na tela de busca — é a mesma troca de
        # aba que o motor faz (form0:editar_server_submit) antes de pesquisar o cadastro.
        d2 = BeautifulSoup(tela, "html.parser")
        f0 = d2.find("form", id="form0")
        acao_tela = (f0.get("action") if f0 else None) or url_tela
        r2 = c.post(acao_tela, data=campos_do_form(d2, "form0") | {
            "form0": "form0",
            "form0:editar_server_submit": "form0:editar_server_submit",
            "javax.faces.ViewState": viewstate(tela) or "",
        })
        editar = r2.text
        if (mm := re.search(r'<meta name="Location" content="([^"]+)"', editar)):
            editar = c.get(mm.group(1).replace("&amp;", "&")).text

        print(f"[6] aba Editar: {len(editar)} bytes"
              f" | campo CNS/CPF={'numeroCADSUS' in editar}"
              f" | painel do paciente={'painelDadosDoPaciente' in editar}")

        if "numeroCADSUS" not in editar:
            (RAIZ / "capturas" / "sonda_modulo_editar.html").write_text(editar, encoding="utf-8")
            print("\n>> Módulo ativou e a tela abriu, mas a aba Editar não trouxe o painel.")
            return 4

        print("\n>> CAMINHO INTEIRO OK: login -> módulo -> tela -> aba Editar com o campo CNS/CPF.")
        print("   É exatamente o que a consulta de cadastro pelo SER precisa.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
