import { FlaskConical } from 'lucide-react';
import { api } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function Exames() {
  return (
    <Lista
      eyebrow="Resultados"
      titulo="Exames"
      carregar={api.exames}
      emptyIcon={FlaskConical}
      emptyTitulo="Nenhum exame disponível"
      emptyDescricao="Resultados e imagens dos seus exames vão aparecer aqui assim que ficarem prontos."
      renderItem={(e) => (
        <Card className="flex items-center justify-between gap-3 p-4">
          <div className="min-w-0">
            <p className="truncate font-display font-semibold text-tinta">{e.nome}</p>
            <p className="text-xs text-tinta-mute">{formatarData(e.data)}</p>
          </div>
          <Etiqueta status={e.status} />
        </Card>
      )}
    />
  );
}

function formatarData(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
