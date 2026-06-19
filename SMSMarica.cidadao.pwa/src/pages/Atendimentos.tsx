import { useState } from 'react';
import { FileText, Stethoscope, X } from 'lucide-react';
import { api, type DocumentoAtendimento } from '@/lib/api';
import { Card } from '@/components/ui';
import { Lista } from '@/components/Lista';

export function Atendimentos() {
  const [doc, setDoc] = useState<DocumentoAtendimento | null>(null);

  return (
    <>
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

            {a.documentos.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2 border-t border-areia pt-3">
                {a.documentos.map((d) => (
                  <button
                    key={d.id}
                    type="button"
                    onClick={() => setDoc(d)}
                    className="inline-flex items-center gap-1.5 rounded-xl border border-areia bg-papel px-3 py-1.5 text-xs font-medium text-tinta transition active:scale-95 active:bg-areia/60"
                  >
                    <FileText className="h-3.5 w-3.5 text-lagoa" />
                    {d.tipo}
                  </button>
                ))}
              </div>
            )}
          </Card>
        )}
      />

      {doc && <DocumentoModal doc={doc} aoFechar={() => setDoc(null)} />}
    </>
  );
}

/** Visualizador do documento clínico. O HTML do hub é renderizado num iframe
 * com sandbox vazio (sem scripts), isolando o conteúdo da aplicação. */
function DocumentoModal({ doc, aoFechar }: { doc: DocumentoAtendimento; aoFechar: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 mx-auto flex max-w-[460px] animate-fade-in flex-col bg-papel"
      role="dialog"
      aria-modal="true"
      aria-label={doc.tipo}
    >
      <header className="flex items-center gap-3 border-b border-areia bg-white px-4 py-3 shadow-topo">
        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-lagoa-claro text-lagoa">
          <FileText className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate font-display font-semibold text-tinta">{doc.tipo}</p>
          {doc.data && <p className="text-xs text-tinta-mute">{formatarData(doc.data)}</p>}
        </div>
        <button
          type="button"
          onClick={aoFechar}
          aria-label="Fechar documento"
          className="grid h-10 w-10 shrink-0 place-items-center rounded-xl text-tinta-mute transition active:bg-areia/60"
        >
          <X className="h-6 w-6" />
        </button>
      </header>

      <iframe
        title={doc.tipo}
        sandbox=""
        srcDoc={doc.conteudoHtml}
        className="min-h-0 w-full flex-1 bg-white"
      />
    </div>
  );
}

function formatarData(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR');
}
