"""Valida o fluxo de consulta por CNS (cadweb50) — espelha o que o backend .NET faz.

Loga (sessão fresca, derruba a do navegador) e POSTa cadweb50 por CNS com
etapa=LISTAR. Confirma que a resposta é a FICHA (não uma lista) e que os rótulos
que o parser .NET espera estão presentes. Só imprime confirmação estrutural.

Uso: python consultar_cns.py <CNS 15 dígitos>
"""

from __future__ import annotations

import os
import re
import sys
import pathlib

from dotenv import load_dotenv
from sisreg import SisregClient, SisregLoginError


def main(argv: list[str]) -> int:
    if not argv:
        print("uso: python consultar_cns.py <CNS>", file=sys.stderr)
        return 2
    cns = re.sub(r"\D", "", argv[0])
    if len(cns) != 15:
        print("CNS deve ter 15 dígitos.", file=sys.stderr)
        return 2

    load_dotenv(".env")
    u = os.getenv("SISREG_USUARIO", "").strip()
    s = os.getenv("SISREG_SENHA", "")
    base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

    cap = pathlib.Path(__file__).parent / "capturas"
    cap.mkdir(exist_ok=True)

    with SisregClient(base_url=base) as cli:
        try:
            print("sessão:", cli.conectar(u, s))
        except SisregLoginError as e:
            print(f"LOGIN FALHOU: {e}", file=sys.stderr)
            return 1

        campos = {
            "nu_cns": cns, "nome_paciente": "", "nome_mae": "", "dt_nascimento": "",
            "uf_nasc": "", "mun_nasc": "", "uf_res": "", "mun_res": "", "sexo": "",
            "standalone": "1", "etapa": "LISTAR", "url": "",
        }
        r = cli.post("/cgi-bin/cadweb50?standalone=1", data=campos)
        html = r.text
        (cap / "cadweb50_cns.html").write_text(html, encoding="utf-8")
        print(f"POST cadweb50 status={r.status_code} len={len(html)}")

        # Confirmações estruturais (sem despejar PII):
        eh_ficha = "Dados Pessoais" in html
        eh_login = 'name="senha_256"' in html and 'name="formLogin"' in html
        rotulos_esperados = ["CNS:", "Nome:", "Nome da Mãe:", "Sexo:", "Data de Nascimento:", "CPF:"]
        presentes = [rot for rot in rotulos_esperados if rot in html]
        # bate o CNS pesquisado na resposta? (identidade correta, sem imprimir outros dados)
        cns_confere = cns in re.sub(r"\D", "", html)

        print(f"é ficha (Dados Pessoais)?   {eh_ficha}")
        print(f"é tela de login?            {eh_login}")
        print(f"rótulos presentes:          {len(presentes)}/{len(rotulos_esperados)} -> {presentes}")
        print(f"CNS pesquisado na resposta? {cns_confere}")
        ok = eh_ficha and not eh_login and len(presentes) == len(rotulos_esperados) and cns_confere
        print(">>>", "FLUXO OK — etapa=LISTAR retorna a ficha; estrutura casa com o parser .NET"
              if ok else "ATENÇÃO — resposta não é a ficha esperada (ver capturas/cadweb50_cns.html)")
        return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
