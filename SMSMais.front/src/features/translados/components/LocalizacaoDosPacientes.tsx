import { MapPin } from 'lucide-react';
import { useLocalizacaoPacientes } from '@/features/translados/api/queries';
import type { AlocacaoDto, CompartilhamentoDaRota } from '@/features/translados/types';

function hora(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function haQuanto(iso: string): string {
  const min = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000);
  return min < 1 ? 'agora há pouco' : `há ${min} min`;
}

function descrever(c: CompartilhamentoDaRota): string {
  return [
    `compartilhando desde ${hora(c.desde)}`,
    c.ultimaPosicaoEm ? `última posição ${haQuanto(c.ultimaPosicaoEm)}` : 'ainda sem posição',
    c.precisaoM != null ? `±${Math.round(c.precisaoM)} m` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

type Props = {
  rotaId: string;
  alocacoes: AlocacaoDto[];
};

/**
 * Quem da rota compartilha a localização pelo app enquanto espera o embarque (ADR-0082): há quanto
 * tempo e com que precisão — sem a coordenada. Onde o paciente está, só o motorista vê, no mapa do
 * tablet; a central fala com ele pelo telefone. Some quando ninguém compartilha (e no servidor sem a
 * função).
 */
export function LocalizacaoDosPacientes({ rotaId, alocacoes }: Props) {
  const consulta = useLocalizacaoPacientes(rotaId, true);
  const lista = consulta.data ?? [];
  if (consulta.isError || lista.length === 0) return null;

  const nome = (sessaoId: string) =>
    alocacoes.find((a) => a.sessaoId === sessaoId && a.tipo === 'Paciente')?.pacienteNome ?? 'Paciente';

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <div className="mb-1 flex items-center gap-2">
        <MapPin className="h-4 w-4 text-gray-500" />
        <h2 className="text-sm font-semibold text-gray-900">Localização compartilhada pelo app</h2>
        <span className="text-xs text-gray-500">· atualiza sozinha</span>
      </div>
      <p className="mb-4 text-xs text-gray-500">
        O motorista vê no mapa do tablet onde o paciente está. Aqui aparece só quem está compartilhando.
      </p>
      <ul className="divide-y divide-gray-100 rounded-md border border-gray-100">
        {lista.map((c) => (
          <li key={`${c.sessaoId}:${c.trecho}`} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
            <span className="font-medium text-gray-900">
              {nome(c.sessaoId)}
              {c.trecho === 'Resgate' ? <span className="font-normal text-gray-500"> (resgate)</span> : null}
            </span>
            <span className="flex-1 text-right text-gray-700">
              {descrever(c)}
              {c.simulada ? <span className="font-medium text-amber-700"> · posição simulada</span> : null}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
