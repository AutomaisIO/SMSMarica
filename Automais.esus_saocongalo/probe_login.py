"""Sonda 0: login nos dois backends do e-SUS de São Gonçalo, sem navegador.

Valida o cliente ponta a ponta — login-light (:8001) + login-sem-permissoes (:9001) —,
imprime quem está logado, a unidade, os módulos/submódulos liberados, faz uma query
GraphQL inofensiva, o keep-alive, e prova que a trava recusa escrita. Termina com logoff.
SOMENTE LEITURA. Não imprime token nem senha.

Uso:  python probe_login.py
"""

from __future__ import annotations

import sys

from esus.client import EsusSession, TravaLeitura

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    with EsusSession() as s:
        d = s.login()
        u = d.get("user") or {}
        print(">>> LOGIN OK (novo + legado)")
        print(f"cliente:  {d['client']['cli_codigo']} — {d['client']['cli_nome']}")
        print(f"usuário:  {u.get('usu_nome')} (usu_id={u.get('usu_id')}, grupo={u.get('usu_id_grupos_usuario')})")
        print(f"unidade:  {s.unidade}  | unidades do usuário: {d.get('unitHealths')}  | setores: {d.get('sectors')}")
        print(f"sessão:   {d['loginInfo']['expireTime']} s  | token legado presente: {bool(s.token_legado)}")
        print(f"flags:    cadsus={d.get('cadsus')} atencaoBasica={d.get('basicAttentionIntegration')} "
              f"prontuarioObrigatorio={d.get('recordNumberRequired')}")

        print("\npermissões (unidade → setor → módulo → submódulo: operações):")
        for uns, setores in (d.get("permissions") or {}).items():
            for setor, modulos in setores.items():
                for mod, subs in modulos.items():
                    for sub, ops in subs.items():
                        print(f"  {uns}/{setor}  {mod}.{sub}: {', '.join(ops)}")

        unid = s.rest("GET", "/unit-health/find-unit-health-by-id/", params={"uns_id": s.unidade})
        nome = (unid or {}).get("uns_nome") if isinstance(unid, dict) else None
        print(f"\nunidade {s.unidade}: {nome or unid}")

        ch = s.gql("query Changelog($v: String) { changelog(ave_nome_versao: $v) { rows { ave_nome_versao ave_data_versao } } }")
        rows = (ch.get("changelog") or {}).get("rows") or []
        if rows:
            print(f"graphql OK — versão mais recente no changelog: {rows[0]['ave_nome_versao']} ({rows[0]['ave_data_versao']})")

        s.renovar()
        print("keep-alive OK (refresh-token + renovar-sessao)")

        for tentativa in (
            lambda: s.gql("mutation { x }"),
            lambda: s.legado("/exames2/controller-fila-exame/salvar", {}),
            lambda: s.rest("POST", "/access-control/qualquer-coisa"),
        ):
            try:
                tentativa()
                print("!!! TRAVA FALHOU — escrita passou")
                return 1
            except TravaLeitura as e:
                print(f"trava OK: {e}")
    print(">>> logoff feito")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
