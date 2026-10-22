# ADR-0078 — Entrada no app do cidadão por CPF + data de nascimento, com o código só para número já conhecido

- **Status:** Aceito
- **Data:** 2026-10-22
- **Contexto módulo:** App do Cidadão — PWA ([`SMSMais.cidadao.pwa`](../../SMSMais.cidadao.pwa/)) e app nativo
  ([`SMSMais.cidadao.app`](../../SMSMais.cidadao.app/))
- **Substitui:** o §2 do [ADR-0036](./0036-login-cidadao-contato-verificado.md) (nº da solicitação como
  prova) e ajusta o §5 do [ADR-0057](./0057-destinatario-correto-e-contato-negado.md)
- **Relacionado:** [ADR-0071](./0071-enriquecer-ficha-cadsus-esus.md) §5 (uso diurno do e-SUS),
  [ADR-0067](./0067-telefone-pelo-esus-pec-rotina-noturna.md) (rotina noturna do e-SUS),
  [ADR-0077](./0077-fontes-de-dados-cadastro-da-plataforma.md) (Fontes de dados)

## Contexto

Pelo ADR-0036, quem não tinha WhatsApp verificado entrava informando data de nascimento, **nº da
solicitação** e um WhatsApp **digitado**, que recebia o código e virava o verificado. Na prática:

- o nº da solicitação trouxe problemas (pedido decidido pelo produto em 22/10/2026): a pessoa não
  acha o número, pedidos do SER/SERNIT/ESUS não servem, e quem não tem pedido não entra;
- o nº da solicitação era a **única** prova além de CPF e nascimento, que não são segredo. Tirar só o
  campo deixaria qualquer pessoa com esses dois dados cadastrar o próprio celular como o WhatsApp de
  outra: veria o prontuário, receberia os laudos retidos e trancaria o dono do lado de fora.

## Decisão

1. **A pessoa digita CPF e data de nascimento, e nada mais.** O par é conferido antes de qualquer
   coisa aparecer:
   - com cadastro: contra a data da ficha (sem data na ficha, na Receita);
   - sem cadastro: na Receita (que também dá o nome do cadastro a criar).

   Errou, a resposta é sempre a mesma ("não conseguimos confirmar os seus dados"), para não ajudar
   quem tenta adivinhar. **5 erros em 30 minutos no mesmo CPF bloqueiam a entrada por 30 minutos**
   (`cidadao_entrada_bloqueio`, no banco, sobrevive a reinício).

2. **O código só vai para um número que o servidor já conhece — nunca para um número digitado.**
   Conferido o par, o app mostra os números possíveis, mascarados (`*** 1234`), e a pessoa escolhe um
   ou diz "nenhum desses é meu":
   - **quem já tem WhatsApp verificado** vê só esse número. Trocar continua sendo no posto
     (ADR-0057 §5): oferecer os outros números do cadastro seria a troca pela porta dos fundos;
   - **quem não tem verificado** vê os celulares do cadastro (ativos, sem "contato negado", e que não
     são o verificado de outro CPF).

   O ADR-0036 tinha deixado de mandar código para número não verificado porque o número do cadastro
   pode ser trocado, herdado ou de parente. Esse risco continua, mas agora **quem está com o aparelho
   também precisa saber CPF + nascimento** da pessoa — e escolher o número certo entre os mascarados.
   É a troca que o produto aceitou para tirar o nº da solicitação.

3. **"Nenhum desses é meu" → e-SUS, se o canal permitir.** A consulta ao e-SUS PEC na hora é **opcional
   por canal** (PWA e nativo, separados), em **Sistema → App do cidadão**. Ligada, o servidor procura no
   e-SUS os números do CPF que ainda não estavam na lista e os oferece do mesmo jeito. O cadastro do
   e-SUS só vale se for a mesma pessoa: mesma data de nascimento, sem óbito, ativo e — havendo ficha —
   sem nome/nascimento de outra pessoa (`ConferenciaPessoa`). Sem número em lugar nenhum: **"procure o
   posto para atualizar o seu WhatsApp"**.

