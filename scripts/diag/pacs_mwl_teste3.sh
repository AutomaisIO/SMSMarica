#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs

echo "=== Limpando entradas FDR-MAMO ==="
curl -s "$B/mwlitems?ScheduledProcedureStepSequence.ScheduledStationAETitle=FDR-MAMO&includefield=all&limit=50" > /tmp/del.json
python3 - <<'PY' > /tmp/dellist.txt
import json
arr=json.load(open('/tmp/del.json'))
for it in arr:
    suid=(it.get('0020000D',{}).get('Value') or [''])[0]
    sps=it.get('00400100',{}).get('Value',[{}])[0]
    spsid=(sps.get('00400009',{}).get('Value') or [''])[0]
    if suid and spsid: print(suid, spsid)
PY
while read SUID SPS; do
  [ -z "$SUID" ] && continue
  curl -s -o /dev/null -w "  DELETE $SPS -> %{http_code}\n" -X DELETE "$B/mwlitems/$SUID/$SPS"
done < /tmp/dellist.txt

mkpat () { curl -s -o /dev/null -w "  patients=%{http_code}" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/patients"; }
mkmwl () { curl -s -o /dev/null -w "  mwlitems=%{http_code}\n" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/mwlitems"; }

# Monta um item: $1=acc $2=pid $3=nome(PN) $4=studyUID
item () {
cat <<JSON
{
 "00080050":{"vr":"SH","Value":["$1"]},
 "00081030":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"$3"}]},
 "00100020":{"vr":"LO","Value":["$2"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["$4"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00401001":{"vr":"SH","Value":["$1"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["151000"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400009":{"vr":"SH","Value":["$1"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}
JSON
}

echo
echo "=== A: UID raiz OID 1.2.826  | nome FAMILY^GIVEN ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"UIDOID^TESTE A"}]},"00100020":{"vr":"LO","Value":["TA260618"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl "$(item 2026009911 TA260618 'UIDOID^TESTE A' 1.2.826.0.1.3680043.8.498.20260618000000011)"

echo "=== B: UID raiz 2.25 (padrao atual) | nome FAMILY^GIVEN  [CONTROLE] ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"UID225^TESTE B"}]},"00100020":{"vr":"LO","Value":["TB260618"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl "$(item 2026009912 TB260618 'UID225^TESTE B' 2.25.700000000000000000000000000000000012)"

echo "=== C: UID raiz OID 1.2.826 | nome family VAZIO (mimica RIS) ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"^TESTE C NOMEVAZIO"}]},"00100020":{"vr":"LO","Value":["TC260618"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl "$(item 2026009913 TC260618 '^TESTE C NOMEVAZIO' 1.2.826.0.1.3680043.8.498.20260618000000013)"

echo
echo "=== Worklist final ==="
curl -s "$B/mwlitems?ScheduledProcedureStepSequence.ScheduledStationAETitle=FDR-MAMO&includefield=all&limit=20" > /tmp/c.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/c.json'))
for it in arr:
    def g(t,it=it):
        v=it.get(t,{}).get('Value',[])
        if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
        return v[0] if v else ''
    print(f"NAME={g('00100010')!r:22} PID={g('00100020')!r:10} ACC={g('00080050')!r:12} UID={g('0020000D')!r}")
PY
