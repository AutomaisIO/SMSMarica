import { BellRing } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → Notificações (SER). A tela não tinha artigo; criado junto com o filtro por
 * recurso (procedimentos).
 *
 * Conferido no código em 07/10/2026: `features/ser/pages/SerNotificacoesPage.tsx`,
 * `shared/regulacao/FiltroTecnicos`, `shared/regulacao/GerenciadorRecursos`, e no backend
 * `SerNotificacaoService` (resumo/lista/tecnicos/recursos) + `SerNotificacaoController`
 * (`regulacao/ser/notificacoes/*`). A seleção dos dois filtros fica salva no perfil do usuário
 * (`PreferenciasUiDto`: `NotificacoesSerTecnicos`, `NotificacoesSerRecursos`).
 */
export const artigoNotificacoesRegulacao: Artigo = {
  slug: 'notificacoes-regulacao',
  titulo: 'Notificações da regulação (SER)',
  resumo:
    'O que mudou no SER e ainda ninguém olhou: cada linha é um movimento (entrou na fila, agendou, cancelou, chegou FollowUP). Marcar como visto tira daqui. Dá para recortar a tela por técnico regulador e por procedimento.',
  grupo: 'regulacao',
  icone: BellRing,
  rota: '/app/regulacao/notificacoes',
  publico: 'Quem acompanha a fila do SER (regulação)',
  atualizadoEm: '2026-10-07',
  palavrasChave: [
    'notificações',
    'notificacoes da regulação',
    'SER',
    'movimentações',
    'o que mudou',
    'marcar como visto',
    'visto',
    'consulta',
    'exame',
    'situação',
    'em fila',
    'pendente',
    'agendada',
    'cancelada',
    'alta',
    'followup',
    'falha de contato',
    'último evento',
    'filtro',
    'técnico regulador',
    'quem incluiu',
    'procedimento',
    'procedimentos',
    'recurso',
    'recursos',
    'filtrar por procedimento',
    'gerenciar recursos',
    'só os selecionados',
    'cardiologia',
    'especialidade',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca:
        'notificações regulação SER o que mudou movimentações não vistas marcar como visto fila de gatilhos espelho',
      conteudo: (
        <>
          <P>
            Esta tela mostra <strong>o que mudou no SER e ainda ninguém olhou</strong>. Cada linha é um{' '}
            <strong>movimento</strong>, não uma solicitação: a solicitação entrou na fila, foi para
            agendada, foi cancelada, ou chegou um FollowUP novo. A mesma solicitação pode aparecer em
            várias linhas, uma por movimento.
          </P>
          <P>
            Quem lê a tela trabalha sobre o <strong>nosso espelho</strong> do SER, atualizado pela
            varredura automática — a tela não vai ao SER a cada abertura.
          </P>
          <Callout tipo="regra" titulo="Marcar como visto é o que tira o item daqui">
            Enquanto ninguém marca, o movimento continua na lista. Usar o botão{' '}
            <BotaoRef>Visto</BotaoRef> da linha some com ela e, por baixo, consome a fila de
            movimentos do motor.
          </Callout>
        </>
      ),
    },
    {
      id: 'abas-e-situacoes',
      titulo: 'Abas e situações',
      busca: 'abas consulta exame contador situação em fila pendente agendada chegada cancelada alta número',
      conteudo: (
        <>
          <P>
            No topo, as abas <AbaRef>Consulta</AbaRef> e <AbaRef>Exame</AbaRef> separam os dois tipos
            de recurso, cada uma com seu contador. Abaixo delas, os atalhos por{' '}
            <strong>situação</strong> — só aparecem as situações que têm movimento, para a linha não
            virar uma fileira de zeros:
          </P>
          <P>
            <SeloRef cor="alerta">Em fila</SeloRef> <SeloRef cor="alerta">Pendente</SeloRef>{' '}
            <SeloRef cor="info">Agendada</SeloRef> <SeloRef cor="alerta">Chegada não confirmada</SeloRef>{' '}
            <SeloRef cor="sucesso">Chegada confirmada</SeloRef> <SeloRef cor="erro">Cancelada</SeloRef>{' '}
            <SeloRef cor="gray">Alta</SeloRef>
          </P>
          <P>
            Os números de cada aba e de cada situação <strong>batem com a lista</strong>: quando você
            liga um filtro (técnico ou procedimento), os contadores recontam junto.
          </P>
        </>
      ),
    },
    {
      id: 'filtros',
      titulo: 'Recortar a tela: técnico regulador e procedimentos',
      busca:
        'filtro técnico regulador quem incluiu procedimento procedimentos recurso recursos gerenciar modal busca só os selecionados salvar perfil cada usuário configura a própria tela cardiologia',
      conteudo: (
        <>
          <P>
            À direita da linha das abas há <strong>dois filtros</strong>. Eles são o primeiro corte de
            quem tria a fila — recortam tudo o que aparece abaixo, inclusive os contadores. Cada um fica{' '}
            <strong>salvo no seu perfil</strong>: você monta a tela do seu jeito uma vez e ela abre
            assim no próximo login, em qualquer máquina. Nada marcado = tudo aparece.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <>Técnico regulador</>,
                descricao: (
                  <>
                    Quem <strong>incluiu</strong> a solicitação no SER (o nome do primeiro “Solicitar” da
                    trilha). Abre uma lista com busca; marque um ou vários. O número ao lado de cada nome
                    é quanto há pendente dele.
                  </>
                ),
              },
              {
                termo: <>Procedimentos</>,
                descricao: (
                  <>
                    O recurso da solicitação (ex.: <em>Consulta em oftalmologia</em>,{' '}
                    <em>Angiorressonância cerebral</em>). O botão mostra <strong>quantos</strong> você
                    marcou (ou <strong>Todos</strong>); clicar abre a janela para gerenciar a seleção.
                  </>
                ),
              },
            ]}
          />
          <P>
            Como são centenas de procedimentos, a seleção é feita numa <strong>janela própria</strong>,
            e não num menu curto:
          </P>
          <Lista>
            <Item>
              <strong>Busca</strong> no topo: digite parte do nome (ex.: <em>cardio</em>) para achar
              tudo daquela área.
            </Item>
            <Item>
              Marque nos <strong>checkboxes</strong> o que você quer ver. A lista traz o catálogo
              inteiro — inclusive procedimentos sem pendência agora, para você já deixar marcado o que
              aparece de vez em quando.
            </Item>
            <Item>
              O atalho <strong>Só os selecionados</strong> esconde o resto, para conferir sua seleção
              sem se perder no meio dos demais.
            </Item>
            <Item>
              A seleção só vale ao clicar em <BotaoRef>Salvar</BotaoRef>. <BotaoRef variante="outline">Cancelar</BotaoRef>{' '}
              descarta o que você mexeu na janela.
            </Item>
          </Lista>
          <Callout tipo="dica" titulo="É configuração sua, não trava de acesso">
            Os filtros só organizam a sua tela — não escondem nada de ninguém nem mudam a fila do SER.
            Quem limpa a seleção volta a ver tudo.
          </Callout>
        </>
      ),
    },
    {
      id: 'ler-e-marcar',
      titulo: 'Ler a linha e marcar como visto',
      busca:
        'followup último evento falha de contato card paciente marcar como visto visto página por página paginação',
      conteudo: (
        <>
          <P>Cada linha reúne, além do movimento, o contexto que ajuda a decidir o que fazer:</P>
          <Lista>
            <Item>
              O <strong>último FollowUP</strong> da solicitação (quando houver) e sua categoria — o
              atalho <SeloRef cor="alerta">Falha de contato</SeloRef> junta a fila de quem a central não
              conseguiu localizar.
            </Item>
            <Item>
              O <strong>último evento</strong> da trilha, de qualquer tipo, para ver o estado mais
              recente sem abrir o histórico.
            </Item>
          </Lista>
          <P>
            O botão <BotaoRef>Visto</BotaoRef> da linha marca aquele movimento como lido e o tira da
            tela — e, com ele, os contadores caem junto. Embaixo, <strong>Mostrar</strong> escolhe
            quantas linhas por página (até 1000), para dar conta de filas grandes.
          </P>
        </>
      ),
    },
  ],
};
