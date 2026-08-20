"""O autocomplete de CID da Hipótese (`form0:procedimento`) — o que ele exige e o que devolve.

Levantado em 20/08/2026, quando a nossa tela de nova solicitação mostrava a Hipótese como campo
de texto e não listava CID nenhum. As três perguntas que esta sonda responde:

  1. **Por que não lista?** Porque o autocomplete só responde depois que o RECURSO está escolhido.
     Sem recurso, qualquer termo — inclusive o código exato — volta "Nenhum CID encontrado".
  2. **A relação dá para espelhar na nossa base?** Não: ela é do RECURSO. O "Ambulatório 1ª vez -
     Cirurgia Geral (Oncologia)" só aceita neoplasia (zero CID para "diab" ou "hipert"), enquanto
     a cardiologia aceita 78 e 75. Uma tabela única ofereceria código que o SER recusa ao gravar.
  3. **O que o campo espera de volta?** A coluna OCULTA da linha: `(A09 ) Diarréia e
     gastroenterite…` — código com espaço até 4 caracteres, entre parênteses, mais a descrição.

Mede também o teto do SER (500 linhas por busca) e confirma que o caminho do nosso motor .NET
(que posta o form INTEIRO da página base) devolve o mesmo que o caminho das sondas (só os hidden).

SOMENTE LEITURA: apenas o FETCH de sugestões, a primeira das duas requisições do protocolo
(docs/ser.md §4.3). O `onselect` — a metade que amarra a escolha na conversa Seam — pertence ao
envio do pedido e não é acionado aqui.

Uso:  python probe_cid.py
"""

from __future__ import annotations

import pathlib
import re
import sys

import httpx

RAIZ = pathlib.Path(__file__).parent
sys.path.insert(0, str(RAIZ))

from probe_editar_solicitacao import (  # noqa: E402
    BASE, CAP, UA, Ser, campos_do_form, sopa, viewstate,
)
from probe_criar_solicitacao import (  # noqa: E402
    CAMPO_CID, CAMPO_RECURSO, CAMPO_SISREG, CAMPO_TIPO,
    Criar, combo, linhas_de_sugestao, suggestion_box,
)

# Dois recursos que provam a diferença: cardiologia geral x oncologia.
RECURSO_AMPLO = "1003"       # Ambulatório 1ª vez em Cardiologia - Cardiopatia Congênita (Adulto)
RECURSO_ONCO = "1063"        # Ambulatório 1ª vez - Cirurgia Geral (Oncologia)

TERMOS = ["diab", "hipert", "C50", "neopl", "malig", "A09", "M54"]


def opcoes_do_box(html: str, campo: str) -> str | None:
    """O literal de opções do `new RichFaces.Suggestion(...)` — minChars, tokens, onselect."""
    m = re.search(r"RichFaces\.Suggestion\(\s*'[^']*'\s*,\s*'" + re.escape(campo)
                  + r"'\s*,\s*'[^']+'\s*,\s*(\{.*?\})\s*,", html, re.S)
    # None, e não uma mensagem: `Criar.ler` para no primeiro fragmento que devolve algo, e
    # uma string de erro contaria como "achei" — o script mora só na página completa.
    return " ".join(m.group(1).split())[:400] if m else None


def fetch(t: Criar, box_id: str, termo: str) -> str:
    """Como as sondas postam: só os hidden do form + o controle."""
    html = t.ser.postar(t.act, t._hidden_atual() | {
        "AJAXREQUEST": "_viewRoot",
        "inputvalue": termo,
        box_id: box_id,
        "ajaxSingle": box_id,
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": t.vs,
    })
    if (vs := viewstate(html)):
        t.vs = vs
    return html


