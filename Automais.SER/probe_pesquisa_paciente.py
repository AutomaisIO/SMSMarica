"""O botão "Pesquisar" do painel de paciente da aba Editar: o que ele devolve?

Descoberto em 10/08/2026 lendo a marcação do próprio SER. Ao lado do campo CNS/CPF
(`form0:numeroCADSUS`) existe um `<a title="Pesquisar">` — `form0:j_id76` — com A4J que
re-renderiza `form0:painelDadosDoPaciente`. O `oncomplete` chama
`aplicarMascaraEmTodosCamposTelefone()`, então a resposta traz telefone.

Isso importa porque hoje o nosso caminho para resolver um CNS é o CADSUS via SISREG, que tem
limitação de acesso. Se o SER resolve pelo motor dele, resolvemos junto — e de graça.

É CONSULTA, não escrita: o botão pesquisa e re-renderiza, não grava. A trava de somente-leitura
continua valendo (o rótulo "Pesquisar" não é verbo de escrita).

Uso:  python probe_pesquisa_paciente.py <cns ou cpf>
"""

from __future__ import annotations

import pathlib
import re
import sys

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
sys.path.insert(0, str(RAIZ))

from probe_campos_dinamicos import BASE, CAP, UA, Editar, hidden_do_form, login_e_modulo, sopa  # noqa: E402

CAMPO = "form0:numeroCADSUS"
PAINEL = "form0:painelDadosDoPaciente"

if len(sys.argv) < 2:
    raise SystemExit("Uso: python probe_pesquisa_paciente.py <cns ou cpf>  "
                     "(o número é PII e por isso não fica em arquivo no repositório)")
alvo = sys.argv[1]


def botao_pesquisar(html: str) -> str | None:
    """Id do <a title="Pesquisar"> do painel do paciente — lido da página, nunca chumbado."""
    for m in re.finditer(r'<a[^>]*title="Pesquisar"[^>]*>', html):
        tag = m.group(0)
        if (idm := re.search(r'id="([^"]+)"', tag)):
            return idm.group(1)
    return None


with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                  follow_redirects=True, verify=False) as c:
    login_e_modulo(c)
    ed = Editar(c)
    ed.abrir()

    botao = botao_pesquisar(ed.full)
    print(f"botão de pesquisa do paciente: {botao}")
    if not botao:
        raise SystemExit("não achei o botão Pesquisar — o layout mudou?")

    dados = hidden_do_form(ed.full)
    dados[CAMPO] = alvo
    dados |= {
        "AJAXREQUEST": "_viewRoot",
        botao: botao,
        "ajaxSingle": botao,
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": ed.vs or "",
    }

    r = c.post(ed.act, data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
    html = r.text
    if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
        html = c.get(m.group(1).replace("&amp;", "&")).text

    (CAP / "pesquisa_paciente.html").write_text(html, encoding="utf-8")
    print(f"resposta: {len(html)} bytes -> capturas/pesquisa_paciente.html\n")

    d = sopa(html)
    painel = d.find(id=PAINEL)
    if painel is None:
        print("!! painelDadosDoPaciente não veio na resposta.")
        print("trecho:", " ".join(html[:600].split()))
        raise SystemExit(1)

    print("=" * 74)
    print("CAMPOS DEVOLVIDOS")
    print("=" * 74)
    for el in painel.find_all(["input", "select", "textarea"]):
        nome = el.get("name") or el.get("id") or "(sem nome)"
        tipo = el.name if el.name != "input" else (el.get("type") or "text")
        valor = el.get("value") or (el.get_text(strip=True) if el.name == "textarea" else "")
        if el.name == "select":
            sel = el.find("option", selected=True)
            valor = (sel.get_text(strip=True) if sel else "")
        print(f"  {nome:44} [{tipo:8}] = {valor!r}")

    print("\n" + "=" * 74)
    print("TEXTO DO PAINEL (rótulos + valores exibidos)")
    print("=" * 74)
    print(" | ".join(t for t in painel.stripped_strings)[:1600])
