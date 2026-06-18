#!/bin/bash
B=http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs

post_pat () { # $1=json
  curl -s -o /dev/null -w "  patients=%{http_code}" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/patients"
}
post_mwl () { # $1=json
  curl -s -o /tmp/r.txt -w "  mwlitems=%{http_code}\n" -X POST -H "Content-Type: application/dicom+json" --data "$1" "$B/mwlitems"
}

############ TESTE-1: casa por DESCRICAO (sem code sequence) ############
echo "== TESTE-1 (descricao exata 'MAMO BILATERAL') =="
post_pat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"TESTE1^DESC EXATA"}]},"00100020":{"vr":"LO","Value":["TESTE1"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
post_mwl '{
 "00080050":{"vr":"SH","Value":["2026009901"]},
 "00080090":{"vr":"PN","Value":[]},
 "00081030":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"TESTE1^DESC EXATA"}]},
 "00100020":{"vr":"LO","Value":["TESTE1"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["2.25.700000000000000000000000000000000001"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00401001":{"vr":"SH","Value":["2026009901"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["150000"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400009":{"vr":"SH","Value":["2026009901"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}'

############ TESTE-2: casa por CODIGO (Requested Proc Code + Scheduled Protocol Code) ############
echo "== TESTE-2 (com code sequences) =="
post_pat '{"00100010":{"vr":"PN","Value":[{"Alphabetic":"TESTE2^COM CODIGO"}]},"00100020":{"vr":"LO","Value":["TESTE2"]},"00100030":{"vr":"DA","Value":["19850113"]},"00100040":{"vr":"CS","Value":["F"]}}'
post_mwl '{
 "00080050":{"vr":"SH","Value":["2026009902"]},
 "00080090":{"vr":"PN","Value":[]},
 "00081030":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00100010":{"vr":"PN","Value":[{"Alphabetic":"TESTE2^COM CODIGO"}]},
 "00100020":{"vr":"LO","Value":["TESTE2"]},
 "00100030":{"vr":"DA","Value":["19850113"]},
 "00100040":{"vr":"CS","Value":["F"]},
 "0020000D":{"vr":"UI","Value":["2.25.700000000000000000000000000000000002"]},
 "00321060":{"vr":"LO","Value":["MAMO BILATERAL"]},
 "00321064":{"vr":"SQ","Value":[{
   "00080100":{"vr":"SH","Value":["MAMOBILAT"]},
   "00080102":{"vr":"SH","Value":["FDR"]},
   "00080104":{"vr":"LO","Value":["MAMO BILATERAL"]}
 }]},
 "00401001":{"vr":"SH","Value":["2026009902"]},
 "00401003":{"vr":"SH","Value":["ROUTINE"]},
 "00400100":{"vr":"SQ","Value":[{
   "00080060":{"vr":"CS","Value":["MG"]},
   "00400001":{"vr":"AE","Value":["FDR-MAMO"]},
   "00400002":{"vr":"DA","Value":["20260618"]},
   "00400003":{"vr":"TM","Value":["150100"]},
   "00400007":{"vr":"LO","Value":["MAMO BILATERAL"]},
   "00400008":{"vr":"SQ","Value":[{
     "00080100":{"vr":"SH","Value":["MAMOBILAT"]},
     "00080102":{"vr":"SH","Value":["FDR"]},
     "00080104":{"vr":"LO","Value":["MAMO BILATERAL"]}
   }]},
   "00400009":{"vr":"SH","Value":["2026009902"]},
   "00400010":{"vr":"SH","Value":["FDR-MAMO"]},
   "00400020":{"vr":"CS","Value":["SCHEDULED"]}
 }]}
}'

echo
echo "== Conferindo o que ficou armazenado =="
curl -s "$B/mwlitems?ScheduledProcedureStepSequence.ScheduledStationAETitle=FDR-MAMO&includefield=all&limit=20" > /tmp/c.json
python3 <<'PY'
import json
arr=json.load(open('/tmp/c.json'))
print("itens p/ FDR-MAMO:",len(arr))
for it in arr:
    def g(t,it=it):
        v=it.get(t,{}).get('Value',[])
        if v and isinstance(v[0],dict): return v[0].get('Alphabetic','')
        return v[0] if v else ''
    sps=it.get('00400100',{}).get('Value',[{}])[0]
    spsid=(sps.get('00400009',{}).get('Value') or [''])[0]
    spsdesc=(sps.get('00400007',{}).get('Value') or [''])
    spsdesc=spsdesc[0] if spsdesc else ''
    proto=sps.get('00400008',{}).get('Value',[])
    pc=''
    if proto:
        p=proto[0]; pc=f"{(p.get('00080100',{}).get('Value') or [''])[0]}/{(p.get('00080102',{}).get('Value') or [''])[0]}"
    print(f"ACC={g('00080050')!r:12} RPID={g('00401001')!r:12} SPSID={spsid!r:12} desc={g('00321060')!r:18} spsdesc={spsdesc!r:16} protoCode={pc!r}")
PY
