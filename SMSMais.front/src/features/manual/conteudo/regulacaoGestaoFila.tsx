import { ClipboardCheck } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → Gestão de fila: o lado de quem avalia e regula as solicitações que as
 * unidades enviaram (ADR-0052). O lado de quem pede está em `regulacaoSolicitacoes`.
 *
 * Conferido no código em 05/10/2026: `features/regulacao` (FilaRegulacaoPage,
 * AnaliseSolicitacaoPage, AbasFilaRegulacao/ABAS_GESTAO_FILA, TabelaSolicitacoes,
 * CabecalhoSolicitacao, AnexosSolicitacao, RespostasRegras, MedicoPendenteCard,
 * ParaLancarNoSistema, ModalRegistrarEnvio, ModalMotivo, LinhaDoTempo, NotificacoesRegulacaoPage)
 * e no backend (`MaquinaDeEstadosRegulacao`, `RegulacaoSolicitacaoService`, `RegulacaoEscopo`,
 * `ModuloPermissao` 48). Em 09/10/2026: `AjusteRiscoCid` e o ajuste do agente em
 * `RegulacaoSolicitacaoService.AtualizarAsync` (Em análise e Falha no envio; obrigatório não
 * esvazia; evento "Ajuste" com autor e de → para).
 */
export const artigoRegulacaoGestaoFila: Artigo = {
  slug: 'regulacao-gestao-fila',
  titulo: 'Gestão de fila (regulação)',
  resumo:
    'Como o agente regulador recebe as solicitações das unidades, confere o pedido e os anexos e decide: aceitar e levar ao sistema de destino, devolver para correção ou recusar.',
  grupo: 'regulacao',
  icone: ClipboardCheck,
  rota: '/app/regulacao/gestao-fila',
  publico: 'Quem avalia e regula as solicitações (agente regulador)',
  atualizadoEm: '2026-10-09',
  palavrasChave: [
    'gestão de fila',
    'classificação de risco',
    'alterar classificação de risco',
    'reclassificar risco',
    'reclassificação',
    'alterar CID',
    'trocar CID',
    'hipótese',
    'ajustada pela regulação',
    'ajuste da regulação',
    'fila da regulação',
    'pré-regulação',
    'agente regulador',
    'técnico regulador',
    'regulador',
    'triagem',
    'recebidas',
    'em análise',
    'devolvidas',
    'analisar',
    'análise',
    'assumir',
    'aceitar',
    'aceitar e registrar envio',
    'aceitar e enviar ao SER',
    'enviar ao SER',
    'aceitar e enviar ao SERNIT',
    'enviar ao SERNIT',
    'SERNIT',
    'envio automático',
    'prévia do envio',
    'senha do SER',
    'falha no envio',
    'pedido parecido',
    'médico não cadastrado',
    'médico novo',
    'autorizo cadastrar',
    'cadastrar médico no SER',
    'é este',
    'cadastro a conferir',
    'já lancei no SER',
    'registrar envio',
    'número do sistema',
    'devolver',
    'devolver à unidade',
    'recusar',
    'motivo',
    'OK interno',
    'já está no SISREG',
    'anexos',
    'visualizar anexo',
    'arquivos anexados',
    'regras do manual',
    'médico novo a cadastrar',
    'para lançar no sistema',
    'copiar campos',
    'linha do tempo',
    'SISREG',
    'SER',
    'SERNIT',
    'ESUS São Gonçalo',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca:
        'gestão de fila regulação agente regulador recebe solicitações unidades analisar decidir duas visões quem pede quem regula fila da regulação',
      conteudo: (
        <>
          <P>
            A solicitação tem dois lados. Quem <strong>pede</strong> trabalha em{' '}
            <strong>Regulação → Solicitações</strong>: abre o pedido, acompanha e corrige o que voltar.
            Quem <strong>avalia e regula</strong> trabalha aqui, em{' '}
            <strong>Regulação → Gestão de fila</strong>: recebe o que as unidades enviaram, confere e
            decide.
          </P>
          <P>
            São telas separadas de propósito. Aqui não há rascunho nem “Nova solicitação” — rascunho
            ainda não chegou à regulação, e abrir pedido é do lado de quem pede. E os comandos de
            decisão (assumir, aceitar, devolver, recusar) só existem aqui.
          </P>
          <Callout tipo="regra" titulo="A tela não escreve nos sistemas de destino">
            Nada aqui grava no SISREG, no SER, no SERNIT ou no ESUS. Aceitar um pedido é incluí-lo na
            tela do próprio sistema e depois <strong>registrar aqui o número</strong> que ele gerou.
          </Callout>
        </>
      ),
    },
    {
      id: 'fila',
      titulo: 'A fila: o que chegou e em que pé está',
      busca:
        'fila abas recebidas em análise devolvidas enviadas no sistema encerradas filtro fluxo destino busca nome cpf número coluna agente unidade topo todas município ordem mais antiga',
      conteudo: (
        <>
          <P>
            A tela abre em <AbaRef>Recebidas</AbaRef>: o que as unidades enviaram e ninguém assumiu
            ainda. Quem espera há mais tempo aparece primeiro.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <AbaRef>Recebidas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="alerta">Na pré-regulação</SeloRef> — chegou da unidade e aguarda um
                    agente.
                  </>
                ),
              },
              {
                termo: <AbaRef>Em análise</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="violeta">Em análise</SeloRef> — um agente assumiu; a coluna{' '}
                    <strong>Agente</strong> mostra quem.
                  </>
                ),
              },
              {
                termo: <AbaRef>Devolvidas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="alerta">Devolvida à unidade</SeloRef> — aguardando a unidade corrigir e
                    reenviar. Quando ela reenvia, o pedido volta para Recebidas.
                  </>
                ),
              },
              {
                termo: <AbaRef>Enviadas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="info">Enviada ao sistema</SeloRef> e{' '}
                    <SeloRef cor="erro">Falha no envio</SeloRef>.
                  </>
                ),
              },
              {
                termo: <AbaRef>No sistema</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="info">Na fila do sistema</SeloRef> e{' '}
                    <SeloRef cor="sucesso">Agendada</SeloRef> — quem traz essas mudanças é o espelho do
                    sistema de destino.
                  </>
                ),
              },
              {
                termo: <AbaRef>Encerradas</AbaRef>,
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Concluída</SeloRef>, <SeloRef>Cancelada</SeloRef> e{' '}
                    <SeloRef cor="erro">Recusada</SeloRef>.
                  </>
                ),
              },
            ]}
          />
          <P>
            A fila segue a <strong>unidade escolhida no topo da tela</strong>: com uma unidade
            escolhida, mostra só os pedidos dela; com “todas”, o município inteiro — e aparece a coluna{' '}
            <strong>Unidade</strong>. Dá para filtrar por <strong>Fluxo</strong> (Interno, Externo, NAR) e
            por <strong>Destino</strong> (SISREG, SER, SERNIT, ESUS São Gonçalo), e buscar por nome do
            paciente, CPF ou número.
          </P>
          <P>
            A coluna <strong>Número</strong> mostra o número do sistema de destino quando ele existe;
            antes disso, o número interno <strong>PR-…</strong> em cinza. Clique na linha para abrir a
            análise do pedido.
          </P>
        </>
      ),
    },
    {
      id: 'analise',
      titulo: 'A análise do pedido',
      busca:
        'análise pedido cabeçalho decisão da regulação classificação de risco e cid hipótese alterar anexos visualizar arquivo pdf imagem caixinha regras do manual respostas condições marcadas médico novo a cadastrar cadastrei já existia para lançar no sistema copiar campos linha do tempo ajustada pela regulação',
      conteudo: (
        <>
          <P>
            A tela de análise reúne tudo o que a unidade mandou, na ordem em que se confere:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Cabeçalho',
                descricao:
                  'Número, paciente, procedimento, situação, fluxo, destino, data de abertura e CPF. Quando a situação tem motivo (devolução, recusa, cancelamento), ele aparece em destaque.',
              },
              {
                termo: 'Decisão da regulação',
                descricao:
                  'Diz em que pé o pedido está e mostra só os comandos que cabem naquele momento — ver “Decidir”.',
              },
              {
                termo: 'Classificação de risco e CID',
                descricao:
                  'Nos pedidos para o SER e o SERNIT: o risco e a hipótese (CID) que a unidade informou, com o botão Alterar para você corrigir antes de enviar — ver “Alterar a classificação de risco e o CID”.',
              },
              {
                termo: 'Regras do manual',
                descricao:
                  'O que a unidade respondeu e o que o sistema deduziu (idade, sexo), com o resultado de cada regra: atende, ressalva, bloqueia ou em aberto. Nas perguntas de lista aparecem as condições marcadas. O cartão mostra o que ficou gravado — não refaz a conta.',
              },
              {
                termo: 'Anexos',
                descricao:
                  'Os arquivos que a unidade anexou, caixinha por caixinha (uma por documento exigido, mais “Anexos gerais”). Clique no nome para abrir no visualizador — PDF ou imagem com zoom. Aqui é só leitura: quem anexa e remove é a unidade.',
              },
              {
                termo: 'Para lançar no sistema',
                descricao:
                  'O formulário campo a campo, com o nome do campo como o sistema de destino chama e um botão de copiar em cada valor: é o que se digita na tela de lá. As Observações já vêm com os CIDs secundários no fim. A classificação de risco aparece com o texto do combo de lá (no SER, “Prioridade 1”) e o badge colorido ao lado, só para conferir.',
              },
              {
                termo: 'Linha do tempo',
                descricao:
                  'Quem fez o quê, de qual situação para qual, com o motivo e o que mudou campo a campo. Uma alteração sua no risco ou no CID aparece como “Ajustada pela regulação”, com o seu nome.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Médico novo a cadastrar">
            Quando a unidade pediu um médico que não está na lista do sistema de destino, aparece um
            cartão amarelo com nome, documento e especialidade. No SER e no SERNIT, o médico se resolve{' '}
            <strong>no próprio envio</strong>: em <BotaoRef>Aceitar e enviar ao SER</BotaoRef>, a prévia
            mostra os nomes parecidos da lista do SER e, se não for nenhum, você autoriza o cadastro — quem
            cadastra é o envio, na tela de nova solicitação do SER (ver “Enviar ao SER ou ao SERNIT”). No
            cartão fica só <BotaoRef variante="outline">Recusar</BotaoRef>, com o motivo (a unidade lê). Se o
            cartão ficar vermelho, “Cadastro no SER a conferir”, veja o aviso sobre cadastro incerto na seção
            do envio.
          </Callout>
        </>
      ),
    },
    {
      id: 'decidir',
      titulo: 'Decidir: assumir, aceitar, devolver ou recusar',
      busca:
        'decidir decisão assumir aceitar e enviar ao ser aceitar e registrar envio já lancei no ser número gerado sistema devolver à unidade recusar motivo obrigatório ok já está no sisreg interno nar outro agente assumiu número repetido duplicado resultado modal',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    <BotaoRef>Assumir</BotaoRef>
                  </>
                ),
                detalhe:
                  'O pedido passa para “Em análise” no seu nome, e a unidade deixa de poder cancelá-lo. Se outro agente assumiu antes, a tela avisa.',
              },
              {
                titulo: 'Conferir',
                detalhe:
                  'Regras do manual, anexos e formulário. É aqui que se decide se o pedido tem condição de seguir.',
              },
              {
                titulo: 'Decidir',
                detalhe: 'Aceitar, devolver para a unidade corrigir, ou recusar — um dos três abaixo.',
              },
            ]}
          />
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef>Aceitar e enviar ao SER</BotaoRef>,
                descricao:
                  'Quando o destino é o SER, a plataforma faz o lançamento: preenche a tela do SER, anexa os documentos, grava e traz o número. Veja “Enviar ao SER”, logo abaixo.',
              },
              {
                termo: <BotaoRef>Aceitar e registrar envio</BotaoRef>,
                descricao:
                  'Para os outros destinos (e quando você já lançou no SER pela tela dele — o botão aparece como “Já lancei no SER — registrar número”): inclua o pedido na tela do sistema (use o quadro “Para lançar no sistema”) e informe aqui o número que ele gerou. O pedido passa para “Enviada ao sistema” e o número passa a identificar o caso. Número repetido no mesmo sistema é recusado — é sinal de pedido lançado duas vezes. No Interno e no NAR o sistema é sempre o SISREG.',
              },
              {
                termo: <BotaoRef variante="outline">Devolver à unidade</BotaoRef>,
                descricao:
                  'Quando falta ou está errado algo que a unidade pode corrigir. Escreva o que precisa ser corrigido (obrigatório): é o que a unidade lê para acertar e reenviar. Sem isso, o pedido volta igual.',
              },
              {
                termo: <BotaoRef variante="outline">Recusar</BotaoRef>,
                descricao:
                  'Quando o pedido não cabe. Pede o motivo (obrigatório; é o que a unidade lê) e encerra o pedido — não há como reabrir; para tentar de novo, a unidade abre outro. Também vale para um pedido que já estava devolvido.',
              },
              {
                termo: <BotaoRef variante="outline">OK — já está no SISREG</BotaoRef>,
                descricao:
                  'Só no fluxo Interno, direto em Recebidas: para o pedido que a unidade já lançou na fila do SISREG. Passa para “Na fila do sistema” sem precisar assumir.',
              },
            ]}
          />
          <Callout tipo="dica" titulo="O resultado aparece na própria janela">
            Ao devolver ou recusar, a janela do motivo mostra o que aconteceu e só fecha quando você
            clicar em <BotaoRef>Fechar</BotaoRef>. Se der erro, a mensagem aparece ali mesmo e o texto
            digitado continua.
          </Callout>
        </>
      ),
    },
    {
      id: 'ajustar-risco-cid',
      titulo: 'Alterar a classificação de risco e o CID',
      busca:
        'alterar classificação de risco reclassificar risco trocar cid hipótese corrigir antes de enviar ajuste da regulação ajustada pela regulação linha do tempo nome do técnico de para obrigatório não pode deixar em branco falha no envio cid recusado sem devolver à unidade',
      conteudo: (
        <>
          <P>
            A unidade é obrigada a preencher a classificação de risco e a hipótese (CID) para enviar o
            pedido. Mas quem decide é a regulação: nos pedidos para o SER e o SERNIT, você pode corrigir
            os dois antes de enviar, sem devolver à unidade.
          </P>
          <Passos
            itens={[
              {
                titulo: <BotaoRef>Assumir</BotaoRef>,
                detalhe:
                  'Só quem assumiu altera. Na fila sem dono, o quadro aparece só para leitura.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Alterar</BotaoRef>, no quadro “Classificação de risco e CID”
                  </>
                ),
                detalhe:
                  'Escolha o risco nos botões coloridos (as opções do sistema de destino) e o CID na caixa — a mesma lista que o destino aceita para aquele procedimento.',
              },
              {
                titulo: <BotaoRef>Salvar alteração</BotaoRef>,
                detalhe:
                  'O pedido passa a ir com o que você escolheu. Depois, “Aceitar e enviar” normalmente — a prévia já mostra o valor novo.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Fica registrado quem mudou e o que mudou">
            Cada alteração entra na linha do tempo como <strong>Ajustada pela regulação</strong>, com o nome
            de quem alterou e o antes → depois de cada campo (por exemplo, “Classificação de risco: URGENCIA →
            EMERGENCIA”). A unidade vê a mesma linha do tempo no pedido dela.
          </Callout>
          <Lista>
            <Item>
              Dá para alterar com o pedido <strong>Em análise</strong> e também depois de uma{' '}
              <strong>Falha no envio</strong> — é o caso do SER recusar o CID: troque e envie de novo.
            </Item>
            <Item>
              Não dá para deixar em branco: o risco e o CID foram obrigatórios para a unidade, e o envio
              pararia neles. Para trocar, escolha outro valor.
            </Item>
            <Item>
              Os demais campos (queixa, exames, médico, anexos) continuam com a unidade: se estiverem
              errados ou faltando, devolva dizendo o que corrigir.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'enviar-ao-ser',
      titulo: 'Enviar ao SER ou ao SERNIT',
      busca:
        'cadastrar o médico e enviar enviar ao ser aceitar e enviar envio automático prévia preencher tela do ser anexar gravar número senha do ser usuário do ser assinatura quem assina pedido parecido duplicado conferi é outro caso falha no envio gravar chegou ao ser conferir no ser enviar de novo registrar número dois anexos 5 mb pdf juntado sernit niterói enviar ao sernit usuário do sernit senha do sernit paciente novo no sernit cadastrado com o nosso cadastro só pelo cns completar cadastro do paciente médico não cadastrado no ser médico novo nomes parecidos abreviado é este autorizo cadastrar no ser cadastrar médico especialidade da lista do ser adicionar médico cadastro incerto cadastro a conferir não entrou duplicar médico no estado',
      conteudo: (
        <>
          <p>
            Com o destino SER, o botão <BotaoRef>Aceitar e enviar ao SER</BotaoRef> faz o que antes era
            feito à mão na tela do SER — como o envio ao SISCAN na anamnese. Com o destino SERNIT (a
            regulação de Niterói), o botão é <BotaoRef>Aceitar e enviar ao SERNIT</BotaoRef> e o
            caminho é o mesmo, com o usuário e a senha do SERNIT. O que muda no SERNIT está no fim
            desta seção.
          </p>
          <Passos
            itens={[
              {
                titulo: 'Entrar com o SEU usuário do SER',
                detalhe:
                  'Na primeira vez da sessão, a janela pede o usuário e a senha que você usa no site do SER. É você quem assina o pedido lá — o SER grava o nome de quem fez. A senha não fica guardada: sair do sistema a apaga. Atenção: entrar aqui derruba a sua aba do SER aberta no navegador (o SER só aceita uma sessão por usuário).',
              },
              {
                titulo: 'Prévia — nada é gravado',
                detalhe:
                  'A plataforma abre a tela de nova solicitação do SER e preenche tudo: recurso, paciente (pelo CNS ou CPF), médico solicitante, classificação de risco, unidade de origem, hipótese (CID) e os campos do recurso. A janela mostra campo a campo o que vai, os anexos como vão e se o SER já tem pedido parecido para o paciente.',
              },
              {
                titulo: <BotaoRef>Enviar ao SER</BotaoRef>,
                detalhe:
                  'Anexa os documentos, grava, relê o pedido no SER para conferir e traz o número. Leva cerca de um minuto — não feche a janela. O resultado aparece nela: o número do SER ou o erro, com o texto que o SER mostrou.',
              },
            ]}
          />
          <ListaDefinicoes
            itens={[
              {
                termo: 'Anexos',
                descricao:
                  'O SER aceita no máximo dois arquivos de até 5 MB. Com até dois, eles vão como estão, com o título que a unidade deu como nome. Com mais, a plataforma junta tudo num PDF só. Arquivo acima de 5 MB é recusado antes de tocar no SER — peça à unidade uma versão menor.',
              },
              {
                termo: 'Pedido parecido no SER',
                descricao:
                  'Se o SER já tem pedido do mesmo paciente para o mesmo recurso, a prévia mostra o número e a situação, e só deixa enviar depois que você marcar “Conferi: é outro caso”.',
              },
              {
                termo: 'Erro antes de gravar',
                descricao:
                  'Recurso que o SER não oferece mais, CPF/CNS que é de outra pessoa no SER, médico escolhido que sumiu da lista do SER, CID que o recurso não aceita, campo obrigatório vazio: a janela diz qual, e nada foi gravado. Classificação de risco e CID você corrige aqui mesmo, no quadro “Classificação de risco e CID”; o resto, devolva à unidade. Depois, tente de novo.',
              },
              {
                termo: '“Falha no envio”',
                descricao:
                  'Se o envio não termina, o pedido fica como “Falha no envio”, com o motivo em amarelo. Quando o motivo diz que o Gravar chegou ao SER, procure o pedido no SER ANTES de qualquer coisa: se ele existe, use “Registrar número (já está no sistema)”; se não existe, “Enviar ao SER de novo”. Se o motivo é o risco ou o CID, altere no quadro e envie de novo.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Médico que não está na lista do SER">
            Quando a unidade pediu um médico que o SER não tem, a prévia mostra o bloco{' '}
            <strong>Médico não cadastrado no SER</strong>, e o envio só libera depois de você decidir ali mesmo:
            <Lista>
              <li>
                <strong>Nomes parecidos na lista do SER hoje</strong> — o SER abrevia muito (“RAFAELA R.
                BEDRAN”) e às vezes tem o mesmo CRM com outro nome. Se for um deles, <BotaoRef>É este</BotaoRef>:
                a solicitação passa a usar o cadastro de lá e a prévia é refeita.
              </li>
              <li>
                <BotaoRef>Não é nenhum — autorizo cadastrar no SER</BotaoRef> — confira nome, documento
                (CRM) e escolha a <strong>especialidade da lista do SER</strong> (a plataforma sugere a que
                parece a escrita pela unidade: “ONCOLOGISTA” → “ONCOLOGIA”) e marque a autorização. Você só
                autoriza: o botão vira <BotaoRef>Cadastrar o médico e enviar ao SER</BotaoRef>, e o envio, com o
                seu usuário, abre a tela de nova solicitação do SER, cadastra o médico pelo “Adicionar Médico”
                (ícone ao lado de “Médico responsável”), confere se o nome entrou na lista e só então preenche e
                grava a solicitação. Se o nome já estava lá, nada é cadastrado e a solicitação usa esse
                cadastro. Se o cadastro do médico não der certo, a solicitação <strong>não</strong> é enviada e
                continua em análise.
              </li>
              <li>Para recusar o médico, use o cartão do médico na solicitação (o motivo vai para a unidade).</li>
            </Lista>
            <strong>O cadastro de médicos do SER não tem editar nem apagar</strong> — um médico duplicado fica
            lá para sempre. Por isso a autorização é sua, a cada médico. Se o SER gravar sem a plataforma
            conseguir ver o nome na lista, o médico fica <strong>“Cadastro no SER a conferir”</strong> e ninguém
            tenta de novo sozinho: confira no SER e, no cartão do médico, use <BotaoRef>Já existia no SER</BotaoRef>{' '}
            (entrou — escolha o cadastro) ou <BotaoRef>Não entrou</BotaoRef> (volta a aguardar cadastro). No
            SERNIT é igual, com a lista e o usuário do SERNIT.
          </Callout>
          <Callout tipo="regra" titulo="No SERNIT: paciente que o SERNIT ainda não conhece">
            O SERNIT só acha o paciente pelo <strong>CNS</strong>, e só se ele já teve pedido lá — o
            SERNIT não consulta o CADSUS. Quando não acha, a plataforma cadastra o paciente na própria
            tela do SERNIT com os dados do <strong>nosso cadastro</strong>: nome, CPF, sexo, data de
            nascimento, nome da mãe, endereço, telefone e raça/cor. A prévia mostra cada um desses
            campos. Se faltar CNS, nome, CPF, sexo ou data de nascimento no nosso cadastro, o envio
            para antes de gravar e diz o que completar no cadastro do paciente.
          </Callout>
          <Callout tipo="dica" titulo="Por que a plataforma acha tudo pelo nome">
            O SER renumera a lista de recursos quando a SES acrescenta um — o número de ontem pode ser
            outra especialidade hoje. Por isso a plataforma escolhe recurso, médico e CID pelo nome, na
            tela do dia. Se a SES renomear um recurso, o envio para com aviso, em vez de mandar o
            pedido para o lugar errado.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem vê e quem pode',
      busca:
        'permissão perfil módulo gestão de fila agente regulador 48 rascunho não aparece cancelar unidade minha fila abrir na gestão de fila notificações todas as unidades',
      conteudo: (
        <>
          <Lista>
            <Item>
              A tela aparece para quem tem, no perfil, o módulo{' '}
              <strong>Regulação — Gestão de fila</strong>. Quem só abre solicitações não a vê.
            </Item>
            <Item>
              <strong>Rascunho não aparece aqui</strong> — é só de quem o abriu, e passa a existir para a
              regulação quando é enviado.
            </Item>
            <Item>
              <strong>Cancelar é da unidade</strong>, e só antes de um agente assumir. Depois disso, o
              pedido sai por devolução ou recusa.
            </Item>
            <Item>
              O agente também pode abrir solicitações, em <strong>Regulação → Solicitações</strong>. Ao
              abrir por lá um pedido que já chegou à regulação, o botão{' '}
              <BotaoRef variante="outline">Abrir na Gestão de fila</BotaoRef> traz para a análise.
            </Item>
            <Item>
              Nas <strong>Notificações</strong>, em <AbaRef>Todas as unidades</AbaRef>, clicar na linha
              abre a análise do pedido.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca:
        'dúvidas não vejo o pedido unidade topo assumi por engano soltar outro agente não consigo registrar envio médico pendente falta documento procedimento errado trocar procedimento fila da regulação sumiu menu risco errado cid errado sem classificação de risco',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Cadê a “Fila da regulação”?',
              descricao:
                'É esta tela. Ela saiu de dentro de Solicitações e virou “Gestão de fila”, no menu Regulação. O endereço antigo continua trazendo para cá.',
            },
            {
              termo: 'A unidade diz que enviou e eu não vejo.',
              descricao:
                'Confira a unidade escolhida no topo da tela — a fila mostra só a dela; com “todas”, o município. E lembre que rascunho não aparece: a unidade precisa ter clicado em “Enviar para a pré-regulação”.',
            },
            {
              termo: 'Assumi um pedido por engano.',
              descricao:
                'Não há como “soltar” um pedido assumido. Mas qualquer agente pode decidir um pedido que está em análise — combine com o colega que vai cuidar dele.',
            },
            {
              termo: 'O envio ao SER pediu minha senha.',
              descricao:
                'É de propósito: quem assina o pedido no SER é você. A credencial da Configuração é só de leitura (sincronismo). A senha vale até você sair do sistema.',
            },
            {
              termo: 'Ficou “Falha no envio”. E agora?',
              descricao:
                'Leia o motivo em amarelo. Se diz que o Gravar chegou ao SER, procure o pedido no SER primeiro — achou, registre o número; não achou, envie de novo. Se diz “Nada foi gravado no SER”, corrija o que ele aponta e envie de novo.',
            },
            {
              termo: 'Não consigo registrar o envio.',
              descricao:
                'Se há o cartão amarelo “Médico novo a cadastrar”, resolva-o primeiro. Se o número já está em outra solicitação do mesmo sistema, confira se o pedido não foi lançado duas vezes.',
            },
            {
              termo: 'A classificação de risco ou o CID estão errados.',
              descricao:
                'Não precisa devolver: assuma, use “Alterar” no quadro “Classificação de risco e CID”, salve e envie. A troca fica na linha do tempo com o seu nome.',
            },
            {
              termo: 'O envio disse que falta a classificação de risco, mas a unidade preencheu.',
              descricao:
                'Era o caso dos pedidos abertos antes de 01/10/2026, quando o formulário do SER ainda não tinha o risco: o envio usava o formulário da abertura. Desde 09/10/2026 o envio usa sempre o formulário como está agora. Se ainda acontecer, confira o quadro “Classificação de risco e CID” e altere ali.',
            },
            {
              termo: 'O procedimento escolhido está errado.',
              descricao:
                'O regulador não troca o procedimento: trocar muda as regras e as perguntas do manual, e quem sabe respondê-las é a unidade. Devolva à unidade dizendo qual procedimento usar — ela troca no assistente, responde as novas perguntas e reenvia.',
            },
            {
              termo: 'O pedido está sem o documento que eu preciso.',
              descricao:
                'Devolva à unidade dizendo qual documento falta. Ela anexa pelo assistente e reenvia — o pedido volta para Recebidas.',
            },
          ]}
        />
      ),
    },
  ],
};
