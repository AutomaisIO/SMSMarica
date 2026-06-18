#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/PACS-CDT/rs

dump_views () { # $1=studyUID  $2=titulo
python3 - "$1" "$2" <<'PY'
import json,sys,subprocess
suid=sys.argv[1]; titulo=sys.argv[2]
arr=json.load(open('/tmp/m.json'))
def txt(node):
    v=node.get('Value',[]);
    if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
    return '' if not v else str(v[0])
def cs(it,tag):
    o=[]
    for sub in it.get(tag,{}).get('Value',[]) or []:
        cv=(sub.get('00080100',{}).get('Value') or [''])[0]
        sc=(sub.get('00080102',{}).get('Value') or [''])[0]
        cm=(sub.get('00080104',{}).get('Value') or [''])[0]
        if isinstance(cm,dict): cm=cm.get('Alphabetic','')
        o.append(f"code={cv!r} scheme={sc!r} meaning={cm!r}")
    return o
print(f"\n######## {titulo} ({len(arr)} instances) ########")
for i,it in enumerate(arr):
    print(f"-- instance {i}: AcqProcDesc(0018,1400)={txt(it.get('00181400',{}))!r} "
          f"AcqProcCode(0018,1401)={txt(it.get('00181401',{}))!r} "
          f"ViewPos={txt(it.get('00185101',{}))!r} Lat={txt(it.get('00200060',{}))!r}")
    for line in cs(it,'00540220'): print(f"     ViewCodeSeq(0054,0220)        {line}")
    for line in cs(it,'00400260'): print(f"     PerformedProtocolCode(0040,0260) {line}")
    for line in cs(it,'00321064'): print(f"     RequestedProcCode(0032,1064)  {line}")
    for line in cs(it,'00400008'): print(f"     ScheduledProtocolCode(0040,0008) {line}")
    for sub in it.get('00400275',{}).get('Value',[]) or []:
        print(f"     RequestAttrSeq(0040,0275): ACC={ (sub.get('00080050',{}).get('Value') or [''])[0]!r}")
        for line in cs(sub,'00321064'): print(f"         >ReqProcCode {line}")
        for line in cs(sub,'00400008'): print(f"         >SchedProtoCode {line}")
PY
}

# ---- estudo de HOJE (real) ----
curl -s "$B/studies?StudyDate=20260618&includefield=0020000D,00100010&limit=200" > /tmp/st.json
SUID=$(python3 - <<'PY'
import json
d=json.load(open('/tmp/st.json'))
def g(it,t):
    v=it.get(t,{}).get('Value',[])
    if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
    return v[0] if v else ''
cand=[it for it in d if 'PHANTON' not in (g(it,'00100010') or '').upper()]
print(cand[-1]['0020000D']['Value'][0] if cand else '')
PY
)
curl -s "$B/studies/$SUID/metadata" > /tmp/m.json
dump_views "$SUID" "HOJE 18/06 (manual) $SUID"

# ---- estudo ANTIGO que veio da worklist do RIS (2025-01-14) ----
curl -s "$B/studies?StudyDate=20250114&includefield=0020000D,00080050&limit=5" > /tmp/old.json
OUID=$(python3 - <<'PY'
import json
d=json.load(open('/tmp/old.json'))
for it in d:
    if (it.get('00080050',{}).get('Value') or [''])[0]:
        print(it['0020000D']['Value'][0]); break
PY
)
curl -s "$B/studies/$OUID/metadata" > /tmp/m.json
dump_views "$OUID" "ANTIGO 14/01/25 (worklist RIS) $OUID"
