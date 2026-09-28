#!/usr/bin/env bash
# Provisiona a VM IA-01 (159): build do llama.cpp com flags nativas + download dos modelos GGUF.
# Rodar NA VM como becape. Downloads e build são longos: use nohup (a VPN derruba sessão longa):
#   nohup bash instalar_vm.sh > /tmp/instalar.log 2>&1 &
set -euo pipefail

MODELOS=/opt/modelos
SRC=$HOME/llama.cpp

sudo apt-get -o DPkg::Lock::Timeout=2400 update
sudo apt-get -o DPkg::Lock::Timeout=2400 install -y build-essential cmake git python3-pip \
  python3-venv libcurl4-openssl-dev htop numactl

# ---- llama.cpp (GGML_NATIVE liga AVX2/FMA do E5-2699 v4) ----
if [ ! -d "$SRC" ]; then git clone --depth 1 https://github.com/ggml-org/llama.cpp "$SRC"; fi
cmake -S "$SRC" -B "$SRC/build" -DGGML_NATIVE=ON -DLLAMA_CURL=ON -DCMAKE_BUILD_TYPE=Release
cmake --build "$SRC/build" --config Release -j8 --target llama-server llama-bench llama-cli
sudo install -m755 "$SRC"/build/bin/llama-{server,bench,cli} /usr/local/bin/

# ---- modelos ----
sudo mkdir -p "$MODELOS" && sudo chown "$USER" "$MODELOS"
python3 -m venv ~/.venv-hf && ~/.venv-hf/bin/pip -q install -U "huggingface_hub[cli]"
HF() { ~/.venv-hf/bin/hf download "$1" --include "$2" --local-dir "$MODELOS/$3"; }

# Candidatos (ver relatório: por que cada um está aqui)
HF unsloth/Qwen3-30B-A3B-Instruct-2507-GGUF  "*Q4_K_M*"  qwen3-30b-a3b   # MoE 3B ativos — favorito p/ CPU
HF unsloth/gpt-oss-20b-GGUF                  "*F16*"     gpt-oss-20b     # MoE 3,6B ativos (MXFP4 nativo)
HF unsloth/Qwen3-4B-Instruct-2507-GGUF       "*Q4_K_M*"  qwen3-4b        # piso de latência
HF unsloth/DeepSeek-R1-Distill-Qwen-14B-GGUF "*Q4_K_M*"  deepseek-r1-14b # só p/ provar a tese do relatório
HF unsloth/Llama-3.3-70B-Instruct-GGUF       "*Q4_K_M*"  llama33-70b     # teto de qualidade (lento; referência)

ls -lhR "$MODELOS"
echo "PRONTO. Subir um servidor, ex.:"
echo "  llama-server -m $MODELOS/qwen3-30b-a3b/*.gguf -t 8 -c 32768 --parallel 2 --cache-reuse 256 --jinja --host 0.0.0.0 --port 8080"
