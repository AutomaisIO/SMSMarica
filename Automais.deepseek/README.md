# Automais.deepseek — PoC de IA local no datacenter EVEO

PoC para validar um LLM **local, CPU-only**, rodando no nosso cluster Proxmox (colocation EVEO
SP2), como substituto/complemento do Claude nos dois cenários de IA do SMSMais:

1. **Robô de atendimento WhatsApp** (hoje Haiku 4.5 via Messages API — ADR-0050);
2. **Módulo Inteligência** (hoje Opus via assinatura Claude Code — o híbrido proposto processa
   as linhas com PII localmente e deixa só o planejamento da query no Claude).

Motivação: custo (~US$ 55/semana só no robô), **resiliência** (o robô ficou parado de 22 a
24/09 por estouro do limite da conta Anthropic) e **LGPD** (hoje até 100 linhas de SELECT com
nome/CPF/CNS entram no contexto enviado à Anthropic).

> **Por que o nome "deepseek" se o DeepSeek não é o modelo?** O projeto nasceu da pergunta
> "dá pra rodar um DeepSeek nosso?". A resposta (ver relatório em `docs/`) é que o DeepSeek
> real (671B) não cabe no nosso hardware e os distills dele são fracos em tool-calling — a PoC
> compara os modelos que de fato servem (Qwen3-30B-A3B, gpt-oss-20b etc.). O nome ficou.

## Infra

| | |
|---|---|
| VM | **159 `IA-01`** no cluster AutomaisCloud (EVEO SP2), grupo HA `VMs-R730` (hv02 pref., hv01) |
| Recursos | 8 vCPU **`cpu: host`** (AVX2 dos E5-2699 v4 — nunca `x86-64-v2-AES`) · 64GB `balloon 0` · 300G thin em Storage-VMS |
| SO | Ubuntu 24.04 cloud, usuário `becape`, chave `id_ed25519_automais_fw`, VLAN 40 (IP por DHCP) |
| Stack | llama.cpp (`llama-server` na porta 8080, API OpenAI-compatível) + modelos GGUF em `/opt/modelos` |

Documentação do datacenter: `C:\Projetos GIT\Automais\EVEO` (README = tabela viva de VMs).

## Layout

```
setup/instalar_vm.sh        # provisiona a VM: build llama.cpp + download dos modelos
bench/tools_openai.py       # as ferramentas do robô no formato OpenAI (espelho do RoboFerramentaCatalogo)
bench/rodar_cena.py         # roda uma cena real do robô contra o llama-server e grava transcript
bench/bench_c.py            # cenário Inteligência: resumo/análise local de linhas de query
bench/bench_d.py            # throughput paralelo (cenário "robô 100% LLM")
bench/resultados/           # JSONs de resultado (SEM dado de paciente — cenas usam os textos da skill)
docs/                       # relatório final (md + html)
```

As cenas vêm da skill `analisar-robo-cenarios-reais` (12 cenas reais + gabarito do juiz).
Saída de teste que contenha dado de paciente **não entra no repositório** (regra da casa).
