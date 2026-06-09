import psycopg2, paramiko, time, datetime, sys

PG = dict(host='smsmarica-do-user-10042663-0.g.db.ondigitalocean.com', port=25060,
          dbname='defaultdb', user='doadmin', password='AVNS_GHxL1beOJ3-JHAq1LiO', sslmode='require')
TABS = ['patient','practitioner','encounter','condition','medication_request','document_reference','observation']
ITERS = int(sys.argv[1]) if len(sys.argv) > 1 else 9
INTERVAL = int(sys.argv[2]) if len(sys.argv) > 2 else 60

def snap():
    conn = psycopg2.connect(**PG); cur = conn.cursor()
    c = {}
    for t in TABS:
        cur.execute(f'select count(*) from fhir.{t}'); c[t] = cur.fetchone()[0]
    cur.execute('select status, mensagem_erro, finalizado_em from smsmarica.pep_sincronizacao_execucao order by iniciado_em desc limit 1')
    st = cur.fetchone()
    conn.close()
    return c, st

def mem():
    cl = paramiko.SSHClient(); cl.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    cl.connect('smsmarica.online', username='root', password='#030957#Be', timeout=20)
    si, so, se = cl.exec_command("free -m | awk '/Mem:/{print $3\"/\"$2\"MB used, \"$7\" avail\"} /Swap:/{print \"swap \"$3\"/\"$2}'; ps -eo rss,comm --sort=-rss | awk '/dotnet/{print int($1/1024)\"MB\"}' | head -1")
    out = so.read().decode().replace('\n', ' | '); cl.close()
    return out.strip()

prev = None; t0 = time.time()
for i in range(ITERS):
    c, st = snap()
    total = sum(c.values())
    now = datetime.datetime.now(datetime.timezone.utc).strftime('%H:%M:%S')
    line = f'[{now}] total={total}'
    if prev is not None:
        dt = time.time() - prev_t
        d = total - prev_total
        rate = d / dt if dt else 0
        line += f'  +{d} em {dt:.0f}s = {rate:.1f} rec/s'
    detail = ' '.join(f'{k[:3]}={v}' for k, v in c.items())
    statestr = f'status={st[0]}' + (f' ERRO={st[1]}' if st[1] else '') + (' FINALIZADO' if st[2] else '')
    print(f'{line}\n   {detail}\n   {statestr} | {mem()}', flush=True)
    if st[2] is not None:
        print('=== EXECUCAO FINALIZADA ==='); break
    prev = c; prev_total = total; prev_t = time.time()
    if i < ITERS - 1:
        time.sleep(INTERVAL)
print('=== monitor encerrado ===')
