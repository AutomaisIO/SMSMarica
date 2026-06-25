import { useEffect, useState } from 'react';
import { AlertCircle, CheckCircle2, Link2, Loader2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useAssociarExame, usePreviewSolicitacao } from '@/features/pacs/api/queries';
import type { Estudo } from '@/features/pacs/types';

type Props = {
  /** Estudo a associar; null fecha o modal. */
  estudo: Estudo | null;
  aoFechar: () => void;
};

/**
 * Modal de associação manual: o operador digita o número da solicitação (SMS...),
 * vê o paciente + resumo do pedido e confirma o vínculo do exame.
 */
export function ModalAssociarExame({ estudo, aoFechar }: Props) {
  const [numero, setNumero] = useState('');
  const [debounced, setDebounced] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const associar = useAssociarExame();

  // Reseta ao abrir/trocar de estudo.
  useEffect(() => {
    setNumero('');
    setDebounced('');
    setErro(null);
  }, [estudo?.studyInstanceUID]);

  // Debounce do número digitado.
  useEffect(() => {
    const t = setTimeout(() => setDebounced(numero.trim()), 350);
    return () => clearTimeout(t);
  }, [numero]);

  // Accession = {AAMMDD}{seq} (só dígitos), ex.: 260625002. Sem "SMS".
  const formatoValido = /^\d{4,}$/.test(debounced);
  const preview = usePreviewSolicitacao(debounced);
  const solicitacao = preview.data ?? null;

  async function confirmar() {
    if (!estudo || !solicitacao) return;
    setErro(null);
    try {
      await associar.mutateAsync({
        studyInstanceUID: estudo.studyInstanceUID,
        accessionNumber: solicitacao.accessionNumber,
        accessionNumberDicomOriginal: estudo.accessionNumber || null,
      });
      aoFechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal
      aberto={!!estudo}
      aoFechar={aoFechar}
      titulo="Associar exame a um pedido"
      descricao="Informe o número da solicitação (SMS…) para vincular este exame ao paciente."
      largura="md"
    >
      {estudo ? (
        <div className="space-y-4">
          {/* Exame de origem (DICOM cru) */}
          <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
            <div className="font-medium text-gray-900">{estudo.patientName || 'Sem nome (DICOM)'}</div>
            <div className="text-xs text-gray-500">
              {[
                estudo.studyDateFormatado,
                estudo.modalidade,
                estudo.accessionNumber && `Accession ${estudo.accessionNumber}`,
              ]
                .filter(Boolean)
                .join(' · ') || '—'}
            </div>
          </div>

          <div>
            <label htmlFor="num-sol" className="mb-1 block text-sm font-medium text-gray-700">
              Número da solicitação
            </label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
              <Input
                id="num-sol"
                value={numero}
                onChange={(e) => setNumero(e.target.value.replace(/\D/g, ''))}
                placeholder="Ex.: 260625002"
                inputMode="numeric"
                className="pl-9 font-mono"
                autoFocus
              />
            </div>
            {debounced && !formatoValido ? (
              <p className="mt-1 text-xs text-amber-700">Use só os números do pedido (ex.: 260625002).</p>
            ) : null}
          </div>

          {/* Preview da solicitação */}
          {formatoValido && preview.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Buscando solicitação…
            </div>
          ) : null}

          {formatoValido && !preview.isLoading && !solicitacao ? (
            <div className="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              <AlertCircle className="h-4 w-4" /> Nenhuma solicitação encontrada com esse número.
            </div>
          ) : null}

          {solicitacao ? (
            <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-3 text-sm">
              <div className="flex items-center gap-2 text-emerald-800">
                <CheckCircle2 className="h-4 w-4" />
                <span className="text-base font-semibold">{solicitacao.pacienteNome}</span>
              </div>
              <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-700">
                <Item rotulo="Exame" valor={solicitacao.tipoExameNome} />
                <Item rotulo="Modalidade" valor={solicitacao.modalidadeDicom} />
                <Item rotulo="Unidade" valor={solicitacao.unidadeNome} />
                <Item rotulo="Solicitante" valor={solicitacao.solicitanteNome} />
                <Item rotulo="CPF" valor={solicitacao.pacienteCpf ?? '—'} />
                <Item rotulo="Status" valor={solicitacao.status} />
              </dl>
            </div>
          ) : null}

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex justify-end gap-2 pt-1">
            <Button variante="outline" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button onClick={confirmar} disabled={!solicitacao || associar.isPending}>
              {associar.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Link2 className="mr-2 h-4 w-4" />
              )}
              Associar
            </Button>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}

function Item({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-gray-500">{rotulo}</dt>
      <dd className="truncate font-medium text-gray-900">{valor || '—'}</dd>
    </div>
  );
}
