#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs
curl -s "$B/mwlitems?includefield=all&limit=20" > /tmp/all.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/all.json'))
print("total itens MWL:",len(arr))
for it in arr:
    def g(t,it=it):
        v=it.get(t,{}).get("Value",[])
        if v and isinstance(v[0],dict): return v[0].get("Alphabetic","")
        return v[0] if v else None
    acc=g("00080050"); rp=g("00401001")
    sps=it.get("00400100",{}).get("Value",[{}])[0]
    spsid=(sps.get("00400009",{}).get("Value") or [None])[0]
    def L(x): return len(str(x)) if x else 0
    print(f"ACC={acc!r:14}(len {L(acc)})  RPID={rp!r:14}(len {L(rp)})  SPSID={spsid!r:16}(len {L(spsid)})")
PY
