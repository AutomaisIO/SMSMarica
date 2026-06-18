#!/bin/bash
echo "=== MWL item bruto (campos de charset e descricoes) ==="
curl -s "http://localhost:8080/dcm4chee-arc/aets/WORK-CDT/rs/mwlitems?includefield=all&limit=5" \
 | python3 -c '
import sys,json
data=json.load(sys.stdin)
for it in data:
    def g(t):
        v=it.get(t,{}).get("Value",[])
        return v[0] if v else None
    print("SpecificCharacterSet (00080005):", repr(g("00080005")))
    print("AccessionNumber   (00080050):", repr(g("00080050")))
    print("PatientName       (00100010):", repr(g("00100010")))
    print("ReqProcDescription(00321060):", repr(g("00321060")))
    seq=it.get("00321064",{}).get("Value",[{}])[0]
    print("  SIGTAP CodeMeaning(00080104):", repr((seq.get("00080104",{}).get("Value") or [None])[0]))
    sps=it.get("00400100",{}).get("Value",[{}])[0]
    print("SPS Description   (00400007):", repr((sps.get("00400007",{}).get("Value") or [None])[0]))
    print("SPS PerfPhysician (00400006):", repr((sps.get("00400006",{}).get("Value") or [None])[0]))
    print("---")
'
