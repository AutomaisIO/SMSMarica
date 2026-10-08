import { AlertTriangle } from 'lucide-react';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import type { FrescorBaseSisreg } from '@/features/sisreg/api/sisregBaseApi';

/** Quantos dias/unidades a faixa lista antes de resumir em "e mais N". */
const CITADOS = 8;

function quando(iso: string | null): string {
  return iso ? formatarInstante(iso, { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : '';
}

/**
 * Faixa da Consulta quando a base ainda não leu tudo do SISREG no recorte. Sem ela, quem faltou num
 * dia cuja lista de faltas não entrou aparece como "Pendente de atualização" — e o relatório vira
 * cobrança à unidade por falta que ela já apontou (07/10/2026, fisioterapia de setembro).
 */
export function AvisoFrescorBase({ frescor }: { frescor: FrescorBaseSisreg | undefined }) {
  if (!frescor || (frescor.diasSemFaltas.length === 0 && frescor.chegadasAtrasadas.length === 0)) return null;
  const dias = frescor.diasSemFaltas;
  const unidades = frescor.chegadasAtrasadas;

  return (
    <div role="alert" className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900">
      <p className="flex items-center gap-2 font-medium">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        A base ainda não leu tudo do SISREG neste período — parte do “Pendente de atualização” pode estar errada.
      </p>
      {dias.length > 0 ? (
        <p className="mt-1">
          <span className="font-medium">Lista de faltas não lida</span> (quem faltou aparece como pendente):{' '}
          {dias.slice(0, CITADOS).map((d, i) => (
            <span key={d.dia} title={d.ultimaLeitura ? `Última leitura em ${quando(d.ultimaLeitura)}` : 'Nunca lida'}>
              {i > 0 ? ', ' : ''}
              {formatarWallClock(d.dia)} ({d.agendamentos} em aberto
              {d.ultimaLeitura ? `, lida em ${quando(d.ultimaLeitura)}` : ', nunca lida'})
            </span>
          ))}
          {dias.length > CITADOS ? ` e mais ${dias.length - CITADOS}` : ''}.
        </p>
      ) : null}
      {unidades.length > 0 ? (
        <p className="mt-1">
          <span className="font-medium">Chegada não relida há mais de {frescor.horasParaAtraso} h</span> (quem
          compareceu e foi confirmado depois aparece como pendente):{' '}
          {unidades.slice(0, CITADOS).map((u, i) => (
            <span key={u.unidadeId}>
              {i > 0 ? '; ' : ''}
              {u.unidade} ({u.agendamentos} em aberto
              {u.ultimaLeitura ? `, desde ${quando(u.ultimaLeitura)}` : ', nunca relida'})
            </span>
          ))}
          {unidades.length > CITADOS ? ` e mais ${unidades.length - CITADOS}` : ''}.
        </p>
      ) : null}
      <p className="mt-1 text-xs text-amber-800">
        A leitura acontece sozinha (faltas de hora em hora, chegadas toda noite). O mesmo aviso sai impresso no
        PDF e vai para o celular do responsável, conferido de hora em hora das 07:00 às 18:00.
      </p>
    </div>
  );
}
