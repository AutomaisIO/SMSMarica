import { CalendarClock } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Atendimentos do Transporte de Pacientes (o antigo "Tratamentos"): agenda por dias da
 * semana, condição do paciente, acompanhantes e o que o paciente vê no app.
 *
 * Conferido no código: `features/tratamentos` (TratamentoFormPage, TratamentoDetalhePage,
 * CamposAgenda, CamposNecessidades, CampoLimiteAcompanhantes, EditorAgenda, SeletorAcompanhantesSessao,
 * PainelConfirmacao), `features/acompanhantes`, `SMSMais.cidadao.pwa` (Transporte, MeusAcompanhantes)
 * e no backend (`TratamentosService`, `AgendaDeSessoes`, `RenovacaoAtendimentosContinuosWorker`,
 * `AcompanhantesService`, `CidadaoController`).
 */
export const artigoAtendimentosTransporte: Artigo = {
  slug: 'atendimentos-transporte',
  titulo: 'Atendimentos (transporte de pacientes)',
  resumo:
    'Quem o transporte leva, para onde e em que dias: a agenda por dias da semana (ou contínua), a condição do paciente para a viagem e os acompanhantes.',
  grupo: 'transporte',
  icone: CalendarClock,
  rota: '/app/tratamentos',
  publico: 'Quem organiza o transporte de pacientes',
  atualizadoEm: '2026-09-29',
  palavrasChave: [
    'atendimento',
    'tratamento',
    'transporte',
    'TFD',
    'sessão',
    'viagem',
    'agenda',
    'dias da semana',
    'número de sessões',
    'contínuo',
    'renovar',
    'hemodiálise',
    'trocar agenda',
    'encerrar',
    'cadeirante',
    'cadeira de rodas',
    'veículo adaptado',
    'maca',
    'oxigênio',
    'imunodeficiente',
    'veículo exclusivo',
    'carro alto',
    'ajuda',
    'acompanhante',
    'segundo acompanhante',
    'liberação',
    'justificativa',
    'CPF',
    'data de nascimento',
    'app do paciente',
    'tempo médio',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é um atendimento',
      busca: 'atendimento tratamento o que é transporte paciente destino dias sessões viagens menu',
      conteudo: (
        <>
          <P>
            O atendimento diz ao transporte <strong>quem</strong> levar, <strong>para onde</strong> e{' '}
            <strong>em que dias</strong>: o paciente, a unidade de atendimento (o destino da van), o
            tipo de tratamento e a agenda. Cada dia da agenda vira uma <strong>sessão</strong> — uma
            viagem que depois entra numa rota do translado.
          </P>
          <P>
            Ele também guarda o que quem monta a rota precisa saber antes de escolher o carro: a{' '}
            <strong>condição do paciente</strong> para a viagem e os <strong>acompanhantes</strong>.
          </P>
          <Callout tipo="dica" titulo="Antes era “Tratamentos”">
            O menu mudou de nome, mas é o mesmo cadastro: <strong>Transporte Pacientes →
            Atendimentos</strong>. No cadastro do paciente, a aba agora se chama{' '}
            <strong>Transporte</strong>.
          </Callout>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar um atendimento',
      busca: 'novo atendimento cadastrar paciente tipo destino descrição passos confirmação',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Transporte Pacientes → Atendimentos</strong>, clique em{' '}
                    <BotaoRef>Novo atendimento</BotaoRef> e busque o paciente por nome ou CPF.
                  </>
                ),
              },
              {
                titulo: 'Escolha o tipo de tratamento, o destino e escreva uma descrição curta.',
                detalhe:
                  'O tipo é obrigatório: é dele que vem o tempo médio. O destino é a unidade de atendimento para onde a van vai.',
              },
              {
                titulo: 'Marque a condição do paciente e quantos acompanhantes podem ir.',
                detalhe: 'Na mesma etapa dá para cadastrar os acompanhantes do paciente.',
              },
              {
                titulo: 'Monte a agenda: data de início, dias da semana e o número de sessões — ou contínuo.',
                detalhe: 'A prévia mostra as datas que vão ser criadas.',
              },
              {
                titulo: (
                  <>
                    Confira o resumo e clique em <BotaoRef>Cadastrar atendimento</BotaoRef>.
                  </>
                ),
              },
            ]}
          />
          <Callout tipo="regra" titulo="Código SUS e horário de busca saíram">
            O atendimento não pede mais código SUS nem horário previsto de busca. O horário de cada
            viagem vem do arranjo do carro, quando a rota é montada.
          </Callout>
        </>
      ),
    },
    {
      id: 'agenda',
      titulo: 'A agenda: dias da semana, número de sessões ou contínuo',
      busca: 'agenda dias da semana segunda quarta sexta número de sessões contínuo renova todo mês fim do mês seguinte óbito prévia datas',
      conteudo: (
        <>
          <P>
            Marque os <strong>dias da semana</strong> em que o paciente vai (ex.: segunda, quarta e
            sexta) e a data a partir da qual a agenda vale. Depois escolha uma das duas durações:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Número de sessões',
                descricao:
                  'O sistema cria todas as sessões de uma vez, nos dias marcados, até completar o número (máximo 365). A primeira é o primeiro dia marcado a partir do início.',
              },
              {
                termo: 'Contínuo',
                descricao:
                  'Para quem não tem fim previsto (ex.: hemodiálise). As sessões são criadas de mês em mês: sempre até o fim do mês seguinte. Todo dia o sistema confere e cria o mês que faltar.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Contínuo não é para sempre">
            A renovação para sozinha quando o atendimento é <strong>encerrado</strong> ou quando o
            cadastro do paciente tem <strong>óbito</strong> registrado. É por isso que as sessões não
            são criadas “para sempre” de uma vez.
          </Callout>
          <P>
            As sessões nascem só com a data. O horário de busca aparece quando a viagem entra numa
            rota; até lá, o paciente vê no app que o horário é informado na véspera.
          </P>
        </>
      ),
    },
    {
      id: 'condicao',
      titulo: 'Condição do paciente para a viagem',
      busca: 'condição necessidades cadeirante cadeira de rodas dobra banco veículo adaptado maca oxigênio imunodeficiente exclusivo carro alto van ajuda embarque escada etiquetas translado',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Como o paciente viaja',
                descricao: (
                  <>
                    Anda e senta sem ajuda especial; cadeirante que <strong>passa para o banco</strong>{' '}
                    (a cadeira dobra e vai guardada); cadeirante que <strong>viaja na cadeira</strong>{' '}
                    (só em veículo adaptado); ou deitado, em maca.
                  </>
                ),
              },
              { termo: 'Dificuldade com veículo alto', descricao: 'Não consegue subir em van ou micro-ônibus.' },
              {
                termo: 'Imunodeficiente',
                descricao: 'Só pode viajar com o próprio acompanhante — precisa de veículo exclusivo.',
              },
              { termo: 'Usa oxigênio', descricao: 'Viaja com oxigênio.' },
              {
                termo: 'Precisa de ajuda',
                descricao: 'Embarque, desembarque, escada… Descreva a ajuda: é o que o motorista lê antes de chegar.',
              },
            ]}
          />
          <P>
            Na lista de sessões do translado e nos cards da geração automática, a condição aparece em
            etiquetas curtas, como <SeloRef cor="alerta">Cadeirante — veículo adaptado</SeloRef> e{' '}
            <SeloRef cor="alerta">Veículo exclusivo</SeloRef>.
          </P>
          <Callout tipo="atencao" titulo="Quem monta a rota decide">
            Por enquanto a geração automática da rota <strong>não</strong> escolhe o veículo pela
            condição do paciente, e reserva só 1 lugar de acompanhante. Confira as etiquetas antes de
            confirmar a rota.
          </Callout>
        </>
      ),
    },
    {
      id: 'acompanhantes',
      titulo: 'Acompanhantes',
      busca: 'acompanhante cadastrar cpf data de nascimento nome confere segundo acompanhante liberação justificativa limite quem vai viagem tirar da lista app',
      conteudo: (
        <>
          <P>
            Os acompanhantes são do <strong>paciente</strong>: a lista vale para todos os
            atendimentos dele. Em cada viagem se escolhe quem vai, dentro do limite do atendimento.
          </P>
          <Sub>Cadastrar</Sub>
          <P>
            Em <BotaoRef variante="outline">Cadastrar acompanhante</BotaoRef>, informe o{' '}
            <strong>CPF</strong> e a <strong>data de nascimento</strong> e clique em{' '}
            <BotaoRef>Conferir</BotaoRef>. O sistema traz o nome — do cadastro de pacientes, se a
            pessoa já está lá, ou da consulta de CPF — e você confirma. O nome nunca é digitado.
          </P>
          <P>O próprio paciente também pode cadastrar acompanhantes pelo app.</P>
          <Sub>Um ou dois por viagem</Sub>
          <P>
            Todo paciente tem direito a <strong>1 acompanhante</strong>. Para <strong>2</strong>, é
            preciso liberar: ao marcar a opção, o sistema pede uma justificativa e registra quem
            liberou e quando.
          </P>
          <Sub>Quem vai em cada viagem</Sub>
          <P>
            Na tabela de sessões, <BotaoRef variante="ghost">Editar</BotaoRef> escolhe quem vai
            naquela viagem. Depois da viagem, <BotaoRef variante="ghost">Confirmar</BotaoRef> registra
            quem foi de fato.
          </P>
          <Callout tipo="regra" titulo="Tirar da lista">
            Ninguém sai da lista enquanto estiver escolhido para uma viagem que ainda vai acontecer.
            Pelo app, o paciente só tira quem ele mesmo cadastrou; quem a equipe cadastrou, só a
            equipe tira.
          </Callout>
        </>
      ),
    },
    {
      id: 'sessoes',
      titulo: 'As sessões (viagens)',
      busca: 'sessões viagens tabela busca prevista status alocado translado editar cancelar confirmar realização adicionar sessão avulsa',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Busca prevista', descricao: 'O horário de busca, quando a rota já definiu.' },
              {
                termo: 'Status',
                descricao: (
                  <>
                    Pendente, confirmada, realizada, cancelada… Com o selo <SeloRef cor="info">Alocado</SeloRef>{' '}
                    quando a viagem já está numa rota — clique para abrir o translado.
                  </>
                ),
              },
              { termo: 'Acompanhantes', descricao: 'Quem vai (ou foi) na viagem.' },
            ]}
          />
          <P>
            Para uma data fora da agenda, use <BotaoRef variante="outline">Adicionar sessão</BotaoRef>.
            Sessão realizada não se edita — só se confirma de novo.
          </P>
        </>
      ),
    },
    {
      id: 'trocar-agenda',
      titulo: 'Trocar a agenda',
      busca: 'trocar agenda mudar dias alterar a partir de refaz sessões pendentes sem rota confirmadas alocadas',
      conteudo: (
        <>
          <P>
            No detalhe do atendimento, <BotaoRef variante="outline">Trocar agenda</BotaoRef> muda os
            dias, o número de sessões ou passa para contínuo, a partir de uma data (hoje ou depois).
          </P>
          <Lista>
            <Item>As sessões pendentes e ainda sem rota, dessa data em diante, são refeitas pela regra nova.</Item>
            <Item>Realizadas, confirmadas e já alocadas em rota ficam como estão.</Item>
            <Item>
              No modo “número de sessões”, o número é o total do atendimento: as que ficaram já contam.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'encerrar',
      titulo: 'Encerrar',
      busca: 'encerrar atendimento cancelar sessões futuras contínuo para rota montada',
      conteudo: (
        <>
          <P>
            Na lista de atendimentos, <BotaoRef variante="danger">Encerrar</BotaoRef> tira o atendimento
            das listas ativas. As sessões futuras que ainda não estão numa rota são canceladas, e o
            contínuo para de renovar.
          </P>
          <Callout tipo="atencao" titulo="Rota já montada fica">
            Viagem que já está numa rota não é cancelada por baixo — fica para quem montou a rota
            decidir, para não deixar um lugar reservado para ninguém.
          </Callout>
        </>
      ),
    },
    {
      id: 'no-app',
      titulo: 'O que o paciente vê no app',
      busca: 'app do paciente cidadão minhas viagens horário véspera meus acompanhantes adicionar cpf nascimento limite de consultas link código',
      conteudo: (
        <>
          <P>
            No app, em <strong>Transporte</strong>, o paciente vê as próximas viagens dos atendimentos
            ativos: dia, destino, situação, o horário de busca (quando a rota já definiu) e quem vai
            acompanhar.
          </P>
          <P>
            Em <strong>Meus acompanhantes</strong>, ele cadastra alguém pelo CPF e pela data de
            nascimento — o app mostra o nome encontrado para ele confirmar.
          </P>
          <Callout tipo="lgpd" titulo="Consulta de dado de outra pessoa">
            A consulta pelo app tem limite diário por paciente. E quem entrou no app pelo link do
            WhatsApp só vê a lista: para cadastrar ou tirar acompanhante, precisa entrar com o código.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo atendimentos do transporte consulta inclusão edição exclusão acompanhantes',
      conteudo: (
        <>
          <P>
            A permissão é <strong>Atendimentos do transporte</strong>, na tela de Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver atendimentos, sessões e acompanhantes.' },
              { termo: 'Inclusão', descricao: 'Cadastrar atendimento.' },
              {
                termo: 'Edição',
                descricao: 'Editar dados, trocar agenda, mexer nas sessões, cadastrar e tirar acompanhantes, liberar o segundo.',
              },
              { termo: 'Exclusão', descricao: 'Encerrar atendimento.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'tempo médio onde está código sus horário não encontramos pessoa contínuo parou segundo acompanhante',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Onde informo o tempo médio?',
              descricao: 'No tipo de tratamento (Transporte Pacientes → Tipos de tratamento). Vale para todo atendimento daquele tipo.',
            },
            {
              termo: '“Não encontramos essa pessoa com esse CPF e essa data de nascimento.”',
              descricao: 'Os dois dados não batem. Confira os números e a data — o sistema não diz qual dos dois está errado.',
            },
            {
              termo: 'O contínuo parou de criar sessões.',
              descricao: 'O atendimento foi encerrado, ou o cadastro do paciente tem óbito registrado.',
            },
            {
              termo: 'Não consigo escolher o segundo acompanhante na viagem.',
              descricao: 'O atendimento permite 1. Em “Editar dados”, libere o segundo com a justificativa.',
            },
          ]}
        />
      ),
    },
  ],
};
