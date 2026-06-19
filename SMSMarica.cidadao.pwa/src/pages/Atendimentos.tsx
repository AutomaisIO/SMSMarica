import { Stethoscope } from 'lucide-react';
import { api } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';

export function Atendimentos() {
  return (
    <Lista
      eyebrow="Histórico de saúde"
      titulo="Meus atendimentos"
      carregar={api.atendimentos}
      emptyIcon={Stethoscope}
      emptyTitulo="Nenhum atendimento ainda"
      emptyDescricao="Quando você passar por consultas na rede municipal, elas aparecem aqui."
      renderItem={(a) => (
        <Card className="p-4">
          <div className="flex items-center justify-between gap-3">
            <span className="font-display font-semibold text-tinta">{a.estabelecimento}</span>
            <span className="shrink-0 text-xs text-tinta-mute">{formatarData(a.data)}</span>
          </div>
          <p className="mt-1 text-sm text-tinta-mute">{a.profissional}</p>
          {a.descricao && <p className="mt-2 text-sm text-tinta">{a.descricao}</p>}
        </Card>
      )}
    />
  );
}

function formatarData(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
