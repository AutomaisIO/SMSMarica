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
 * `ModuloPermissao` 48).
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
  atualizadoEm: '2026-10-05',
  palavrasChave: [
    'gestão de fila',
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
        'análise pedido cabeçalho decisão da regulação anexos visualizar arquivo pdf imagem caixinha regras do manual respostas condições marcadas médico novo a cadastrar cadastrei já existia para lançar no sistema copiar campos linha do tempo',
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
                  'Quem fez o quê, de qual situação para qual, com o motivo e o que mudou campo a campo.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Médico novo a cadastrar">
            Quando a unidade pediu um médico que não está na lista do sistema de destino, aparece um
            cartão amarelo com nome, documento e especialidade. Cadastre o médico no sistema (ícone
            “Adicionar médico” ao lado de “Médico responsável”) e resolva aqui:{' '}
            <BotaoRef>Cadastrei no SER</BotaoRef>, <BotaoRef variante="outline">Já existia no SER</BotaoRef>{' '}
            (escolhe o cadastro que já estava lá — a solicitação passa a usar esse nome) ou{' '}
            <BotaoRef variante="outline">Recusar</BotaoRef>, com o motivo. O registro do envio só libera
            depois disso.
          </Callout>
        </>
      ),
    },
    {
      id: 'decidir',
      titulo: 'Decidir: assumir, aceitar, devolver ou recusar',
      busca:
        'decidir decisão assumir aceitar e registrar envio número gerado sistema devolver à unidade recusar motivo obrigatório ok já está no sisreg interno nar outro agente assumiu número repetido duplicado resultado modal',
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
                termo: <BotaoRef>Aceitar e registrar envio</BotaoRef>,
                descricao:
                  'Aceitar é levar o pedido ao sistema de destino: inclua-o na tela do sistema (use o quadro “Para lançar no sistema”) e informe aqui o número que ele gerou. O pedido passa para “Enviada ao sistema” e o número passa a identificar o caso. Número repetido no mesmo sistema é recusado — é sinal de pedido lançado duas vezes. No Interno e no NAR o sistema é sempre o SISREG.',
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
        'dúvidas não vejo o pedido unidade topo assumi por engano soltar outro agente não consigo registrar envio médico pendente falta documento procedimento errado trocar procedimento fila da regulação sumiu menu',
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
              termo: 'Não consigo registrar o envio.',
              descricao:
                'Se há o cartão amarelo “Médico novo a cadastrar”, resolva-o primeiro. Se o número já está em outra solicitação do mesmo sistema, confira se o pedido não foi lançado duas vezes.',
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
