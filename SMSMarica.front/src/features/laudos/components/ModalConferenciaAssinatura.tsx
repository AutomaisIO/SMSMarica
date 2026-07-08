import { useEffect, useState } from 'react';
import { CheckCircle2, Loader2, XCircle } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { obterPdfAprovacao } from '@/features/laudos/api/laudosApi';
import { useAprovarAssinatura, useRejeitarAssinatura } from '@/features/laudos/api/queries';

/**
 * Conferência do laudo ASSINADO antes de valer (ticket da Dra.): mostra o PDF
 * exatamente como ficou (carimbo aplicado) e só a APROVAÇÃO oficializa a
 * assinatura — que é o gatilho do aviso ao paciente. Rejeitar cancela a
 * assinatura (o PDF fica guardado para auditoria) e libera assinar de novo.
 */
export function ModalConferenciaAssinatura({
  laudoId,
  aberto,
  aoFechar,
}: {
  laudoId: string;
  aberto: boolean;
  /** Fecha só a JANELA (sem decidir) — o botão "Conferir e aprovar" da página reabre. */
  aoFechar: () => void;
}) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const aprovar = useAprovarAssinatura();
  const rejeitar = useRejeitarAssinatura();
  const decidindo = aprovar.isPending || rejeitar.isPending;

  useEffect(() => {
    if (!aberto) return;
    let vivo = true;
    let url: string | null = null;
    setErro(null);
    obterPdfAprovacao(laudoId)
      .then((blob) => {
        if (!vivo) return;
        url = URL.createObjectURL(blob);
        setPdfUrl(url);
      })
      .catch((e) => {
        if (vivo) setErro(extrairMensagemDeErro(e));
      });
    return () => {
      vivo = false;
      if (url) URL.revokeObjectURL(url);
      setPdfUrl(null);
    };
  }, [aberto, laudoId]);

  function decidir(acao: 'aprovar' | 'rejeitar') {
    setErro(null);
    const m = acao === 'aprovar' ? aprovar : rejeitar;
    m.mutate(laudoId, { onError: (e) => setErro(extrairMensagemDeErro(e)) });
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={decidindo ? () => {} : aoFechar}
      titulo="Confira o laudo assinado"
      descricao="Verifique o conteúdo e o carimbo da assinatura. O paciente só é avisado depois da sua aprovação."
      largura="lg"
    >
      <div className="space-y-3">
        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        {pdfUrl ? (
          <iframe
            src={pdfUrl}
            title="Laudo assinado para conferência"
            className="h-[62vh] w-full rounded-md border border-gray-200"
          />
        ) : !erro ? (
          <div className="flex h-[62vh] items-center justify-center gap-2 text-sm text-gray-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Carregando o documento assinado…
          </div>
        ) : null}

        <div className="flex items-center justify-between gap-3 border-t border-gray-100 pt-4">
          <Button
            variante="outline"
            onClick={() => decidir('rejeitar')}
            disabled={decidindo}
            title="Cancela esta assinatura (o documento não vale e o paciente não é avisado). O laudo volta a poder ser assinado."
          >
            {rejeitar.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <XCircle className="h-4 w-4" />
            )}
            Rejeitar (assinar de novo)
          </Button>
          <Button
            onClick={() => decidir('aprovar')}
            disabled={decidindo || !pdfUrl}
            title="Confirma o documento: o laudo passa a valer como assinado e o paciente é avisado."
          >
            {aprovar.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <CheckCircle2 className="h-4 w-4" />
            )}
            Aprovar e liberar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
