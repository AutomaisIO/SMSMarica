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

Mecanismos de conformidade já implementados:

- **`LICENSE`** com o texto completo da AGPL-3.0 na raiz de `Automais.Assinador/`.
- **`GET /source`** — oferta da *Corresponding Source* a quem interage pela rede (AGPL §13),
  apontando para o repositório público (config `Assinador:FonteUrl`).
- A string *Producer* do PDF assinado já nomeia o iText (atribuição padrão).

> ⚠️ **Obrigação pendente (decisão do responsável):** a AGPL §13 exige que a *Corresponding
> Source* deste serviço esteja **publicamente disponível**. Hoje o código vive no monorepo
> **privado**. Para fechar a conformidade é preciso **publicar o fonte de `src/` num repositório
> público** (extraindo-o do monorepo, que contém PII) **ou** adquirir a **licença comercial do
> iText (Apryse)** e então remover esta narrativa AGPL. Ajuste `Assinador:FonteUrl` para o repo
> público real assim que existir.
