#!/usr/bin/env python3
"""
Verificacao pos-migracao do rename TFD x mensageria/geo (ADR-0038).

Compara o estado de PRODUCAO depois do rename contra o backup CSV tirado antes.
So faz SELECT — nao escreve nada.

A checagem central e a §1: TODO id que existia no backup precisa continuar existindo
na tabela nova. Nao basta comparar contagem, porque o sistema esta vivo e o WhatsApp
grava durante a janela — a contagem cresce legitimamente. Perda so aparece por id.

Uso:  python scripts/verificar-rename-tfd.py
Saida: exit 0 = tudo certo | exit 1 = divergencia (NAO seguir, avaliar rollback)
"""
import csv, json, os, sys, psycopg2

csv.field_size_limit(10**9)

BACKUP = os.path.expandvars(r"%USERPROFILE%\Backups\SMSMarica\pre-rename-20260731")

# origem (backup)            -> destino (depois do rename)
RENOMEADAS = {
    "tfd_mensagem_whatsapp":    "whatsapp_mensagem",
    "tfd_config_whatsapp":      "whatsapp_configuracao",
    "tfd_geocodigo":            "geo_endereco",
    "tfd_config_google":        "geo_configuracao",
    "tfd_config_faturamento":   "tfd_configuracao",
    "tfd_registro_faturamento": "tfd_registro_faturamento",  # permanece
}

falhas = []


def conectar():
    p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = dict(x.split("=", 1) for x in cs.split(";") if "=" in x)
    return psycopg2.connect(host=d["Host"], port=d["Port"], dbname=d["Database"],
                            user=d["Username"], password=d["Password"], sslmode="require")


def ids_do_backup(tabela):
    with open(os.path.join(BACKUP, f"{tabela}.csv"), encoding="utf-8", newline="") as f:
        r = csv.DictReader(f)
        return {linha["id"] for linha in r}


def secao(titulo):
    print(f"\n{'=' * 70}\n{titulo}\n{'=' * 70}")


def checar(condicao, descricao, detalhe=""):
    print(f"  [{'OK ' if condicao else 'FALHA'}] {descricao}{('  -> ' + detalhe) if detalhe and not condicao else ''}")
    if not condicao:
        falhas.append(descricao)


def main():
    c = conectar()
    cur = c.cursor()

    # Guarda: sem as tabelas de destino nao ha o que verificar. Sair cedo e com
    # mensagem clara evita uma cascata de erros que esconde o motivo real.
    cur.execute("""select table_name from information_schema.tables
                   where table_schema='smsmarica' and table_name = any(%s)""",
                (list(set(RENOMEADAS.values())),))
    existentes = {r[0] for r in cur.fetchall()}
    faltando = set(RENOMEADAS.values()) - existentes
    if faltando:
        secao("PRE-VOO: MIGRACAO AINDA NAO APLICADA")
        for t in sorted(faltando):
            print(f"  [--] {t}: ainda nao existe")
        print("\n  Nada a verificar. Rodar novamente DEPOIS de aplicar a migracao.")
        return 1

    secao("1. NENHUM REGISTRO PERDIDO (id a id, contra o backup)")
    for origem, destino in RENOMEADAS.items():
        esperados = ids_do_backup(origem)
        if not esperados:
            print(f"  [OK ] {destino}: backup vazio, nada a conferir")
            continue
        try:
            cur.execute(f'select id::text from smsmarica."{destino}"')
            atuais = {r[0] for r in cur.fetchall()}
        except psycopg2.errors.UndefinedTable:
            c.rollback()
            checar(False, f"{destino}: tabela existe", "tabela NAO existe (migracao nao aplicada?)")
            continue
        ausentes = esperados - atuais
        novos = len(atuais) - len(esperados & atuais)
        checar(not ausentes,
               f"{destino}: {len(esperados)} do backup presentes (+{novos} novos desde o backup)",
               f"{len(ausentes)} AUSENTES, ex.: {list(ausentes)[:3]}")

    secao("2. NOME ANTIGO NAO EXISTE MAIS")
    for origem, destino in RENOMEADAS.items():
        if origem == destino:
            continue
        cur.execute("""select count(*) from information_schema.tables
                       where table_schema='smsmarica' and table_name=%s""", (origem,))
        checar(cur.fetchone()[0] == 0, f"tabela {origem} nao existe mais")

    secao("3. INDICES E CONSTRAINTS SEM RESQUICIO DE 'tfd_' INDEVIDO")
    for origem, destino in RENOMEADAS.items():
        if destino.startswith("tfd_"):
            continue  # tfd_* legitimas mantem o prefixo
        cur.execute("""select indexname from pg_indexes
                       where schemaname='smsmarica' and tablename=%s and indexname ilike '%%tfd%%'""",
                    (destino,))
        sobras_idx = [r[0] for r in cur.fetchall()]
        cur.execute("""select conname from pg_constraint
                       where conrelid=('smsmarica.'||%s)::regclass and conname ilike '%%tfd%%'""",
                    (destino,))
        sobras_con = [r[0] for r in cur.fetchall()]
        checar(not sobras_idx and not sobras_con,
               f"{destino}: sem indice/constraint com nome antigo",
               f"indices={sobras_idx} constraints={sobras_con}")

    secao("4. CORDAO UMBILICAL CORTADO (a mensagem nao conhece mais o TFD)")
    cur.execute("""select count(*) from information_schema.columns
                   where table_schema='smsmarica' and table_name='whatsapp_mensagem'
                     and column_name='sessao_id'""")
    checar(cur.fetchone()[0] == 0, "whatsapp_mensagem.sessao_id removida")

    secao("5. INTEGRIDADE REFERENCIAL PRESERVADA")
    cur.execute("""select count(*) from smsmarica.comunicacao_paciente cp
                   join smsmarica.whatsapp_mensagem m on m.id = cp.mensagem_whatsapp_id""")
    ligadas = cur.fetchone()[0]
    cur.execute("select count(*) from smsmarica.comunicacao_paciente where mensagem_whatsapp_id is not null")
    checar(ligadas == cur.fetchone()[0],
           f"comunicacao_paciente -> whatsapp_mensagem: {ligadas} vinculos intactos")

    cur.execute("""select count(*) from smsmarica.whatsapp_mensagem m
                   join smsmarica.conversa cv on cv.id = m.conversa_id""")
    print(f"  [OK ] whatsapp_mensagem -> conversa: {cur.fetchone()[0]} vinculos")

    secao("6. O SQL CRU DO MODULO ESTATISTICAS RESPONDE")
    try:
        cur.execute("""select count(*), count(distinct template), count(autor_usuario_id)
                       from smsmarica.whatsapp_mensagem m
                       where m.ocorrido_em >= now() - interval '30 days'""")
        checar(True, f"consulta do dashboard executa -> {cur.fetchone()}")
    except Exception as e:
        c.rollback()
        checar(False, "consulta do dashboard executa", str(e)[:120])

    secao("RESULTADO")
    if falhas:
        print(f"  {len(falhas)} FALHA(S). NAO PROSSEGUIR — avaliar rollback:")
        for f in falhas:
            print(f"    - {f}")
        return 1
    print("  Migracao verificada: nenhum registro perdido, nomes limpos, FKs intactas.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
