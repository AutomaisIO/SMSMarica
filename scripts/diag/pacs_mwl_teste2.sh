#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs

echo "=== Limpando TODAS as entradas FDR-MAMO da worklist ==="
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
  code=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$B/mwlitems/$SUID/$SPS")
  echo "  DELETE $SPS -> $code"
done < /tmp/dellist.txt

mkpat () { curl -s -o /dev/null -w "  patients=%{http_code}" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/patients"; }
mkmwl () { curl -s -o /dev/null -w "  mwlitems=%{http_code}\n" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/mwlitems"; }

echo
echo "=== T4: SO muda o StudyInstanceUID para raiz OID 1.2.826 (resto = padrao atual) ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"ALMEIDA^MARIANA ALVES ANTUNES"}]},"00100020":{"vr":"LO","Value":["10861085744"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl '{
 "00080050":{"vr":"SH","Value":["2026009904"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"T4^UID 1.2 OID"}]},
 "00100020":{"vr":"LO","Value":["10861085744"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["1.2.826.0.1.3680043.8.498.20260618000000004"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00401001":{"vr":"SH","Value":["2026009904"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["150400"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400009":{"vr":"SH","Value":["2026009904"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}'

echo "=== T5: SO adiciona StudyID (0020,0010) (UID continua 2.25) ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"ALMEIDA^MARIANA ALVES ANTUNES"}]},"00100020":{"vr":"LO","Value":["10861085744"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl '{
 "00080050":{"vr":"SH","Value":["2026009905"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"T5^COM STUDYID"}]},
 "00100020":{"vr":"LO","Value":["10861085744"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["2.25.700000000000000000000000000000000005"]},
 "00200010":{"vr":"SH","Value":["2026009905"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00401001":{"vr":"SH","Value":["2026009905"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["150500"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400009":{"vr":"SH","Value":["2026009905"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}'

echo "=== T3: MIMICA o RIS antigo (UID 1.2.826 + StudyID + PatientName family vazio + PatientID curto) ==="
mkpat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"^MARIANA ALVES ANTUNES"}]},"00100020":{"vr":"LO","Value":["260618003"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
mkmwl '{
 "00080050":{"vr":"SH","Value":["2026009903"]},
 "00081030":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"^MARIANA ALVES ANTUNES"}]},
 "00100020":{"vr":"LO","Value":["260618003"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["1.2.826.0.1.3680043.8.498.20260618000000003"]},
 "00200010":{"vr":"SH","Value":["2026009903"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00401001":{"vr":"SH","Value":["2026009903"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["150300"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400009":{"vr":"SH","Value":["2026009903"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}'

echo
echo "=== Worklist final p/ FDR-MAMO ==="
curl -s "$B/mwlitems?ScheduledProcedureStepSequence.ScheduledStationAETitle=FDR-MAMO&includefield=all&limit=20" > /tmp/c.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/c.json'))
for it in arr:
    def g(t,it=it):
        v=it.get(t,{}).get('Value',[])
        if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
        return v[0] if v else ''
    print(f"NAME={g('00100010')!r:26} ACC={g('00080050')!r:12} UID={g('0020000D')!r:46} StudyID={g('00200010')!r}")
PY
