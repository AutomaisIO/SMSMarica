import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useDocumentoConhecimento, useSalvarDocumento } from '@/features/ia/api/conhecimentoQueries';

type Props = {
  fonteId: string;
  /** `null` = novo documento. */
  docId: string | null;
  soLeitura: boolean;
  onFechar: () => void;
};

export function EditorDocumento({ fonteId, docId, soLeitura, onFechar }: Props) {
  const novo = docId === null;
  const detalhe = useDocumentoConhecimento(fonteId, docId ?? undefined);
  const salvar = useSalvarDocumento(fonteId);

  const [caminho, setCaminho] = useState('');
  const [conteudo, setConteudo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (detalhe.data) {
      setCaminho(detalhe.data.caminho);
      setConteudo(detalhe.data.conteudo);
    }
  }, [detalhe.data]);

  async function gravar() {
    setErro(null);
    try {
      await salvar.mutateAsync({ docId: docId ?? undefined, caminho: caminho.trim(), conteudo });
      onFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const carregando = !novo && detalhe.isLoading;

  return (
    <Modal
      aberto
      aoFechar={onFechar}
      titulo={novo ? 'Novo documento de conhecimento' : caminho || 'Documento'}
      largura="lg"
    >
      {carregando ? (
        <div className="flex items-center justify-center py-10 text-gray-400">
          <Loader2 className="h-5 w-5 animate-spin" />
        </div>
      ) : (
        <div className="space-y-4">
          <Campo
            label="Nome / caminho"
            htmlFor="doc-caminho"
            dica="Ex.: regras-de-negocio, relacionamentos, exemplos-sql. Documentos manuais ficam sob manual/."
          >
            <Input
              id="doc-caminho"
              value={caminho}
              disabled={soLeitura || !novo}
              placeholder="regras-de-negocio"
              onChange={(e) => setCaminho(e.target.value)}
            />
          </Campo>

          <div>
            <label htmlFor="doc-conteudo" className="mb-1 block text-sm font-medium text-gray-700">
              Conteúdo (markdown)
            </label>
            <textarea
              id="doc-conteudo"
              value={conteudo}
              readOnly={soLeitura}
              onChange={(e) => setConteudo(e.target.value)}
              spellCheck={false}
              rows={20}
              className="w-full rounded-lg border border-gray-200 bg-gray-50 p-3 font-mono text-xs leading-relaxed text-gray-800 focus:border-primary-400 focus:outline-none"
              placeholder={
                '# Regras de negócio\n\n- A tabela X guarda...\n- O relacionamento entre A e B se dá por...\n\n## Exemplos de SQL\n\n```sql\nSELECT ...\n```'
              }
            />
            <p className="mt-1 text-xs text-gray-400">
              Ao salvar, o documento é re-fatiado e re-embeddado para a busca por contexto.
            </p>
          </div>

          {erro && <p className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

          {soLeitura && (
            <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700">
              Este documento vem do repositório (versionado em git) — só leitura por aqui.
            </p>
          )}

          <div className="flex items-center justify-end gap-2 border-t border-gray-100 pt-4">
            <Button variante="secundaria" onClick={onFechar}>
              Fechar
            </Button>
            {!soLeitura && (
              <Button onClick={gravar} disabled={salvar.isPending}>
                {salvar.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Save className="h-4 w-4" />
                )}
                Salvar
              </Button>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}
