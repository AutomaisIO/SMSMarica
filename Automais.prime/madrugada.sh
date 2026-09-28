#!/usr/bin/env bash
# Condutor do mutirão de duplicatas para rodar sem ninguém olhando.
#
# Por que um condutor em shell e não um Python longo: esta máquina mata processo longo por pressão
# de memória (aconteceu 3x em 25/09) e o túnel SSH cai. Cada passo aqui é um processo CURTO — se um
# morrer, o laço segue no próximo, e nada se perde porque todos os passos são idempotentes:
#   - `fundir_com_repontamento.py` descarta par já fundido antes de aplicar o --limite;
#   - `conferir_cpf_receita.py` não reconsulta CPF que já tem veredito, e grava a cada resposta.
#
# Ordem: esgota o que a Receita já liberou, depois confere mais CPFs, depois funde os novos. Repete.
#
# Uso:  bash madrugada.sh            (registra em capturas/hub/madrugada.log)
set -u
cd "$(dirname "$0")"

LOG=capturas/hub/madrugada.log
PORTA=15081
LOTE=40                      # 50 completou sempre; 40 dá margem contra a morte por memória
MAX_CICLOS=${MAX_CICLOS:-40}
HUB_CHUNK=150                # consultas por rodada: se o Hub limitar, perde-se pouco

diga() { echo "[$(date '+%d/%m %H:%M:%S')] $*" | tee -a "$LOG"; }

tunel_ok() { curl -s -o /dev/null --max-time 8 "http://127.0.0.1:$PORTA/fhir/Patient?_count=0"; }

garantir_tunel() {
  if tunel_ok; then return 0; fi
  diga "tunel caiu — reabrindo"
  pkill -f "$PORTA:127.0.0.1:5081" 2>/dev/null
  ssh -o BatchMode=yes -o ConnectTimeout=20 -o ServerAliveInterval=30 \
      -o ServerAliveCountMax=10 -o ExitOnForwardFailure=yes \
      -i ~/.ssh/id_ed25519_smsmarica -f -N -L "$PORTA:127.0.0.1:5081" root@smsmarica.online
  sleep 3
  tunel_ok || { diga "NAO consegui reabrir o tunel — abortando"; return 1; }
  diga "tunel de volta"
}

diga "=== inicio ==="

for ciclo in $(seq 1 "$MAX_CICLOS"); do
  garantir_tunel || break

  # ---- 1. fundir o que a Receita já liberou, em lotes curtos, até esgotar
  esgotou=0
  while :; do
    garantir_tunel || break 2
    saida=$(python -u fundir_com_repontamento.py --limite "$LOTE" --gravar \
              --api "http://127.0.0.1:$PORTA" 2>&1)
    n=$(printf '%s\n' "$saida" | grep -cE '^   ok ')
    prob=$(printf '%s\n' "$saida" | grep -cE 'PARANDO|ERRO ')
    printf '%s\n' "$saida" >> "$LOG"
    diga "ciclo $ciclo: fundidos $n neste lote"
    if [ "$prob" -gt 0 ]; then
      diga "PARANDO TUDO — o script acusou problema de conferencia (linha sobrando). Precisa de gente."
      break 3
    fi
    [ "$n" -eq 0 ] && { esgotou=1; break; }
  done

  # ---- 2. conferir mais CPFs na Receita (libera o próximo bloco)
  if [ "$esgotou" -eq 1 ]; then
    if [ -z "${HUB_TOKEN:-}" ]; then
      diga "sem HUB_TOKEN no ambiente — nao ha mais o que fundir e nao posso conferir. Encerrando."
      break
    fi
    diga "ciclo $ciclo: nada liberado sobrou; conferindo ate $HUB_CHUNK CPFs na Receita"
    python -u conferir_cpf_receita.py --pendentes --direto --max-consultas "$HUB_CHUNK" >> "$LOG" 2>&1
    liberou=$(python - <<'PY'
import sys, pathlib
sys.path.insert(0, ".")
from fundir_com_repontamento import pares
print(len(pares([])))
PY
)
    diga "ciclo $ciclo: agora ha $liberou pares liberados"
    [ "${liberou:-0}" -eq 0 ] && { diga "nada mais a liberar — encerrando"; break; }
  fi
done

diga "=== fim ==="
python - <<'PY' | tee -a "$LOG"
import sys, pathlib
sys.path.insert(0, str(pathlib.Path("..").resolve() / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn
with conn() as c, c.cursor() as cur:
    cur.execute("select count(*) from fhir.patient where not is_deleted and content->'link' is not null")
    print("pacientes com link:", cur.fetchone()[0])
    cur.execute("""select count(*) from (select cpf from fhir.patient where not is_deleted
                   and coalesce(cpf,'')<>'' group by cpf having count(*)>1) x""")
    print("grupos com CPF repetido:", cur.fetchone()[0])
    cur.execute("""select count(*) from fhir.patient where not is_deleted
                   and content->'link'->0->>'type'='replaced-by'
                   and jsonb_array_length(coalesce(content->'identifier','[]'::jsonb))>0""")
    print("lapides COM identifier (deve ser 0):", cur.fetchone()[0])
PY
