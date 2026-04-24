import { useMemo } from 'react';
import { cn } from '@/shared/lib/cn';
import {
  MapaDeAssentos,
  type LinhaLayout,
} from '@/features/veiculos/components/MapaDeAssentos';
import type { Veiculo } from '@/features/veiculos/types';
import type { AlocacaoDto, SessaoElegivel } from '@/features/translados/types';

type Props = {
  veiculo: Veiculo;
  alocacoes: AlocacaoDto[];
  sessaoSelecionada?: SessaoElegivel | null;
  somenteLeitura?: boolean;
  aoClicarAssentoVazio?: (info: { fileiraOrdem: number; numero: number; assentoId: string }) => void;
  aoClicarAssentoAlocado?: (alocacao: AlocacaoDto) => void;
  className?: string;
};

function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

export function MapaDeAssentosAlocavel({
  veiculo,
  alocacoes,
  sessaoSelecionada,
  somenteLeitura,
  aoClicarAssentoVazio,
  aoClicarAssentoAlocado,
  className,
}: Props) {
  const alocacoesPorAssento = useMemo(() => {
    const mapa = new Map<string, AlocacaoDto>();
    for (const a of alocacoes) mapa.set(a.assentoId, a);
    return mapa;
  }, [alocacoes]);

  const idsPorPosicao = useMemo(() => {
    const mapa = new Map<string, string>();
    for (const f of veiculo.fileiras) {
      for (const a of f.assentos) {
        mapa.set(`${f.ordem}:${a.numero}`, a.id);
      }
    }
    return mapa;
  }, [veiculo]);

  const linhas: LinhaLayout[] = useMemo(
    () =>
      veiculo.fileiras
        .slice()
        .sort((a, b) => a.ordem - b.ordem)
        .map((f) => ({
          ordem: f.ordem,
          assentos: f.assentos
            .slice()
            .sort((a, b) => a.numero - b.numero)
            .map((a) => ({
              numero: a.numero,
              tipo: a.tipo,
              bloqueado: a.bloqueado,
            })),
        })),
    [veiculo],
  );

  function aoClicar(posicao: { fileiraOrdem: number; numero: number }) {
    if (somenteLeitura) return;
    const assentoId = idsPorPosicao.get(`${posicao.fileiraOrdem}:${posicao.numero}`);
    if (!assentoId) return;

    const alocacao = alocacoesPorAssento.get(assentoId);
    if (alocacao) {
      aoClicarAssentoAlocado?.(alocacao);
      return;
    }

    const assento = veiculo.fileiras
      .find((f) => f.ordem === posicao.fileiraOrdem)
      ?.assentos.find((a) => a.numero === posicao.numero);
    if (!assento) return;
    if (assento.bloqueado) return;
    if (assento.tipo === 'Motorista') return;

    aoClicarAssentoVazio?.({
      fileiraOrdem: posicao.fileiraOrdem,
      numero: posicao.numero,
      assentoId,
    });
  }

  const destacarLivres = Boolean(sessaoSelecionada) && !somenteLeitura;

  return (
    <MapaDeAssentos
      className={className}
      linhas={linhas}
      onClickAssento={somenteLeitura ? undefined : aoClicar}
      renderAssento={(assento) => {
        const assentoId = idsPorPosicao.get(`${assento.fileiraOrdem}:${assento.numero}`);
        const alocacao = assentoId ? alocacoesPorAssento.get(assentoId) : undefined;

        if (alocacao) {
          return (
            <div className="flex flex-col items-center gap-0.5">
              <span className="text-[11px] font-bold leading-none">{iniciais(alocacao.pacienteNome)}</span>
              <span className="text-[9px] leading-none opacity-70">
                F{assento.fileiraOrdem}·{assento.numero}
              </span>
            </div>
          );
        }

        const livreAlocavel =
          assento.tipo !== 'Motorista' &&
          !veiculo.fileiras
            .find((f) => f.ordem === assento.fileiraOrdem)
            ?.assentos.find((a) => a.numero === assento.numero)?.bloqueado;

        if (destacarLivres && livreAlocavel) {
          return (
            <div
              className={cn(
                'flex h-full w-full flex-col items-center justify-center rounded-md ring-2 ring-red-400',
                'animate-pulse bg-red-50/60 text-red-700',
              )}
            >
              <span className="text-[11px] font-semibold">{assento.numero}</span>
              <span className="text-[9px] opacity-70">livre</span>
            </div>
          );
        }

        return null;
      }}
    />
  );
}