4. **O e-SUS de dia usa a conta da PLATAFORMA**, a de Sistema → Fontes de dados. Em 22/10/2026 a conta
   cedida por uma servidora foi trocada pela conta própria da plataforma, o que tira o motivo do
   ADR-0071 §5 ("de dia só para quem tem acesso global") para este uso. As regras da consulta:
   - **nunca força a entrada** — conta em uso vira "tente de novo em instantes";
   - **uma consulta por vez** (o PEC é sessão única por conta), com espera curta pela vez;
   - **não consulta na janela da rotina da madrugada** (ADR-0067), que força a entrada;
   - **para de tentar por 30 minutos** se o login da conta falhar (senha trocada não pode virar conta
     bloqueada no PEC);
   - **guarda o resultado por CPF por 20 minutos**;
   - uma consulta por tentativa de entrada.

5. **A configuração é só de admin global** (usuário com acesso global — `usuario.acesso_global`),
   conferido no servidor a cada chamada. Cada ajuste fica na trilha de auditoria
   (`ConfiguracaoAppCidadao`) e a tela mostra o histórico: quando, quem e o que mudou. Nasce com a
   consulta ao e-SUS **desligada** nos dois canais.

6. **O app diz o canal** no cabeçalho `X-App-Canal` (`pwa` | `nativo`); sem cabeçalho vale `pwa`. É
   preferência de produto, não fronteira de segurança: as travas acima valem nos dois canais.

7. **Revisão das lojas (Google Play, App Store) sem porta na produção.** O revisor entra pela
   **entrada de demonstração do app nativo**: CPF `111.111.111-11` + nascimento `01/01/2000` ligam o
   modo demonstração local (dados fictícios embutidos, sem rede). Foi descartada uma "conta de revisão"
   no servidor (código na tela para um CPF configurado): seria uma porta de entrada sem a prova pelo
   WhatsApp, e com ela o revisor mexeria no chat e nos envios da equipe de verdade.

## Contrato (`/auth/paciente`, anônimo, limite `login-cidadao`)

| Rota | Corpo | Resposta |
|---|---|---|
| `POST entrada` | `{ cpf, dataNascimento: "aaaa-mm-dd" }` | `EntradaCidadaoDto` |
| `POST entrada/{desafioId}/esus` | — | `EntradaCidadaoDto` (lista inteira, já com os números do e-SUS) |
| `POST entrada/{desafioId}/enviar` | `{ opcaoId }` | `OtpEmitidoDto` (o mesmo de antes) |
| `POST validar-otp` | `{ cpf, codigo }` | sem mudança |

`EntradaCidadaoDto = { desafioId, opcoes: [{ id, final, origem: "cadastro" | "esus", verificado }],
podeBuscarEsus, validadeSegundos }`. O desafio vive 10 minutos no servidor e guarda os números de
verdade; o app só vê o final de cada um. Até 3 envios de código por desafio.

Erros (400 `errors.<código>`, salvo indicação):
- `cpf`, `nascimento.invalido`;
- `entrada.nao_confere`;
- `entrada.bloqueada`;
- `paciente.inativo`;
- `entrada.expirada` (o desafio venceu: começar de novo);
- `entrada.opcao_invalida`, `entrada.muitos_envios`;
- `esus.indisponivel` (409);
- `telefone.duplicado` (409);
- `otp.envio_falhou`.

As rotas antigas `solicitar-otp` e `solicitar-otp-verificacao` respondem **410** (`entrada.atualizar`,
"atualize o app"): o PWA em cache pede para recarregar, e nenhum app nativo publicado as usa.

## Consequências

- Sai o nº da solicitação (e o furo da sentinela `0000`). Quem tem o celular no cadastro entra sozinho,
  inclusive sem nenhum pedido.
- Sem cadastro, não há mais "digite o seu número": a pessoa só entra se o e-SUS (ligado no canal) tiver
  um celular dela; senão, posto. O cadastro continua nascendo só na confirmação do código.
- O primeiro passo deixa de revelar se o CPF tem cadastro e o final do telefone a quem não sabe o
  nascimento.
- O material impresso que fala em "nº da solicitação" (banner do app) fica desatualizado e precisa ser
  refeito.
- Pendências: o e-SUS na hora depende da conta da plataforma estar em Fontes de dados e o PEC no ar; com
  ele fora, a orientação é o posto.
