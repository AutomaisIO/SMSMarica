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

## Licença (AGPL-3.0 — iText)

Este serviço linka **iText** in-process, que é **AGPL-3.0**. Para que a AGPL **não** contamine os
produtos fechados que o consomem, ele é mantido como **serviço isolado por HTTP** (nunca linkado
in-process em produto fechado — sempre via API) e o próprio código deste serviço é distribuído
sob AGPL-3.0.

Conformidade AGPL implementada:

- **Fonte público (AGPL §13):** o código deste serviço está publicamente disponível em
  **https://github.com/AutomaisIO/Automais.Assinador**, espelhado **automaticamente pelo CI**
  (workflow `publicar-fonte-assinador`) a cada mudança em `Automais.Assinador/**`. É a
  *Corresponding Source* da versão em execução.
- **`LICENSE`** com o texto completo da AGPL-3.0 na raiz do repositório.
- **`GET /source`** — oferta da *Corresponding Source* a quem interage pela rede, apontando
  para o repo público (config `Assinador:FonteUrl`).
- A string *Producer* do PDF assinado nomeia o iText (atribuição AGPL mantida — removê-la
  exigiria licença comercial do iText/Apryse). O branding Automais fica só em *Creator*/*Author*.

> Observação: o espelho público é um *snapshot* (sem histórico do monorepo privado, que contém
> PII). Isso satisfaz o §13, que exige a fonte correspondente da versão distribuída — não o
> histórico de desenvolvimento.
