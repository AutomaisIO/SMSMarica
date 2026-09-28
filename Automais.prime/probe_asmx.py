"""Chama os serviços `.asmx` do Prime direto — sem passar por tela. SOMENTE LEITURA.

Os `.asmx` são o único caminho verdadeiramente "objetivo" do Prime: JSON, cookie de sessão,
`X-Requested-With`, sem ViewState nem `__EVENTVALIDATION`. Servem para consulta (tabelas,
combos, paciente), não para escrita.

Uso:  python probe_asmx.py <Servico.asmx/Metodo> [json-do-corpo]
      python probe_asmx.py ComboTipoLogradouroService.asmx/GetData
"""

from __future__ import annotations

import json
import sys
import time

from prime.client import APP, CAP, PrimeSession

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def chamar(s: PrimeSession, caminho: str, corpo: dict) -> tuple[int, str, float]:
    url = f"{APP}/Services/{caminho}"
    t0 = time.perf_counter()
    r = s.c.post(url, json=corpo,
                 headers={"Content-Type": "application/json; charset=UTF-8",
                          "X-Requested-With": "XMLHttpRequest",
                          "Referer": f"https://marica.ecosistemas.com.br{APP}/Paciente/CadastroPaciente.aspx"})
    return r.status_code, r.text, time.perf_counter() - t0


# Assinaturas que o Telerik usa nos WebServices de combo; tentamos da mais provável à menos.
def tentativas(texto: str) -> list[dict]:
    ctx = {"Text": texto, "NumberOfItems": 0, "IsCaseSensitive": False}
    return [
        {"context": ctx},
        {"context": {**ctx, "Filter": texto}},
        {},
        {"text": texto},
        {"prefixText": texto, "count": 50},
    ]


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    caminho = sys.argv[1]
    texto = sys.argv[2] if len(sys.argv) > 2 else ""

    with PrimeSession() as s:
        s.entrar()
        for i, corpo in enumerate(tentativas(texto), 1):
            status, txt, dt = chamar(s, caminho, corpo)
            marca = "OK " if status == 200 else "   "
            print(f"{marca}tentativa {i}: status={status}  {dt*1000:6.0f} ms  corpo={json.dumps(corpo)[:70]}")
            if status != 200:
                erro = ""
                try:
                    erro = json.loads(txt).get("Message", "")[:110]
                except Exception:
                    erro = txt[:110].replace("\n", " ")
                print(f"     -> {erro}")
                continue

            (CAP / "asmx_resposta.json").write_text(txt, encoding="utf-8")
            try:
                dados = json.loads(txt)
            except Exception:
                print("     resposta não-JSON:", txt[:200])
                return
            d = dados.get("d", dados)
            itens = d.get("Items", d) if isinstance(d, dict) else d
            print(f"     itens: {len(itens) if isinstance(itens, list) else '?'}")
            if isinstance(itens, list):
                for it in itens[:40]:
                    if isinstance(it, dict):
                        # os combos do Telerik devolvem Text/Value
                        print("       %-46s %s" % (str(it.get("Text", it))[:46], it.get("Value", "")))
                    else:
                        print("      ", str(it)[:80])
            return
        print("nenhuma assinatura aceita — ver capturas/asmx_resposta.json")


if __name__ == "__main__":
    main()
