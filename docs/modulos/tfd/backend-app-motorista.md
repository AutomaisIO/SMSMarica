# Pedido de backend — App do Motorista (TFD) com login facial on-device

> Documento de **interface**: o que o app Flutter do motorista (`SMSMarica.agente.app`)
> precisa do `SMSMais.server`. O reconhecimento facial roda **on-device** (ML Kit +
> MobileFaceNet TFLite); o servidor **não** faz matching — guarda a foto de referência e
> a trilha de auditoria das verificações.

## Contexto / decisões

- Login e re-verificação do motorista são **faciais, on-device** (offline, template fica no tablet).
- Cadastro do rosto é por **foto interna** (gestor), sem Datavalid/CNH por ora.
- Re-verificação **antes de iniciar cada translado** (além do login).
- Biometria é **dado sensível (LGPD art. 5º II)** → consentimento + finalidade; manter rosto no device, servidor só guarda selfie de auditoria com base legal.

---

## BE-1 — Foto de referência facial do motorista (+ hash)

App precisa baixar a foto de referência do(s) motorista(s) e saber quando ela mudou.

**Pergunta de design:** reusar `Usuario.FotoBase64` (foto de perfil já existente) ou criar campo dedicado `FotoReferenciaFacial`? Recomendação: **campo dedicado** (qualidade/frontalidade importam para o match e a semântica é biométrica), mas reusar é aceitável no MVP.

Necessário:
- Incluir em **`GET /identidade/me`** (resposta `UsuarioDto`) os campos:
  - `fotoReferenciaFacialBase64: string?` (ou URL autenticada)
  - `fotoReferenciaFacialHash: string?` (SHA-256 dos bytes — o app só re-baixa quando muda)
  - `fotoReferenciaFacialAtualizadaEm: DateTime?`
- Endpoint do gestor para **definir/atualizar** a foto de referência (pode ser via edição de motorista já existente): `PUT /motoristas/{id}/foto-referencia` `{ fotoBase64 }` → recalcula hash.
- (Futuro multi-motorista no mesmo tablet) endpoint para listar referências de um conjunto de motoristas: `GET /motoristas/referencias-faciais?ids=...` → `[{ motoristaId, cpf, nome, fotoBase64, hash, atualizadaEm }]`.

---

## BE-2 — Auditoria de verificação facial (entidade + endpoints)

Como o match é on-device, o servidor guarda a **trilha de auditoria**: quem verificou, quando, com selfie + score + liveness, para um humano poder conferir depois.

Nova entidade `VerificacaoFacial` (schema `smsmarica`):
- `Id: Guid`
- `MotoristaId: Guid` (FK)
- `Momento: MomentoVerificacao` (enum: `Login = 1`, `InicioTranslado = 2`)
- `RotaDiariaId: Guid?`
- `SessaoId: Guid?` / `AlocacaoId: Guid?` (qual translado, quando `Momento = InicioTranslado`)
- `CapturadoEm: DateTime` (relógio do device)
- `SelfieBase64: string` (ou referência de storage) — selfie do momento
- `ScoreSimilaridade: double` (cosseno 0..1)
- `LimiarUsado: double`
- `LivenessAprovado: bool`
- `Aprovado: bool` (resultado final no device)
- `Latitude: double?` / `Longitude: double?` (posição no momento)
- `ModeloVersao: string?` (versão do MobileFaceNet)
- `CriadoEm: DateTime`

Endpoints:
- **`POST /rastreamento/verificacoes-faciais`** — perm. `Rastreamento.Inclusao`
  - body = campos acima (sem `Id`/`CriadoEm`) → `201 { id }`
- **`GET /rastreamento/verificacoes-faciais`** — perm. `Rastreamento.Consulta` (painel/auditoria)
  - query: `motoristaId?`, `rotaId?`, `momento?`, `desde?`, `ate?`, `apenasReprovadas?`
  - → lista paginada.

Migration nova (não editar migrations aplicadas).

---

## BE-3 — "Iniciar translado" com verificação obrigatória

