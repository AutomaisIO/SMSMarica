import type { ReactNode } from 'react';
import { Send } from 'lucide-react';
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  LabelList,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo CONCEITUAL e TÉCNICO (pedido do dono em 08/10/2026): como uma mensagem ao paciente sai
 * do sistema — gatilhos, fila, guardas, escolha do modelo, janela, régua de reforço, tentativas,
 * recibos da Meta e o que fazer quando falha. Público: gestão e equipe técnica.
 *
 * Não documenta uma tela (sem `rota`): documenta a ENGRENAGEM por trás de Mensageria e
 * Confirmações. Os números (prazos, tentativas, horários) são os do código — quando um deles
 * mudar, este artigo muda no mesmo commit.
 */

// ---------- peças visuais (só deste artigo) ----------

type Tom = 'sistema' | 'pessoa' | 'decisao' | 'fim' | 'alerta' | 'espera';

const TOM: Record<Tom, string> = {
  sistema: 'border-gray-300 bg-white',
  pessoa: 'border-sky-300 bg-sky-50',
  decisao: 'border-amber-300 bg-amber-50',
  fim: 'border-emerald-300 bg-emerald-50',
  alerta: 'border-primary-300 bg-primary-50',
  espera: 'border-violet-300 bg-violet-50',
};

function Caixa({ tom, titulo, children }: { tom: Tom; titulo: ReactNode; children?: ReactNode }) {
  return (
    <div className={`rounded-lg border px-3 py-2 text-xs shadow-sm ${TOM[tom]}`}>
      <p className="font-semibold text-gray-900">{titulo}</p>
      {children && <div className="mt-0.5 text-gray-600">{children}</div>}
    </div>
  );
}

function Seta({ rotulo }: { rotulo?: string }) {
  return (
    <div className="flex items-center gap-2 py-0.5 pl-6 text-[11px] text-gray-500">
      <span aria-hidden>↓</span>
      {rotulo && <span className="italic">{rotulo}</span>}
    </div>
  );
}

function Ramo({ children }: { children: ReactNode }) {
  return <div className="ml-6 border-l-2 border-dashed border-gray-200 pl-3">{children}</div>;
}

function Legenda() {
  const itens: [Tom, string][] = [
    ['sistema', 'o sistema age'],
    ['decisao', 'uma decisão'],
    ['pessoa', 'o paciente'],
    ['espera', 'fica esperando'],
    ['fim', 'sai / termina bem'],
    ['alerta', 'não sai'],
  ];
  return (
    <div className="flex flex-wrap gap-2 text-[11px] text-gray-600">
      {itens.map(([tom, rotulo]) => (
        <span key={tom} className={`rounded border px-2 py-0.5 ${TOM[tom]}`}>
          {rotulo}
        </span>
      ))}
    </div>
  );
}

/** O caminho de uma mensagem, da esquerda para a direita (quebra em telas estreitas). */
function Esteira() {
  const etapas: { titulo: string; detalhe: string; tom: Tom }[] = [
    { titulo: '1. Gatilho', detalhe: 'importação do SISREG, exame realizado, laudo assinado, cancelamento, varredura da régua', tom: 'sistema' },
    { titulo: '2. Fila', detalhe: 'uma linha por solicitação × finalidade, "Na fila"', tom: 'espera' },
    { titulo: '3. Passada', detalhe: 'a cada minuto, até N mensagens (vazão), a mais antiga primeiro', tom: 'sistema' },
    { titulo: '4. Guardas', detalhe: 'ainda faz sentido? horário? número? quem está do outro lado?', tom: 'decisao' },
    { titulo: '5. Modelo', detalhe: 'escolhido NA HORA, pelo estado de agora', tom: 'decisao' },
    { titulo: '6. Relay → Meta', detalhe: 'o Automais.Zap entrega à Meta; volta o identificador da mensagem', tom: 'sistema' },
    { titulo: '7. Recibos', detalhe: 'enviada → entregue → lida (ou falha), pelo webhook', tom: 'fim' },
  ];
  return (
    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-4">
      {etapas.map((e) => (
        <Caixa key={e.titulo} tom={e.tom} titulo={e.titulo}>
          {e.detalhe}
        </Caixa>
      ))}
    </div>
  );
}

