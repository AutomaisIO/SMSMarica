"""A crítica de "pedido parecido" do envio automático acha o pedido ativo do paciente?

Em 09/10/2026 (PR-17) o envio ao SER não achou um pedido Em fila do MESMO paciente e
recurso, e o Gravar foi recusado pelo SER ("Existe uma Solicitação de Consulta ativa deste recurso
para este paciente"). O motor pesquisa a fila com `form0:cpf` = só dígitos; a tela tem máscara
`999.999.999-99` — o navegador manda pontuado. Esta sonda compara as variações:

  A) Em fila + CPF só dígitos      (o que o motor faz)
  B) Em fila + CPF com máscara     (o que o navegador manda)
  C) Em fila + CNS
  D) Em fila + ID (controle)

Uso:  python probe_pesquisa_cpf.py <cpf> <cns> <id_esperado>
SOMENTE LEITURA. Imprime contagens e IDs, nunca nome/documento do paciente.
"""

from __future__ import annotations

import sys

import httpx

from probe_historico_por_id import (BASE, CAMPO_ID, CAMPO_SITUACAO, UA, Motor, conta,
                                    login_e_modulo)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    cpf, cns, alvo = sys.argv[1], sys.argv[2], sys.argv[3]
    cpf = "".join(ch for ch in cpf if ch.isdigit())
    mascarado = f"{cpf[:3]}.{cpf[3:6]}.{cpf[6:9]}-{cpf[9:]}"
    c = httpx.Client(base_url=BASE, timeout=90, follow_redirects=True,
                     headers={"User-Agent": UA, "Accept-Language": "pt-BR,pt;q=0.9"})
    login_e_modulo(c)
    m = Motor(c)
    sit = "EM_FILA"

    for rotulo, extras in (
        ("A) Em fila + CPF só dígitos (motor)", {"form0:cpf": cpf}),
        ("B) Em fila + CPF com máscara (navegador)", {"form0:cpf": mascarado}),
        ("C) Em fila + CNS", {"form0:cns": cns}),
        ("D) Em fila + ID (controle)", {CAMPO_ID: alvo}),
    ):
        m.preparar()
        conta(rotulo, m.pesquisar({CAMPO_SITUACAO: sit} | extras, reusar=True), alvo)

    c.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
