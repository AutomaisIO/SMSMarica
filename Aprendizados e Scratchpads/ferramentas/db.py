import json, os, psycopg2, sys
p = os.path.expandvars(r"%APPDATA%\Microsoft\UserSecrets\smsmarica-api-dev\secrets.json")
cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
d = dict(x.split("=",1) for x in cs.split(";") if "=" in x)
g = lambda *k: next(d[x] for x in k if x in d)
def conn():
    return psycopg2.connect(host=g("Host","Server"), port=int(g("Port")), dbname=g("Database"),
                            user=g("Username","User ID"), password=g("Password"), sslmode="require")
def q(sql, args=None, title=None):
    with conn() as c, c.cursor() as cur:
        cur.execute(sql, args or ())
        cols = [x[0] for x in cur.description]
        rows = cur.fetchall()
    if title: print("=== " + title)
    print(" | ".join(cols))
    for r in rows:
        print(" | ".join("" if v is None else str(v) for v in r))
    print(f"({len(rows)} linhas)\n")
    return rows
