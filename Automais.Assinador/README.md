# Automais.Assinador

Serviço .NET autônomo de **assinatura digital PAdES (ICP-Brasil)** de PDFs, via iText, no
modelo de **assinatura diferida (two-step)** — a chave privada nunca chega aqui.

É consumido por HTTP pelos produtos Automais (ex.: SMSMarica). O fluxo:

1. `POST /pades/preparar` — recebe o PDF + a cadeia do certificado do signatário, reserva o
   placeholder de assinatura, aplica o carimbo visual fixo e devolve **o hash a ser assinado**
   + um `transferState` opaco (stateless: o estado volta ao chamador).
2. O cliente (agente local → VIDaaS Connect → HSM da VALID, ou token/A1) assina o hash.
3. `POST /pades/concluir` — recebe `transferState` + a assinatura crua, embute o CMS no PDF e
   devolve o **PDF assinado** + a identidade do certificado (titular, emissor, CPF).

Porta padrão: **5082**. OpenAPI/Scalar em `/docs`. Health em `/health`.

## Licença

Este serviço usa **iText** sob **AGPL-3.0**. Por isso o serviço é **aberto** (código-fonte
público) e **isolado por HTTP** — o que satisfaz a AGPL sem contaminar os consumidores que o
chamam pela rede. **Não** linkar este assembly in-process em produto fechado; usar sempre via API.
O PDF assinado carrega a atribuição do iText nos metadados (obrigação AGPL).
