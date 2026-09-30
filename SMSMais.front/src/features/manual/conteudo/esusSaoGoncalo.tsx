import { ClipboardList } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do espelho do ESUS de São Gonçalo (ADR-0063): fila, detalhe, notificações, estatísticas e
 * configuração do motor.
 *
 * Conferido no código: `features/esussg` (EsusSgFilaPage, EsusSgSolicitacaoDetalhePage,
 * EsusSgNotificacoesPage, RegulacaoEsusSgConfiguracaoPage, AbaEsusSgConfiguracao, TrilhaEsusSg,
 * ModalSolicitacaoEsusSg), `features/regulacao-estatisticas` (fonte `esussg`) e no backend
 * (`EsusSgController`, `EsusSgMotorService`, `EsusSgSincronizacaoService`, `EsusSgJanelas`,
 * enums `SituacaoEsusSg`/`ModoVarreduraEsusSg`/`StatusVarreduraEsusSg`).
 */
export const artigoEsusSaoGoncalo: Artigo = {
  slug: 'esus-sao-goncalo',
  titulo: 'ESUS São Gonçalo (fila e agendados da PPI)',
  resumo:
    'O espelho, só de leitura, dos pedidos do município no ESUS de São Gonçalo: quem espera na fila, quem foi agendado, quem saiu da fila — com notificações, estatísticas e o motor de varredura.',
  grupo: 'regulacao',
  icone: ClipboardList,
  rota: '/app/regulacao/esussg',
  publico: 'Quem acompanha os pedidos de exame da PPI encaminhados ao ESUS de São Gonçalo',
  atualizadoEm: '2026-09-30',
  palavrasChave: [
    'ESUS',
    'ESUS SG',
    'ESUS São Gonçalo',
    'São Gonçalo',
    'esusmais',
    'Novo Esus',
    'PPI',
    'pactuação',
    'exame',
    'retina',
    'glaucoma',
    'auditiva',
    'fila',
    'fila de regulação',
    'agendados',
    'agendado',
    'agendamento',
    'saiu da fila',
    'sumiu da fila',
    'excluído',
    'cancelado',
    'pendente',
    'pendência',
    'em fila',
    'posição na fila',
    'prioridade',
    'urgente',
    'mandado judicial',
    'espelho',
    'só leitura',
    'somente leitura',
    'varredura',
    'motor',
    'carga inicial',
    'diária',
    'só a fila',
    'parcial',
    'cobertura incompleta',
    'fatias incompletas',
    'retomada',
    'interrompida',
    'credencial',
    'cliente SGO',
    'catálogo',
    'notificações',
    'visto',
    'técnico',
    'quem incluiu',
    'quem agendou',
    'estatísticas',
    'trilha',
    'histórico',
    'código interno',
    'SIGTAP',
    'comprovante',
    'TFD',
    'análise das regras',
    'e-SUS',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca:
        'esus são gonçalo esusmais ppi espelho só leitura não é e-sus governo unidade solicitante exame retina glaucoma auditiva',
      conteudo: (
        <>
          <P>
            Parte dos exames do município é feita em São Gonçalo, pela <strong>PPI</strong>
            (retina, glaucoma, auditiva e outros). Esses pedidos são incluídos no sistema de regulação
            de lá — o <strong>ESUS de São Gonçalo</strong> —, onde o município é uma{' '}
            <strong>unidade solicitante</strong>. Esta tela é o <strong>espelho</strong> desses
            pedidos na nossa base: quem ainda espera na fila, quem já foi agendado e quem saiu da fila.
          </P>
          <Callout tipo="atencao" titulo="ESUS não é o e-SUS do governo">
            O ESUS de São Gonçalo é um produto próprio de lá (saogoncalo.esusmais.com.br). Não tem
            relação com o e-SUS do Ministério da Saúde — os dois só têm o nome parecido.
          </Callout>
          <Callout tipo="regra" titulo="Só leitura">
            Nada nesta tela escreve no ESUS: não há FollowUP, não há edição de telefone, não há
            inclusão de pedido. A inclusão continua sendo feita na tela do próprio ESUS. O que o
            painel faz é ler, organizar e avisar do que mudou.
          </Callout>
        </>
      ),
    },
    {
      id: 'situacoes',
      titulo: 'As situações — e o que quer dizer “Saiu da fila”',
      busca:
        'situação em fila pendente agendada saiu da fila motivo não visível exclusão cancelamento transferência cinza tracejado não é cancelado',
      conteudo: (
        <>
          <P>
            O ESUS mostra à conta do município duas listas: a <strong>Fila de Regulação</strong> (o
            que ainda espera) e os <strong>Pacientes Agendados pela Fila</strong> (o que saiu da fila
            com data marcada). A situação de cada pedido é tirada dessas duas listas:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: <SeloRef cor="alerta">Em fila</SeloRef>, descricao: 'Está na Fila de Regulação, sem pendência.' },
              {
                termo: <SeloRef cor="alerta">Pendente</SeloRef>,
                descricao: 'Está na fila com uma pendência ativa no ESUS (o texto da pendência aparece no detalhe).',
              },
              {
                termo: <SeloRef cor="info">Agendada</SeloRef>,
                descricao: 'Saiu da fila com data marcada — aparece nos agendados, com unidade, setor e horário.',
              },
              {
                termo: <SeloRef>Saiu da fila</SeloRef>,
                descricao:
                  'Não consta mais na fila nem nos agendados do ESUS. A conta do município não vê o motivo: pode ter sido excluído, cancelado ou transferido.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="“Saiu da fila” não é “Cancelado”">
            A tela de exclusões do ESUS é negada à nossa conta. Por isso o sistema não afirma que o
            pedido foi cancelado — ele declara o que viu: o pedido sumiu das duas listas. Na ficha do
            paciente a situação aparece com o mesmo nome, “Saiu da fila”, em cinza tracejado.
          </Callout>
        </>
      ),
    },
    {
      id: 'fila',
      titulo: 'A fila',
      busca:
        'fila cartões situação agendados próximos 30 dias filtro tipo procedimento recurso buscar nome cpf cns número pedido prioridade cor entrada dias posição agendado para análise das regras chip veredito',
      conteudo: (
        <>
          <P>
            Em <strong>Regulação → ESUS SG → Fila</strong>, os cartões do alto dão o tamanho de cada
            situação antes de qualquer filtro; clicar num cartão filtra por ela. O cartão{' '}
            <strong>Agendados nos próximos 30 dias</strong> mostra só os agendados de hoje até daqui a
            30 dias.
          </P>
          <P>
            Logo abaixo, os chips da <strong>análise das regras</strong> contam os pedidos em aberto
            por veredito (Bloqueado, A conferir, Com ressalva…) e também servem de filtro — ver o
            artigo “Análise automática das regras”.
          </P>
          <Sub>Filtros</Sub>
          <Lista>
            <Item>Situação, tipo (consulta ou exame) e procedimento — a lista de procedimentos é a que o espelho já tem, com a quantidade de pedidos de cada um;</Item>
            <Item>Análise das regras (o veredito);</Item>
            <Item>Buscar: nome, CPF, CNS ou o número do pedido no ESUS.</Item>
          </Lista>
          <Sub>Colunas</Sub>
          <ListaDefinicoes
            itens={[
              { termo: 'Nº ESUS', descricao: 'O número do pedido no ESUS — o mesmo na fila e nos agendados. É por ele que se fala com São Gonçalo.' },
              { termo: 'Paciente', descricao: 'Quando o paciente já foi reconhecido na nossa base, o nome ganha o resumo (com o atalho para a ficha) e o WhatsApp.' },
              { termo: 'Prioridade', descricao: 'Como o ESUS escreve (“A REGULAR”, “URGENTE”, “MANDADO JUDICIAL”…), com a bolinha na cor que o próprio ESUS usa.' },
              { termo: 'Entrada na fila', descricao: 'A data em que o pedido entrou na fila do ESUS e há quantos dias espera.' },
              { termo: 'Posição', descricao: 'A posição regulada na fila do procedimento, no ESUS. Só existe enquanto o pedido está na fila.' },
              { termo: 'Agendado para', descricao: 'Data e hora do atendimento e a unidade que vai atender.' },
              { termo: 'Análise das regras', descricao: 'O veredito do parecer automático. Passe o mouse para ler o motivo.' },
            ]}
          />
          <P>Clique numa linha para abrir o pedido.</P>
        </>
      ),
    },
    {
      id: 'detalhe',
      titulo: 'O pedido (detalhe)',
      busca:
        'detalhe pedido paciente ficha cpf cns mãe telefone bairro procedimento subprocedimentos código interno não é sigtap prioridade pendência posição solicitante unidade solicitante quem incluiu regulador unidade executora cnes setor local quem agendou comprovante impresso tfd notificação resposta do paciente',
      conteudo: (
        <>
          <P>O detalhe junta tudo o que o espelho sabe do pedido, em blocos:</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Paciente',
                descricao:
                  'Nome, CPF, CNS, nascimento, sexo, mãe, bairro e município, e os telefones como estão no ESUS. Com o paciente reconhecido, aparece o atalho “Abrir a ficha do paciente”.',
              },
              {
                termo: 'Pedido',
                descricao:
                  'Procedimento, subprocedimentos, código interno, prioridade, pendência, posição, entrada na fila, data do pedido, profissional e unidade solicitante, quem incluiu na fila e o regulador.',
              },
              {
                termo: 'Agendamento',
                descricao:
                  'Unidade executora (com o CNES, quando o ESUS o escreve no nome), setor, local, data e hora, quem agendou, quando o agendamento foi cadastrado, saída da fila, comprovante impresso, TFD e a notificação que o próprio ESUS mandou ao paciente (canal, entrega e resposta).',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Código interno não é SIGTAP">
            O “código interno” é um código do próprio ESUS — o mesmo número aparece em procedimentos
            diferentes. Ele fica guardado só para rastrear; não use como código SIGTAP.
          </Callout>
          <P>
            O detalhe também traz o painel da <strong>análise das regras</strong>, com cada regra
            avaliada e o botão <BotaoRef variante="outline">Reanalisar</BotaoRef>.
          </P>
        </>
      ),
    },
    {
      id: 'trilha',
      titulo: 'A trilha do pedido',
      busca:
        'trilha histórico marcos inclusão na fila agendamento reagendamento mudança de prioridade pendência saiu da fila retorno à fila percebido pela varredura data sem hora',
      conteudo: (
        <>
          <P>
            Diferente do SER e do SERNIT, o ESUS <strong>não mostra o histórico</strong> de cada
            pedido à nossa conta. A trilha que aparece no detalhe é <strong>montada</strong> pela
            varredura, de duas fontes:
          </P>
          <Lista>
            <Item>
              os <strong>marcos que as listas trazem</strong>: “Inclusão na fila” (com quem incluiu) e
              “Agendamento” (com quem agendou) — esses vêm só com a data, sem hora;
            </Item>
            <Item>
              as <strong>diferenças entre uma varredura e a seguinte</strong>: reagendamento, mudança
              de prioridade, pendência, retorno à fila, saída da fila.
            </Item>
          </Lista>
          <P>
            Os eventos da segunda fonte levam o selo <SeloRef>percebido pela varredura</SeloRef>: a
            data deles é a da varredura que <strong>notou</strong> a mudança, não a hora exata em que
            ela aconteceu no ESUS.
          </P>
        </>
      ),
    },
    {
      id: 'notificacoes',
      titulo: 'Notificações',
      busca:
        'notificações movimento novo pedido mudou de situação remarcado mudou a prioridade saiu da fila visto vista técnico quem incluiu filtro salvo consulta exame',
      conteudo: (
        <>
          <P>
            Em <strong>Regulação → ESUS SG → Notificações</strong> fica cada movimento que a varredura
            percebeu e ninguém viu ainda: <strong>novo pedido</strong>, <strong>mudou de situação</strong>{' '}
            (entrou nos agendados, saiu da fila…), <strong>remarcado</strong> e{' '}
            <strong>mudou a prioridade</strong>. Clicar na linha abre o pedido num modal, sem perder o
            filtro.
          </P>
          <P>
            O filtro <strong>Técnico (quem incluiu)</strong> recorta pelo servidor que incluiu o pedido
            na fila do ESUS; a escolha fica salva no seu usuário. O ESUS não tem FollowUP — por isso
            esta tela não tem os filtros de FollowUP das irmãs do SER e do SERNIT.
          </P>
          <Callout tipo="dica" titulo="“Vista” é marca nossa">
            <BotaoRef variante="outline">Vista</BotaoRef> só tira o movimento desta lista. Não escreve
            nada no ESUS — por isso basta a permissão de consulta do ESUS SG.
          </Callout>
        </>
      ),
    },
    {
      id: 'estatisticas',
      titulo: 'Estatísticas',
      busca: 'estatísticas operadores quem incluiu quem agendou equipe individual ranking módulo próprio habilitar nomes',
      conteudo: (
        <>
          <P>
            Em <strong>Regulação → ESUS SG → Estatísticas</strong>, a mesma tela de estatísticas do SER
            e do SERNIT, lendo a trilha do ESUS: conta <strong>quem incluiu</strong> pedidos na fila
            (servidores do município) e <strong>quem agendou</strong> (operadores de São Gonçalo). Na
            aba Configuração dela, marque os nomes da nossa equipe — só eles entram na Equipe e no
            Individual.
          </P>
          <P>
            Como os marcos do ESUS vêm só com a data, a distribuição por hora do dia não diz muito
            nesta fonte. Tem permissão própria (Estatísticas — ESUS São Gonçalo), desligada por padrão.
          </P>
        </>
      ),
    },
    {
      id: 'configuracao',
      titulo: 'Configuração: credencial e motor',
      busca:
        'configuração credencial usuário senha cliente sgo testar salvar rodar agora modo sincronizar catálogo rodada diária automática horário últimas rodadas',
      conteudo: (
        <>
          <P>
            Em <strong>Regulação → ESUS SG → Configuração</strong> (permissão Regulação —
            Configuração) ficam o estado do motor e o que o faz andar.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Credencial: preencha Usuário, Senha e Cliente (o padrão é SGO).',
                detalhe:
                  '“Testar” só faz o login e mostra o nome com que o ESUS reconheceu a conta, sem gravar. “Salvar” testa primeiro e só então guarda — cifrada. A senha não volta para a tela.',
              },
              {
                titulo: (
                  <>
                    Escolha o modo e clique em <BotaoRef>Rodar agora</BotaoRef>.
                  </>
                ),
                detalhe:
                  'A rodada vai para a fila e roda em segundo plano; acompanhe em “Últimas rodadas”. Se já houver uma rodando, o sistema avisa e não começa outra.',
              },
              {
                titulo: (
                  <>
                    <BotaoRef variante="outline">Sincronizar catálogo</BotaoRef> quando o ESUS ganhar
                    procedimento novo.
                  </>
                ),
                detalhe:
                  'Relê a lista de procedimentos do ESUS numa requisição só e mostra quantos foram lidos, novos, alterados e inativados. O que mudou vai ao catálogo canônico da regulação.',
              },
              {
                titulo: 'Ligue a rodada diária automática e escolha o horário (Brasília).',
                detalhe: 'A automática é sempre no modo Diária. A carga inicial é sempre manual.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'varredura',
      titulo: 'Como a varredura funciona (carga inicial × diária)',
      busca:
        'varredura carga inicial todo histórico fatias de um ano 2015 alguns minutos diária janela 45 dias atrás 400 dias frente só a fila detecta saída retomável cursor retomada interrompida sinal',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Carga inicial',
                descricao:
                  'A fila inteira e os agendados de TODO o histórico desde 2015, lidos em fatias de um ano e aplicados pedido a pedido (um tratamento com várias sessões fica com todas). Leva alguns minutos. É para a primeira vez (ou para refazer a base).',
              },
              {
                termo: 'Diária',
                descricao:
                  'A fila inteira e os agendados de uma janela móvel: de 45 dias atrás a 400 dias à frente. Compara com o que já temos e registra quem mudou de situação, foi remarcado ou saiu da fila.',
              },
              {
                termo: 'Só a fila',
                descricao: 'Atualiza posição, prioridade e pendência de quem está na fila, sem ler os agendados.',
              },
            ]}
          />
          <P>
            A janela da diária vai longe para a frente de propósito: um pedido agendado para daqui a
            meses precisa estar dentro dela, senão pareceria ter sumido.
          </P>
          <P>
            Se o serviço reiniciar no meio (um deploy, por exemplo), a rodada fica{' '}
            <strong>Interrompida</strong> e <strong>retoma sozinha</strong> de onde parou — a coluna
            Progresso mostra a fase (fila ou agendados), o mês em leitura, há quanto tempo veio o último
            sinal e quantas vezes ela foi retomada.
          </P>
        </>
      ),
    },
    {
      id: 'parcial',
      titulo: 'Por que existe a rodada “Parcial”',
      busca: 'parcial concluída cobertura incompleta fatias incompletas lido declarado contagem não fechou fila não lida rodar de novo',
      conteudo: (
        <>
          <P>
            Para cada fatia de agendados (até um ano), o ESUS diz quantos registros existem, e a
            varredura confere se leu todos. Quando alguma fatia <strong>não fecha a conta</strong>
            (lido ≠ declarado), ou uma fila não pôde ser lida, a rodada termina como{' '}
            <SeloRef cor="alerta">Parcial</SeloRef> — e não como Concluída —, com a coluna Cobertura
            dizendo quantas fatias ficaram incompletas.
          </P>
          <Callout tipo="regra" titulo="Declarado, não escondido">
            Uma rodada parcial avisa que a cobertura ficou incompleta em vez de fingir que leu tudo.
            O que ela leu vale; o que faltou é lido na próxima rodada. Se “Parcial” se repetir, rode de
            novo e, persistindo, abra um ticket.
          </Callout>
        </>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo regulação esus são gonçalo consulta estatísticas configuração perfil',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Regulação — ESUS São Gonçalo',
              descricao:
                'Consulta: ver fila, detalhe, notificações, marcar como visto e reanalisar as regras. As outras ações não têm efeito — a integração é só leitura.',
            },
            { termo: 'Estatísticas — ESUS São Gonçalo', descricao: 'Ver as estatísticas dos operadores.' },
            {
              termo: 'Regulação — Configuração',
              descricao: 'Ver o motor (consulta); rodar, sincronizar catálogo, mudar o horário e a credencial (edição).',
            },
          ]}
        />
      ),
    },
  ],
};
