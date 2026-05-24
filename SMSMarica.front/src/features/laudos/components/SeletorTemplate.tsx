import { useMemo, useState } from 'react';
import { FileText, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { useListarTemplates } from '@/features/laudo-templates/api/queries';
import { obterTemplate } from '@/features/laudo-templates/api/laudoTemplatesApi';
import type { LaudoTemplate } from '@/features/laudo-templates/types';

type Props = {
  /** Chamado quando o usuário confirma a escolha. Recebe o template completo (com conteúdoHtml/Json). */
  aoEscolher: (template: LaudoTemplate) => void;
  rotulo?: string;
};

export function SeletorTemplate({ aoEscolher, rotulo = 'Carregar template' }: Props) {
  const [aberto, setAberto] = useState(false);
  const [carregando, setCarregando] = useState<string | null>(null);
  const lista = useListarTemplates(undefined, false);

  const porCategoria = useMemo(() => {
    const map = new Map<string, typeof lista.data>();
    for (const t of lista.data ?? []) {
      const arr = map.get(t.categoria) ?? [];
      arr.push(t);
      map.set(t.categoria, arr);
    }
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [lista.data]);

  async function escolher(id: string) {
    setCarregando(id);
    try {
      const completo = await obterTemplate(id);
      aoEscolher(completo);
      setAberto(false);
    } finally {
      setCarregando(null);
    }
  }

  return (
    <>
      <Button variante="outline" onClick={() => setAberto(true)}>
        <FileText className="mr-2 h-4 w-4" />
        {rotulo}
      </Button>

      <Modal
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        titulo="Escolher template"
        descricao="O conteúdo do template substitui o que estiver atualmente no editor."
        largura="md"
      >
        {lista.isPending ? (
          <div className="flex items-center justify-center py-10 text-gray-500">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Carregando…
          </div>
        ) : porCategoria.length === 0 ? (
          <p className="py-6 text-center text-sm text-gray-500">
            Nenhum template cadastrado. Vá em <strong>Templates de Laudo</strong> para criar.
          </p>
        ) : (
          <div className="space-y-4">
            {porCategoria.map(([categoria, itens]) => (
              <div key={categoria}>
                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                  {categoria}
                </div>
                <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">
                  {(itens ?? []).map((t) => (
                    <li key={t.id}>
                      <button
                        type="button"
                        onClick={() => escolher(t.id)}
                        disabled={carregando !== null}
                        className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left hover:bg-gray-50 disabled:opacity-60"
                      >
                        <div className="min-w-0">
                          <div className="truncate text-sm font-medium text-gray-900">{t.nome}</div>
                          {t.descricao ? (
                            <div className="truncate text-xs text-gray-500">{t.descricao}</div>
                          ) : null}
                        </div>
                        {carregando === t.id ? (
                          <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
                        ) : (
                          <span className="text-xs text-primary-600">Usar</span>
                        )}
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </Modal>
    </>
  );
}
