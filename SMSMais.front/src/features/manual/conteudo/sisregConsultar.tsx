import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SISREG → Consultar. Conferido no código em 06/10/2026:
 * `features/sisreg/pages/SisregConsultaPage`, `ConsultaBaseSisregService` (a régua da situação,
 * a mesma de `AgendamentosPacienteService`) e `ConsultaBaseSisregPdf`.
 */
export const artigoSisregConsultar: Artigo = {
  slug: 'sisreg-consultar',
  titulo: 'Consultar SISREG',
  resumo:
    'Os agendamentos do SISREG que já estão na nossa base, com a situação de cada um (compareceu, faltou, pendente de atualização…), filtros por unidade e procedimento e PDF nominal.',
  grupo: 'regulacao',
  icone: ClipboardList,
  rota: '/app/sisreg',
  publico: 'Quem acompanha a regulação e cobra das unidades o apontamento de chegada e falta',
  atualizadoEm: '2026-10-06',
  palavrasChave: [
    'consultar sisreg',
    'pendente de atualização',
    'em aberto',
    'compareceu',
    'faltou',
    'absenteísmo',
    'relatório nominal',
    'exportar pdf',
    'unidade executante',
    'procedimento',
  ],
  secoes: () => [
    {
      id: 'de-onde-vem',
      titulo: 'De onde vêm os dados',
      busca: 'nossa base não acessa sisreg importação sincronismo diário orçamento captcha',
      conteudo: (
        <>
          <P>
            A consulta lê a <strong>nossa base</strong>: os agendamentos que a importação e o sincronismo
            diário trouxeram do SISREG. Ela <strong>não acessa o SISREG</strong>. Por isso responde rápido e
            não gasta as consultas que o SISREG permite por hora antes de pedir CAPTCHA ao operador.
          </P>
          <P>
            O sincronismo relê a chegada dos últimos 31 dias toda noite, e a lista oficial de faltas é lida de
            hora em hora. Se a unidade apontar algo hoje no SISREG, aparece aqui depois da próxima leitura.
          </P>
        </>
      ),
    },
    {
      id: 'situacoes',
      titulo: 'O que quer dizer cada situação',
      busca: 'situação na fila agendada pendente de atualização compareceu faltou cancelada em aberto não é falta',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Na fila', descricao: 'Solicitada no SISREG, ainda sem data.' },
              { termo: 'Agendada', descricao: 'Com data de hoje em diante.' },
              {
                termo: 'Pendente de atualização',
                descricao:
                  'A data já passou e a unidade executante não apontou no SISREG nem a chegada nem a falta.',
              },
              {
                termo: 'Compareceu',
                descricao: 'A unidade confirmou a chegada no SISREG, ou a recepção registrou a chegada aqui.',
              },
              { termo: 'Faltou', descricao: 'A unidade marcou falta: o atendimento está na lista oficial de faltas.' },
              { termo: 'Cancelada', descricao: 'A solicitação foi cancelada.' },
            ]}
          />
          <Callout tipo="regra" titulo="Pendente não é falta">
            Quem aponta comparecimento e falta no SISREG é a unidade executante. “Pendente de atualização”
            quer dizer que a unidade <strong>não disse nada</strong>, e não que o paciente faltou. É essa a
            lista para cobrar da unidade.
          </Callout>
        </>
      ),
    },
    {
      id: 'filtros',
      titulo: 'Como filtrar',
      busca: 'período data do agendamento data da solicitação unidades situação procedimentos exames consultas marcar todas desmarcar pesquisar',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Período</strong>: pela data do agendamento (padrão) ou pela data da solicitação. Vai até
              um ano. “Na fila” só aparece pela data da solicitação, porque ainda não tem agendamento.
            </Item>
            <Item>
              <strong>Unidades executantes</strong>, <strong>Situação</strong> e <strong>Procedimentos</strong>:
              marque uma, várias ou nenhuma. Nenhuma marcada = todas. As listas mostram só o que existe no
              período, com a quantidade ao lado.
            </Item>
            <Item>
              Na lista de procedimentos, digite parte do nome (ex.: “fisioterap”) e use{' '}
              <BotaoRef>Marcar as filtradas</BotaoRef> para marcar de uma vez todos os que aparecem.
            </Item>
            <Item>
              <strong>Exames</strong> e <strong>Consultas</strong>: marque um ou os dois.
            </Item>
          </Lista>
          <P>
            O filtro só vale quando você clica em <BotaoRef>Pesquisar</BotaoRef>. A quantidade por página
            (100, 200 ou 500) muda na hora.
          </P>
        </>
      ),
    },
    {
      id: 'resultado',
      titulo: 'Lendo o resultado',
      busca: 'contagem pessoas atendimentos por situação clicar abrir solicitação modal detalhe',
      conteudo: (
        <>
          <P>
            Em cima da tabela aparecem quantas <strong>pessoas</strong> e quantos <strong>atendimentos</strong>{' '}
            o filtro trouxe, e quantos há em cada situação. Pessoas são contadas uma vez só: quem tem três
            sessões pendentes é uma pessoa a procurar.
          </P>
          <P>Clique numa linha para abrir a solicitação completa.</P>
        </>
      ),
    },
    {
      id: 'pdf',
      titulo: 'Exportar PDF',
      busca: 'exportar pdf relatório nominal imprimir nome data procedimento situação 5000 lgpd',
      conteudo: (
        <>
          <P>
            <BotaoRef>Exportar PDF</BotaoRef> gera um relatório nominal com <strong>todo o resultado</strong>,
            não só a página aberta: nome do paciente, data do agendamento, procedimento e situação, em ordem
            alfabética. O filtro usado sai escrito no topo. O limite é de 5.000 atendimentos; acima disso,
            diminua o período ou marque menos unidades.
          </P>
          <Callout tipo="lgpd">
            O PDF tem nome de paciente. Envie só por canal interno e só para quem precisa cobrar ou resolver os
            atendimentos da lista.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode',
      busca: 'permissão módulo sisreg consulta escopo unidade',
      conteudo: (
        <P>
          Quem tem o módulo SISREG com consulta. Cada pessoa vê só as unidades do seu escopo: quem é ligado a
          uma unidade vê os atendimentos que ela executa.
        </P>
      ),
    },
  ],
};
