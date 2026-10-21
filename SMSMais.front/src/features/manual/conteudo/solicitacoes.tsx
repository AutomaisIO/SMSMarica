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
 * `CancelarAsync`; `SolicitacoesController` para as permissões). Pedido com mais de um código
 * interno (ADR-0074): `SolicitacoesService.LinhasDoPedido`, `SateliteImagemSisreg`, `SeloVinculado`.
 * "Tem exame" ≠ "é imagem" (adendo de 20/10/2026 ao ADR-0021): `CategoriaSigtap.CriaSatelite` no
 * backend e `temExame()` em `features/solicitacoes/types.ts`.
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
  atualizadoEm: '2026-10-20',
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
    'vinculado',
    'vinculado a',
    'mais de um procedimento',
    'dois procedimentos',
    'direito e esquerdo',
    'código interno',
    'accession com sufixo',
    'procedimentos deste pedido',
    'holter',
    'MAPA',
    'pressão arterial',
    'eletrocardiograma',
    'ECG',
    'tem exame',
    'modalidade DICOM',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca:
        'lista única exames de imagem consultas laboratório cirurgia regulado SISREG uma tela só tem exame holter MAPA eletrocardiograma ECG modalidade DICOM gráfico funcional worklist integrada escopo ressonância outro',
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
          <Callout tipo="regra" titulo="O pedido com exame tem mais coisas que o resto">
            Só o pedido com <strong>exame</strong> vai para o aparelho (worklist), volta do PACS e
            ganha laudo. Por isso a linha e o detalhe dele têm botões que a consulta não tem. O resto
            da tela — busca, situação, chegada, cancelamento, mensagens ao paciente — é igual para
            todos.
          </Callout>
          <Sub>Quem tem exame</Sub>
          <P>
            Todo exame de imagem tem. Fora da imagem, só quando as duas coisas estão configuradas: o
            tipo de exame tem uma <strong>modalidade DICOM</strong> (em Tipos de Exame) e a unidade que
            executa tem esse exame no escopo com o <strong>envio à worklist ligado</strong> — ou seja, a
            unidade está integrada a um aparelho. Hoje, o <strong>Holter</strong> e o{' '}
            <strong>MAPA</strong> no CDT. Eles continuam sendo do tipo{' '}
            <strong>Gráfico/funcional</strong>, que é o que são, mas ganham nº do pedido e vão para o
            aparelho da cardiologia como um exame de imagem, e o laudo volta pelo PACS.
          </P>
          <P>
            O <strong>eletrocardiograma</strong> não tem exame: a máquina imprime o traçado na hora e
            ele é entregue ao paciente, sem nada a mandar nem a receber. Audiometria, espirometria e
            os demais métodos gráficos também seguem sem — e também o pedido que chega com o tipo
            errado (ex.: uma ressonância marcada como “Outro”) numa clínica com que não há integração.
          </P>
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
                  'Solicitada, Agendada, Realizada e Cancelada valem para todos os tipos. Enviada ao PACS, Recebida, Em execução e Laudada só existem no pedido com exame (imagem, Holter, MAPA).',
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
      busca: 'seta recebida enviada pedido procedimento situação aguardando confirmado atrasado falta autorizado linha laranja vermelha apagada vinculado a selo',
      conteudo: (
        <>
          <Sub>As colunas</Sub>
          <Lista>
            <Item>
              <strong>Pedido</strong> — a seta verde (para dentro) é o que a sua unidade recebe para
              realizar; a azul (para fora), o que ela pediu. No pedido com exame aparece o nº do
              pedido e, embaixo, o do SISREG. Na consulta não existe nº do pedido: o nº do SISREG
              ocupa o lugar. O selo <strong>vinculado a</strong> marca um pedido com mais de um
              procedimento — ver a seção <em>Pedido com mais de um procedimento</em>, abaixo.
            </Item>
            <Item>
              <strong>Procedimento</strong> — no exame, o tipo de exame com a modalidade (MG, US… e,
              no Holter e no MAPA, ECG e OT); nos demais, a especialidade com um selo do tipo
              (Consulta, Laboratório…). Embaixo, a unidade que executa.
            </Item>
            <Item>
              <strong>Situação</strong> — enquanto o paciente não chegou, a tela mostra a situação
              “de fora”: <em>Aguardando</em> (sem resposta), <em>Confirmado</em> (respondeu que vem),
              <em> Atrasado</em> (passou uma hora do horário), <em>Falta</em> (passou das 18h do dia),
              <em> Autorizado</em> (chegada registrada) ou <em>Cancelado</em> (avisou que não vem). Ao
              lado, os checks das mensagens de WhatsApp.
            </Item>
            <Item>
              <strong>Ações</strong> — anamnese, declaração, imagens e laudo. Só existem no pedido
              com exame; na consulta a coluna fica vazia.
            </Item>
          </Lista>
          <Sub>As cores da linha</Sub>
          <Lista>
            <Item>
              <strong>Laranja</strong> — pedido com exame cujo paciente veio do SISREG sem CPF. Ao
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
      busca: 'detalhe exame consulta ciclo do exame equipamento worklist PACS laudo histórico de mensagens seção SISREG diagnóstico inicial CID CID-10 hipótese diagnóstica procedimentos deste pedido',
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
            Só no pedido com exame aparecem o <strong>ciclo do exame</strong> (Solicitada → Enviada →
            Recebida → Em execução → Realizada → Laudada), o equipamento de destino, a troca de
            unidade executante, o reenvio à worklist, o envio do resultado por WhatsApp, a correção
            de identidade e os botões de declaração, imagens e laudo.
          </P>
          <P>
            No Holter e no MAPA o cabeçalho continua com o selo <strong>Gráfico/funcional</strong>{' '}
            — o selo diz o que o pedido é; o ciclo do exame embaixo diz que ele vai ao aparelho.
          </P>
        </>
      ),
    },
    {
      id: 'varios-procedimentos',
      titulo: 'Pedido com mais de um procedimento',
      busca:
        'vinculado vinculado a selo dois procedimentos mais de um procedimento direito esquerdo joelho mesmo número SISREG código interno accession sufixo -1 -2 procedimentos deste pedido um laudo por exame anamnese do pedido',
      conteudo: (
        <>
          <P>
            Às vezes o SISREG manda um pedido só com mais de um procedimento — por exemplo, raio-X do
            joelho direito e do esquerdo com o mesmo nº. Para o SISREG o pedido é um só: ele cancela,
            marca presença e marca falta no pedido inteiro, nunca num procedimento.
          </P>
          <P>
            Aqui, cada procedimento vira um <strong>exame</strong>, porque cada um vai ao aparelho com o
            seu código, gera o seu estudo e recebe o seu laudo. O primeiro fica com o nº do pedido de
            sempre; os seguintes, com o mesmo nº e um sufixo:
          </P>
          <Lista>
            <Item>
              <strong>261016019</strong> — joelho direito (o primeiro procedimento do pedido);
            </Item>
            <Item>
              <strong>261016019-1</strong> — joelho esquerdo (o segundo), e assim por diante (-2, -3…).
            </Item>
          </Lista>
          <P>
            Na lista, o pedido aparece em uma linha por exame, e cada linha tem o selo{' '}
            <strong>vinculado a</strong> com o nº da outra. No detalhe, o cartão do exame mostra{' '}
            <strong>Procedimentos deste pedido</strong>: o nº, o código interno e a situação de cada
            um, com atalho para abrir o outro.
          </P>
          <Sub>O que vale para o pedido inteiro</Sub>
          <Lista>
            <Item>
              <strong>Chegada</strong> — <BotaoRef>Autorizar e enviar</BotaoRef> em qualquer uma das
              linhas recebe o pedido e manda <strong>todos</strong> os exames dele para a worklist. Não
              é preciso autorizar duas vezes.
            </Item>
            <Item>
              <strong>Cancelar</strong> — cancela o pedido inteiro: todos os exames que ainda não foram
              feitos saem da worklist.
            </Item>
            <Item>
              <strong>Anamnese</strong> — é uma só para o pedido. Preenchida em qualquer linha, vale
              para as duas, e o laudo de cada exame a encontra.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Um código, um exame">
            Só vira mais de um exame quando o SISREG manda mais de um código. Um procedimento que já
            é dos dois lados num código só (ex.: “quadril direito e esquerdo”) continua sendo um exame.
            E o mais comum — direito e esquerdo em pedidos separados — já chega como dois pedidos, sem
            selo nenhum.
          </Callout>
        </>
      ),
    },
    {
      id: 'chegada',
      titulo: 'Registrar a chegada',
      busca: 'chegada autorizar chave de confirmação registrar chegada recepção CPF telefone verificado dispensa equipamento todos os exames do pedido vinculado',
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
              No <strong>pedido com exame</strong> (imagem, Holter, MAPA), o botão é{' '}
              <BotaoRef>Autorizar e enviar</BotaoRef>{' '}
              e ele também libera o envio ao aparelho. Por isso exige CPF, telefone verificado (ou a
              dispensa registrada) e, quando a unidade tem mais de uma sala, a escolha do
              equipamento. Num pedido com mais de um procedimento, um clique envia todos os exames
              dele.
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
      busca: 'cancelar cancelamento motivo SISREG dcm4chee worklist pedido inteiro vinculado',
      conteudo: (
        <>
          <P>
            <BotaoRef>Cancelar</BotaoRef> pede um motivo — é o que responde, semanas depois, por que
            a vaga foi cancelada. A consulta pode ser cancelada enquanto estiver Solicitada ou
            Agendada; o pedido com exame, antes de ser executado, e nesse caso o item também sai da
            worklist do aparelho. Num pedido com mais de um procedimento, cancela o pedido inteiro —
            como o SISREG faz.
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
              mensagens, registrar contato e, no pedido com exame, mexer em equipamento, unidade e
              worklist.
            </Item>
            <Item>
              <strong>Exclusão</strong> — excluir um pedido com exame ainda não executado.
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
      busca:
        'onde está a tela de consultas favoritos sem número do pedido declaração de comparecimento consulta holter MAPA eletrocardiograma antigo sem exame',
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
              descricao: 'Por enquanto a declaração sai só para pedido com exame (imagem, Holter, MAPA) já realizado.',
            },
            {
              termo: 'Por que o Holter tem nº do pedido e o eletrocardiograma não?',
              descricao:
                'O Holter e o MAPA vão ao aparelho da cardiologia e o laudo volta pelo PACS — para isso precisam do nº do pedido. O eletrocardiograma sai impresso na hora e é entregue ao paciente: não há o que mandar nem o que receber.',
            },
            {
              termo: 'Um Holter ou MAPA antigo aparece sem nº do pedido.',
              descricao:
                'Os agendados para antes desta mudança (outubro de 2026) podem ainda estar sem exame e aparecem como um gráfico/funcional comum. Os de agora em diante já chegam com exame.',
            },
          ]}
        />
      ),
    },
  ],
};
