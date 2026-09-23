"""O SISCAN derruba a sessão anterior quando o mesmo login entra de novo?

No SISREG e no SER, sim — e isso tem consequência de gente: o robô entrando com a
conta de alguém derruba essa pessoa do sistema no meio do trabalho. No SISCAN
nunca foi medido, e supor por analogia já nos custou caro antes (docs/ser.md
desmentiu a "sessão única herdada do SISREG por analogia").

O teste é o mínimo que decide: duas sessões com a MESMA credencial, e a primeira
tenta ler de novo depois que a segunda entrou.

    python probe_sessao_unica.py

SOMENTE LEITURA. Abre tela de pesquisa, não submete nada.
"""

from __future__ import annotations

import sys

from siscan.client import SiscanClient
from siscan.inspecao import titulos


def esta_logada(c: SiscanClient) -> tuple[bool, str]:
    """Abre o índice e diz se a sessão ainda vale. Sessão morta no SISCAN não dá
    401 nem redirect: dá HTTP 200 com a tela de login no corpo."""
    r = c.get("/visao/index.jsf", "sessao-check")
    if 'id="formLogin"' in r.text or 'name="formLogin"' in r.text:
        return False, "tela de login"
    doc = c.sopa(r)
    itens = [s.get_text(" ", strip=True) for s in doc.find_all("span", class_="rich-menu-item-label")]
    return bool(itens), (f"{len(itens)} itens de menu" if itens else f"tela {titulos(doc)[:1]}")


def main() -> int:
    print("A: entrando…")
    a = SiscanClient(capturar=False)
    a.login()
    viva, como = esta_logada(a)
    print(f"   A logada? {viva} ({como})")
    if not viva:
        print("   A nem entrou — teste inconclusivo.")
        return 1

    print("B: entrando com a MESMA credencial…")
    b = SiscanClient(capturar=False)
    b.login()
    viva_b, como_b = esta_logada(b)
    print(f"   B logada? {viva_b} ({como_b})")

    print("A: tentando ler de novo…")
    viva_a, como_a = esta_logada(a)
    print(f"   A ainda logada? {viva_a} ({como_a})")

    a.close()
    b.close()

    print()
    if viva_a and viva_b:
        print("RESULTADO: o SISCAN ACEITA sessões simultâneas do mesmo login.")
        print("           Entrar pelo painel NÃO derruba quem está no navegador.")
    elif viva_b and not viva_a:
        print("RESULTADO: SESSÃO ÚNICA — o login novo derrubou o anterior.")
        print("           Entrar pelo painel DERRUBA a pessoa do navegador dela.")
    else:
        print("RESULTADO: inconclusivo — ver acima.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
