"""Sonda 0: login + ativação de módulo no SERNIT.

Valida o cliente ponta a ponta (a sequência que destrava o form de login) e imprime
quem está logado, o build da instância e os módulos oferecidos. SOMENTE LEITURA.

Uso:  python probe_login.py
"""

from __future__ import annotations

import re
import sys

from sernit.client import CAP, SernitSession, sopa

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    s = SernitSession()
    s.login()
    print(">>> LOGIN OK")

    home = s.get("/ser/home.seam").text
    (CAP / "home.html").write_text(home, encoding="utf-8")
    d = sopa(home)
    body = d.find("body")
    texto = " ".join((body.get_text(" ", strip=True) if body else "").split())

    usr = re.search(r"Usu.rio:\s*([^\|]+?)\s+(Home|Alterar)", texto)
    build = re.search(r"build:\s*([^\s]+)", texto)
    print("usuário:", usr.group(1).strip() if usr else "(?)")
    print("build:  ", build.group(1) if build else "(?)")

    modulos = sorted(set(re.findall(r"goModulo\('([^']+)'\)", home)))
    print("módulos:", modulos or "(nenhum goModulo encontrado)")

    s.ativar_modulo("ambulatorial")
    print(">>> módulo 'ambulatorial' ativado")

    tela = s.get(s.c.base_url.join("/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam")).text
    (CAP / "pesquisar.html").write_text(tela, encoding="utf-8")
    tem_form0 = '<form id="form0"' in tela
    print(f"tela de pesquisa: {len(tela)} bytes, form0 presente={tem_form0} -> capturas/pesquisar.html")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
