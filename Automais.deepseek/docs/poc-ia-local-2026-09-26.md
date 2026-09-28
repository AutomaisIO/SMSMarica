# PoC IA local — relatório final (26–27/09/2026)

**Pergunta original:** "dá para levantar uma VM na EVEO com um DeepSeek e usar 100% IA nossa no
robô, sem gastar token?"

**Resposta em uma linha:** a infra funciona e está de pé (VM `IA-01`, benches completos), o
DeepSeek está **descartado com prova nossa** (0 de 24 no contrato de ferramenta), e a conclusão
honesta é: **em CPU pura, nenhum modelo local substitui o Haiku hoje** — latência 2–4× maior e
qualidade abaixo do aceitável nas cenas críticas. O caminho que os números apontam: **GPU de
~R$ 3k por servidor (P40 24GB)** muda o jogo de classe — latência igual ou melhor que o Haiku,
custo zero por token, PII em casa, sem risco de "estourou o limite e o robô parou".

Todo número deste relatório foi **medido** (26–27/09): produção real no banco (`robo_tarefa`,
hora de pico de 25/09), e a suíte de benches na VM (12 cenas reais do robô × 2 passadas ×
5 modelos, velocidade bruta, concorrência).

---

## 1. Infra construída (o que existe agora)

| | |
|---|---|
| VM | **159 `IA-01`** — hv02, grupo HA novo `VMs-R730` (hv02→hv01, nunca hv03) |
| Recursos | **16 vCPU `cpu: host`** (AVX2/FMA dos E5-2699 v4) · 64GB `balloon 0` · 300G thin | 
| Rede | VLAN 40 · `10.90.40.240` (DHCP; reserva no CCR pendente — MAC `BC:24:11:3F:99:8D`) |
| Stack | llama.cpp (build nativo) · 5 modelos GGUF em `/opt/modelos` (~80GB) · `llama-server` :8080, API OpenAI-compatível |
| Descoberta | hv01/hv02 = **2× E5-2699 v4 (88 threads) + 256GB** cada (não estava documentado); ~160–190GB livres por nó |

## 2. Por que NÃO o DeepSeek (agora com prova nossa)

**2.1 O grande (671B) não cabe — e se coubesse, não andaria.** Gerar 1 token em CPU exige ler
os parâmetros ativos: 37B × ~0,55 byte (Q4) ≈ 20GB/token, contra ~65GB/s de banda por socket →
**~2–3 tok/s** mesmo com 768GB de RAM. Uma resposta = 1 minuto só de geração.

**2.2 Os distills falham no que o robô exige.** Medido nas nossas 12 cenas, contrato do motor
real (`tool_choice` obrigatório, resposta só pela ferramenta `responder_cidadao`):

> **DeepSeek-R1-Distill-14B: 24 execuções, 24 violações (100%).** Nunca chamou a ferramenta de
> resposta — ficou "raciocinando" em texto que o motor descarta. Em produção, todo cidadão
> receberia o *"Só um momento…"* e viraria handoff. É o perfil do modelo (raciocínio), o oposto
> do que o ADR-0050 exige.

**2.3 A regra geral que os números confirmaram:** velocidade de geração em CPU ≈ banda de
memória ÷ bytes ativos por token. Por isso MoE pequeno (Qwen3-30B-**A3B**: 3B ativos) gera a
19,5 tok/s enquanto o denso 70B faz 1,3 tok/s no mesmo hardware.

## 3. Bench A — velocidade bruta (16 vCPU)

| Modelo | Prefill 4k (tok/s) | Geração (tok/s) | Observação |
|---|---|---|---|
| **Qwen3-30B-A3B** Q4 | 51,0 | **19,5** | MoE 3B ativos — o mais rápido em geração |
| **gpt-oss-20b** MXFP4 | 53,8 | 13,0 | MoE 3,6B ativos; gasta tokens em "raciocínio" |
| Qwen3-4B Q4 | 74,7 | 19,0 | pequeno e rápido — mas reprova em tudo (§4) |
| DeepSeek-R1-14B Q4 | 24,3 | 6,3 | denso + reasoning: lento e reprovado |
| Llama-3.3-70B Q4 | 5,5 | **1,3** | teto de qualidade, velocidade inviável em CPU |