/** As 24 horas do dia (Brasília) e o que acontece com uma confirmação em cada uma. */
function FaixaDoDia() {
  const corDe = (h: number) =>
    h >= 8 && h < 18 ? 'bg-emerald-400' : h >= 18 && h < 21 ? 'bg-amber-300' : 'bg-gray-200';
  return (
    <div className="max-w-3xl space-y-2">
      <div className="flex overflow-hidden rounded-md border border-gray-200">
        {Array.from({ length: 24 }, (_, h) => (
          <div key={h} className={`h-8 flex-1 ${corDe(h)} border-r border-white/60 last:border-r-0`} title={`${h}h`} />
        ))}
      </div>
      <div className="flex text-[10px] text-gray-500">
        {Array.from({ length: 24 }, (_, h) => (
          <div key={h} className="flex-1 text-center">
            {h % 3 === 0 ? `${h}h` : ''}
          </div>
        ))}
      </div>
      <div className="flex flex-wrap gap-3 text-xs text-gray-700">
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-3 rounded-sm bg-emerald-400" /> sai (janela padrão 08h–18h)
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-3 rounded-sm bg-amber-300" /> só a confirmação de atendimento de AMANHÃ (até 21h)
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-3 rounded-sm bg-gray-200" /> empilha e sai quando a janela abrir
        </span>
      </div>
    </div>
  );
}

/** Linha do tempo de um agendamento de número NÃO verificado, com a régua ligada. */
function LinhaDoTempo() {
  const marcos: { dia: number; titulo: string; detalhe: string; tom: Tom }[] = [
    { dia: -12, titulo: 'D-12 · Importação', detalhe: 'primeira mensagem curta', tom: 'sistema' },
    { dia: -9, titulo: 'D-9 · Reforço', detalhe: '72h sem resposta', tom: 'espera' },
    { dia: -6, titulo: 'D-6 · Orientação', detalhe: '72h após o reforço (1ª vez)', tom: 'alerta' },
    { dia: -2, titulo: 'D-2 · Lembrete', detalhe: 'só para quem já confirmou ou se identificou', tom: 'pessoa' },
    { dia: 0, titulo: 'D · Atendimento', detalhe: 'nada automático de confirmação', tom: 'fim' },
  ];
  const pos = (d: number) => `${((d + 13) / 14) * 100}%`;
  return (
    <div className="max-w-3xl">
      <div className="relative mx-2 mb-2 mt-6 h-1.5 rounded-full bg-gray-200">
        {marcos.map((m) => (
          <span
            key={m.dia}
            className="absolute -top-1.5 h-4 w-4 -translate-x-1/2 rounded-full border-2 border-white bg-primary-600 shadow"
            style={{ left: pos(m.dia) }}
          />
        ))}
      </div>
      <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-5">
        {marcos.map((m) => (
          <Caixa key={m.dia} tom={m.tom} titulo={m.titulo}>
            {m.detalhe}
          </Caixa>
        ))}
      </div>
    </div>
  );
}

/** Tentativas e espera entre elas (backoff 5 · 2^(n-1) minutos). */
const DADOS_TENTATIVAS = [
  { tentativa: '1ª', espera: 0, acumulado: 0 },
  { tentativa: '2ª', espera: 5, acumulado: 5 },
  { tentativa: '3ª', espera: 10, acumulado: 15 },
  { tentativa: '4ª', espera: 20, acumulado: 35 },
  { tentativa: '5ª', espera: 40, acumulado: 75 },
];

