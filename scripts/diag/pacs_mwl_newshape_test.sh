#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs
SUID="2.25.88888888888888888888888888888888888"

cat >/tmp/pat.json <<'JSON'
{"00100010":{"vr":"PN","Value":[{"Alphabetic":"ZZ^TESTE WORKLIST"}]},
 "00100020":{"vr":"LO","Value":["ZZTESTPAC"]},
 "00100030":{"vr":"DA","Value":["19900101"]},
 "00100040":{"vr":"CS","Value":["F"]}}
JSON

cat >/tmp/mwl.json <<JSON
{"00080050":{"vr":"SH","Value":["ZZTEST0001"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"ZZ^TESTE WORKLIST"}]},
 "00100020":{"vr":"LO","Value":["ZZTESTPAC"]},
 "00100030":{"vr":"DA","Value":["19900101"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["$SUID"]},
 "00321060":{"vr":"LO","Value":["TESTE"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
    "00080060":{"vr":"CS","Value":["MG"]},
    "00400001":{"vr":"AE","Value":["ZZTEST"]},
    "00400002":{"vr":"DA","Value":["20260618"]},
    "00400003":{"vr":"TM","Value":["120000"]},
    "00400007":{"vr":"LO","Value":["TESTE"]},
    "00400010":{"vr":"SH","Value":["ZZTEST"]},
    "00400020":{"vr":"CS","Value":["SCHEDULED"]}}]}}
JSON

echo "=== POST /patients (status) ==="
curl -s -o /dev/null -w "%{http_code}\n" -X POST -H "Content-Type: application/dicom+json" --data @/tmp/pat.json "$B/patients"

echo "=== POST /mwlitems (status) ==="
curl -s -o /tmp/resp.txt -w "%{http_code}\n" -X POST -H "Content-Type: application/dicom+json" --data @/tmp/mwl.json "$B/mwlitems"
echo "--- corpo resposta (se houver) ---"; head -c 400 /tmp/resp.txt; echo

echo "=== GET de volta: como ficou o item (SPS ID atribuido?) ==="
curl -s "$B/mwlitems?StudyInstanceUID=$SUID&includefield=all" > /tmp/back.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/back.json'))
print("itens encontrados:",len(arr))
for it in arr:
    def g(t,it=it):
        v=it.get(t,{}).get("Value",[]);
        return v[0] if v else None
    sps=it.get("00400100",{}).get("Value",[{}])[0]
    spsid=(sps.get("00400009",{}).get("Value") or [None])[0]
    print("AccessionNumber:",g("00080050"))
    print("SPS ID (0040,0009) atribuido:",repr(spsid))
    print("RequestedProcedureID (0040,1001):",repr(g("00401001")))
PY

echo "=== Limpeza: tenta DELETE ==="
# tenta descobrir o sps id do GET e deletar
SPS=$(python3 -c "import json;a=json.load(open('/tmp/back.json'));s=a[0]['00400100']['Value'][0].get('00400009',{}).get('Value',[''])[0] if a else '';print(s)")
echo "sps para delete: '$SPS'"
curl -s -o /dev/null -w "DELETE status=%{http_code}\n" -X DELETE "$B/mwlitems/$SUID/$SPS"
