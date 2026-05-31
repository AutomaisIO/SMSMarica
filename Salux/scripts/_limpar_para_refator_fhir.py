"""Limpa as tabelas que bloqueiam a aplicação da migration de refator FHIR
no PostgreSQL `defaultdb`. Dev-only — apaga laudos/tratamentos/solicitações/
pacientes de teste para deixar a FK cross-schema apontar pra `fhir.patient`
sem viola constraint.

Uso:
    PYTHONIOENCODING=utf-8 python _limpar_para_refator_fhir.py

Não faz nada se as tabelas já estiverem vazias.
"""
from __future__ import annotations

import os
import psycopg2

# Connection string vem do user-secrets do .NET (lida via env var aqui).
# Fallback hardcoded apenas pra ambiente dev local — não commitar prod.
DSN = os.environ.get("SMSMARICA_PG_DSN") or (
    "host=smsmarica-do-user-10042663-0.g.db.ondigitalocean.com "
    "port=25060 dbname=defaultdb user=doadmin "
    "password=AVNS_GHxL1beOJ3-JHAq1LiO sslmode=require"
)

TABELAS = [
    "smsmarica.alocacao",
    "smsmarica.sessao_de_tratamento",
    "smsmarica.periodicidade",
    "smsmarica.tratamento",
    "smsmarica.laudo",
    "smsmarica.solicitacao_exame",
    "smsmarica.paciente",
]


def main() -> None:
    with psycopg2.connect(DSN) as conn:
        with conn.cursor() as cur:
            for t in TABELAS:
                # Algumas tabelas podem ainda não existir se migration parcial.
                cur.execute(
                    "SELECT EXISTS(SELECT 1 FROM information_schema.tables "
                    "WHERE table_schema = %s AND table_name = %s)",
                    t.split(".", 1),
                )
                exists = cur.fetchone()[0]
                if not exists:
                    print(f"[skip] {t} não existe")
                    continue
                cur.execute(f"SELECT COUNT(*) FROM {t}")
                n = cur.fetchone()[0]
                if n == 0:
                    print(f"[ok]   {t} já está vazia")
                    continue
                cur.execute(f"TRUNCATE {t} CASCADE")
                print(f"[clean] {t} ({n} linhas removidas)")
        conn.commit()
    print("Pronto. Pode rodar `dotnet ef database update`.")


if __name__ == "__main__":
    main()
