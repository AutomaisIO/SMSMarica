# SMSMais.EquipamentoSim

Simulador de equipamento DICOM (mamógrafo) para testar o ciclo completo de **Solicitação de Exame → Worklist → Execução** do sistema SMSMarica, sem precisar de um equipamento real.

CLI em Python que conversa direto com o `dcm4chee` via DICOM:
- **C-FIND** para puxar a worklist do dia.
- **C-STORE** para enviar imagens DICOM (fantasmas ou reais).

## Instalação

Pré-requisito: Python 3.11+ instalado e no PATH.

### Opção A — Com venv (recomendado)

```powershell
cd "C:\Projetos GIT\SMSMarica\SMSMais.EquipamentoSim"
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .
equipamento mwl
```

> Em Linux/macOS: `source .venv/bin/activate`.

### Opção B — Sem venv (Python da Microsoft Store)

O Python instalado pela Microsoft Store **não** adiciona `Scripts\` ao PATH,
então `equipamento` não fica acessível mesmo após `pip install -e .`. Use a
forma `python -m`:

```cmd
cd "C:\Projetos GIT\SMSMarica\SMSMais.EquipamentoSim"
pip install -e .
python -m equipamento_sim mwl
python -m equipamento_sim exec --accession SMS2026000001 --fantasma
```

Funciona idêntico ao `equipamento`, só muda o prefixo.

Se aparecer erro `execução de scripts desabilitada` no PowerShell, rode antes:
`Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.

## Defaults

Os valores default abaixo são lidos de variáveis de ambiente prefixadas `EQSIM_*` (ou sobrescritos por flags `--host`, `--port`, etc):

| Variável | Default |
|---|---|
| `EQSIM_HOST` | `pacs.marica.automais.cloud` |
| `EQSIM_PORT` | `11112` |
| `EQSIM_CALLING_AE` | `MAMO-SIM` |
| `EQSIM_CALLED_AE_MWL` | `WORK-CDT` |
| `EQSIM_CALLED_AE_STORE` | `PACS-CDT` |

> **Sobre os AE titles**: o dcm4chee da SMS Maricá tem AEs distintos por papel — **`PACS-CDT`** recebe imagens (C-STORE) e **`WORK-CDT`** serve worklist (MWL/UPS). Os AEs **`DCM4CHEE`/`WORKLIST`** continuam ativos como alias legado do mesmo acervo. O dcm4chee está em modo `unsecure` (não valida o Calling AE), então o `EQSIM_CALLING_AE` (`MAMO-SIM`) não precisa ser pré-cadastrado.

## Comandos

### `equipamento mwl` — Listar worklist

```powershell
equipamento mwl
equipamento mwl --modalidade MG
equipamento mwl --accession SMS2026000001
```

Saída:
```
┃ Accession       ┃ Paciente            ┃ Agendado            ┃ Modal ┃ Descrição
┃ SMS2026000001   ┃ MARIA DA SILVA      ┃ 2026-05-24 10:30    ┃ MG    ┃ MAMOGRAFIA BILATERAL
┃ SMS2026000002   ┃ JOAO PEREIRA        ┃ 2026-05-24 11:00    ┃ MG    ┃ MAMOGRAFIA BILATERAL
```

### `equipamento exec` — Simular execução

```powershell
# Imagem fantasma (gradiente sintético — basta para testes)
equipamento exec --accession SMS2026000001 --fantasma

# Imagem real (JPEG/PNG é convertida para DICOM monocromo 16-bit)
equipamento exec --accession SMS2026000001 --imagem .\examples\mamo.jpg
```

Fluxo interno:
1. C-FIND para localizar o worklist item por AccessionNumber.
2. Monta DICOM herdando `PatientName/ID/BirthDate/Sex` e `StudyInstanceUID` do worklist (fundamental — é a chave que amarra o study à solicitação no SMSMarica).
3. C-STORE no AE `PACS-CDT`.

Após o sucesso, o `SincronizadorExamesService` do SMSMais.Api detecta o study no próximo polling (~30s) e marca a solicitação como **Realizada**. Quando o radiologista finaliza o laudo, vira **Laudada**.

## Troubleshooting

- **`Não foi possível associar`** — verifique se o firewall permite a porta `11112` (e que o host responde: `Test-NetConnection pacs.marica.automais.cloud -Port 11112`).
- **`Nenhum item encontrado`** — confirme que existe uma `SolicitacaoExame` com status `Agendada` para o AccessionNumber (o worklist item só é criado quando o POST UPS-RS retorna 201). Cheque o log do backend.
- **`status 0xA700`** (Refused: Out of Resources) — espaço/permissões do storage no dcm4chee. Olhe `/opt/wildfly/standalone/log/server.log`.

## Roadmap

- `equipamento exec --num-frames 4` — simular múltiplas imagens por estudo
- `equipamento cancel --accession N --motivo "..."` — MPPS Discontinued
- `equipamento watch` — modo daemon: puxa worklist a cada N segundos e executa automaticamente (load test)
- Suporte a Transfer Syntax JPEG Baseline para imagens compactadas
