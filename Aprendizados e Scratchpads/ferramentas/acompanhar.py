"""Acompanha a execucao viva da varredura ate ela terminar, e imprime o veredito."""
import time
from db import conn

STATUS = {1: 'Pendente', 2: 'EmExecucao', 3: 'Concluida', 4: 'PARCIAL', 5: 'Erro', 6: 'Cancelada'}
LIMITE_MIN = 45


def estado():
    with conn() as c, c.cursor() as cur:
        cur.execute("""
            select id, status, requisicoes, registros_encontrados, validos, invalidos,
                   ja_existiam, duracao_segundos, coalesce(mensagem_erro,''),
                   janela_inicio, janela_fim
            from smsmarica.sisreg_varredura_execucao
            order by iniciado_em desc limit 1
        """)
        return cur.fetchone()


inicio = time.time()
ultimo = None
while time.time() - inicio < LIMITE_MIN * 60:
    e = estado()
    marca = (e[1], e[2], e[3], e[4])
    if marca != ultimo:
        print(f"[{time.strftime('%H:%M:%S')}] {STATUS.get(e[1], e[1])} | "
              f"req={e[2]} lidos={e[3]} validos={e[4]}", flush=True)
        ultimo = marca
    if e[1] != 2:
        break
    time.sleep(30)

e = estado()
print("\n=== VEREDITO ===")
print(f"janela .......: {e[9]} a {e[10]}")
print(f"status .......: {STATUS.get(e[1], e[1])}")
print(f"requisicoes ..: {e[2]}")
print(f"lidos ........: {e[3]}   validos: {e[4]}   invalidos: {e[5]}   ja existiam: {e[6]}")
print(f"duracao ......: {e[7]}s")
print(f"mensagem .....: {e[8][:400]}")

with conn() as c, c.cursor() as cur:
    cur.execute("""
        select tipo, count(*)
        from smsmarica.sisreg_alteracao_agenda
        where detectada_em >= timestamptz '2026-09-08 18:05:00+00'
        group by 1 order by 1
    """)
    novas = cur.fetchall()

print("\nalteracoes criadas por esta corrida:",
      ", ".join(f"tipo {t}={n}" for t, n in novas) if novas else "NENHUMA")
