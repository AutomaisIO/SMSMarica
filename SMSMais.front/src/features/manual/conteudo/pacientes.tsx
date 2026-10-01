import { Users } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Pacientes: a lista (busca, sem CPF, excluir) e a ficha, com foco na aba
 * Agendamentos — de onde vem cada linha e o que cada selo de comparecimento quer dizer.
 *
 * Conferido no código: `features/pacientes` (PacientesPage, PacienteDetalhePage — abas e
 * SecaoAgendamentos, types.ts) e no backend `AgendamentosPacienteService` (fontes, próximos ×
 * histórico, regra dos selos), `VarreduraAgendaService.ConferirChegadasAsync` (chegadas dos últimos
 * 31 dias toda noite) e o coletor `FaltasRecentes` (lista de faltas de hora em hora).
 */
export const artigoPacientes: Artigo = {
  slug: 'pacientes',
  titulo: 'Pacientes (cadastro e ficha)',
  resumo:
    'Buscar e cadastrar pacientes e ler a ficha — em especial a aba Agendamentos, que junta SISREG, SER, SERNIT e ESUS de São Gonçalo e diz se o paciente compareceu, faltou ou se a unidade ainda não apontou.',
  grupo: 'cadastros',
  icone: Users,
  rota: '/app/pacientes',
  publico: 'Quem atende, regula ou acompanha o paciente e precisa da ficha dele',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'paciente',
    'pacientes',
    'cadastro',
    'ficha',
    'buscar',
    'CPF',
    'sem CPF',
    'nome',
    'novo paciente',
    'excluir',
    'reativar',
    'agendamentos',
    'histórico de agendamentos',
    'próximos agendamentos',
    'comparecimento',
    'compareceu',
    'faltou',
    'falta',
    'em aberto',
    'chegada',
    'chegada não confirmada',
    'sem registro de chegada',
    'saiu da fila',
    'pendente',
    'em fila',
    '0000',
    'SISREG',
    'SER',
    'SERNIT',
    'ESUS SG',
    'São Gonçalo',
    'absenteísmo',
    'unidade executante',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'cadastro paciente ficha prontuário onde fica para que serve',
      conteudo: (
        <>
          <P>
            É o cadastro único do cidadão na plataforma. Tudo o que acontece com ele — solicitações de
            regulação, exames, transporte, conversas pelo WhatsApp — se pendura nesta ficha. A lista
            serve para achar a pessoa; a ficha, para entender a situação dela sem abrir outro sistema.
          </P>
        </>
      ),
    },
    {
      id: 'buscar',
      titulo: 'Achar um paciente',
      busca: 'buscar procurar nome CPF dez últimos cadastros sem CPF identidade incompleta ver editar',
      conteudo: (
        <>
          <P>
            Sem nada digitado, a lista mostra os <strong>10 últimos cadastros</strong>. Para procurar,
            digite qualquer parte do <strong>nome</strong> (pedaços separados por espaço) ou o{' '}
            <strong>CPF</strong>, com ou sem pontos — a busca devolve até 10 resultados. Clique no nome
            (ou em <BotaoRef variante="ghost">Ver</BotaoRef>) para abrir a ficha.
          </P>
          <Callout tipo="atencao" titulo="O selo “sem CPF”">
            Aparece no cadastro que veio de um prontuário de origem sem CPF. Sem CPF não há como unir
            esse cadastro ao do mesmo cidadão em outras bases, então a mesma pessoa pode aparecer mais
            de uma vez. Confirme a identidade antes de usar o cadastro para algo definitivo.
          </Callout>
          <P>
            <BotaoRef variante="ghost">Excluir</BotaoRef> tira o paciente das buscas, mas o histórico
            é preservado e o cadastro pode ser reativado entrando de novo com o CPF.
          </P>
        </>
      ),
    },
    {
      id: 'ficha',
      titulo: 'As abas da ficha',
      busca: 'abas resumo atendimentos agendamentos transporte exames anexados conversas histórico de acesso histórico de alterações dados pessoais',
      conteudo: (
        <>
          <P>A ficha abre no Resumo. As outras abas, na ordem da tela:</P>
          <Lista>
            <Item>
              <AbaRef>Atendimentos</AbaRef>, <AbaRef>Agendamentos</AbaRef>,{' '}
              <AbaRef>Transporte</AbaRef> e <AbaRef>Exames anexados</AbaRef> — o que o paciente teve e
              tem marcado;
            </Item>
            <Item>
              <AbaRef>Conversas</AbaRef> — o WhatsApp com ele;
            </Item>
            <Item>
              <AbaRef>Histórico de Acesso</AbaRef> — as entradas do próprio paciente no aplicativo;
            </Item>
            <Item>
              <AbaRef>Histórico de alterações</AbaRef> — quem mudou o cadastro, e quando;
            </Item>
            <Item>
              <AbaRef>Dados pessoais</AbaRef> — identificação, filiação, endereço, contatos, saúde,
              documentos e origem.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'agendamentos',
      titulo: 'A aba Agendamentos: de onde vem cada linha',
      busca: 'agendamentos próximos histórico SISREG SER SERNIT ESUS SG São Gonçalo regulação estadual municipal origem fonte detalhe abrir linha',
      conteudo: (
        <>
          <P>
            A aba junta, num lugar só, o que o paciente tem nos sistemas de regulação: o{' '}
            <strong>SISREG</strong> (regulação municipal), o <strong>SER</strong> (regulação
            estadual), o <strong>SERNIT</strong> (regulação de Niterói) e o{' '}
            <strong>ESUS de São Gonçalo</strong>. A coluna Origem diz de qual sistema veio cada linha.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Próximos agendamentos',
                descricao:
                  'O que ainda vai acontecer e o que está esperando regulação (em fila ou pendente), do mais próximo para o mais distante. Pedido na fila fica aqui mesmo que tenha data antiga — é trabalho em aberto.',
              },
              {
                termo: 'Histórico de agendamentos',
                descricao:
                  'O que já passou ou já se encerrou (cancelado, concluído, saiu da fila), do mais recente para o mais antigo. É aqui que aparece se o paciente veio.',
              },
            ]}
          />
          <P>
            Clicar numa linha do SER, do SERNIT, do ESUS SG ou de exame de imagem abre o detalhe da
            solicitação — desde que o seu perfil tenha permissão de consulta naquele módulo. Consulta
            no SISREG que não é exame de imagem não tem detalhe.
          </P>
        </>
      ),
    },
    {
      id: 'selos',
      titulo: 'Os selos: compareceu, faltou, em aberto',
      busca: 'selo situação compareceu faltou falta em aberto chegada confirmada chegada não confirmada sem registro de chegada saiu da fila concluído cancelado comparecimento absenteísmo',
      conteudo: (
        <>
          <P>
            No histórico, o selo responde “o paciente veio?” — e só afirma com registro de quem
            atendeu. Passe o mouse no selo para ver de onde veio a informação e quando foi lida.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="sucesso">Compareceu</SeloRef>,
                descricao:
                  'A unidade executante confirmou a chegada no sistema de regulação, ou a recepção registrou a chegada aqui.',
              },
              {
                termo: <SeloRef cor="erro">Faltou</SeloRef>,
                descricao: 'A unidade executante registrou a falta.',
              },
              {
                termo: <SeloRef cor="alerta">Em aberto</SeloRef>,
                descricao: (
                  <>
                    SISREG e ESUS de São Gonçalo: a data passou e a unidade ainda{' '}
                    <strong>não apontou</strong> nem a chegada nem a falta. <strong>Não é falta</strong> — é uma pendência da unidade, não do
                    paciente.
                  </>
                ),
              },
              {
                termo: <SeloRef cor="alerta">Chegada não confirmada</SeloRef>,
                descricao:
                  'SER e SERNIT: é a situação que o próprio sistema estadual mostra para o agendamento cuja chegada a unidade não confirmou.',
              },
              {
                termo: <SeloRef>Sem registro de chegada</SeloRef>,
                descricao:
                  'A data passou e não há informação: ninguém conferiu o sistema de origem depois do dia. Some na próxima leitura.',
              },
              {
                termo: <SeloRef>Saiu da fila</SeloRef>,
                descricao:
                  'ESUS de São Gonçalo: o pedido não consta mais na fila nem nos agendados. O motivo não é informado — e não é o mesmo que cancelado.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="No SISREG e no ESUS, a unidade escolhe entre três respostas">
            Para cada agendamento que já passou, a unidade executante aponta uma de três situações: no
            SISREG, <strong>Confirmado</strong>, <strong>Falta</strong> ou{' '}
            <strong>Pendente de confirmação</strong>; no ESUS de São Gonçalo, <strong>Efetivado</strong>,{' '}
            <strong>Não efetivado</strong> (com o motivo, em geral “Não Compareceu”) ou nada. A ficha mostra as três
            separadas — Compareceu, Faltou e Em aberto. Agendamento em aberto nunca vira falta sozinho:
            há unidades que deixam de apontar, e tratar isso como ausência do paciente seria injusto
            com ele.
          </Callout>
        </>
      ),
    },
    {
      id: 'atualizacao',
      titulo: 'Quando o selo muda',
      busca: 'atualização quando muda de hora em hora toda noite varredura 31 dias lista de faltas absenteísmo confirmação atrasada SER SERNIT base inteira',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Faltas do SISREG</strong>: a lista de faltas das últimas semanas é relida de hora
              em hora, das 01:20 às 18:00. A falta que a unidade registrou aparece na ficha na rodada
              seguinte.
            </Item>
            <Item>
              <strong>Chegadas do SISREG</strong>: a sincronização noturna de cada unidade relê os
              últimos 31 dias. A chegada confirmada hoje aparece amanhã.
            </Item>
            <Item>
              <strong>SER e SERNIT</strong>: a sincronização da madrugada relê a base inteira — o selo
              acompanha o que estiver lá na manhã seguinte.
            </Item>
            <Item>
              <strong>ESUS de São Gonçalo</strong>: a sincronização da madrugada lê, no histórico de cada
              paciente, a efetivação dos exames agendados nos últimos 31 dias. Exame efetivado não é
              relido.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="Por que um atendimento recente pode estar “Em aberto”">
            As unidades costumam apontar chegadas e faltas com dias — às vezes semanas — de atraso. Um
            “Em aberto” de ontem é normal; um de dois meses atrás quer dizer que a unidade não apontou.
          </Callout>
        </>
      ),
    },
    {
      id: 'solicitacao-0000',
      titulo: 'Linhas “Pendente” com número 0000',
      busca: '0000 sem número pendente cadastrada à mão manual recepção duplicada cancelar solicitação de exame',
      conteudo: (
        <>
          <P>
            <strong>0000</strong> no número da solicitação quer dizer que ela foi cadastrada à mão no
            painel, sem o número do SISREG. Sem número e sem data, ela fica como{' '}
            <SeloRef cor="alerta">Pendente</SeloRef> em Próximos agendamentos — e{' '}
            <strong>continua lá até alguém cancelar</strong>, porque não há sistema de origem para dizer
            que acabou.
          </P>
          <Callout tipo="atencao" titulo="Antes de cadastrar à mão, procure a do SISREG">
            Se o paciente já tem a solicitação do SISREG para o mesmo exame, use aquela. Cadastrar outra
            sem número cria uma linha duplicada que ninguém fecha.
          </Callout>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'não aparece agendamento paciente diz que foi aparece faltou em aberto antigo sem registro de chegada não abre detalhe permissão',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O paciente diz que foi, mas aparece “Faltou”.',
              descricao:
                'A falta foi registrada pela unidade executante no sistema de regulação. Só ela pode corrigir lá; quando corrigir, a ficha acompanha na próxima leitura.',
            },
            {
              termo: 'Um atendimento de meses atrás está “Em aberto”.',
              descricao:
                'A unidade executante não apontou o resultado no SISREG. Não conte como falta; se precisar saber, pergunte à unidade.',
            },
            {
              termo: 'Um exame antigo do ESUS de São Gonçalo está “Sem registro de chegada”.',
              descricao:
                'A efetivação é lida só para os agendamentos dos últimos 31 dias (a partir de 01/10/2026). Os mais antigos ficam sem registro.',
            },
            {
              termo: 'Cliquei na linha e o detalhe não abriu.',
              descricao:
                'Ou a linha não tem detalhe (consulta do SISREG), ou o seu perfil não tem permissão de consulta no módulo daquele sistema (SER, SERNIT, ESUS SG ou Solicitações de Exame).',
            },
          ]}
        />
      ),
    },
  ],
};
