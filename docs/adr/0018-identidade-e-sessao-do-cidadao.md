# ADR-0018 — Identidade e sessão do cidadão (app), separada de Usuario/RBAC

- **Status:** Aceito
- **Data:** 2026-06-19
- **Contexto módulo:** App do Cidadão (PWA) — [`SMSMarica.cidadao.pwa`](../../SMSMarica.cidadao.pwa/), TFD e além

## Contexto

O **cidadão** (paciente) passa a autenticar no App do Cidadão (PWA em `app.smsmarica.online`).
Ele **não é** um `Usuario` do sistema (não tem papel/RBAC) — sua identidade clínica já vive no
hub FHIR (`fhir.patient`, ADR-0007/0008/0010). Mas precisamos controlar **como ele entra**:
senha (opcional), login social (Google/Microsoft/Facebook) e, por segurança, **uma sessão
ativa por vez (single-device)**. Isso é regra de negócio do município, então mora em
`smsmarica.*`, e o login OTP (CPF + código no WhatsApp) já existente continua.

## Decisão

1. **Cidadão ≠ Usuario.** Credenciais e sessão do cidadão vivem em tabelas próprias em
   `smsmarica`, **fora** de `usuario`/RBAC:
   - **`cidadao_acesso`** (1:1 com o paciente): `patient_id` (referência **lógica** ao recurso
     Patient do hub — sem FK, pois o FHIR é serviço autônomo, ADR-0010), `cpf` (**fonte da
     verdade**, único), `senha_hash` (nullable — PBKDF2 via `PasswordHasher`), `google_sub` /
     `microsoft_sub` / `facebook_sub` (vínculos sociais, únicos filtrados), `ativo`, auditoria.
   - **`cidadao_sessao`** (histórico de sessões): `id` (= **`jti`** do JWT), `canal`
     (`otp-whatsapp`/`senha`/`google`/`microsoft`/`facebook`), `dispositivo`, `ip`, `criada_em`,
     `expira_em`, `revogada_em` (null = ativa).

2. **CPF é a fonte da verdade.** Todo método de login resolve para o mesmo cidadão pelo CPF;
   login social só **vincula** a um cadastro de paciente já existente (não cria paciente).

3. **Single-device por token (sessão server-side).** Qualquer autenticação **revoga as sessões
   ativas anteriores** e cria uma nova; o `jti` do JWT é o id da sessão. A cada request, o
   `JwtBearer.OnTokenValidated` valida — quando `tipo=cidadao` — o `jti` contra a sessão ativa do
   paciente (`ICidadaoSessaoService.SessaoValidaAsync`). O aparelho antigo, ao mandar o token
   revogado, recebe 401. Logout revoga a sessão atual.

4. **Token do cidadão** reusa a mesma chave/issuer/audience do token de staff, com claims
   `tipo=cidadao`, `sub`=id do paciente (FHIR), `jti`=id da sessão; validade própria
   (`Tfd:Cidadao:SessaoDias`, default 30). Endpoints autenticados sob `/auth/paciente/*`
   (`CidadaoController`, `[Authorize]`) só enxergam os dados do próprio `sub`.

5. **Edição do próprio cadastro é cirúrgica.** `GET /auth/paciente/me` lê o paciente; e-mail/
   telefones/foto são atualizados por métodos que **carregam o estado completo e regravam só o
   campo** (`IPacientesService.AtualizarContatoAsync` / `AtualizarFotoAsync`), porque
   `AplicarAtualizacao` regrava o payload FHIR (request esparso apagaria dados).

## Consequências

- Há um custo de **1 consulta ao banco por request de cidadão** (validar a sessão). Aceitável e
  necessário para o single-device com JWT.
- Os endpoints clínicos do app (`meus-translados`, `atendimentos`, `exames`, `laudos`) entram
  como **stub (lista vazia)** com shape estável; o preenchimento (TFD/FHIR/Salux) é leva futura.
- Login por **senha e social (Google/Microsoft/Facebook)** tem o **modelo de dados pronto**; os
  fluxos de autenticação em si (telas + endpoints) são implementados em seguida, reusando
  `ICidadaoSessaoService.AbrirSessaoAsync` (que já é agnóstico ao canal).
- Migration: `20260619153353_CidadaoAcessoESessao` (apenas cria tabelas — não destrutiva).