Contexto: o prefill frio do prompt do robô (~5–7k tokens) custa 1,5–2,5 min. **O cache de
prefixo corta 60–75%** (medido: S01 do gpt-oss 120s→36s) — em produção, com o system por
assunto quente, paga-se o frio uma vez por assunto, não por pessoa.
A/B de vCPU: 8→16 derrubou a cena fria de ~325s para ~120s (prefill escala com cores); a
geração **não muda** (limitada pela banda de memória — mais vCPU/RAM não resolve; 24 vCPU
cruzaria para o 2º socket e tende a piorar).

## 4. Bench B — as 12 cenas reais (contrato × qualidade)

Protocolo: cenas, system e ferramentas exatos do build de produção (skill
`analisar-robo-cenarios-reais`); cada cena 2× (fria/quente); julgamento contra o gabarito de
cada cena.

| Modelo | Contrato (sem violação) | Latência quente p50 | Qualidade (cenas aceitáveis) | Erros graves |
|---|---|---|---|---|
| Haiku 4.5 (produção, referência) | ~100% | **12,5s** (p90 19,4s) | ~10/12 (análise de 01/09; 2/10 no turno crítico) | raros |
| **gpt-oss-20b** | **24/24 ✅** | 36s | **~6/12** | prometeu registro que não fez (S09); ignorou desabafo (S11) |
| **Qwen3-30B-A3B** | 16/24 | 98s (faz 3–4 chamadas reais) | **~2/12** | **inventou CPF/nascimento p/ chamar ferramentas** (S01/S02/S05) e saudou com identidade falsa — o pior comportamento possível p/ o guardrail |
| Qwen3-4B | 6/27 | 123s | ~0 | não segue o contrato |
| DeepSeek-R1-14B | **0/24 ❌** | 85s (sem resposta) | 0 | nunca respondeu pela ferramenta |
| Llama-3.3-70B (3 cenas) | 6/6 ✅ | 69s | 2–3/3 (S07 saiu idêntico ao gabarito) | truncou uma resposta |

**Leituras:**
- **gpt-oss-20b é o único candidato CPU sério**: contrato perfeito e latência quente na casa
  dos 30s — mas responde raso (resolve tudo em 1 chamada, quase não usa as consultas) e cometeu
  2 erros graves de conteúdo. Abaixo do Haiku.
- **Qwen3-30B tem o processo certo e o vício errado**: usa as ferramentas de verdade (fluxos de
  4 chamadas), mas **alucina argumentos de identidade** — comportamento eliminatório. Parte
  pode ser tuning (temperatura, template de tool-call do llama.cpp p/ Qwen3, gramática) — fica
  registrado como experimento pendente, não como veredito definitivo.
- **A qualidade cresce com o tamanho** (70B acerta o que os pequenos erram) — e o 70B só é
  viável com GPU. É o argumento técnico central do §7.

## 5. Bench D — simultaneidade (turnos curtos, 16 vCPU)

| Concorrência | gpt-oss p50 | Qwen3-30B p50 | Vazão 30B |
|---|---|---|---|
| 1 | 10,7s | **1,8s** | 1.626/h |
| 2 | 11,2s | 3,0s | 2.084/h |
| 4 | 19,8s | 5,0s | 2.409/h |
| 8 | 39,0s | 9,0s | **2.695/h** |

Duas conversas simultâneas quase não degradam (o lote compartilha a leitura dos pesos); o
joelho fica entre 4 e 8. Em turnos curtos o Qwen3-30B é 5× o gpt-oss (que queima tokens de
raciocínio até em "bom dia").

## 6. Simulação do momento real — pico de 25/09, 11h–12h (121 tarefas reais)

Mesmas chegadas do banco; serviço = latências quentes medidas × nº de chamadas da tarefa:

| | p50 | p90 | fila (espera p90) |
|---|---|---|---|
| **Haiku (real, medido)** | **15s** | **22s** | — |
| Local gpt-oss, 4 slots | 46s | 92s | 10s |
| Local gpt-oss, 2 slots | 119–449s | 238–638s | colapsa |

A VM **dá vazão** ao pico atual com 4 slots (fila ~zero) — mas cada resposta leva 3–4× o tempo
do Haiku. E o cenário **"robô 100% LLM"** (desligar as respostas por código): pico real de
**3.649 msgs/h** contra teto medido de ~640/h (gpt-oss) a ~2.700/h (30B em turnos curtos) —
**não fecha em CPU** nem no melhor caso; com botões/cortesias continuando por código (que é o
desenho certo), o volume LLM (144/h de pico) cabe com folga.