function GraficoTentativas() {
  return (
    <div className="h-64 max-w-3xl rounded-lg border border-gray-200 bg-white p-3">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={DADOS_TENTATIVAS} margin={{ top: 18, right: 16, bottom: 4, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
          <XAxis dataKey="tentativa" tick={{ fontSize: 12 }} />
          <YAxis tick={{ fontSize: 12 }} unit=" min" width={56} />
          <Tooltip
            formatter={(v: number, nome: string) => [`${v} min`, nome === 'espera' ? 'espera antes' : 'desde a 1ª']}
          />
          <Bar dataKey="espera" fill="rgb(var(--c-primary-600))" radius={[4, 4, 0, 0]} name="espera">
            <LabelList dataKey="espera" position="top" fontSize={11} formatter={(v: number) => (v ? `+${v}` : '')} />
          </Bar>
          <Line dataKey="acumulado" stroke="#6b7280" strokeDasharray="4 3" dot={{ r: 3 }} name="acumulado" />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}

/** Os estados de uma mensagem e para onde cada um vai. */
function MapaDeEstados() {
  return (
    <div className="max-w-3xl space-y-3">
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-4">
        <Caixa tom="espera" titulo={<SeloRef cor="gray">Na fila</SeloRef>}>esperando a passada, a janela ou a próxima tentativa</Caixa>
        <Caixa tom="sistema" titulo={<SeloRef cor="info">Enviada</SeloRef>}>a Meta aceitou</Caixa>
        <Caixa tom="sistema" titulo="Entregue">chegou ao aparelho</Caixa>
        <Caixa tom="fim" titulo="Lida">o paciente abriu</Caixa>
      </div>
      <p className="text-[11px] italic text-gray-500">
        → o caminho feliz, da esquerda para a direita. Uma falha avisada pela Meta depois do aceite volta a mensagem
        para "Na fila" enquanto houver tentativa.
      </p>
      <Sub>Esperando alguém (não é erro, e sai sozinha quando a condição muda)</Sub>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
        <Caixa tom="espera" titulo="Aguardando o paciente se identificar">
          a primeira mensagem curta saiu; os dados ficam retidos até a identificação
        </Caixa>
        <Caixa tom="espera" titulo="Aguardando contato verificado">
          exame/laudo pronto, mas o número não é verificado — sai quando a recepção verificar
        </Caixa>
        <Caixa tom="espera" titulo="Número inválido (não é do paciente)">
          quem atende negou ser o paciente — sai quando a pendência for resolvida
        </Caixa>
      </div>
      <Sub>Encerradas</Sub>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-4">
        <Caixa tom="alerta" titulo="Falha">tentativas esgotadas, erro permanente, modelo incompatível ou o envio perdeu o sentido</Caixa>
        <Caixa tom="alerta" titulo="Sem celular válido">nenhum celular brasileiro no cadastro</Caixa>
        <Caixa tom="sistema" titulo="Atendida por pessoa">uma atendente assumiu em Confirmações</Caixa>
        <Caixa tom="sistema" titulo="Dispensada">coberta por outra, ou deixou de fazer sentido — o motivo diz qual</Caixa>
      </div>
    </div>
  );
}

// ---------- o artigo ----------

export const artigoEnvioMensagens: Artigo = {
  slug: 'envio-mensagens',
  titulo: 'Como as mensagens ao paciente são enviadas',
  resumo:
    'A engrenagem do WhatsApp automático: o que dispara cada mensagem, a fila, o horário, a escolha do modelo, a régua de reforço, as tentativas e o que fazer quando uma mensagem falha.',
  grupo: 'atendimento',
  icone: Send,
  publico: 'Gestão e equipe técnica — quem configura o automático e quem investiga por que uma mensagem não chegou',
  atualizadoEm: '2026-10-08',
  palavrasChave: [
    'envio',
    'mensagem',
    'whatsapp',
    'fila',
    'worker',
    'passada',
    'vazão',
    'janela',
    'horário',
    'véspera',
    '21h',
    'mesmo dia',
    'tentativas',
    'retentativa',
    'backoff',
    'falha',
    'erro',
    'meta',
    'relay',
    'automais zap',
    'recibo',
    'entregue',
    'lida',
    'modelo',
    'template',
    'confirmação',
    'lembrete',
    'reforço',
    'orientação ao posto',
    'retorno',
    'primeira vez',
    'cancelamento',
    'exame liberado',
    'laudo pronto',
    'reenviar',
    'assumo o risco',
    'número inválido',
    'sem celular',
    'dispensada',
    '131026',
  ],
  secoes: () => [
    {
      id: 'caminho',
      titulo: 'O caminho de uma mensagem',
      busca: 'caminho esteira fila passada gatilho relay meta recibo visão geral como funciona',
      conteudo: (
        <div className="space-y-4">
          <P>
            Nenhuma mensagem automática sai no momento em que algo acontece. O que acontece (um agendamento importado,
            um laudo assinado) só <strong>põe uma linha na fila</strong>. Quem envia é uma passada que roda a cada
            minuto, olha o estado <strong>daquele instante</strong> e decide se ainda faz sentido, para qual número e
            com qual modelo. É por isso que uma mensagem pode ficar dias na fila e, ao sair, já levar o texto certo
            para a situação de hoje.
          </P>
          <Legenda />
          <Esteira />
          <Callout tipo="regra" titulo="Uma linha por solicitação e finalidade">
            Cada agendamento tem no máximo uma linha de cada tipo (uma confirmação, um lembrete, um reforço…). Reenviar
            não cria outra linha: rearma a mesma. A tela <AbaRef>Envios</AbaRef> da Mensageria mostra exatamente essas
            linhas.
          </Callout>
        </div>
      ),
    },
    {
      id: 'finalidades',
      titulo: 'O que dispara cada mensagem',
      busca:
        'finalidade gatilho confirmação de agendamento lembrete aviso de cancelamento reforço orientação ao posto exame liberado laudo pronto modelo confirmacao_regulacao confirmar_agendamento_urlapp confirmacao_exame confirmacao_consulta agendamento_proximo agendamento_cancelado_anonimo agendamento_aviso_pendente agendamento_aguardando_resposta agendamento_procure_posto exame_liberado laudo_disponivel',
      conteudo: (
        <div className="space-y-4">
          <div className="max-w-4xl overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full text-left text-xs">
              <thead className="bg-gray-50 text-gray-700">
                <tr>
                  <th className="px-3 py-2 font-semibold">Mensagem</th>
                  <th className="px-3 py-2 font-semibold">O que dispara</th>
                  <th className="px-3 py-2 font-semibold">Modelo (Meta)</th>
                  <th className="px-3 py-2 font-semibold">Horário</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-gray-700">
                <tr>
                  <td className="px-3 py-2 font-medium">Confirmação de agendamento</td>
                  <td className="px-3 py-2">
                    Importação do SISREG, se a unidade <em>e</em> o procedimento estão com o aviso ligado (ou há
                    campanha com envio automático). Também o lote e o reenvio.
                  </td>
                  <td className="px-3 py-2">
                    não verificado: <code>confirmacao_exame</code>/<code>confirmacao_consulta</code> (curta)
                    <br />
                    verificado, 1ª vez: <code>confirmacao_regulacao</code>
                    <br />
                    verificado, retorno ou campanha: <code>confirmar_agendamento_urlapp</code>
                  </td>
                  <td className="px-3 py-2">janela + véspera até 21h</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 font-medium">Lembrete</td>
                  <td className="px-3 py-2">
                    Os dias antes configurados em Regras. Não sai se a primeira mensagem tem menos de 7 dias, se a
                    pessoa disse que não vai, ou depois da orientação ao posto / "Vou ao posto".
                  </td>
                  <td className="px-3 py-2">
                    já confirmou: <code>agendamento_proximo</code>
                    <br />
                    não respondeu: a curta (não verificado) ou a confirmação completa (verificado)
                  </td>
                  <td className="px-3 py-2">janela</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 font-medium">Aviso de cancelamento</td>
                  <td className="px-3 py-2">Cancelamento feito aqui ou trazido do SISREG — só os feitos depois de ligar o aviso.</td>
                  <td className="px-3 py-2">
                    <code>agendamento_cancelado_anonimo</code>
                  </td>
                  <td className="px-3 py-2">janela; não sai se o horário já passou</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 font-medium">Reforço</td>
                  <td className="px-3 py-2">72h depois da primeira mensagem curta, para quem não escreveu nada.</td>
                  <td className="px-3 py-2">
                    <code>agendamento_aviso_pendente</code> (não leu; retorno sempre) ou{' '}
                    <code>agendamento_aguardando_resposta</code>
                  </td>
                  <td className="px-3 py-2">janela; nunca no domingo</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 font-medium">Orientação ao posto</td>
                  <td className="px-3 py-2">72h depois do reforço/lembrete, ou nos dias do lembrete. Nunca a menos de 24h. Nunca para retorno.</td>
                  <td className="px-3 py-2">
                    <code>agendamento_procure_posto</code>
                  </td>
                  <td className="px-3 py-2">janela; nunca no domingo</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 font-medium">Exame liberado / Laudo pronto</td>
                  <td className="px-3 py-2">Exame realizado / laudo assinado. Só para número verificado.</td>
                  <td className="px-3 py-2">
                    <code>exame_liberado</code> / <code>laudo_disponivel</code>
                  </td>
                  <td className="px-3 py-2">qualquer hora</td>
                </tr>
              </tbody>
            </table>
          </div>
          <Callout tipo="lgpd" titulo="Número não verificado nunca recebe dado do agendamento de saída">
            A primeira mensagem para um número que ainda não provou ser do paciente só diz que existe um agendamento. O
            procedimento, a data e o local vêm depois que a pessoa se identifica — o passo a passo está no artigo{' '}
            <em>Como o WhatsApp decide</em>. Resultado de exame e laudo vão só para número verificado.
          </Callout>
        </div>
      ),
    },
    {
      id: 'horario',
      titulo: 'O horário: janela, véspera e o próprio dia',
      busca: 'janela horário 8h 18h 21h véspera atendimento de amanhã empilha mesmo dia atendimento hoje primeira vez dispensada importação 18h',
      conteudo: (
        <div className="space-y-4">
          <P>
            Mensagem sobre agenda (confirmação, lembrete, cancelamento, reforço, orientação) só sai dentro da janela do
            menu Regras — <strong>08h às 18h</strong> por padrão. Fora dela, ela <strong>empilha</strong>: não gasta
            tentativa e sai quando a janela abre. Resultado de exame e laudo saem a qualquer hora.
          </P>
          <FaixaDoDia />
          <Sub>Duas exceções, as duas por causa da importação das 18h</Sub>
          <P>
            A importação do SISREG roda logo depois das 18h, com a janela já fechada. Sem tratamento, o agendamento da
            manhã seguinte esperava a janela abrir e chegava no próprio dia — às vezes uma hora antes da consulta.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Véspera até 21h',
                descricao:
                  'A confirmação de um atendimento de AMANHÃ ainda sai depois que a janela fechou, até as 21h. Só ela — as outras mensagens seguem a janela.',
              },
              {
                termo: 'Primeira vez de hoje não sai',
                descricao:
                  'A confirmação automática de primeira vez para um atendimento de HOJE vira Dispensada ("Atendimento hoje"): ela pede para retirar a guia antes do dia, o que no próprio dia não dá. A ficha segue em Confirmações para a equipe ligar.',
              },
              {
                termo: 'Saem mesmo no dia',
                descricao:
                  'Retorno (só lembra local e hora), campanha, envio manual e lote (decisão de uma pessoa) e os dados para quem acabou de se identificar (foi ela quem pediu — e esses ignoram a janela).',
              },
            ]}
          />
        </div>
      ),
    },
    {
      id: 'modelo',
      titulo: 'Qual texto sai: a escolha do modelo',
      busca: 'escolha do modelo qual texto campanha retorno primeira vez verificado não verificado guia posto endereço da unidade',
      conteudo: (
        <div className="space-y-4">
          <P>
            O modelo da confirmação é decidido no instante do envio, nesta ordem. É o que garante, por exemplo, que um
            paciente de retorno não seja mandado ao posto buscar uma guia que ele já tem.
          </P>
          <div className="max-w-md space-y-0.5">
            <Caixa tom="sistema" titulo="Confirmação pronta para sair" />
            <Seta rotulo="há campanha vigente na unidade e data?" />
            <Caixa tom="decisao" titulo="Campanha?" />
            <Ramo>
              <Seta rotulo="SIM" />
              <Caixa tom="fim" titulo="confirmar_agendamento_urlapp">
                com o local e o endereço da campanha
              </Caixa>
            </Ramo>
            <Seta rotulo="NÃO — o número é verificado?" />
            <Caixa tom="decisao" titulo="Número verificado?" />
            <Ramo>
              <Seta rotulo="NÃO" />
              <Caixa tom="espera" titulo="Primeira mensagem curta">
                "Sua consulta foi agendada!" — os dados esperam a identificação
              </Caixa>
            </Ramo>
            <Seta rotulo="SIM — a vaga é de retorno no SISREG?" />
            <Caixa tom="decisao" titulo="Retorno?" />
            <Ramo>
              <Seta rotulo="SIM" />
              <Caixa tom="fim" titulo="confirmar_agendamento_urlapp">
                "você tem um retorno de … local: unidade · Endereço: …" — sem guia
              </Caixa>
            </Ramo>
            <Seta rotulo="NÃO (primeira vez)" />
            <Caixa tom="fim" titulo="confirmacao_regulacao">
              "Boas notícias! … retire a guia no posto … leve pedido médico, guia, comprovante e cartão do SUS"
            </Caixa>
          </div>
          <Callout tipo="dica" titulo="O texto dos modelos é fixo">
            Os modelos são aprovados na Meta: o sistema só preenche as variáveis (nome, procedimento, data…). Mudar uma
            frase — tirar a guia, trocar a saudação — exige submeter um modelo novo e esperar a aprovação. Por isso o
            retorno troca de <em>modelo</em>, em vez de "apagar" a frase da guia.
          </Callout>
        </div>
      ),
    },
    {
      id: 'guardas',
      titulo: 'Antes de sair: as guardas',
      busca: 'guardas impedimento solicitação cancelada data passou já respondeu só sisreg aviso retroativo número negado pendência sem celular atendente assumiu silêncio por número',
      conteudo: (
        <div className="space-y-4">
          <P>Na passada, cada linha atravessa estas perguntas. A primeira que barra decide o destino dela.</P>
          <div className="max-w-3xl space-y-1.5">
            <Caixa tom="alerta" titulo="A solicitação foi cancelada, excluída, o horário passou, ou o paciente já respondeu?">
              → <strong>Falha</strong>, com o motivo. Não há o que avisar.
            </Caixa>
            <Caixa tom="alerta" titulo="Primeira vez, automática, atendimento hoje?">
              → <strong>Dispensada</strong> ("Atendimento hoje").
            </Caixa>
            <Caixa tom="espera" titulo="Fora da janela de horário?">
              → continua <strong>Na fila</strong> até a janela abrir, sem gastar tentativa.
            </Caixa>
            <Caixa tom="espera" titulo="Exame/laudo para número não verificado?">
              → <strong>Aguardando contato verificado</strong> (ou segue, se a recepção registrou dispensa com canal).
            </Caixa>
            <Caixa tom="alerta" titulo="Nenhum celular brasileiro no cadastro?">
              → <strong>Sem celular válido</strong>, e o cadastro ganha a marca de telefone comprometido.
            </Caixa>
            <Caixa tom="espera" titulo="O número foi negado (quem atende disse que não conhece o paciente)?">
              → <strong>Número inválido</strong>, até a pendência ser resolvida em Pendências de Cadastro.
            </Caixa>
            <Caixa tom="decisao" titulo="Número não verificado, confirmação?">
              → sai a <strong>primeira mensagem curta</strong> e a linha fica aguardando a identificação.
            </Caixa>
            <Caixa tom="fim" titulo="Passou por tudo">
              → escolhe o modelo e entrega ao relay.
            </Caixa>
          </div>
          <P>
            O telefone é escolhido nesta ordem: o <strong>verificado</strong>; senão o primeiro celular entre o principal,
            o campo celular e o residencial — pulando o número negado.
          </P>
        </div>
      ),
    },
    {
      id: 'regua',
      titulo: 'Quem não se identifica: a régua de reforço',
      busca: 'régua reforço orientação ao posto linha do tempo 72 horas domingo silêncio 48 horas um por número retorno lembrete ocupa',
      conteudo: (
        <div className="space-y-4">
          <P>
            Para o número não verificado que recebeu a primeira mensagem curta e não respondeu, a régua dá no máximo
            mais dois toques — e depois desiste de insistir por mensagem. Cada uma das duas tem a sua chave em Regras.
          </P>
          <LinhaDoTempo />
          <ListaDefinicoes
            itens={[
              {
                termo: 'Reforço (toque 2)',
                descricao:
                  '72h depois da primeira mensagem, se o número não escreveu nada e o atendimento está a 3 dias ou mais. Retorno sai sempre pelo modelo sem a guia; sem esse modelo aprovado, o retorno fica sem reforço (Dispensada).',
              },
              {
                termo: 'Orientação ao posto (toque 3)',
                descricao:
                  '72h depois do toque 2, ou quando faltam os dias do lembrete — o que vier primeiro. Nunca a menos de 24h. Retorno não recebe (ela manda retirar a guia).',
              },
              {
                termo: 'Freios por número',
                descricao:
                  'Um toque por número por passada; nada se o número recebeu outro automático nas últimas 48h; reforço no máximo a cada 7 dias e orientação a cada 20 por número. Domingo, nada.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="Com o reforço ligado, o lembrete deixa de ir para quem não se identificou">
            A régua passa a ser o segundo toque dessas pessoas: dois caminhos insistindo com o mesmo número é o que faz a
            conta ser bloqueada. Quem já confirmou continua recebendo o lembrete normalmente.
          </Callout>
        </div>
      ),
    },
    {
      id: 'falhas',
      titulo: 'Quando falha: tentativas e erros',
      busca: 'falha erro tentativas retentativa backoff 5 10 20 40 minutos permanente transitório 131026 131030 não é whatsapp modelo incompatível variáveis catálogo relay fora do ar',
      conteudo: (
        <div className="space-y-4">
          <P>
            Uma linha tem até <strong>5 tentativas</strong>. Erro passageiro (Meta instável, relay fora do ar, tempo
            esgotado) reagenda com espera que dobra a cada vez. Esperar a janela, o domingo ou o silêncio do número
            <strong> não</strong> conta como tentativa.
          </P>
          <GraficoTentativas />
          <p className="max-w-3xl text-[11px] italic text-gray-500">
            Barras: espera antes de cada tentativa. Linha: minutos desde a primeira. Falhou a 5ª, a linha vira{' '}
            <strong>Falha</strong> — cerca de 1h15 depois da primeira.
          </p>
          <Sub>Erros que encerram na hora</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Número não é WhatsApp (131026 / 131030)',
                descricao:
                  'Falha imediata, e o cadastro ganha a marca de telefone comprometido ("não é WhatsApp"), que aparece na aba Telefone comprometido de Confirmações. A maioria desses chega depois do aceite, pelo recibo da Meta.',
              },
              {
                termo: 'Modelo incompatível',
                descricao:
                  'O modelo aprovado pede mais variáveis do que o sistema monta: Falha antes de chamar a Meta, com o nome do modelo. Variável a mais é cortada sozinha.',
              },
              {
                termo: 'Modelo com imagem sem arte',
                descricao: 'Recusado antes da Meta: a arte do cabeçalho se define em Mensageria → Modelos.',
              },
            ]}
          />
          <Sub>Aceita e depois falhou</Sub>
          <P>
            A Meta pode aceitar a mensagem e, minutos depois, avisar pelo recibo que não conseguiu entregar. Nesse
            caso a linha <strong>volta para a fila</strong> com a mesma espera crescente, enquanto houver tentativa — e
            vira Falha quando acabam, ou na hora se o erro for de número que não é WhatsApp.
          </P>
          <Sub>Os estados</Sub>
          <MapaDeEstados />
        </div>
      ),
    },
    {
      id: 'o-que-fazer',
      titulo: 'O que fazer com uma mensagem que não saiu',
      busca: 'o que fazer reenviar revogar e reenviar assumo o risco corrigir telefone pendências de cadastro motivo envios detalhe',
      conteudo: (
        <div className="space-y-4">
          <P>
            Comece pelo motivo: <AbaRef>Envios</AbaRef> da Mensageria → clique no paciente → o detalhe mostra a linha do
            tempo, o erro da Meta e o conteúdo exato que saiu.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Falha por erro passageiro',
                descricao: (
                  <>
                    <BotaoRef>Reenviar</BotaoRef> no detalhe do envio. Volta para a fila e sai na próxima passada, dentro
                    da janela. Recusado se a data já passou ou o paciente já respondeu.
                  </>
                ),
              },
              {
                termo: 'Número errado / não é WhatsApp / sem celular',
                descricao: (
                  <>
                    Corrija o telefone no cadastro e use <BotaoRef>Reenviar</BotaoRef>: cada tentativa escolhe o número do
                    cadastro de novo. (Quando a correção vem da rotina do e-SUS, as mensagens do agendamento futuro são
                    rearmadas sozinhas.)
                  </>
                ),
              },
              {
                termo: 'Número inválido (negado)',
                descricao: 'Resolva ou ignore a pendência em Pendências de Cadastro: a mensagem é liberada e o número é escolhido de novo.',
              },
              {
                termo: 'Aguardando contato verificado',
                descricao: (
                  <>
                    Verifique o contato na recepção, ou registre a dispensa. Para mandar mesmo assim, o detalhe da
                    solicitação tem <BotaoRef>Assumo o risco e enviar</BotaoRef> — vale para um envio só.
                  </>
                ),
              },
              {
                termo: 'Quer refazer do zero',
                descricao: (
                  <>
                    No histórico de comunicações da solicitação, <BotaoRef>Revogar e reenviar</BotaoRef>: invalida o link
                    anterior, zera o contador e envia na hora.
                  </>
                ),
              },
              {
                termo: 'Dispensada',
                descricao: 'Não é erro: o motivo diz por que deixou de fazer sentido. Se precisar falar com o paciente, use Confirmações.',
              },
            ]}
          />
        </div>
      ),
    },
    {
      id: 'configuracao',
      titulo: 'Onde se ajusta cada coisa',
      busca: 'configuração regras parâmetros vazão mensagens por rodada janela lembrete dias antes chaves variável de ambiente ComunicacaoPaciente MaxTentativas HoraLimiteVesperaConfirmacao ReforcoAposHoras',
      conteudo: (
        <div className="space-y-4">
          <Sub>Na tela (Mensageria → Regras)</Sub>
          <Lista>
            <Item>
              <strong>Começa / Para de enviar às</strong> — a janela. <strong>Mensagens por rodada</strong> — a vazão por
              minuto. <strong>Só agendamentos do SISREG</strong>.
            </Item>
            <Item>
              <strong>Lembrete</strong> (ligado e dias antes), <strong>reforço</strong>, <strong>orientação ao posto</strong>{' '}
              e <strong>aviso de cancelamento</strong> — cada um com a sua chave.
            </Item>
            <Item>
              Por unidade e por procedimento: a chave de aviso no mapeamento do SISREG. As duas precisam estar ligadas
              para a confirmação nascer.
            </Item>
          </Lista>
          <Sub>No ambiente do servidor (prefixo ComunicacaoPaciente__)</Sub>
          <ListaDefinicoes
            itens={[
              { termo: 'MaxTentativas', descricao: '5 — tentativas antes de Falha.' },
              { termo: 'IntervaloSegundos', descricao: '60 — de quanto em quanto tempo roda a passada.' },
              { termo: 'HoraLimiteVesperaConfirmacao', descricao: '21:00 — até quando sai a confirmação de amanhã; vazio desliga.' },
              { termo: 'ReforcoAposHoras / OrientacaoAposReforcoHoras', descricao: '72 / 72 — os prazos da régua.' },
              { termo: 'SilencioMinimoPorNumeroHoras', descricao: '48 — nenhum toque da régua se o número recebeu outro automático há menos que isso.' },
              { termo: 'ReforcoPorNumeroDias / OrientacaoPorNumeroDias', descricao: '7 / 20 — teto por número.' },
              { termo: 'LembreteIntervaloMinimoDias', descricao: '7 — o lembrete se cala se a primeira mensagem é mais nova que isso.' },
              { termo: 'Template…', descricao: 'o nome de cada modelo na Meta — trocar um modelo aprovado é mudar aqui.' },
            ]}
          />
          <Callout tipo="atencao" titulo="Mudança no ambiente pede reinício do serviço">
            Os valores da tela valem na próxima passada. Os do ambiente só depois de reiniciar o servidor.
          </Callout>
        </div>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'dúvidas por que não chegou mensagem parada na fila retorno guia mesmo dia lembrete não saiu',
      conteudo: (
        <div className="space-y-4">
          <ListaDefinicoes
            itens={[
              {
                termo: 'A mensagem está "Na fila" há horas.',
                descricao:
                  'Quase sempre é a janela (fora do horário, espera) ou a espera entre tentativas — o detalhe mostra a próxima tentativa. Com fila grande, a vazão por minuto também segura.',
              },
              {
                termo: 'O paciente de retorno recebeu "retire a guia no posto".',
                descricao:
                  'Confira se a vaga veio como retorno do SISREG (a coluna de vaga da solicitação). Se veio como primeira vez, o sistema trata como primeira vez.',
              },
              {
                termo: 'Por que o lembrete não saiu?',
                descricao:
                  'A primeira mensagem saiu há menos de 7 dias, a pessoa disse que não vai, foi orientada ao posto, ou — com o reforço ligado — ainda não se identificou e está com a régua.',
              },
              {
                termo: 'A confirmação de hoje aparece como Dispensada.',
                descricao: 'É a regra da primeira vez no próprio dia. A equipe confirma por telefone em Confirmações.',
              },
            ]}
          />
        </div>
      ),
    },
  ],
};