def fetch_como_o_servidor(t: Criar, box_id: str, termo: str) -> str:
    """Como o motor .NET posta: o form INTEIRO da página base + os extras."""
    d = sopa(t.base)
    campos = campos_do_form(d, "form0") | {
        "form0": "form0",
        "AJAXREQUEST": "_viewRoot",
        "inputvalue": termo,
        box_id: box_id,
        "ajaxSingle": box_id,
        "AJAX:EVENTS_COUNT": "1",
        "javax.faces.ViewState": t.vs,
    }
    html = t.ser.postar(d.find("form", id="form0").get("action"), campos)
    if (vs := viewstate(html)):
        t.vs = vs
    return html


def cids(html: str, box_id: str) -> list[list[str]]:
    """As linhas de CID, sem a `NothingLabel` — que vem escondida em TODA resposta."""
    return [c for c in linhas_de_sugestao(html, box_id) if len(c) >= 3 and c[1].strip()]


def main() -> None:
    with httpx.Client(base_url=BASE, timeout=90, follow_redirects=False,
                      headers={"User-Agent": UA}) as c:
        ser = Ser(c)
        ser.login()

        t = Criar(ser)
        t.abrir()
        t.trocar(CAMPO_SISREG, "false")
        t.trocar(CAMPO_TIPO, "CONSULTA")
        recursos = dict(t.ler(lambda h: combo(h, CAMPO_RECURSO)) or [])

        caixa = t.ler(lambda h: suggestion_box(h, CAMPO_CID))
        if caixa is None:
            raise SystemExit("não achei o script do autocomplete da Hipótese")
        box_id, onselect = caixa
        print(f"\nsuggestionbox : box={box_id!r}  onselect={onselect!r}")
        print("opções        : "
              + (t.ler(lambda h: opcoes_do_box(h, CAMPO_CID)) or "(sem literal de opções)"))

        # --- 1) sem recurso escolhido, não existe CID nenhum
        print("\n=== SEM recurso escolhido ===")
        for termo in ("A09", "diab", "I10"):
            print(f"    {termo!r:8} -> {len(cids(fetch(t, box_id, termo), box_id))} CID(s)")

        # --- 2) a relação é do recurso
        for alvo in (RECURSO_AMPLO, RECURSO_ONCO):
            t.trocar(CAMPO_RECURSO, alvo)
            print(f"\n=== recurso {alvo} — {recursos.get(alvo, '?')[:58]!r}")
            for termo in TERMOS:
                linhas = cids(fetch(t, box_id, termo), box_id)
                amostra = ", ".join(l[1] for l in linhas[:8])
                print(f"    {termo!r:8} -> {len(linhas):4} : {amostra}")

        # --- 3) teto do SER e prefixos (o que uma varredura futura teria de respeitar)
        t.trocar(CAMPO_RECURSO, RECURSO_AMPLO)
        print("\n=== teto e prefixos (recurso amplo) ===")
        for termo in ("a", "A0", "M5", "Z9"):
            print(f"    {termo!r:8} -> {len(cids(fetch(t, box_id, termo), box_id)):4} CID(s)")

        # --- 4) o caminho do motor .NET responde igual?
        print("\n=== o form inteiro (motor .NET) x só os hidden (sonda) ===")
        for termo in ("diab", "M54", "I10"):
            servidor = [l[1] for l in cids(fetch_como_o_servidor(t, box_id, termo), box_id)]
            sonda = [l[1] for l in cids(fetch(t, box_id, termo), box_id)]
            print(f"    {termo!r:8} servidor={len(servidor):4} sonda={len(sonda):4} -> "
                  + ("IGUAL" if servidor == sonda else "DIFERENTE"))

        # --- 5) o HTML cru, onde se vê a coluna oculta que vira o texto do campo
        html = fetch(t, box_id, "A09")
        (CAP / "cid_sugestao.html").write_text(html, encoding="utf-8")
        linha = cids(html, box_id)
        if linha:
            print(f"\ntexto que o campo recebe: {linha[0][0]!r}")
        print("HTML cru em capturas/cid_sugestao.html")


if __name__ == "__main__":
    main()
