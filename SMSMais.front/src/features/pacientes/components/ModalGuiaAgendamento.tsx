import { useEffect, useRef, useState } from 'react';
import { Download, FileWarning, Loader2, Printer } from 'lucide-react';

import { baixarArquivo, caminhoConteudoPaciente } from '@/shared/acervo/api';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';

type Estado =
  | { fase: 'carregando' }
  | { fase: 'erro' }
  | { fase: 'pronto'; url: string; nomeArquivo: string };

/**
 * A guia do agendamento aberta da aba Agendamentos da ficha (ADR-0076): o PDF que a fila já guardou
 * no acervo do paciente, com Imprimir e Baixar em cima — o mesmo arranjo do documento de atendimento.
 *
 * <p>Só abre o que já está no acervo: nada é emitido no sistema de regulação por aqui (no SER e no
 * SERNIT, emitir gasta uma via). O arquivo vem com o token (blob), porque o conteúdo é privado.</p>
 */
export function ModalGuiaAgendamento({
  pacienteId,
  documentoId,
  descricao,
  aoFechar,
}: {
  pacienteId: string;
  /** `guiaDocumentoId` da linha; `null` = fechado. */
  documentoId: string | null;
  descricao?: string;
  aoFechar: () => void;
}) {
  const [estado, setEstado] = useState<Estado>({ fase: 'carregando' });
  const quadro = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    if (!documentoId) return;
    let vivo = true;
    let url: string | null = null;
    setEstado({ fase: 'carregando' });
    baixarArquivo(caminhoConteudoPaciente(pacienteId, { tipo: 'Documento', id: documentoId }))
      .then((a) => {
        if (!vivo) return;
        url = URL.createObjectURL(a.blob);
        setEstado({ fase: 'pronto', url, nomeArquivo: a.nomeArquivo ?? 'guia.pdf' });
      })
      .catch(() => {
        if (vivo) setEstado({ fase: 'erro' });
      });
    return () => {
      vivo = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [pacienteId, documentoId]);

  function imprimir() {
    if (estado.fase !== 'pronto') return;
    // Imprime o próprio quadro do PDF (mesma origem: é um blob da página). Se o navegador não deixar,
    // abre o PDF numa aba, onde a impressão é a do leitor de PDF.
    try {
      const janela = quadro.current?.contentWindow;
      if (!janela) throw new Error('sem quadro');
      janela.focus();
      janela.print();
    } catch {
      window.open(estado.url, '_blank', 'noopener');
    }
  }

  function baixar() {
    if (estado.fase !== 'pronto') return;
    const a = document.createElement('a');
    a.href = estado.url;
    a.download = estado.nomeArquivo;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  return (
    <Modal aberto={documentoId !== null} aoFechar={aoFechar} titulo="Guia do agendamento" descricao={descricao} largura="xl">
      <div className="mb-4 flex flex-wrap gap-2 border-b border-gray-100 pb-3">
        <Button variante="outline" tamanho="sm" onClick={imprimir} disabled={estado.fase !== 'pronto'}>
          <Printer className="h-4 w-4" /> Imprimir
        </Button>
        <Button variante="outline" tamanho="sm" onClick={baixar} disabled={estado.fase !== 'pronto'}>
          <Download className="h-4 w-4" /> Baixar PDF
        </Button>
      </div>

      {estado.fase === 'carregando' ? (
        <div className="flex h-[65vh] items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-gray-400" />
        </div>
      ) : estado.fase === 'erro' ? (
        <p className="flex items-center gap-2 text-sm text-red-700">
          <FileWarning className="h-5 w-5" /> Não foi possível abrir a guia.
        </p>
      ) : (
        <iframe
          ref={quadro}
          title="Guia do agendamento"
          src={estado.url}
          className="h-[65vh] w-full rounded border border-gray-200 bg-white"
        />
      )}
    </Modal>
  );
}
