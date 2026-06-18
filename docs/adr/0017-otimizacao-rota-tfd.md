# ADR-0017 — Geração inteligente de translado TFD (Google Maps + Claude), tempo real e faturamento

- **Status:** Aceito
- **Data:** 2026-06-18
- **Contexto módulo:** [`docs/modulos/tfd/`](../modulos/tfd/)

## Contexto

O módulo TFD (Tratamento Fora do Domicílio) evolui de uma alocação manual de pacientes em
assentos para um processo **inteligente, rastreável e comunicado**: distribuir pacientes nos
carros, otimizar rotas de coleta/entrega, falar com o paciente por WhatsApp, acompanhar a
frota em tempo real e faturar ao SUS por BPA. Isto introduz integrações externas (Google
Maps, WhatsApp/Meta) e um cliente PWA novo, além do app Flutter do motorista.

## Decisão

1. **Sequência única de paradas por rota** (não três `RotaDiaria` por fase). `Alocacao`
   ganha `OrdemParada`, `TipoParada` (`Coleta`/`Destino`/`Retorno`), `EtaPrevisto` e janela.
   `RotaDiaria` ganha `Origem` (`Manual`/`Gerada`), métricas e **`PlanoRotaJson`** (snapshot
   auditável da geração).

2. **Google Maps é a verdade geográfica** (Geocoding, Distance Matrix, Routes com otimização
   de waypoints). **Claude decide a distribuição** nos carros sob restrições de negócio
   (capacidade, assento de acompanhante, região), em saída estruturada. Há **heurística
   determinística de fallback** — o sistema gera rota mesmo sem IA/Google. A geração **não
   substitui** a alocação manual: produz um plano editável e auditável.

3. **Geocodificação em cache local** (`tfd_geocodigo`, schema `smsmarica`), chaveado por hash
   do endereço normalizado. **Não altera o hub FHIR.** Endereços de baixa confiança entram em
   **fila de revisão** com pin manual no mapa.

4. **Tempo real via SignalR** (já incluso no SDK Web): hub único `/hubs/rastreamento`, grupos
   `painel`/`motorista:{id}`/`rota:{id}`/`paciente:{id}`. Abstração `IRastreamentoNotificador`
   no Core (no-op), implementação na Api.

5. **WhatsApp via Meta Cloud API** com **conta da Secretaria** (pré-requisito externo,
   verificação de negócio) e templates HSM; webhook idempotente (`WaMessageId`) com validação
   de assinatura. **Login do paciente = CPF + OTP por WhatsApp** (sem senha).

6. **Cliente do paciente é PWA** (React+Vite) nesta fase; o app Flutter `cidadao.app` fica
   para a Fase 2. O app do **motorista é Flutter** (GPS em background), instalado manualmente
   no tablet.

7. **Faturamento SUS/BPA:** cada paciente transportado gera **1 unidade a cada 50 km a
   bordo**. Registros nascem na conclusão da sessão; exportação BPA por competência reusa
   `ProcedimentoSigtap`. Ver [`docs/modulos/tfd/faturamento.md`](../modulos/tfd/faturamento.md).

8. **Segredos cifrados** (`IProtetorSegredos`/DataProtection) para Google e WhatsApp, em
   tabelas de configuração linha-única. **RBAC** append-only: `Cidadao=29`,
   `IntegracoesConfig=30`, `Faturamento=31`.

## Consequências

- **Positivas:** rotas mais curtas e auditáveis; comunicação no canal do paciente;
  rastreabilidade ao vivo; receita via BPA; tudo no schema `smsmarica`, sem tocar o hub FHIR.
- **Custos/risco:** dependência de APIs externas (mitigada por cache + fallback) e do
  lead-time de verificação da Meta (mitigado por sandbox); LGPD exige política de retenção de
  `PontoGps` e `tfd_mensagem_whatsapp`.
- **Decisões em aberto (faturamento):** arredondamento das unidades, base da distância
  (a bordo vs origem→destino; planejada vs GPS real), código SIGTAP de transporte e layout
  BPA (C/I) + CNES — ver `faturamento.md`.
