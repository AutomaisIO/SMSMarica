"""
Liga ou desliga em bloco as regras importadas dos manuais.

    python ativar_regras.py --ativar     # liga todas
    python ativar_regras.py --desativar  # desliga todas (desfaz)

O desfazer é exato porque a importação gravou todas inativas: desligar tudo devolve o estado de
logo após a importação. Depois que alguém ativar ou desativar regras uma a uma pela tela, isto
deixa de valer — daí em diante o desfazer em bloco apaga a curadoria junto.
"""

import io
import json
import os
import sys
from datetime import datetime, timezone

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
    if "--ativar" in sys.argv:
        alvo = True
    elif "--desativar" in sys.argv:
        alvo = False
    else:
        print(__doc__)
        return 1

    cn = conectar()
    cur = cn.cursor()
    cur.execute(
        "select count(*) filter (where ativo), count(*) from smsmarica.regulacao_regra"
    )
    antes = cur.fetchone()
    print(f"antes:  {antes[0]} ativas de {antes[1]}")

    try:
        cur.execute(
            "update smsmarica.regulacao_regra set ativo = %s, atualizado_em = %s "
            "where ativo <> %s",
            (alvo, datetime.now(timezone.utc), alvo),
        )
        alteradas = cur.rowcount
        cn.commit()
    except Exception:
        cn.rollback()
        raise

    cur.execute(
        "select count(*) filter (where ativo), count(*) from smsmarica.regulacao_regra"
    )
    print(f"depois: {cur.fetchone()[0]} ativas ({alteradas} alteradas)")
    cn.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
