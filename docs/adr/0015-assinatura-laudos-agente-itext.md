# ADR-0015 — Assinatura digital de laudos: Assinador iText aberto + agente local outbound

- **Status**: Aceito
- **Data**: 2026-06-10
- **Decisores**: Bruno (product/eng)
- **Relaciona-se com**: [ADR-0004](./0004-arquitetura-tres-projetos.md), [ADR-0010](./0010-servico-fhir-autonomo.md) (precedente de serviço autônomo), [ADR-0006](./0006-papel-derivado-e-auditoria-explicita.md)/[ADR-0007](./0007-schema-fhir-separado.md) (papel Médico)

## Contexto

O PDF do laudo é gerado on-demand (QuestPDF, `LaudoPdfRenderer`) **sem validade jurídica plena** — carrega a tarja "sem assinatura ICP-Brasil". A CFM 2.299/2021 exige assinatura digital qualificada (ICP-Brasil) do médico. O certificado real dos médicos é um **e-CPF A3 em nuvem da VALID (VIDaaS)**: a chave privada vive num HSM da VALID, exposta na **loja de certificados do Windows** pelo **VIDaaS Connect** (provedor CSP/KSP). É o que o Adobe usa hoje.

Restrições que moldaram a decisão:
- **iText não assina** — ele monta/cola o PAdES (ByteRange/CMS). Quem assina é a chave, que **nunca sai** do HSM/token. O hash precisa viajar até a chave.
- **iText7 é AGPL** — usá-lo in-process num produto fechado obrigaria a abrir todo o produto.
- **Lacuna Web PKI é pago em produção** (licença por domínio) + um SDK de backend pago.
- **VIDaaS REST (nuvem)** exige onboarding comercial como integrador na Valid (channel/contrato).
- Tudo é **IP da Automais** (não da Secretaria) e pode ser aberto/reutilizado.

## Decisão

### 1. Serviço aberto `Automais.Assinador` (iText) isolado por HTTP

Novo serviço .NET autônomo, **aberto (AGPL)**, espelhando o padrão do [ADR-0010](./0010-servico-fhir-autonomo.md) (solução própria `Automais.Assinador.slnx`, porta 5082, systemd, deploy GitHub Actions). Stateless (Api + Core, sem banco). Faz **só** o PAdES via iText (`PdfSigner` + `ExternalBlankSignatureContainer`/`SignDeferred` + `PdfPKCS7`), em **duas chamadas**: `POST /pades/preparar` (devolve o hash a assinar) e `POST /pades/concluir` (embute o CMS). Aplica o **carimbo visual fixo**.

Como o iText é AGPL e o serviço é **aberto e isolado por HTTP**, a AGPL está satisfeita por construção e **não contamina** os produtos fechados que o chamam pela rede. O PDF assinado carrega a atribuição do iText nos metadados.

### 2. Agente local Automais lançado por **protocolo** `automais-assinador://` (não Web PKI, não VIDaaS REST, não socket)

A assinatura acontece na **máquina do médico**, via um **agente local** (app Windows `Automais.Assinador.Agente`) que assina pela **loja de certificados do Windows** (`X509Store` → `GetRSAPrivateKey().SignHash`), disparando o **VIDaaS Connect** localmente — **mesma experiência do Adobe**, e **sem deal com a Valid** (pega carona no VIDaaS Connect que o médico já tem). Cobre também token A3 físico e A1.

O browser lança o agente via **protocolo customizado** `automais-assinador://assinar?chave=…&server=…`. O **Windows** entrega a chave + a URL do servidor ao agente **por linha de comando** — **sem socket, sem servidor em localhost, sem extensão, sem mixed-content/CORS/PNA**. O agente é **efêmero e sob demanda**: é lançado no clique, faz reivindicar→preparar→assinar→concluir e **sai** (não persiste, não faz polling). A única rede é a chamada HTTPS **de saída** do agente para o backend. O agente é **fino**: acha o certificado pelo CPF + assina o hash. Toda a complexidade PAdES (iText) fica no serviço aberto, server-side — **o PDF não trafega ao agente, só o hash**.

### 3. Fluxo diferido (two-step); a chave privada nunca sai

`iniciar` (médico, JWT) cria o job e devolve uma **chave de uso único** → o front lança o agente com ela → agente reivindica pela chave, recebe o CPF do médico e envia a **cadeia do certificado** → server renderiza o PDF **sem tarja** e chama `Assinador.preparar` → agente **assina o hash** (VIDaaS Connect) → server chama `Assinador.concluir`, **valida que o CPF do certificado == CPF do médico autor** (resolvido no hub FHIR), e persiste o **PDF assinado byte-estável**.