A re-verificação é **antes de iniciar cada translado**. Hoje existe `POST /rotas/{id}/iniciar` (rota inteira) e `confirmar` da sessão.

**Definição pendente (decisão do backend):** o que é "um translado" no modelo atual? Provavelmente uma **perna por sessão/alocação** (ida = Coleta→Destino; volta = Retorno). Precisamos de:
- Endpoint para **marcar início de um translado** por alocação/sessão, ex.:
  - `POST /rotas/{rotaId}/alocacoes/{alocacaoId}/iniciar` (ou `/sessoes/{sessaoId}/iniciar-translado`) → `204`.
- Regra: aceitar o início **somente se houver uma `VerificacaoFacial` aprovada recente** (ex.: nos últimos N minutos) para aquele `MotoristaId` com `Momento = InicioTranslado`. Alternativa: o próprio POST recebe o `verificacaoFacialId` recém-criado e valida.

Confirmar o mapeamento de domínio de "translado" e expor o endpoint correspondente.

---

## BE-4 — Login do motorista: **primeiro acesso CPF + OTP (WhatsApp)**, depois só rosto

Decisão (2026-06-19): o motorista **não** loga por email/senha. O fluxo é:

1. **Primeiro acesso:** **CPF + OTP por WhatsApp** — exatamente o mesmo modelo do
   paciente (`CidadaoController`, rota `auth/paciente`, com modo teste = código na
   tela sem WhatsApp). Pedido: **espelhar esse fluxo para o motorista**, ex.:
   - `POST /auth/motorista/solicitar-otp` `{ cpf }` → envia/retorna (modo teste) o código.
   - `POST /auth/motorista/confirmar-otp` `{ cpf, codigo }` → valida e emite **JWT** com o `motoristaId` no token.
   - Reusar o serviço de OTP do paciente; só muda o público-alvo (resolve `Motorista` pelo CPF do `Usuario`).
2. **Cadastro facial só após autenticado** — a tela de cadastro do rosto vive
   *dentro* da sessão (associação justa ao motorista logado). O app já reflete isso.
3. **Logins seguintes:** **somente rosto** (match on-device contra o template
   cadastrado), sem CPF/OTP. O app abre direto na captura facial quando já há
   template no tablet.

Itens correlatos:
- JWT precisa carregar o **`motoristaId`** (claim) para o app montar GPS/rotas/auditoria sem `/me` extra.
- O motorista criado por `POST /motoristas` nasce com `SenhaHash = "PENDENTE_AUTH"` e `DeveTrocarSenha = true` — coerente com login sem senha (OTP). Confirmar que o fluxo OTP não exige senha.
- (Opcional, futuro) **registro de device**: `POST /motoristas/{id}/dispositivos` `{ deviceId, modelo }` + refresh-token longo, para o tablet não repetir nem o OTP.
- Garantir permissões do **perfil motorista**: `Rastreamento` (Consulta+Inclusao+Edicao), `Translados` (Consulta+Edicao), `Tratamentos` (Edicao).

---

## BE-5 — Conveniências de rota/GPS (já existem, confirmar)

Já mapeado e suficiente, só confirmar:
- `GET /rotas?data=&motoristaId=` e `GET /rotas/{id}` (com `alocacoes`) — OK.
- `POST /rastreamento/pontos { motoristaId, latitude, longitude, capturadoEm }` — OK.
- Hub SignalR `/hubs/rastreamento?access_token=` — OK.
- (Opcional) `GET /rotas/minhas?data=` resolvendo o `motoristaId` pelo `sub` do JWT, para o app não precisar carregar `motoristaId` separado.

---

## Resumo priorizado

| Item | O que | Prioridade |
|---|---|---|
| BE-1 | Foto de referência facial + hash em `/identidade/me` | **Alta** (bloqueia enrollment) |
| BE-2 | Entidade + endpoints `VerificacaoFacial` (auditoria) | **Alta** |
| BE-3 | Endpoint "iniciar translado" gated por verificação | Média |
| BE-4 | Vínculo de device / refresh-token longo | Baixa (UX) |
| BE-5 | Confirmar rotas/GPS/SignalR + `/rotas/minhas` | Baixa |
