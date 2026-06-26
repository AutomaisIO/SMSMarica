import { useState } from 'react';
import { Download, FileText, Loader2 } from 'lucide-react';
import { api, pdfUrls, type Laudo } from '@/lib/api';
import { abrirPdf } from '@/lib/pdf';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';
import { Etiqueta } from '@/components/Etiqueta';

export function Laudos() {
  const [abrindo, setAbrindo] = useState<string | null>(null);

  async function abrir(l: Laudo) {
    setAbrindo(l.id);
    try {
      await abrirPdf(pdfUrls.laudo(l.id), `laudo-${l.id}.pdf`);
    } finally {
      setAbrindo(null);
    }
  }

  return (
    <Lista
      eyebrow="Documentos"
      titulo="Laudos"
      carregar={api.laudos}
      emptyIcon={FileText}
      emptyTitulo="Nenhum laudo ainda"
      emptyDescricao="Laudos assinados pelos profissionais ficam guardados aqui para você consultar e baixar."
      renderItem={(l) => (
        <Card
          className="flex items-center justify-between gap-3 p-4 transition active:scale-[.99]"
          role="button"
          onClick={() => abrir(l)}
        >
          <div className="flex min-w-0 items-center gap-3">
            <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-marica/10 text-marica">
              {abrindo === l.id ? <Loader2 className="h-5 w-5 animate-spin" /> : <Download className="h-5 w-5" />}
            </span>
            <div className="min-w-0">
              <p className="truncate font-display font-semibold text-tinta">{l.titulo}</p>
              <p className="text-xs text-tinta-mute">{formatarData(l.data)}</p>
            </div>
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