### 4. Auth do agente por **chave de uso único** (não device token)

A identidade do médico vem da **sessão web autenticada**: ao clicar "Assinar", o backend gera uma **chave aleatória** (`LaudoAssinatura.ChaveAgente`), **atada ao job + médico, expira em ~3 min, consumida ao concluir**. O front a passa ao agente na URL do protocolo; o agente a apresenta para reivindicar/preparar/concluir (endpoints `[AllowAnonymous]` — a chave É a autorização). **Sem device token persistente, sem `.json` com CPF/médico** — resolve máquina compartilhada / multi-médico: quem está logado no browser é quem assina, com o cert do CPF dele. A chave só autoriza assinar **aquele** laudo, e a assinatura ainda exige a chave privada na máquina.

### 5. Persistência no `smsmarica` (não no Assinador)

O PDF assinado e os metadados vivem em `smsmarica.laudo_assinatura` (bytea, byte-estável, índice único filtrado: 1 assinatura concluída por laudo). O `Assinador` permanece stateless — o estado do two-step (`transferState`) volta ao `smsmarica` entre `preparar` e `concluir`. `GET /laudos/{id}/pdf` passa a servir o assinado quando existe; **nunca re-renderiza** após assinado.

### 6. Régua: agente local **hoje**; Web PKI/REST **não** (por ora)

Web PKI (pago) e VIDaaS REST (deal comercial) ficam descartados enquanto o escopo for o certificado VIDaaS em nuvem e a máquina desktop do médico. A abstração `IAssinadorPdfPades` + o canal do agente isolam a escolha — adicionar um canal Web PKI/REST depois não quebra o resto.

## Alternativas consideradas

- **Lacuna Web PKI + PKI SDK.** Plumbing browser↔loja do Windows pronto e mantido. **Rejeitada (por ora):** licença paga no front + SDK; o agente outbound entrega o mesmo sem licença.
- **VIDaaS REST (nuvem, push no celular).** Zero instalação, qualquer dispositivo. **Rejeitada (por ora):** exige virar integrador na Valid (custo/contrato) e só cobre VIDaaS (não token físico); muda a UX familiar do Adobe.
- **iText AGPL in-process no backend fechado.** **Rejeitada:** contaminaria o produto com AGPL. Isolar por serviço aberto resolve.
- **BouncyCastle puro (montar PAdES na mão).** Grátis e sem AGPL. **Rejeitada:** alto risco de gerar PDF que abre mas reprova no Verificador do ITI — inaceitável num laudo médico.

## Consequências

### Positivas
- **R$ 0 de licença** (iText grátis via serviço aberto; sem Web PKI; sem deal Valid).
- **UX idêntica ao Adobe** (VIDaaS Connect local) e cobertura de **qualquer certificado** da loja do Windows (nuvem, token A3, A1).
- Assinador é **ativo Automais reutilizável** por outros produtos, via HTTP.
- Conformidade PAdES delegada ao iText (maduro) — menor risco no Verificador do ITI.

### Negativas
- **Mantém um cliente instalado** (agente) em cada máquina: instalador, **code-signing do .exe**, auto-update, manutenção conforme browsers/Windows evoluem. Assumido conscientemente vs. a licença recorrente do Web PKI.
- Mais um deployable (`Automais.Assinador`) com CI/porta próprios.
- O agente depende do VIDaaS Connect já instalado na máquina do médico (que ele já usa pro Adobe).

### Condições para revisitar
- Se aparecer necessidade de assinar de **celular/tablet** ou de muitos médicos sem instalar nada → reabrir **VIDaaS REST** (avaliar o onboarding na Valid).
- Se o custo de manter o agente (code-signing/suporte) superar a licença → reabrir **Web PKI**.

## Enforcement
- iText vive **só** no `Automais.Assinador` (serviço aberto). Code review rejeita referência a iText in-process em projeto fechado.
- A escolha de lib de assinatura fica atrás de `IAssinadorPdfPades` (uma implementação). Trocar de canal não toca controller/serviço/persistência.
- PDF assinado é **byte-estável**: depois de `Concluida`, `GET /pdf` serve o bytea; jamais regenera.
- O agente é autorizado pela **chave de uso único** (curta, atada ao job+médico, consumida ao concluir) — não há device token/JSON com identidade de médico.
- O agente é lançado **só por protocolo** (`automais-assinador://`); não abre porta/socket/localhost.