## 7. Hardware: o que cada passo destrava (e por quê)

| Cenário | Custo | Efeito | Por quê |
|---|---|---|---|
| A. Atual (16 vCPU/64GB) | R$ 0 | piloto tolerante a 30–90s | banda de memória é o teto; feito |
| B. 2ª VM no hv01 (ativo-ativo) | R$ 0 | 2× vazão + failover instantâneo | 2º socket/2º nó = 2ª banda de 65GB/s; inferência é stateless |
| C. Mais vCPU/RAM | R$ 0–4k | ~0% na geração | RAM dá capacidade, não banda; >22 vCPU cruza socket e piora |
| **D. 1× P40 24GB usada POR servidor (2× R$ 3.000 + kits ~R$ 600)** | **~R$ 6,6k únicos** | **Qwen3-30B inteiro em VRAM: ~30–45 tok/s, resposta quente 3–6s, fria <15s; 70B Q4 não cabe, mas 32B denso sim** | banda salta de 65 → 346GB/s; e vira 2 nós GPU redundantes (o ativo-ativo do B) |
| E. 2× T10 alugadas EVEO | R$ 1.690/mês | classe similar ao D sem CAPEX | empata com o D em ~4 meses |
| F. 3º R730 usado no lugar do hv03 | ~R$ 6–12k | `cpu: host` e HA plenos em 3 nós + fecha a 3ª réplica Ceph | R720 (Sandy Bridge) é o freio do cluster |

**Checklist da P40 no R730 (antes de comprar):** fontes 1100W (conferir no iDRAC), kit GPU do
R730 (riser/suportes + **cabo EPS 8p — cabo PCIe comum queima a placa**), slot x16 livre, BIOS
"MMIO above 4GB", passthrough VT-d→VM. Pascal está em fim de linha de driver NVIDIA — para
inferência dedicada via llama.cpp, ok por 2–3 anos.

## 8. Custo e risco (robô)

| | Haiku (hoje) | CPU local (feito) | 2× P40 (proposto) |
|---|---|---|---|
| Custo | ~R$ 1.200–1.400/mês | ~R$ 0 marginal | R$ 6,6k únicos |
| Latência p50 | 12,5s | 36–98s | ~3–6s (estimado) |
| Qualidade | referência | abaixo | a validar (30B/32B com tuning; reavaliar cenas) |
| Indisponibilidade por limite de conta | **aconteceu 22–24/09** | zero | zero |
| PII | sai p/ Anthropic | fica em casa | fica em casa |
| Extras | — | — | Whisper p/ telefonia + embeddings (ADR-0011 adiou por falta de GPU) |

## 9. Recomendações

1. **Não substituir o robô por CPU-only agora** — latência 3–4× e qualidade abaixo do Haiku.
2. **Comprar as 2× P40 (1 por R730)** após o checklist do §7-D — é o degrau de melhor
   custo/benefício já medido contra alternativas; reavaliar então as 12 cenas na GPU com
   Qwen3-30B *tunado* (temperatura baixa, template de tool-call ajustado) e 32B denso.
3. **Manter o desenho híbrido**: respostas por código continuam (botões/cortesia); LLM local
   como motor primário quando passar nas cenas; **Haiku como transbordo/fallback** — o
   `RoboAtendimentoMotorSeletor` já suporta a chave, falta só a 3ª implementação
   (`MotorRobo.Local`, formato OpenAI — fase 2, ~1 dia de .NET).
4. **Módulo Inteligência**: fora desta PoC por decisão (26/09). O desenho do híbrido de PII
   (corte em `dados_tool.py::_formatar()`) fica documentado para quando houver GPU.
5. Registrar a reserva DHCP da IA-01 no CCR na próxima janela com o operador.

---
*Artefatos: `bench/resultados/<modelo>/` (bench_a.json, S*.json, bench_d.json, transcripts),
`bench/rodar_tudo.sh` (suíte), VM 159 no repo EVEO (commit 72a58ad). Baseline Haiku: banco de
produção + análise da skill de 01/09. Cenas: fotografia de 01/09 — regenerar do material vivo
antes de decisões finais de qualidade.*
