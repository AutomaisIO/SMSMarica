#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs
echo "=== TODAS as entradas da worklist (qualquer station) ==="
curl -s "$B/mwlitems?includefield=all&limit=100" > /tmp/all.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/all.json'))
print("total:",len(arr))
def g(it,t):
    v=it.get(t,{}).get('Value',[])
    if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
    return v[0] if v else ''
for it in arr:
    sps=it.get('00400100',{}).get('Value',[{}])[0]
    sta=(sps.get('00400001',{}).get('Value') or [''])[0]
    print(f"PID={g(it,'00100020')!r:12} NAME={g(it,'00100010')!r:34} ACC={g(it,'00080050')!r:14} Station={sta!r}")
PY
echo
echo "=== DUMP COMPLETO das entradas TESTE/RAFAEL (que FUNCIONAM) ==="
python3 <<'PY'
import json
arr=json.load(open('/tmp/all.json'))
def txt(node):
    v=node.get('Value',[])
    if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
    return '' if not v else str(v[0])
def walk(it,prefix=""):
    for tag in sorted(it.keys()):
        node=it[tag]; vr=node.get('vr','')
        val=node.get('Value',[])
        if vr=='SQ':
            print(f"{prefix}{tag} SQ [{len(val)}]")
            for i,sub in enumerate(val): walk(sub,prefix+f"  >#{i} ")
            continue
        print(f"{prefix}{tag} {vr:2} {txt(node)!r}")
for it in arr:
    pid=(it.get('00100020',{}).get('Value') or [''])[0]
    name=''
    v=it.get('00100010',{}).get('Value',[])
    if v and isinstance(v[0],dict): name=v[0].get('Alphabetic','')
    if pid in ('01','02') or 'RAFAEL' in name.upper() or (name.strip().upper()=='TESTE'):
        print(f"\n######## PID={pid!r} NAME={name!r} ########")
        walk(it)
PY
