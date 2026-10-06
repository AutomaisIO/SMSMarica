import { ClipboardCheck } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo da lista de solicitações — "Exames e consultas" (`/app/solicitacoes`).
 *
 * Exames de imagem e consultas eram duas telas; viraram uma lista só, com o filtro "Tipo". A ordem
 * segue a dúvida de quem chega: o que entra aqui → como filtrar → como ler uma linha → o que muda
 * entre exame e consulta no detalhe → chegada e cancelamento → quem pode o quê → dúvidas.
 *
 * Conferido em `features/solicitacoes` (SolicitacoesPage, SolicitacaoDetalhePage,
 * SituacaoSolicitacao) e no backend (`SolicitacoesService.ListarAsync`, `AutorizarAsync`,
 * `CancelarAsync`; `SolicitacoesController` para as permissões).
 */
export const artigoSolicitacoes: Artigo = {
  slug: 'solicitacoes',
  titulo: 'Exames e consultas',
  resumo:
    'Tudo o que foi regulado para a unidade — exames de imagem, consultas, laboratório e demais procedimentos — numa lista só, com chegada, cancelamento e comunicação com o paciente.',
  grupo: 'assistencial',
  icone: ClipboardCheck,
  rota: '/app/solicitacoes',
  publico: 'Recepção das unidades e quem acompanha os agendamentos',
  atualizadoEm: '2026-10-06',
  palavrasChave: [
    'diagnóstico inicial',
    'CID',
    'CID-10',
    'hipótese diagnóstica',
    'solicitações',
    'exames',
    'consultas',
    'exames e consultas',
    'tela de consultas',
    'tipo',
    'laboratório',
    'gráfico funcional',
    'endoscopia',
    'cirurgia',
    'SISREG',
    'agendamento',
    'chegada',
    'registrar chegada',
    'autorizar',
    'chave de confirmação',
    'cancelar',
    'situação',
    'falta',
    'atrasado',
    'confirmado',
    'aguardando',
    'retorno',
    'ver como solicitante',
    'ordem de chegada',
    'sem CPF',
    'linha laranja',
    'urgente',
    'accession',
    'nº do pedido',
    'especialidade',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'lista única exames de imagem consultas laboratório cirurgia regulado SISREG uma tela só',
      conteudo: (
        <>
          <P>
            É a lista de tudo o que foi regulado para a unidade: exames de imagem, consultas,
            laboratório, métodos gráficos (ECG, Holter…), endoscopia, cirurgia e o que mais vier do
            SISREG. Antes, exames e consultas ficavam em telas separadas; agora estão juntos, e o
            filtro <strong>Tipo</strong> separa quando for preciso.
          </P>
          <P>
            Quase tudo entra pela importação do SISREG. A exceção é o botão{' '}
            <BotaoRef>Nova solicitação</BotaoRef>, que cria um pedido de exame de imagem à mão — e só
            aparece para quem tem essa permissão específica.
          </P>
          <Callout tipo="regra" titulo="O exame de imagem tem mais coisas que o resto">
            Só o exame de imagem vai para o aparelho (worklist), volta do PACS e ganha laudo. Por
            isso a linha e o detalhe dele têm botões que a consulta não tem. O resto da tela — busca,
            situação, chegada, cancelamento, mensagens ao paciente — é igual para todos.
          </Callout>
        </>
      ),
    },
    {
      id: 'filtros',
      titulo: 'Como achar o que procura',
      busca:
        'buscar nome CPF CNS número SISREG pedido especialidade tipo status data hoje período ver como solicitante somente retornos ordem de chegada',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Buscar',
                descricao:
                  'Nome, CPF, CNS, nº do SISREG, nº do pedido ou a especialidade (“cardiologia”). A busca procura em qualquer data — o período é ignorado enquanto ela estiver preenchida.',
              },
              {
                termo: 'Tipo',
                descricao:
                  'Exame de imagem, Consulta, Laboratório… A tela lembra a sua escolha: quem só trabalha com imagem escolhe uma vez e ela já abre assim.',
              },
              {
                termo: 'Status',
                descricao:
                  'Solicitada, Agendada, Realizada e Cancelada valem para todos os tipos. Enviada ao PACS, Recebida, Em execução e Laudada só existem no exame de imagem.',
              },
              {
                termo: 'Data inicial / final e Hoje',
                descricao: 'Filtram pela data do agendamento. “Hoje” trava no dia corrente e fica lembrado.',
              },
              {
                termo: 'Ordem de Chegada',
                descricao: 'Com “Hoje” ligado, põe no topo quem chegou primeiro (chegada registrada na recepção).',
              },
              {
                termo: 'Ver como solicitante',
                descricao:
                  'Desmarcado, mostra o que a sua unidade REALIZA. Marcado, o que ela pediu a outra unidade.',
              },
              {
                termo: 'Somente retornos',
                descricao: 'Só as vagas de retorno no SISREG.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'como-ler',
      titulo: 'Como ler uma linha',
      busca: 'seta recebida enviada pedido procedimento situação aguardando confirmado atrasado falta autorizado linha laranja vermelha apagada',
      conteudo: (
        <>
          <Sub>As colunas</Sub>
          <Lista>
            <Item>
              <strong>Pedido</strong> — a seta verde (para dentro) é o que a sua unidade recebe para
              realizar; a azul (para fora), o que ela pediu. No exame de imagem aparece o nº do
              pedido e, embaixo, o do SISREG. Na consulta não existe nº do pedido: o nº do SISREG
              ocupa o lugar.
            </Item>
            <Item>
              <strong>Procedimento</strong> — no exame, o tipo de exame com a modalidade (MG, US…);
              nos demais, a especialidade com um selo do tipo (Consulta, Laboratório…). Embaixo, a
              unidade que executa.
            </Item>
            <Item>
              <strong>Situação</strong> — enquanto o paciente não chegou, a tela mostra a situação
              “de fora”: <em>Aguardando</em> (sem resposta), <em>Confirmado</em> (respondeu que vem),
              <em> Atrasado</em> (passou uma hora do horário), <em>Falta</em> (passou das 18h do dia),
              <em> Autorizado</em> (chegada registrada) ou <em>Cancelado</em> (avisou que não vem). Ao
              lado, os checks das mensagens de WhatsApp.
            </Item>
            <Item>
              <strong>Ações</strong> — anamnese, declaração, imagens e laudo. Só existem no exame de
              imagem; na consulta a coluna fica vazia.
            </Item>
          </Lista>
          <Sub>As cores da linha</Sub>
          <Lista>
            <Item>
              <strong>Laranja</strong> — exame de imagem cujo paciente veio do SISREG sem CPF. Ao
              clicar, a tela pede o CPF antes de abrir: sem ele o exame não vai para o aparelho.
              Consulta não pede, porque não passa pelo PACS.
            </Item>
            <Item>
              <strong>Vermelha</strong> — solicitação urgente.
            </Item>
            <Item>
              <strong>Apagada</strong> — o paciente avisou que não vem.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'detalhe',
      titulo: 'O detalhe: exame e consulta',
      busca: 'detalhe exame consulta ciclo do exame equipamento worklist PACS laudo histórico de mensagens seção SISREG diagnóstico inicial CID CID-10 hipótese diagnóstica',
      conteudo: (
        <>
          <P>
            Clicar na linha abre o detalhe. Nos dois tipos ele mostra o paciente, quem solicitou, a
            regulação (nº do SISREG, chave, prioridade), a chegada, o histórico de mensagens e
            contatos, a seção do SISREG e a linha do tempo.
          </P>
          <P>
            Na <strong>Regulação</strong> aparece também o <strong>diagnóstico inicial</strong> — o
            CID‑10 que o médico informou no pedido, do jeito que vem do SISREG (ex.:{' '}
            <em>CID Z12</em>). É o que ajuda o laudista a saber o tipo de laudo a fazer. Passe o
            mouse sobre o código para ler o que ele significa por extenso (ex.: <em>Exame especial
            de rastreamento de neoplasias</em>). Quando o pedido não traz CID, a linha não aparece.
          </P>
          <P>
            Só no exame de imagem aparecem o <strong>ciclo do exame</strong> (Solicitada → Enviada →
            Recebida → Em execução → Realizada → Laudada), o equipamento de destino, a troca de
            unidade executante, o reenvio à worklist, o envio do resultado por WhatsApp, a correção
            de identidade e os botões de declaração, imagens e laudo.
          </P>
        </>
      ),
    },
    {
      id: 'chegada',
      titulo: 'Registrar a chegada',
      busca: 'chegada autorizar chave de confirmação registrar chegada recepção CPF telefone verificado dispensa equipamento',
      conteudo: (
        <>
          <P>
            Com o paciente no balcão, a recepção digita a <strong>chave de confirmação</strong> do
            comprovante do SISREG. Se a chave do SISREG já tiver sido lida pela seção SISREG, a
            digitada precisa bater com ela.
          </P>
          <Lista>
            <Item>
              Na <strong>consulta</strong> (e nos demais tipos), o botão é{' '}
              <BotaoRef>Registrar chegada</BotaoRef> e basta a chave.
            </Item>
            <Item>
              No <strong>exame de imagem</strong>, o botão é <BotaoRef>Autorizar e enviar</BotaoRef>{' '}
              e ele também libera o envio ao aparelho. Por isso exige CPF, telefone verificado (ou a
              dispensa registrada) e, quando a unidade tem mais de uma sala, a escolha do
              equipamento.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Presença vence a resposta do WhatsApp">
            Registrar a chegada marca a presença como confirmada, mesmo que o paciente tenha dito
            pelo WhatsApp que não viria.
          </Callout>
        </>
      ),
    },
    {
      id: 'cancelar',
      titulo: 'Cancelar',
      busca: 'cancelar cancelamento motivo SISREG dcm4chee worklist',
      conteudo: (
        <>
          <P>
            <BotaoRef>Cancelar</BotaoRef> pede um motivo — é o que responde, semanas depois, por que
            a vaga foi cancelada. A consulta pode ser cancelada enquanto estiver Solicitada ou
            Agendada; o exame de imagem, antes de ser executado, e nesse caso o item também sai da
            worklist do aparelho.
          </P>
          <Callout tipo="atencao" titulo="Cancelar aqui não cancela no SISREG">
            O cancelamento vale no sistema. No SISREG ele continua sendo feito por lá.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo exames e consultas solicitações consulta edição exclusão nova solicitação',
      conteudo: (
        <>
          <P>
            Tudo aqui é governado pelo módulo <strong>Exames e consultas (solicitações)</strong> do
            perfil. O escopo de unidade continua valendo: cada pessoa só vê o que a sua unidade
            realiza ou pediu.
          </P>
          <Lista>
            <Item>
              <strong>Consulta</strong> — ver a lista, o detalhe, o histórico de mensagens e baixar
              declaração, imagens e laudo.
            </Item>
            <Item>
              <strong>Edição</strong> — registrar a chegada, cancelar, informar o CPF, reenviar
              mensagens, registrar contato e, no exame de imagem, mexer em equipamento, unidade e
              worklist.
            </Item>
            <Item>
              <strong>Exclusão</strong> — excluir um pedido de exame de imagem ainda não executado.
            </Item>
          </Lista>
          <P>
            O botão <BotaoRef>Nova solicitação</BotaoRef> tem permissão própria (Solicitação de
            exame manual), porque criar à mão é a exceção.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'onde está a tela de consultas favoritos sem número do pedido declaração de comparecimento consulta',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Onde foi parar a tela de Consultas?',
              descricao:
                'Está aqui: escolha “Consulta” no filtro Tipo. Quem tinha acesso a Consultas passou a ter acesso a esta tela, e os endereços antigos salvos nos favoritos trazem para cá.',
            },
            {
              termo: 'Por que a consulta não tem nº do pedido?',
              descricao:
                'O nº do pedido (accession) é o que o aparelho de imagem usa. A consulta não vai a aparelho nenhum — o nº do SISREG basta.',
            },
            {
              termo: 'Não aparece declaração de comparecimento na consulta.',
              descricao: 'Por enquanto a declaração sai só para exame de imagem realizado.',
            },
          ]}
        />
      ),
    },
  ],
};
