import { useState } from 'react';
import { CalendarCheck2, DownloadCloud, MessageCircle, PhoneCall, Users } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Etapa = {
  id: string;
  rotulo: string;
  icone: LucideIcon;
  resumo: string;
  detalhe: string;
};

const ETAPAS: Etapa[] = [
  {
    id: 'sisreg',
    rotulo: 'Agendou no SISREG',
    icone: CalendarCheck2,
    resumo: 'A vaga nasce lá.',
    detalhe:
      'Quem marca a consulta ou o exame é o SISREG, como sempre foi. Nada nesta tela cria agendamento — ela trabalha sobre o que já está marcado.',
  },
  {
    id: 'importacao',
    rotulo: 'O sistema importa',
    icone: DownloadCloud,
    resumo: 'Entra na lista sozinho.',
    detalhe:
      'A importação traz o agendamento com paciente, procedimento, unidade e data. É nessa hora que ele passa a existir nas filas de confirmação — ninguém digita nada.',
  },
  {
    id: 'whatsapp',
    rotulo: 'WhatsApp automático',
    icone: MessageCircle,
    resumo: 'Mensagem com link.',
    detalhe:
      'O paciente recebe a mensagem com a data e um link para confirmar ou avisar que não vai. Às vésperas sai um lembrete — menos para quem acabou de ser avisado e para quem já disse que não vai.',
  },
  {
    id: 'resposta',
    rotulo: 'O paciente responde (ou não)',
    icone: Users,
    resumo: 'Aqui as filas se separam.',
    detalhe:
      'Confirmou pelo link, pelo botão, pelo robô, no app ou na recepção: vai para Confirmados. Não respondeu: fica em Não confirmados. O número não recebe: cai em Telefone comprometido. Quem atendeu disse que não é o paciente: Contato errado.',
  },
  {
    id: 'humano',
    rotulo: 'A atendente entra',
    icone: PhoneCall,
    resumo: 'O telefonema que fecha.',
    detalhe:
      'O que sobrou é trabalho humano: pegar a ficha, ligar e dar o desfecho — confirmado, pendente, contato errado ou cancelado. A partir do momento em que você pega a ficha, o automático para de tentar aquele agendamento.',
  },
];

/**
 * O caminho que um agendamento percorre até virar (ou não) uma presença.
 *
 * Existe porque quase toda dúvida de quem chega na tela é a mesma: "de onde vem essa lista?".
 * O tracejado animado entre as etapas é só isso — animação; a informação está nos rótulos.
 */
export function FluxoConfirmacao() {
  const [ativa, setAtiva] = useState<string>('resposta');
  const etapa = ETAPAS.find((e) => e.id === ativa) ?? ETAPAS[0];

  return (
    <div className="max-w-3xl space-y-3">
      <ol className="flex flex-col gap-2 md:flex-row md:items-stretch">
        {ETAPAS.map((e, i) => {
          const Icone = e.icone;
          const selecionada = e.id === ativa;
          return (
            <li key={e.id} className="flex flex-1 items-center gap-2 md:flex-col md:gap-1">
              <button
                type="button"
                onClick={() => setAtiva(e.id)}
                aria-pressed={selecionada}
                className={cn(
                  'flex w-full flex-1 flex-col items-center gap-1.5 rounded-theme-md border px-2 py-3 text-center transition-colors',
                  selecionada
                    ? 'border-primary-400 bg-primary-50 text-primary-900 shadow-sm'
                    : 'border-gray-200 bg-white text-gray-700 hover:border-primary-200 hover:bg-primary-50/50',
                )}
              >
                <span
                  className={cn(
                    'flex h-9 w-9 items-center justify-center rounded-full',
                    selecionada ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-600',
                  )}
                >
                  <Icone className="h-4 w-4" />
                </span>
                <span className="text-xs font-semibold leading-tight">{e.rotulo}</span>
                <span className="text-[11px] leading-tight text-gray-500">{e.resumo}</span>
              </button>
              {i < ETAPAS.length - 1 ? (
                <>
                  {/* Conector: horizontal no desktop, vertical no celular. */}
                  <svg className="hidden h-3 w-8 shrink-0 md:block" aria-hidden viewBox="0 0 32 12">
                    <line
                      x1="0"
                      y1="6"
                      x2="32"
                      y2="6"
                      stroke="rgb(var(--c-primary-400))"
                      strokeWidth="2"
                      strokeDasharray="6 6"
                      className="anim-fluxo"
                    />
                  </svg>
                  <svg className="h-8 w-3 shrink-0 md:hidden" aria-hidden viewBox="0 0 12 32">
                    <line
                      x1="6"
                      y1="0"
                      x2="6"
                      y2="32"
                      stroke="rgb(var(--c-primary-400))"
                      strokeWidth="2"
                      strokeDasharray="6 6"
                      className="anim-fluxo"
                    />
                  </svg>
                </>
              ) : null}
            </li>
          );
        })}
      </ol>

      <div key={etapa.id} className="anim-entrada rounded-theme-md border border-gray-200 bg-white px-4 py-3">
        <p className="text-sm font-semibold text-gray-900">{etapa.rotulo}</p>
        <p className="mt-1 text-sm leading-relaxed text-gray-600">{etapa.detalhe}</p>
      </div>
    </div>
  );
}
