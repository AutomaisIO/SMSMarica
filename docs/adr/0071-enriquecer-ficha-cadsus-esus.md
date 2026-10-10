# ADR-0071 — Enriquecer a ficha do paciente pelo CADSUS e pelo e-SUS (escolha humana, campo a campo)

**Status:** aceito · **Data:** 2026-10-10 · fase 1 implementada em 10/10/2026
**Relacionado:** [ADR-0067](./0067-telefone-pelo-esus-pec-rotina-noturna.md) (credencial do e-SUS, rotina da
madrugada) · [ADR-0041](./0041-identidade-incompleta-no-hub.md) (paciente sem CPF) ·
[ADR-0057](./0057-destinatario-correto-e-contato-negado.md) (contato negado) ·
[ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md) (proveniência)

## Contexto

O envio ao SERNIT mediu em 08/10/2026 (e um teste confirmou em 10/10) que o SERNIT **não consulta o CADSUS**: paciente que ele não
conhece volta com o painel vazio. Quem digita o cadastro lá é a plataforma, com a nossa ficha — e a nossa
ficha, muitas vezes, está incompleta (sem mãe, sem endereço, sem celular) ou desatualizada.

Duas fontes têm o que falta:

- o **CADSUS**, pela porta do **SER** (botão Pesquisar do painel de paciente) — não gasta o orçamento
  anti-robô do SISREG;
- o **e-SUS PEC** do município, onde a atenção básica atualiza o cadastro — a fonte mais fresca de
  telefone e filiação (ADR-0067).

Faltava um jeito de, numa ficha só, buscar nas duas e trazer o que interessa **sem atropelar o que já
está certo**. E o e-SUS tem uma restrição dura: **sessão única por usuário**. A conta da plataforma foi
cedida por uma servidora e só é usada de madrugada, para não derrubá-la.

## Decisão

1. **Dois botões na ficha**, ao lado do *Verificar*: **CADSUS** e **e-SUS**. Busca pelo CPF (o CNS só
   quando não há CPF). Se nada difere, a tela diz que confere; se algo difere, abre a comparação.
2. **A pessoa escolhe, campo a campo.** O que completa um vazio vem marcado; o que diverge vem
   desmarcado (fica o da ficha). Nada é gravado sem essa escolha.
3. **Os valores ficam no servidor.** A consulta guarda a ficha da fonte por 20 minutos, amarrada ao
   paciente e ao operador; a tela devolve só *quais* campos quer. Ao gravar, a ficha é relida e
   recomparada.
4. **As réguas que já existiam continuam valendo:**
   - **CPF diferente na fonte bloqueia tudo** (pode ser outra pessoa — mesma guarda do CADSUS). CPF só
     se acrescenta em quem não tem, e não entra se for de outro cadastro (o caminho é Unificar).
   - **Nome e data de nascimento** só mudam se a **Receita** confirmar o CPF com o dado novo (regra de
     02/10/2026). Todas as conferências acontecem **antes** da primeira escrita.
   - **CNS** novo vira o oficial; o anterior fica como `old` (continua em `cns_todos`, a pessoa segue
     sendo achada por ele).
   - **Telefone só se acrescenta**, com `contato-origem` (`cadsus` ou `esus-pec`); nunca troca o
     principal nem apaga número.
   - Campo vazio na fonte nunca apaga o da ficha.
   - Cada mudança entra no Histórico de alterações; nascimento corrigido é marcado como editado e
     vence o reimport do PEP.
5. **e-SUS de dia, sem derrubar ninguém:**
   - **Conta da plataforma** (a do ADR-0067): só para quem tem **acesso global**, e **nunca força** a
     entrada. Se a servidora estiver no sistema, a tela pede a senha do próprio operador.
   - **Conta do operador**: usuário e senha digitados na hora, usados **só naquela consulta** — não
     são guardados nem logados. Se ele estiver no e-SUS em outra janela, a tela avisa e só força a
     entrada se ele marcar *Encerrar a minha outra sessão*.
   - Sempre login → acesso (primeira lotação que consiga buscar cidadão) → busca → **logout**.
   - O cliente continua só-leitura: 3 mutações de sessão e agora 4 consultas (sessão, acessos, busca
     e detalhe do cidadão). Nada grava no e-SUS.
6. **Permissão:** módulo Pacientes, ação Edição (consultar e gravar).

## Consequências

- A ficha passa a poder ser completada antes de um envio ao SER/SERNIT, por quem está com ela aberta.
- O e-SUS registra, na auditoria dele, a consulta com o usuário do próprio operador — quando é a conta
  dele que entra.
- A conta cedida pode ser usada de dia por quem tem acesso global; como não força a entrada, o pior
  caso é a servidora encontrar "você já está logado" durante os ~10 s da consulta.
- Consulta por CNS sem CPF pode trazer outra pessoa com o mesmo cartão: a tela avisa quando o nome é
  muito diferente, e a decisão é de quem está olhando.

## Fora de escopo (fases seguintes)

- Rodar o enriquecimento em lote ou automaticamente (ex.: antes do envio ao SERNIT).
- Trazer a Receita como terceira coluna da comparação.
- Guardar a sessão do e-SUS do operador entre consultas.

## Complemento (10/10/2026) — antes do envio ao SERNIT, e o telefone confirmado

Pedido do Bernardo no mesmo dia, já sabendo o que o SERNIT exige no painel do paciente (nome, CPF,
sexo, nascimento, nome da mãe, logradouro, UF, município e celular — medido na PR-23):

1. **Quem pede vê antes.** Na nova solicitação (passo Paciente e Revisão) aparece o quadro *Cadastro do
   paciente*: o telefone confirmado (ou o aviso de que não há) e, com destino SERNIT, o que ele exige e
   o que falta na ficha, com os botões CADSUS e e-SUS ali mesmo. Não trava o envio — informa a tempo.
   O CPF que entra pelo quadro já vale para a solicitação (ela relê o cadastro).
2. **No envio do regulador**, a recusa por dado do paciente mostra o mesmo quadro no modal, para
   completar e "Tentar de novo" sem sair dali; a prévia avisa quando o paciente não tem telefone confirmado.
3. **O SERNIT recebe o que temos.** Paciente que o SERNIT já conhece: todo campo **vazio** lá recebe o
   nosso dado (antes, só os obrigatórios). O que lá está preenchido não é tocado — exceto o celular.
4. **Telefone confirmado primeiro.** O "Telefone Celular" do SERNIT é por onde ele avisa o paciente: vai
   o telefone **confirmado** na plataforma (OTP pelo WhatsApp) antes do celular e do principal da ficha,
   e ele substitui o celular que o SERNIT tinha quando são diferentes — a prévia mostra o número antigo.
   Número negado nunca vai; confirmado que depois foi negado deixa de contar como confirmado.
