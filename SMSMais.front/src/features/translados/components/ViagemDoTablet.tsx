import { AlertTriangle, CheckCircle2, Flag, LogIn, LogOut, Play, Tablet, UserX } from 'lucide-react';
import { Link } from 'react-router-dom';
import { usePermissao } from '@/shared/auth/authStore';
import { useViagemDaRota } from '@/features/translados/api/queries';
import type { AlocacaoDto, EventoViagem, StatusRota, TipoEventoViagem } from '@/features/translados/types';

const ICONES: Record<TipoEventoViagem, typeof Play> = {
  Inicio: Play,
  Embarque: LogIn,
  Ausencia: UserX,
  Desembarque: LogOut,
  Conclusao: Flag,
};

function hora(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function quem(e: EventoViagem): string {
  const paciente = e.pacienteNome ?? 'Paciente';
  return e.pessoa === 'Acompanhante' ? `${e.acompanhanteNome ?? 'Acompanhante'} (acompanhante de ${paciente})` : paciente;
}

function descrever(e: EventoViagem): string {
  if (e.volta) {
    if (e.tipo === 'Embarque') return `Embarcou na volta: ${quem(e)}`;
    if (e.tipo === 'Ausencia') return `Não estava na unidade na hora da volta: ${quem(e)}`;
    if (e.tipo === 'Desembarque') return `Chegou em casa: ${quem(e)}`;
  }
  switch (e.tipo) {
    case 'Inicio':
      return e.motoristaNome ? `Viagem começou · ${e.motoristaNome} ao volante` : 'Viagem começou';
    case 'Embarque':
      return `Embarcou: ${quem(e)}`;
    case 'Ausencia':
      return `Não estava na coleta: ${quem(e)}`;
    case 'Desembarque':
      return `Desceu no destino: ${quem(e)}`;
    default:
      return 'Viagem concluída';
  }
}

/** Situação de cada paciente: a correção da central, quando houve; senão, a última marcação do tablet. */
function situacaoDoPaciente(eventos: EventoViagem[], a: AlocacaoDto): { texto: string; cor: string } {
  if (a.corrigidaNoPainel) {
    return a.statusSessao === 'Realizada'
      ? { texto: 'Corrigido pela central: transporte feito', cor: 'text-emerald-700' }
      : { texto: 'Corrigido pela central: não realizada', cor: 'text-red-700' };
  }
  const doPaciente = eventos.filter((e) => e.sessaoId === a.sessaoId && e.pessoa === 'Paciente' && e.aplicado);
  const ultima = doPaciente[doPaciente.length - 1];
  if (!ultima) return { texto: 'Sem marcação do tablet', cor: 'text-gray-500' };
  if (ultima.volta) {
    if (ultima.tipo === 'Desembarque') return { texto: `Voltou para casa às ${hora(ultima.ocorridoEm)}`, cor: 'text-emerald-700' };
    if (ultima.tipo === 'Ausencia') {
      return a.statusSessao === 'Realizada'
        ? { texto: 'Ficou na unidade; voltou por outra rota', cor: 'text-emerald-700' }
        : { texto: 'Ficou na unidade: aguardando a volta', cor: 'text-amber-700' };
    }
    return { texto: `Na volta desde ${hora(ultima.ocorridoEm)}`, cor: 'text-amber-700' };
  }
  if (ultima.tipo === 'Desembarque') return { texto: `Transporte feito · desceu às ${hora(ultima.ocorridoEm)}`, cor: 'text-emerald-700' };
  if (ultima.tipo === 'Ausencia') return { texto: `Não estava na coleta (${hora(ultima.ocorridoEm)})`, cor: 'text-red-700' };
  return { texto: `A bordo desde ${hora(ultima.ocorridoEm)}`, cor: 'text-amber-700' };
}

type Props = {
  rotaId: string;
  status: StatusRota;
  alocacoes: AlocacaoDto[];
};

/**
 * O que o tablet do veículo registrou na viagem e a situação de cada paciente. O que conta é o
 * transporte feito: o desembarque no destino marca a sessão como realizada e entra no faturamento;
 * a ausência na coleta, como não realizada. Errado? "Corrigir" abre a sessão no atendimento — e o
 * que a central corrige o tablet não muda mais.
 */
export function ViagemDoTablet({ rotaId, status, alocacoes }: Props) {
  const viagem = useViagemDaRota(rotaId, status === 'EmAndamento');
  const podeCorrigir = usePermissao('Tratamentos', 'Edicao');
  const eventos = viagem.data ?? [];
  // Uma linha por paciente (quem foi puxado para a volta também tem alocação nesta rota).
  const pacientes = alocacoes.filter((a, i, todas) => a.tipo === 'Paciente'
    && todas.findIndex((b) => b.tipo === 'Paciente' && b.sessaoId === a.sessaoId) === i);

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <div className="mb-4 flex items-center gap-2">
        <Tablet className="h-4 w-4 text-gray-500" />
        <h2 className="text-sm font-semibold text-gray-900">Viagem registrada pelo tablet</h2>
        {status === 'EmAndamento' ? <span className="text-xs text-gray-500">· atualiza sozinha</span> : null}
      </div>

      {pacientes.length > 0 ? (
        <ul className="mb-5 divide-y divide-gray-100 rounded-md border border-gray-100">
          {pacientes.map((a) => {
            const s = situacaoDoPaciente(eventos, a);
            return (
              <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                <span className="font-medium text-gray-900">{a.pacienteNome}</span>
                <span className={`flex-1 text-right ${s.cor}`}>{s.texto}</span>
                {podeCorrigir ? (
                  <Link
                    to={`/app/tratamentos/${a.tratamentoId}?sessao=${a.sessaoId}`}
                    className="text-xs font-medium text-primary-700 hover:underline"
                  >
                    Corrigir
                  </Link>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}

      {viagem.isLoading ? (
        <p className="text-sm text-gray-500">Carregando a viagem…</p>
      ) : viagem.isError ? (
        <p className="text-sm text-red-700">Não foi possível carregar a viagem do tablet.</p>
      ) : eventos.length === 0 ? (
        <p className="text-sm text-gray-500">
          O tablet ainda não registrou nada desta rota. A viagem aparece aqui quando o motorista começa a navegação.
        </p>
      ) : (
        <ol className="space-y-2">
          {eventos.map((e) => {
            const Icone = e.aplicado ? ICONES[e.tipo] : AlertTriangle;
            return (
              <li key={e.id} className="flex items-start gap-3 text-sm">
                <span className="w-12 shrink-0 tabular-nums text-gray-500">{hora(e.ocorridoEm)}</span>
                <Icone className={`mt-0.5 h-4 w-4 shrink-0 ${e.aplicado ? 'text-gray-500' : 'text-amber-600'}`} />
                <div className="min-w-0">
                  <div className="text-gray-900">{descrever(e)}</div>
                  {!e.aplicado && e.motivoNaoAplicado ? (
                    <div className="text-xs text-amber-700">Não mudou a viagem: {e.motivoNaoAplicado}</div>
                  ) : null}
                </div>
              </li>
            );
          })}
        </ol>
      )}

      {status === 'Concluida' ? (
        <p className="mt-4 flex items-center gap-1.5 text-xs text-gray-500">
          <CheckCircle2 className="h-3.5 w-3.5" /> Translados feitos entram no Faturamento; o que for corrigido aqui sai dele.
        </p>
      ) : null}
    </section>
  );
}
