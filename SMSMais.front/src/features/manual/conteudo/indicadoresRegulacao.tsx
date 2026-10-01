import { Gauge } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Indicadores de Regulação — a série mensal de SISREG, SER, SERNIT e ESUS São Gonçalo.
 *
 * Conferido no código: `features/regulacao-indicadores` (IndicadoresRegulacaoPage, Resumo,
 * SecaoBloco, GraficoSecao, Tabelas, SeloOrigem, lib/indicadores — presets, `agregar`, escolha do
 * resumo), no relatório de origem (`docs/regulacao/relatorio-2025-2026`: dados.json e montar.py) e
 * no backend (`Core/Regulacao/Indicadores`: DTOs, IndicadoresSisregCalculo, MotivoCancelamento).
 */
export const artigoIndicadoresRegulacao: Artigo = {
  slug: 'indicadores-regulacao',
  titulo: 'Indicadores de Regulação',
  resumo:
    'A série mensal de cada sistema de regulação (SISREG, SER, SERNIT, ESUS São Gonçalo): vagas, absenteísmo, regulados, fila, cancelamentos, tempo de espera e demandas judiciais — cada número com o selo de origem, e o PDF do período.',
  grupo: 'regulacao',
  icone: Gauge,
  rota: '/app/regulacao/indicadores',
  publico: 'Quem acompanha e presta contas da regulação: gestão, coordenação e controle',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'indicadores',
    'indicadores de regulação',
    'relatório',
    'relatório mensal',
    'série histórica',
    'PDF',
    'exportar',
    'SISREG',
    'SER',
    'SERNIT',
    'ESUS',
    'ESUS SG',
    'São Gonçalo',
    'Niterói',
    'vagas',
    'vagas ofertadas',
    'oferta',
    'escalas',
    'cotas PPI',
    'PPI',
    'ocupação',
    'agendamentos',
    'absenteísmo',
    'faltas',
    'chegada',
    'comparecimento',
    'regulados',
    'consultas',
    'exames',
    'fila',
    'fila no fim do mês',
    'solicitações registradas',
    'atendidas',
    'canceladas',
    'excluídas',
    'devolvidas',
    'negadas',
    'motivos de cancelamento',
    'tempo de espera',
    'mediana',
    'percentil 90',
    'judicial',
    'judicializadas',
    'mandado judicial',
    'selo',
    'oficial',
    'calculado',
    'parcial',
    'indisponível',
    'piso',
    'cobertura dos dados',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'série mensal relatório prestação de contas quatro sistemas mesma tela indicadores',
      conteudo: (
        <>
          <P>
            É o retrato mês a mês da regulação, um sistema por vez: quanto foi ofertado e usado, quanto
            faltou, quanto foi regulado, quanta gente esperava na fila no fim de cada mês, como os pedidos
            terminaram, quanto tempo se esperou e quantos vieram por ordem judicial.
          </P>
          <P>
            Há uma tela para cada sistema — em <strong>Regulação → SISREG</strong>,{' '}
            <strong>SER</strong>, <strong>SERNIT</strong> e <strong>ESUS SG</strong>, item{' '}
            <strong>Indicadores</strong>. As quatro têm o mesmo desenho, e é de propósito: o mesmo
            indicador fica no mesmo lugar, para dar para comparar. O que muda é o que cada sistema
            entrega ao município — e o que ele não entrega aparece como Indisponível, não some.
          </P>
          <P>
            É a versão viva do relatório em PDF: os números vêm das bases espelhadas e das consultas feitas
            aos próprios sistemas, e o botão <BotaoRef variante="outline">Exportar PDF</BotaoRef> gera o
            mesmo relatório para o período escolhido.
          </P>
        </>
      ),
    },
    {
      id: 'como-ler',
      titulo: 'Como ler a tela',
      busca: 'resumo cobertura dos dados seções gráfico tabela por ano no período total último mês média no ano destaque notas',
      conteudo: (
        <>
          <P>De cima para baixo:</P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Resumo',
                descricao:
                  'Seis números, um valor por ano do período: agendamentos, regulados (SISREG) ou solicitações registradas (demais), fila no fim do período, absenteísmo, canceladas e espera (mediana).',
              },
              {
                termo: 'Cobertura dos dados',
                descricao:
                  'De onde saiu cada parte e até quando os dados estão atualizados (última varredura, desde quando a fila é lida etc.). Leia antes de comparar meses: é aqui que se descobre que um trecho é piso.',
              },
              {
                termo: 'Seções',
                descricao:
                  'Vagas, Absenteísmo, Regulados, Fila, Desfechos, Tempo de espera e Judicial. Os botões logo acima levam direto a cada uma.',
              },
              {
                termo: 'Gráfico',
                descricao:
                  'Barras por mês das séries principais da seção. Com duas séries, lado a lado e com legenda; empilhado quando as partes somam o todo (consultas + exames). Passe o mouse para ver o valor.',
              },
              {
                termo: 'Tabela por ano',
                descricao:
                  'Uma tabela para cada ano do período: indicador, selo de origem, um valor por mês e a coluna “No período”. Linha em negrito é a principal da seção; linha recuada é detalhe da de cima.',
              },
              {
                termo: 'Notas',
                descricao:
                  'No pé de cada seção, a regra de cada indicador que tem nota (o “i” ao lado do nome mostra a mesma nota).',
              },
            ]}
          />
          <Sub>A coluna “No período”</Sub>
          <P>
            Fecha o ano do jeito certo para cada indicador, e diz ao lado qual foi a conta:
          </P>
          <Lista>
            <Item>
              <strong>total</strong> — contagens (agendamentos, faltas, canceladas) somam os meses;
            </Item>
            <Item>
              <strong>último mês</strong> — estoque (fila no fim do mês) não soma: vale a posição do último
              mês;
            </Item>
            <Item>
              <strong>no ano</strong> — percentuais e tempos vêm calculados sobre o ano inteiro: absenteísmo é
              faltas do ano ÷ agendamentos do ano, e a espera é a mediana de todos os casos do ano;
            </Item>
            <Item>
              <strong>média</strong> — só quando não há o valor do ano pronto.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Não tire média dos meses de um percentual">
            A média dos 12 absenteísmos mensais não é o absenteísmo do ano — mês com pouco agendamento pesa
            igual a mês cheio. Use o valor “no ano” da tabela (é o mesmo do Resumo).
          </Callout>
        </>
      ),
    },
    {
      id: 'selos',
      titulo: 'Os quatro selos de origem',
      busca: 'selo oficial calculado parcial indisponível origem do número piso nota',
      conteudo: (
        <>
          <P>
            Todo número traz um selo. Ele existe para um número parcial nunca ser lido como oficial — quem
            leva o relatório para uma reunião precisa saber o que está afirmando.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="info">Oficial</SeloRef>,
                descricao:
                  'Lido do próprio sistema de origem (ou do espelho fiel dele mantido pela Secretaria). Pode ser citado como o número do sistema.',
              },
              {
                termo: <SeloRef cor="gray">Calculado</SeloRef>,
                descricao:
                  'Derivado de dados oficiais — uma razão, uma mediana, uma reconstrução. A regra do cálculo está na nota do indicador.',
              },
              {
                termo: <SeloRef cor="alerta">Parcial</SeloRef>,
                descricao:
                  'Sabidamente incompleto: o valor é um piso (o de verdade é igual ou maior). A nota diz o que falta.',
              },
              {
                termo: <SeloRef cor="gray">Indisponível</SeloRef>,
                descricao:
                  'O sistema de origem não fornece esse dado ao município. Na tela, o selo tem borda tracejada, e a seção inteira vira um aviso tracejado quando nada dela existe.',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'indicadores',
      titulo: 'O que cada indicador quer dizer',
      busca:
        'vagas ofertadas escalas cotas PPI ocupação vagas utilizadas agendamentos absenteísmo faltas chegada confirmada regulados consultas exames fila solicitações registradas atendidas canceladas marcações desfeitas devolvidas negadas excluídas espera mediana 90% quem ainda aguarda especialidade procedimento recurso judicial mandado martelo varredura diária conferência fase mandado judicial',
      conteudo: (
        <>
          <Sub>Vagas disponibilizadas e utilizadas</Sub>
          <Lista>
            <Item>
              <strong>SISREG</strong>: a oferta é a soma, dia a dia, das escalas cadastradas para as unidades
              executantes da rede (regulação, retorno e agenda local), comparada aos agendamentos com data de
              atendimento no mês; a <strong>ocupação</strong> é agendamentos ÷ vagas ofertadas. Traz também as
              cotas PPI pactuadas e utilizadas.
            </Item>
            <Item>
              <strong>SER e SERNIT</strong>: só a utilização — o primeiro agendamento de cada solicitação do
              município no mês (remarcação não soma de novo).
            </Item>
            <Item>
              <strong>ESUS SG</strong>: só a utilização — agendamentos com data de atendimento no mês.
            </Item>
          </Lista>
          <Sub>Absenteísmo</Sub>
          <Lista>
            <Item>
              <strong>SISREG</strong>: faltas da consulta de absenteísmo do próprio SISREG — os agendamentos
              em que a unidade executante <strong>registrou falta</strong> —, casadas com os agendamentos do
              mês. Os agendamentos do mês se dividem em três: <strong>comparecimento confirmado</strong>,{' '}
              <strong>faltas</strong> e <strong>em aberto</strong> (a unidade não apontou chegada nem falta —
              não conta como falta nem como atendido). O em aberto só aparece nos meses com a lista de faltas
              completa; o confirmado dos meses mais recentes sobe conforme a varredura diária relê as chegadas.
              A linha “Faltas sem agendamento correspondente” é auditoria: falta oficial cujo agendamento não
              está na base (em geral, remarcado).
            </Item>
            <Item>
              <strong>SER e SERNIT</strong>: o registro de chegada que a unidade executora faz (“Chegada no
              destino”) — não confirmadas ÷ chegadas registradas. “Agendados sem registro de chegada” são os
              que já passaram da data e a unidade não registrou nada.
            </Item>
          </Lista>
          <Sub>Quantitativo regulado</Sub>
          <P>
            No SISREG, as autorizações com data de regulação no mês, separadas em consultas × exames e demais
            procedimentos. Nos outros três, as solicitações do município que receberam agendamento no mês,
            por tipo.
          </P>
          <Sub>Fila e solicitações</Sub>
          <P>
            Quantos aguardavam no último dia de cada mês (ver “Como a fila no fim do mês é reconstruída”) e
            quantas solicitações entraram no mês.
          </P>
          <Sub>Atendidas, canceladas e excluídas</Sub>
          <Lista>
            <Item>
              <strong>SISREG</strong>: atendidas (chegada confirmada pela unidade executante ou registrada pela
              recepção — o agendamento que a unidade deixou em aberto não conta), marcações canceladas (pela
              data do cancelamento) e solicitações excluídas da fila sem agendamento — devolvidas, negadas ou
              canceladas antes do agendamento (pela data da solicitação).
            </Item>
            <Item>
              <strong>SER e SERNIT</strong>: atendidas (chegada confirmada) e canceladas. Esses sistemas não
              têm “excluída”: quem sai da fila sem atendimento sai cancelado, com motivo. “Marcações desfeitas”
              e “Devolvidas para a regulação” voltaram para a fila — não são saída.
            </Item>
            <Item>
              <strong>ESUS SG</strong>: só as que saíram da fila com agendamento.
            </Item>
          </Lista>
          <Sub>Tempo de espera</Sub>
          <P>
            Mediana, “90% até” (o tempo dentro do qual ficam 9 de cada 10 casos), menor e maior, em dias. No
            SISREG e no ESUS, a espera <strong>até o atendimento</strong> (do pedido à data marcada) e a
            espera <strong>em fila</strong> (do pedido à autorização, no SISREG, ou ao agendamento, no ESUS); no SISREG, as
            autorizações no mesmo dia do pedido (agenda direta) ficam à parte, em percentual. No SER e no
            SERNIT, do pedido ao primeiro agendamento. Abaixo, as tabelas por especialidade, por procedimento
            (ou por recurso, no ESUS) e de quem ainda aguarda hoje.
          </P>
          <Sub>Demandas judicializadas</Sub>
          <P>
            As solicitações marcadas como mandado judicial no próprio sistema, pela data do pedido: quantas
            foram agendadas, a mediana e os extremos de dias até agendar, e a lista delas (sem dado pessoal).
          </P>
          <P>
            No SER e no SERNIT a grade não mostra o martelo: a marcação vem da <strong>varredura diária</strong>,
            que ao final refaz a pesquisa com o filtro "Somente com mandado judicial" (fase{' '}
            <strong>Mandado judicial</strong> na lista de execuções da Configuração). A marcação só é acrescentada,
            nunca retirada; se a conferência de uma noite ficar incompleta, vale a da noite anterior.
          </P>
        </>
      ),
    },
    {
      id: 'indisponivel',
      titulo: 'Por que alguns itens aparecem como Indisponível',
      busca: 'indisponível oferta de vagas não aparece SER SERNIT ESUS comparecimento cancelamento motivo SISREG judicial marcador',
      conteudo: (
        <>
          <P>
            Não é falha da tela nem da varredura: é o que cada sistema mostra ao município que solicita.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'SER e SERNIT',
                descricao:
                  'A oferta de vagas pertence ao gestor do sistema (Estado / Niterói) e não é exibida ao município solicitante. Utilização, absenteísmo, desfechos e judicial existem.',
              },
              {
                termo: 'ESUS SG',
                descricao:
                  'O ESUS de São Gonçalo mostra só duas listas, fila e agendados. Não informa se o paciente compareceu (absenteísmo), não mostra a oferta (as cotas da PPI são do município executor) e não mostra cancelamento, exclusão nem motivo — quem sai das duas listas sem agendamento simplesmente some.',
              },
              {
                termo: 'SISREG',
                descricao:
                  'Não tem marcador de mandado judicial — nem na solicitação, nem na fila do regulador. O indicador depende de um registro próprio da Secretaria (por exemplo, a lista da Procuradoria).',
              },
            ]}
          />
        </>
      ),
    },
    {
      id: 'sisreg-atualizacao',
      titulo: 'Como os números oficiais do SISREG se atualizam',
      busca:
        'coleta SISREG faltas absenteísmo oficial 30 dias faltas recentes hora em hora em aberto sem apontamento ficha do paciente PPI cotas canceladas motivos amostra estimativa conciliação devolvidas negadas unidade configuração ligar coletor',
      conteudo: (
        <>
          <P>
            Faltas, cotas PPI, marcações canceladas e devolvidas/negadas não estão na agenda espelhada: vêm de
            telas próprias do SISREG. O passado (jan/2025 a ago/2026) foi carregado de uma vez; daqui para
            frente quem mantém é o <strong>coletor</strong>, ligado em SISREG → Configuração → “Indicadores de
            Regulação — coleta no SISREG” (nasce desligado).
          </P>
          <Lista>
            <Item>
              <strong>Faltas</strong>: para o indicador, cada semana só vale quando completa 30 dias — antes
              disso a lista ainda muda, porque as unidades apontam (e corrigem) as faltas com atraso. Por
              isso o último mês aparece como Indisponível no absenteísmo até ~30 dias depois de fechar. A
              mesma lista é lida também das semanas mais novas, de hora em hora, mas só para a ficha do
              paciente (aba Agendamentos): lá a falta já registrada aparece na hora, e o agendamento sem
              apontamento aparece como “Em aberto”. Essa leitura adiantada <strong>não entra</strong> no
              absenteísmo.
            </Item>
            <Item>
              <strong>Cotas PPI</strong>: a competência fechada é lida a partir do dia 5 do mês seguinte.
            </Item>
            <Item>
              <strong>Canceladas e motivos</strong>: o total de cada mês é o que o SISREG declara. Daqui para
              frente, a conciliação de cancelamentos, que já lê o SISREG todo dia, grava cada cancelamento com a
              justificativa. Nos meses passados os motivos vêm de uma <strong>amostra</strong> (6 páginas
              espalhadas por mês): a tabela mostra o % estimado de cada motivo — cada mês pesando pelo seu total —
              e a estimativa em número, sempre somando o total oficial do ano.
            </Item>
            <Item>
              <strong>Devolvidas, negadas e canceladas antes de agendar</strong>: lidas unidade por unidade e
              relidas por cerca de três meses, porque um pedido de um mês pode ser devolvido meses depois.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'fila-fim-do-mes',
      titulo: 'Como a fila no fim do mês é reconstruída',
      busca: 'fila no fim do mês reconstruída piso trilha de eventos sem trilha em fila pendente saiu da fila parcial',
      conteudo: (
        <>
          <P>
            Nenhum desses sistemas guarda “quantos estavam na fila no dia 31”. O número é remontado, pedido a
            pedido, a partir do que se sabe hoje:
          </P>
          <Lista>
            <Item>
              <strong>SISREG</strong> <SeloRef cor="alerta">Parcial</SeloRef>: quem ainda aguarda e já
              tinha pedido naquela data, mais quem foi autorizado ou saiu da fila depois dela. Quem saiu sem
              agendamento (negada, devolvida, cancelada) antes de a leitura diária da fila começar não tem data
              de saída conhecida e fica de fora — por isso é um <strong>piso</strong>. A data em que a leitura
              começou está na Cobertura dos dados.
            </Item>
            <Item>
              <strong>SER e SERNIT</strong> <SeloRef cor="gray">Calculado</SeloRef>: pela trilha de eventos
              de cada solicitação — o último estado até aquela data (“Em fila” ou “Pendente”). A linha “das
              quais sem trilha de eventos” conta as que não têm histórico legível; essas entram pela situação
              atual.
            </Item>
            <Item>
              <strong>ESUS SG</strong> <SeloRef cor="alerta">Parcial</SeloRef>: pelas datas de entrada e
              de saída da fila. Quem saiu sem agendamento antes da primeira varredura do espelho já não aparece
              no ESUS — também é piso.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Piso não é número errado — é o mínimo garantido">
            Um piso pode ser citado como “pelo menos N”. O que não se pode é comparar um mês parcial com um
            mês completo como se fossem iguais: veja o selo e a nota antes.
          </Callout>
        </>
      ),
    },
    {
      id: 'motivos',
      titulo: 'Como os motivos de cancelamento são agrupados',
      busca: 'motivos de cancelamento categoria amostra estimativa outros sem motivo informado FollowUP texto livre',
      conteudo: (
        <>
          <P>
            O motivo de um cancelamento é texto livre, escrito por quem cancelou — e pode trazer nome e
            telefone. Por isso ele <strong>nunca</strong> aparece na tela nem no PDF: é lido pela máquina e
            contado numa <strong>categoria</strong> (Óbito, Desistência / impedimento do paciente, Já
            atendido / realizado, Erro de marcação / agenda, Sem contato com o paciente, Duplicidade…), pela
            primeira regra de palavras que casar.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'SISREG',
                descricao:
                  'Nos meses passados a justificativa vem de uma AMOSTRA da lista de marcações canceladas (6 páginas espalhadas por mês); daqui para frente, da conciliação diária, que grava todos. Quando as linhas lidas não cobrem quase tudo, a tabela mostra o % estimado de cada categoria — cada mês pesando pelo seu total oficial, para um mês cheio não valer o mesmo que um mês fraco — e a “Estimativa” = esse % × o total oficial de canceladas do ano. É estimativa, não contagem.',
              },
              {
                termo: 'SER e SERNIT',
                descricao:
                  'Contagem de todos os cancelamentos, por ano. Quando o texto do cancelamento é genérico (ex.: “não respondida no prazo”), vale o último FollowUP anterior a ele (ex.: “sem contato: diversas tentativas”).',
              },
              {
                termo: 'ESUS SG',
                descricao: 'Não há motivo: o ESUS não mostra cancelamento ao município.',
              },
            ]}
          />
          <P>
            “Outros” (texto que não casou com nenhuma categoria) e “Sem motivo informado” (em branco, ou só
            “cancelar”) ficam sempre no fim da tabela: o topo é para motivo que diz alguma coisa.
          </P>
        </>
      ),
    },
    {
      id: 'periodo-pdf',
      titulo: 'Mudar o período e exportar o PDF',
      busca: 'período últimos 12 meses ano anterior ano até agora mês inicial mês final 24 meses exportar PDF baixar relatório',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Escolha um atalho: <BotaoRef>Últimos 12 meses</BotaoRef>, o ano anterior inteiro ou o ano
                    corrente até agora.
                  </>
                ),
                detalhe:
                  'Sem escolha, a tela abre nos últimos 12 meses já fechados — o mês corrente fica de fora porque ainda está acontecendo. “Até agora” também para no último mês fechado.',
              },
              {
                titulo: 'Ou ajuste “De” e “Até”, mês a mês.',
                detalhe:
                  'Até 24 meses. Cada ano civil do período vira uma tabela própria, e o Resumo mostra um valor por ano.',
              },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef variante="outline">Exportar PDF</BotaoRef>.
                  </>
                ),
                detalhe:
                  'O PDF sai com o mesmo período da tela e é baixado como arquivo. Pode levar alguns segundos; se falhar, o motivo aparece logo abaixo do botão.',
              },
            ]}
          />
          <Callout tipo="dica" titulo="Os números mudam com a varredura">
            A tela é montada com o que as bases espelhadas têm no momento. Uma varredura nova pode completar
            um mês antigo — por isso o PDF traz a data em que foi gerado. Guarde o PDF quando precisar de um
            retrato fixo.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode ver',
      busca: 'permissão perfil indicadores de regulação consulta menu',
      conteudo: (
        <>
          <P>
            A permissão é <strong>Indicadores de Regulação (SISREG, SER, SERNIT, ESUS SG)</strong>, na tela
            de Perfis — uma só para os quatro sistemas, e só a <strong>Consulta</strong> vale: ela libera as
            quatro telas e o PDF.
          </P>
          <P>
            Ela não depende da permissão das filas: quem tem só os Indicadores enxerga, dentro de cada
            sistema no menu Regulação, apenas o item Indicadores.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'número diferente do sistema mês atual não aparece ocupação média absenteísmo ano PDF não baixa',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O número não bate com o que vejo no próprio sistema.',
              descricao:
                'Confira o selo e a nota: Calculado e Parcial seguem a regra da nota, que pode ser diferente do relatório do sistema (ex.: no SISREG, os agendamentos contam pela data do atendimento, não pela data em que foram marcados). E veja na Cobertura até quando os dados foram lidos.',
            },
            {
              termo: 'Por que o mês corrente não aparece?',
              descricao:
                'O padrão são os meses já fechados. Dá para incluir o mês corrente ajustando o “Até”, sabendo que ele está incompleto.',
            },
            {
              termo: 'O “No período” do absenteísmo não é a média dos meses.',
              descricao:
                'Certo: é a razão do ano (faltas do ano ÷ agendamentos do ano). Ver “A coluna No período”.',
            },
            {
              termo: 'Uma seção inteira está tracejada.',
              descricao: 'O sistema não fornece aquele indicador ao município. Ver “Por que alguns itens aparecem como Indisponível”.',
            },
            {
              termo: 'O PDF não baixou.',
              descricao:
                'O motivo aparece embaixo do botão. Se o navegador bloqueou o download, libere downloads para este site e tente de novo.',
            },
          ]}
        />
      ),
    },
  ],
};
