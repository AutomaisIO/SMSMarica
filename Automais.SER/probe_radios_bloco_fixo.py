"""Os três radios do bloco fixo escondem ou revelam campos?

Motivo (10/08/2026): "É AMBULATÓRIO ESTADUAL?" parecia um select comum e era um interruptor de
catálogo — 31 consultas ficaram fora da nossa base por causa disso. Os três radios do bloco fixo
também disparam A4J:

  form0:booleanMedicoSolicitanteIdentificado_radio  -> form0:j_id194
  form0:naturezaSolicitacaoMandato_radio            -> form0:j_id241
  form0:unidadeDeOrigemIdentificada_radio           -> form0:j_id259

Disparar A4J significa que ALGO é re-renderizado. Esta sonda descobre o quê, comparando os campos
que a resposta parcial traz com "Sim" e com "Não". Campo que só existe num dos lados é campo
condicional — e a nossa tela precisa saber disso antes de o envio ser ligado, senão monta pedido
sem um dado que o SER exige naquele caminho.

SOMENTE LEITURA. Marcar radio apenas re-renderiza a view; nada é gravado.
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

from probe_campos_dinamicos import BASE, CAP, UA, Editar, login_e_modulo  # noqa: E402

RADIOS = [
    ("form0:booleanMedicoSolicitanteIdentificado_radio", "Médico solicitante identificado?"),
    ("form0:naturezaSolicitacaoMandato_radio", "Mandado judicial"),
    ("form0:unidadeDeOrigemIdentificada_radio", "Unidade de origem identificada?"),
]


def evento_do_radio(html: str, nome: str) -> str | None:
    """Id do a4j:support lido do onchange do próprio input — j_id é posicional."""
    for m in re.finditer(r"<input[^>]*>", html):
        tag = m.group(0)
        if f'name="{nome}"' not in tag:
            continue
        ev = re.search(r"'similarityGroupingId'\s*:\s*'([^']+)'", tag)
        if ev:
            return ev.group(1)
    return None


def controles(html: str) -> dict[str, str]:
    """{nome do campo: tipo} de tudo que a resposta traz — é o inventário a comparar."""
    d = BeautifulSoup(html, "html.parser")
    out = {}
    for el in d.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome:
            continue
        tipo = el.name if el.name != "input" else (el.get("type") or "text").lower()
        if tipo in {"hidden", "submit", "button", "image", "reset"}:
            continue
        out[nome] = tipo
    return out


def rotulo_perto(html: str, campo: str) -> str:
    """Texto imediatamente antes do campo — para o achado sair legível."""
    i = html.find(f'name="{campo}"')
    if i < 0:
        return ""
    trecho = BeautifulSoup(html[max(0, i - 900):i], "html.parser").get_text(" ", strip=True)
    return " ".join(trecho.split())[-70:]


with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                  follow_redirects=True, verify=False) as c:
    login_e_modulo(c)
    ed = Editar(c)

    for nome, descricao in RADIOS:
        ed.abrir()
        evento = evento_do_radio(ed.full, nome)
        if not evento:
            print(f"\n!! {descricao}: não achei o onchange A4J — layout mudou?")
            continue

        html_sim = ed.mudar(nome, "true", evento)
        ed.abrir()
        html_nao = ed.mudar(nome, "false", evento)

        sim, nao = controles(html_sim), controles(html_nao)
        so_sim = {k: v for k, v in sim.items() if k not in nao}
        so_nao = {k: v for k, v in nao.items() if k not in sim}

        print(f"\n{'='*74}\n{descricao}  ({nome})\n{'='*74}")
        print(f"  campos com Sim: {len(sim)}   com Não: {len(nao)}")
        if not so_sim and not so_nao:
            print("  -> NENHUM campo condicional: o radio só guarda a resposta.")
        for k, v in so_sim.items():
            print(f"  + SÓ com Sim : {k} ({v})   ...{rotulo_perto(html_sim, k)}")
        for k, v in so_nao.items():
            print(f"  + SÓ com Não : {k} ({v})   ...{rotulo_perto(html_nao, k)}")

        alvo = nome.replace(":", "_").replace("form0_", "")
        (CAP / f"radio_{alvo}_sim.html").write_text(html_sim, encoding="utf-8")
        (CAP / f"radio_{alvo}_nao.html").write_text(html_nao, encoding="utf-8")

    print("\nparciais salvas em capturas/radio_*.html")
