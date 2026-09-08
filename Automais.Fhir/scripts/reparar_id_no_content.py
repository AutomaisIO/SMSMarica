"""
Repara as fichas cujo `content` não carrega a chave `id`.

INCIDENTE 08/09/2026: `Automais.SISREG/conciliar_pacientes.py` inseriu 36.257 fichas com o id
gerado por `gen_random_uuid()` na COLUNA, mas sem a chave `"id"` dentro do documento. Ao ler
essas fichas, o `PacienteFhirMapper` do SMSMais.server faz `Guid.Parse(p.Id!)` — e
`Guid.Parse(null)` lança `ArgumentNullException (Parameter 'input')`, que virou 500 em
`/pacientes`, `/pacientes/por-cpf`, `/conversas/{id}/pacientes`, `/auth/paciente/solicitar-otp`
e no webhook do WhatsApp (214 ocorrências — mensagem de paciente parando de ser processada).

A coluna `id` é a verdade: é ela que a PK guarda e é por ela que o recurso é endereçado. O
reparo copia esse valor para dentro do documento.

    python reparar_id_no_content.py            # confere e não grava
    python reparar_id_no_content.py --gravar   # grava, em transação única

Idempotente: só toca linhas em que `content->'id'` é nulo.
"""

import io
import json
import os
import sys

import psycopg2


def conectar():
    caminho = os.path.expandvars(
        r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json"
    )
    cs = json.load(io.open(caminho, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(p.split("=", 1) for p in cs.split(";") if "=" in p)
    return psycopg2.connect(
        host=d["Host"], port=d["Port"], dbname=d["Database"],
        user=d["Username"], password=d["Password"], sslmode="require",
    )


def main():
    gravar = "--gravar" in sys.argv
    cn = conectar()
    cur = cn.cursor()

    cur.execute("select count(*) from fhir.patient where content->'id' is null")
    quebradas = cur.fetchone()[0]
    cur.execute("select count(*) from fhir.patient")
    total = cur.fetchone()[0]
    print(f"fichas sem id no content: {quebradas} de {total}")

    # Segurança: se alguma linha já tiver id divergente da coluna, o reparo não é trivial e
    # precisa de olho humano. Aqui só se repara o que está AUSENTE.
    cur.execute("""
        select count(*) from fhir.patient
        where content->'id' is not null and content->>'id' <> id::text
    """)
    divergentes = cur.fetchone()[0]
    print(f"fichas com id divergente da coluna (NÃO tocadas): {divergentes}")

    if quebradas == 0:
        print("nada a fazer.")
        cn.close()
        return 0

    if not gravar:
        print("\n(conferência — nada gravado; use --gravar)")
        cn.close()
        return 0

    try:
        cur.execute("""
            update fhir.patient
               set content = jsonb_set(content, '{id}', to_jsonb(id::text))
             where content->'id' is null
        """)
        alteradas = cur.rowcount
        cn.commit()
    except Exception:
        cn.rollback()
        raise

    cur.execute("select count(*) from fhir.patient where content->'id' is null")
    print(f"\nGRAVADO. {alteradas} fichas reparadas; restam {cur.fetchone()[0]} sem id.")

    cur.execute("""
        select count(*) from fhir.patient where content->>'id' is distinct from id::text
    """)
    print(f"conferência final — content.id divergente da coluna: {cur.fetchone()[0]}")
    cn.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
