import { Info, TriangleAlert } from 'lucide-react';
import { useCoberturaAgenda } from '@/features/agenda/api/queries';

function br(iso: string | null | undefined) {
  if (!iso) return '—';
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

function dias(de: string | null, ate: string | null) {
  if (!de || !ate) return 0;
  return Math.round((Date.parse(ate) - Date.parse(de)) / 86_400_000);
}

type Props = {
  /** Recorte que a tela está pedindo, para avisar quando ele passa do que foi importado. */
  de: string;
  ate: string;
};

/**
 * Diz até onde o dado existe.
 *
 * <p>Sem isto o módulo mente por omissão: em 05/09/2026 os agendamentos importados iam de 28/05 a
 * 09/10/2026, e setembro sozinho concentrava 79% dos registros — por ser o mês varrido, não por
 * ser o mês movimentado. Uma tela que plotasse isso como série temporal mostraria uma explosão de
 * demanda que nunca aconteceu.</p>
 *
 * <p>O aviso é permanente e não se fecha de propósito. É contexto para ler o número, não
 * notificação a ser dispensada.</p>
 */
export function AvisoCobertura({ de, ate }: Props) {
  const { data: c } = useCoberturaAgenda();
  if (!c) return null;

  const foraAntes = !!c.primeiroDiaAgendado && de < c.primeiroDiaAgendado;
  const foraDepois = !!c.ultimoDiaAgendado && ate > c.ultimoDiaAgendado;
  const fora = foraAntes || foraDepois;
  const futuroPendente = dias(c.ultimoDiaAgendado, c.ultimoDiaDeEscala);

  const Icone = fora ? TriangleAlert : Info;
  const cor = fora
    ? 'border-amber-300 bg-amber-50 text-amber-900'
    : 'border-sky-200 bg-sky-50 text-sky-900';

  return (
    <div className={`flex gap-2 rounded-lg border px-3 py-2 text-xs ${cor}`}>
      <Icone className="mt-0.5 h-4 w-4 shrink-0" />
      <div className="space-y-1">
        <p>
          <strong>Cobertura da importação:</strong> agendamentos de{' '}
          <strong>{br(c.primeiroDiaAgendado)}</strong> a <strong>{br(c.ultimoDiaAgendado)}</strong> (
          {c.agendamentos.toLocaleString('pt-BR')} registros).
          {futuroPendente > 0 ? (
            <>
              {' '}
              A escala do SISREG vai até <strong>{br(c.ultimoDiaDeEscala)}</strong> —{' '}
              <strong>{futuroPendente} dias</strong> de futuro ainda não varridos.
            </>
          ) : null}
        </p>

        {foraAntes ? (
          <p>
            O período pedido começa <strong>antes</strong> do primeiro agendamento importado. O que
            aparece vazio ali é falta de importação, não falta de movimento.
          </p>
        ) : null}

        {foraDepois ? (
          <p>
            O período pedido passa do último dia importado. Vaga além dessa data aparece como livre
            por <strong>falta de dado</strong> — não confunda com vaga realmente disponível.
          </p>
        ) : null}
      </div>
    </div>
  );
}
