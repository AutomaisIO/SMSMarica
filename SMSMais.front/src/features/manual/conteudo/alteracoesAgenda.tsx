import { CalendarClock } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Alterações de Agenda (`/app/alteracoes-agenda`) — escrito junto com o aviso automático
 * da remarcação (23/10/2026), que mudou o que a fila significa: antes ela era o único lugar onde a
 * remarcação existia; agora o paciente é avisado sozinho e a fila guarda só o que precisa de alguém.
 *
 * Conferido em `features/alteracoes-agenda/pages/AlteracoesAgendaPage` e, no servidor, em
 * `ImportacaoSisregService.ReconciliarAsync` (o que vira alteração), `ComparadorMarcacao`,
 * `VarreduraAgendaService.DetectarAusentesAsync` (Sumiu do SISREG), `AvisoRemarcacaoService`
 * (decisão, freio, vai-e-volta, lembrete), `AlteracoesAgendaService` (escopo, botões) e
 * `SolicitacaoHistoricoService` (linha do tempo da solicitação).
 */
export const artigoAlteracoesAgenda: Artigo = {
  slug: 'alteracoes-agenda',
  titulo: 'Alterações de agenda',
  resumo:
    'O que o SISREG remarcou, trocou ou deixou de mandar em agendamentos que já estavam aqui — e o que foi feito com o paciente: o aviso da data nova sai sozinho, e a fila guarda só o que precisa de alguém.',
  grupo: 'regulacao',
  icone: CalendarClock,
  rota: '/app/alteracoes-agenda',
  publico: 'Regulação e unidades (executante e solicitante) que acompanham os agendamentos do SISREG',
  atualizadoEm: '2026-10-23',
  palavrasChave: [
    'alterações de agenda',
    'remarcação',
    'remarcado',
    'data mudou',
    'mudou o dia',
    'mudou a hora',
    'nova data',
    'avisar paciente',
    'aviso automático',
    'aviso de remarcação',
    'confirmação de novo',
    'confirmação antiga',
    'mudança de data',
    'agendamento_data_alterada',
    'modelo da meta',
    'trocou o profissional',
    'trocou o procedimento',
    'sumiu do sisreg',
    'cancelado no sisreg',
    'só tratar',
    'tratar selecionadas',
    'retida',
    'vai e volta',
    'leitura errada',
    'linha do tempo',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'De onde vem esta fila',
      busca: 'sincronização diária releitura agenda sisreg mesmo número código de solicitação data hora profissional procedimento',
      conteudo: (
        <>
          <P>
            Toda noite a sincronização relê a agenda de cada unidade no SISREG, de hoje até a última escala. Quando
            encontra um número de solicitação que já está aqui, ela compara com o que temos. O que mudou é aplicado
            na solicitação — a data passa a ser a nova — e vira uma linha nesta fila, com o <strong>de</strong> e o{' '}
            <strong>para</strong>.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <SeloRef cor="alerta">Remarcado</SeloRef>,
                descricao:
                  'A regulação mudou o dia ou a hora. É a única que muda o que o paciente precisa fazer — e a única que o sistema avisa sozinho.',
              },
              {
                termo: <SeloRef>Trocou o profissional</SeloRef>,
                descricao: 'Mesmo dia e hora, outro profissional. Muitas vezes não muda nada para quem vai comparecer.',
              },
              {
                termo: <SeloRef>Trocou o procedimento</SeloRef>,
                descricao: 'O SISREG passou a mandar outro procedimento para o mesmo pedido.',
              },
              {
                termo: <SeloRef cor="erro">Sumiu do SISREG</SeloRef>,
                descricao:
                  'O agendamento deixou de vir na leitura. Quase sempre é cancelamento lá — mas é dedução, e por isso não se avisa o paciente a partir daqui (ver abaixo).',
              },
            ]}
          />
          <P>
            Cada mudança também aparece na <strong>linha do tempo da solicitação</strong> (detalhe do exame ou da
            consulta, quadro <em>Comunicação com o paciente</em>), com a data antiga, a nova e o que foi feito com o
            paciente. Quem abre a solicitação semanas depois vê que um dia ela foi outra.
          </P>
        </>
      ),
    },
    {
      id: 'aviso-automatico',
      titulo: 'O aviso da data nova sai sozinho',
      busca: 'aviso automático remarcação mudança de data data anterior nova data confirmação de novo resposta anterior deixa de valer confirmado volta não confirmados lembrete horário de envio modelo meta aprovado',
      conteudo: (
        <>
          <P>
            Quando a linha é um <SeloRef cor="alerta">Remarcado</SeloRef>, o sistema decide sozinho o que fazer com o
            paciente, poucos minutos depois de a leitura da unidade terminar:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Já conhecia a data antiga',
                descricao:
                  'Recebeu a mensagem com ela, ou tinha confirmado. Recebe o aviso "Mudança de data": com o número confirmado, a data anterior, a nova e os botões Confirmar nova data e Não poderei ir; sem o número confirmado, só que a data mudou — as datas chegam depois que ele toca em Quero mais informações e se identifica. Se tinha confirmado, a confirmação deixa de valer — era sobre a data antiga — e a ficha volta para Não confirmados em Confirmações até ele responder.',
              },
              {
                termo: 'A mensagem ainda não tinha saído',
                descricao:
                  'Nada a reenviar: ela é montada na hora do envio e já sai com a data nova. Inclui quem recebeu só a primeira mensagem (a que não diz a data) e ainda não se identificou — quando ele se identificar, os dados chegam com a data nova.',
              },
              {
                termo: 'Nunca recebeu mensagem',
                descricao: 'Se a unidade e o procedimento avisam por WhatsApp, a primeira mensagem já sai com a data nova.',
              },
            ]}
          />
          <P>
            A mensagem segue o horário de envio e a vazão de <em>Mensageria → Regras</em>: a leitura roda de madrugada
            e o paciente recebe de manhã. O lembrete das vésperas também passa a valer para a data nova.
          </P>
          <P>
            Avisada, a linha sai da fila sozinha. Com <em>Só as que faltam tratar</em> desmarcado, ela aparece como{' '}
            <em>avisado automaticamente</em>, e a coluna <strong>Aviso ao paciente</strong> diz o que foi feito.
          </P>
          <Callout tipo="regra" titulo="Mudança de data nunca sai como confirmação comum">
            Para quem já conhecia a data antiga, a confirmação comum leria como se nada tivesse mudado. Por isso a
            mensagem sai em modelos próprios da Meta, com a foto <strong>Mudança de data</strong>. Enquanto eles não
            estiverem aprovados, a mensagem <strong>espera</strong> na fila e a remarcação fica aqui, com esse motivo
            — ligue se o atendimento estiver perto. Aprovados os modelos, ela sai sozinha.
          </Callout>
          <P>
            O aviso automático tem chave própria, em <em>Mensageria → Regras → Remarcações feitas no SISREG</em>, e
            respeita as mesmas chaves por unidade e por procedimento da confirmação. Ligar vale daqui para frente.
          </P>
        </>
      ),
    },
    {
      id: 'fica-para-a-equipe',
      titulo: 'O que fica na fila, e por quê',
      busca: 'fica para a equipe aviso ao paciente coluna motivo não vou atendimento humano hoje telefone unidade sem aviso falha outro procedimento',
      conteudo: (
        <>
          <P>
            Algumas remarcações o sistema não avisa sozinho, de propósito. Elas ficam pendentes, e a coluna{' '}
            <strong>Aviso ao paciente</strong> diz o motivo — é por ela que se começa:
          </P>
          <Lista>
            <Item>
              <strong>O paciente tinha dito que não ia.</strong> Talvez a remarcação seja justamente a resposta da
              regulação a isso. Ligue e confirme se a data nova serve; o botão reabre a confirmação.
            </Item>
            <Item>
              <strong>Remarcado para hoje.</strong> A mensagem não chegaria a tempo: ligue.
            </Item>
            <Item>
              <strong>A unidade ou o procedimento não avisa por WhatsApp.</strong> Avise por telefone.
            </Item>
            <Item>
              <strong>Em atendimento humano</strong> em Confirmações: depois que uma pessoa entrou no circuito, o
              automático não fala mais com o paciente — quem atende avisa.
            </Item>
            <Item>
              <strong>A mensagem da data antiga não chegou</strong> (número sem WhatsApp, sem celular): ligue.
            </Item>
            <Item>
              <strong>A data nova não é a da solicitação</strong> — acontece quando o que mudou foi outro
              procedimento do mesmo pedido.
            </Item>
            <Item>
              <strong>Os modelos de &quot;Mudança de data&quot; ainda não estão aprovados como utilidade na Meta.</strong> A mensagem já
              está na fila e sai sozinha quando forem; até lá, ligue se o atendimento estiver perto.
            </Item>
          </Lista>
          <P>
            Se o paciente tinha confirmado a data antiga, a confirmação deixa de valer mesmo sem mensagem (menos quando
            a data da solicitação não mudou): a ficha volta para Não confirmados, que é onde a equipe trabalha.
          </P>
        </>
      ),
    },
    {
      id: 'retida',
      titulo: 'Retida: quando o sistema desconfia da leitura',
      busca: 'retida freio volume leitura errada vai e volta aviso no celular conferir no sisreg remarcações demais',
      conteudo: (
        <>
          <P>
            Remarcação de verdade é pontual: a agenda de um profissional que mudou, algumas dezenas de pacientes. Uma
            leitura errada do SISREG mexe em quase tudo de uma vez — e avisar cada paciente de uma data errada é a
            única mensagem sem conserto. Por isso há duas travas:
          </P>
          <Lista>
            <Item>
              <strong>Volume.</strong> Se uma unidade tem 20 ou mais remarcações em 24 horas <em>e</em> elas passam de
              10% da agenda futura dela, nenhuma sai: todas ficam aqui como <em>Retida</em>, e chega um aviso no
              celular (<em>Remarcações do SISREG retidas</em>). Nada na ficha do paciente é mexido.
            </Item>
            <Item>
              <strong>Vai e volta.</strong> Se a data voltou para uma que já tinha sido trocada no último mês, a
              remarcação também fica retida — podem ser duas leituras discordando.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Unidade pequena também cai no freio">
            Se a agenda inteira de uma unidade pequena foi mudada de verdade, o freio segura do mesmo jeito. Confira
            algumas no SISREG e, se estiverem certas, use <BotaoRef>Avisar paciente</BotaoRef> nelas.
          </Callout>
        </>
      ),
    },
    {
      id: 'botoes',
      titulo: 'Os botões',
      busca: 'avisar paciente só tratar tratar selecionadas lote reabre confirmação não vou',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: <BotaoRef>Avisar paciente</BotaoRef>,
              descricao:
                'Reenvia a confirmação com a data de agora, na hora (dentro do horário de envio) — com o título "Mudança de data" quando o paciente conhecia a data antiga. É uma pessoa decidindo, então passa por cima do que o automático respeita: reabre até o "não vou" do paciente e vale para unidade sem aviso automático. Não vale para Sumiu do SISREG.',
            },
            {
              termo: <BotaoRef variante="ghost">Só tratar</BotaoRef>,
              descricao: 'Tira a linha da fila sem mandar nada — você já resolveu por outro caminho, ou não há o que avisar.',
            },
            {
              termo: <BotaoRef variante="ghost">Tratar selecionadas</BotaoRef>,
              descricao: 'O mesmo, para várias de uma vez (até 200), depois de marcar as caixas.',
            },
          ]}
        />
      ),
    },
    {
      id: 'sumiu',
      titulo: 'Sumiu do SISREG',
      busca: 'sumiu do sisreg ausente cancelado lá vaga ocupada cancelar solicitação conferir',
      conteudo: (
        <>
          <P>
            O agendamento deixou de vir na leitura. Quase sempre foi cancelado lá — mas a solicitação aqui continua
            com a data antiga, e uma mensagem a partir daqui seria a confirmação de um horário que provavelmente não
            existe mais. Por isso não há <em>Avisar paciente</em> nesta linha.
          </P>
          <P>
            Confira no SISREG: se foi cancelado, cancele a solicitação (é isso que libera a vaga aqui); se continua de
            pé, <em>Só tratar</em>.
          </P>
        </>
      ),
    },
    {
      id: 'quem-ve',
      titulo: 'Quem vê o quê',
      busca: 'permissão módulo alterações agenda unidade executante solicitante escopo',
      conteudo: (
        <P>
          A tela pede o módulo <strong>Alterações de Agenda</strong>. Cada pessoa vê as alterações das unidades em
          que atua, seja como <strong>executante</strong> ou como <strong>solicitante</strong> — a unidade que pediu o
          exame entra de propósito: é ela que conhece o paciente e muitas vezes é quem consegue falar com ele.
        </P>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'paciente recebeu de novo confirmação duas vezes por que confirmado sumiu horário mudou só a hora',
      conteudo: (
        <>
          <Sub>O paciente perguntou por que a data mudou.</Sub>
          <P>
            A mudança foi feita no SISREG, pela regulação. Abra a solicitação: a linha do tempo mostra a
            remarcação, de quando para quando, e a mensagem que ele recebeu aparece marcada como{' '}
            <em>mudança de data</em>.
          </P>
          <Sub>Mudou só a hora, no mesmo dia. Avisa?</Sub>
          <P>
            Avisa. Para o paciente, 8h e 14h são compromissos diferentes. A única exceção é a solicitação que estava
            sem hora (meia-noite) e ganhou a hora no mesmo dia — isso não é remarcação, e nem entra na fila.
          </P>
          <Sub>Ele tinha confirmado e agora aparece como não confirmado em Confirmações.</Sub>
          <P>
            É o esperado: ele confirmou a data antiga. Quando responder à mensagem com a data nova, volta para
            Confirmados.
          </P>
        </>
      ),
    },
  ],
};
