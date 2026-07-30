# ADR-0036 — Login do cidadão exige contato verificado (e como se prova quem é)

- **Status:** Aceito
- **Data:** 2026-07-30
- **Contexto módulo:** App do Cidadão (PWA) — [`SMSMarica.cidadao.pwa`](../../SMSMarica.cidadao.pwa/)
- **Relacionado:** [ADR-0018](./0018-identidade-e-sessao-do-cidadao.md) (identidade e sessão do
  cidadão), [ADR-0020](./0020-paciente-fhir-nativo.md) (telefone verificado no telecom FHIR)

## Contexto

O login do PWA pedia **só o CPF** e mandava o código para o telefone que estivesse no cadastro —
verificado ou não. Quase todo telefone do cadastro chegou por importação (SISREG, Salux, digitação
da recepção) e **ninguém provou** que aquele número é da pessoa. Quem estivesse com o aparelho de
um número trocado, herdado ou digitado errado recebia o código e abria o prontuário alheio:
atendimentos, exames, laudos.

Do outro lado, a régua rígida ("só entra quem tem contato verificado") tranca fora justamente quem
o município mais quer alcançar — a pessoa cujo cadastro nunca foi verificado, e a que nem cadastro
tem.

## Decisão

1. **Sem contato verificado, não sai código.** `POST /auth/paciente/solicitar-otp` (CPF) passa a
   devolver um discriminador `situacao`:
   - `otp` — o cadastro tem contato **verificado** (marcador no telecom do Patient FHIR): o código
     vai **para esse número** e a resposta traz os **últimos 4 dígitos** (`***-1234`) para a pessoa
     reconhecer o aparelho.
   - `verificacao` — o cadastro existe mas o contato não é verificado. **Nada é enviado.**
   - `cadastro` — o CPF não tem cadastro na Saúde. **Nada é enviado.**

2. **Passo 2 — prova de identidade** (`POST /auth/paciente/solicitar-otp-verificacao`), com o
   telefone que vai receber o código informado pelo próprio cidadão:
   - **Com cadastro:** data de nascimento tem que bater com o cadastro **e** o **nº da solicitação
     (SISREG)** tem que ser de uma solicitação **do próprio paciente**. Sem match, a resposta é
     "Solicitação não encontrada. Entre em contato com o posto de atendimento." — quem não tem
     solicitação importada **não entra pelo app** (decisão de produto: a régua fraca seria pior que
     a porta fechada; a recepção resolve presencialmente).
   - **Sem cadastro:** não existe solicitação nossa para conferir (a importação SISREG sempre cria
     o paciente **com CPF**). Quem confere é a **Receita**, pelo proxy CPF já em produção: o par
     **CPF + nascimento** é validado e o **nome oficial** vem de lá — melhor que qualquer nome
     digitado na tela.
   - Cadastro antigo **sem data de nascimento** cai na mesma conferência da Receita.

3. **O cadastro novo só nasce quando o código é confirmado.** Até a confirmação, o telefone é só
   uma alegação; criar antes deixaria um cadastro órfão por tentativa. Na confirmação, o paciente é
   criado (nome oficial + nascimento + sexo da Receita) com o número informado como principal, e o
   número nasce **verificado**.

4. **O número que recebeu o código é o que vira verificado** (`MarcarValidadoAsync`, origem
   `pwa-cidadao`) — inclusive quando substitui um verificado antigo. Isso dá a saída para quem
   **perdeu o número/chip**: na tela do código há "Não tenho mais esse número", que leva ao mesmo
   passo 2. Como efeito conhecido do carimbo, a **dispensa de verificação cai** e as comunicações
   retidas por falta de contato verificado são liberadas.

5. **Conflito de número é barrado ANTES do envio.** Número que já é o contato confirmado de outro
   CPF nunca chegaria a ser validado; falhar só na confirmação queimaria o código do cidadão por um
   erro já conhecido. `ITelefoneValidacaoService.GarantirNumeroLivreAsync` foi exposto para isso.

6. **Rate limit por IP** (`login-cidadao`, 20 req/min) em `/auth/paciente/*`: o passo 2 é uma
   superfície de varredura de CPF e de nº de solicitação, e cada tentativa custa um WhatsApp e uma
   consulta à Receita.

## Consequências

- **Supera o ponto 2 do [ADR-0018](./0018-identidade-e-sessao-do-cidadao.md)** ("não cria
  paciente") **apenas para o login OTP**: o autocadastro nasce aqui, conferido na Receita. Login
  social continua só **vinculando** a cadastro existente.
- Paciente com cadastro, sem verificação e **sem solicitação importada** fica de fora do app até
  passar na unidade. É deliberado — reavaliar quando houver outra prova de vínculo (ex.: cartão do
  SUS conferido no CADSUS).
- O autocadastro depende de um motor de CPF ativo no proxy. Sem motor, o cidadão novo recebe a
  mensagem de indisponibilidade do proxy (409) e não se cadastra.
- Cadastro criado pelo app entra **sem endereço e sem CNS** — a unidade completa depois.
- O PWA ganha a rota `/login/verificacao`; o passo 1 deixa de enviar código na maioria dos casos,
  então versões antigas do app em cache seguem para a tela de código e não recebem nada. O
  service worker (autoUpdate) resolve na próxima abertura.
