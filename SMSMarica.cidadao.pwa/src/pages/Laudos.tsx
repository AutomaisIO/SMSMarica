import { FileText } from 'lucide-react';
import { api } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function Laudos() {
  return (
    <Lista
      eyebrow="Documentos"
      titulo="Laudos"
      carregar={api.laudos}
      emptyIcon={FileText}
      emptyTitulo="Nenhum laudo ainda"
      emptyDescricao="Laudos assinados pelos profissionais ficam guardados aqui para você consultar e baixar."
      renderItem={(l) => (
        <Card className="flex items-center justify-between gap-3 p-4">
          <div className="min-w-0">
            <p className="truncate font-display font-semibold text-tinta">{l.titulo}</p>
            <p className="text-xs text-tinta-mute">{formatarData(l.data)}</p>
          </div>
          <Etiqueta status={l.status} />
        </Card>
      )}
    />
  );
}

function formatarData(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
