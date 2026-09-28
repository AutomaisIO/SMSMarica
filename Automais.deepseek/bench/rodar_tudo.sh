#!/usr/bin/env bash
# Suíte v2 — foco ATENDIMENTO + CACHE, VM com 16 vCPU.
# Cada cena roda 2x: r1 fria, r2 = cache quente (prefixo idêntico -> slot com KV reaproveitado).
#   setsid nohup bash rodar_tudo.sh > /tmp/bench2.log 2>&1 < /dev/null &
set -uo pipefail
cd "$(dirname "$0")"
export CENAS_DIR=$HOME/bench/cenarios
PORT=8080
BASE=http://127.0.0.1:$PORT/v1
T=16

gguf_de() { ls /opt/modelos/$1/*.gguf 2>/dev/null | sort | head -1; }
espera_saude() { for _ in $(seq 1 120); do curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:$PORT/health | grep -q 200 && return 0; sleep 5; done; return 1; }
para_servidor() { pkill -f 'llama-server -m' 2>/dev/null; sleep 3; }

roda_modelo() {  # nome dir cenas bench_d(0/1)
  local nome=$1 dir=$2 cenas=$3 bd=$4
  local gguf; gguf=$(gguf_de "$dir")
  [ -z "$gguf" ] && { echo "== $nome: SEM GGUF, pulando"; return; }
  mkdir -p resultados/$nome
  echo "== $nome: bench A $(date +%H:%M)"
  llama-bench -m "$gguf" -t $T -p 512,4096 -n 128 -r 2 -o json > resultados/$nome/bench_a.json 2>resultados/$nome/bench_a.err

  echo "== $nome: servidor (parallel 2, c 32768) $(date +%H:%M)"
  para_servidor
  nohup llama-server -m "$gguf" -t $T -c 32768 --parallel 2 --cache-reuse 256 --jinja \
        --host 127.0.0.1 --port $PORT > resultados/$nome/server.log 2>&1 &
  espera_saude || { echo "== $nome: servidor NAO subiu"; para_servidor; return; }

  echo "== $nome: bench B 2x (fria/quente) $(date +%H:%M)"
  python3 -u rodar_cena.py --base $BASE --modelo $nome --repeticoes 2 \
      ${cenas:+--cenas $cenas} > resultados/$nome/bench_b.txt 2>&1

  if [ "$bd" = 1 ]; then
    echo "== $nome: bench D (concorrencia) $(date +%H:%M)"
    para_servidor
    nohup llama-server -m "$gguf" -t $T -c 32768 --parallel 4 --cache-reuse 256 --jinja \
          --host 127.0.0.1 --port $PORT > resultados/$nome/server_d.log 2>&1 &
    espera_saude && python3 -u bench_d.py --base $BASE --modelo $nome \
        --concorrencias 1,2,4,8 --por_nivel 16 > resultados/$nome/bench_d.txt 2>&1
  fi
  para_servidor
  echo "== $nome: FIM $(date +%H:%M)"
}

roda_modelo gpt-oss-20b     gpt-oss-20b     ""            1
roda_modelo qwen3-30b-a3b   qwen3-30b-a3b   ""            1
roda_modelo deepseek-r1-14b deepseek-r1-14b ""            0
roda_modelo qwen3-4b        qwen3-4b        ""            1
roda_modelo llama33-70b     llama33-70b     "S01,S03,S07" 0
echo "BENCHES CONCLUIDOS $(date)"
