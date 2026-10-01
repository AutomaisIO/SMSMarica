import { useEffect, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Download, ExternalLink, FileWarning, Loader2, X } from 'lucide-react';

import { VisualizadorImagem } from '@/shared/ui/VisualizadorImagem';
import { baixarArquivo } from './api';

type Props = {
  /** Endpoint autenticado do conteúdo (ex.: `/pacientes/{id}/documentos/Documento/{id}/conteudo`). */
  caminho: string;
  titulo: string;
  descricao?: string | null;
  /** Nome sugerido ao baixar, quando o servidor não manda. */
  nomeArquivo?: string | null;
  /** Botões numa barra embaixo — "Adicionar ao cadastro do paciente", "Aceitar", "Anexar"… */
  acoes?: ReactNode;
  aoFechar: () => void;
};

type Estado =
  | { fase: 'carregando' }
  | { fase: 'erro' }
  | { fase: 'pronto'; url: string; mimeType: string; nomeArquivo: string };

/**
 * Visualizador de arquivo do sistema: abre PDF e imagem na própria tela, com ações embaixo.
 *
 * <p>Imagem usa o visualizador de imagem de sempre (zoom, arraste, salvar). PDF abre no leitor do
 * navegador. O arquivo vem com o token (blob), porque os endpoints de conteúdo são privados.</p>
 */
export function VisualizadorArquivo({ caminho, titulo, descricao, nomeArquivo, acoes, aoFechar }: Props) {
  const [estado, setEstado] = useState<Estado>({ fase: 'carregando' });

  useEffect(() => {
    let vivo = true;
    let url: string | null = null;
    setEstado({ fase: 'carregando' });
    baixarArquivo(caminho)
      .then((a) => {
        if (!vivo) return;
        url = URL.createObjectURL(a.blob);
        setEstado({
          fase: 'pronto',
          url,
          mimeType: a.mimeType,
          nomeArquivo: a.nomeArquivo ?? nomeArquivo ?? titulo,
        });
      })
      .catch(() => {
        if (vivo) setEstado({ fase: 'erro' });
      });
    return () => {
      vivo = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [caminho, nomeArquivo, titulo]);

  if (estado.fase === 'pronto' && estado.mimeType.startsWith('image/')) {
    return (
      <VisualizadorImagem
        imagens={[{ url: estado.url, legenda: titulo, nomeArquivo: estado.nomeArquivo }]}
        aoFechar={aoFechar}
        acoes={acoes}
        semCopiarLink
      />
    );
  }

  return createPortal(
    <TelaCheia aoFechar={aoFechar}>
      <div className="flex items-center justify-between gap-3 px-4 py-3 text-white">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{titulo}</p>
          {descricao ? <p className="truncate text-xs text-white/60">{descricao}</p> : null}
        </div>
        <div className="flex shrink-0 items-center gap-1">
          {estado.fase === 'pronto' ? (
            <>
              <BotaoBarra titulo="Baixar" aoClicar={() => baixar(estado.url, estado.nomeArquivo)}>
                <Download className="h-5 w-5" />
              </BotaoBarra>
              <BotaoBarra titulo="Abrir em nova aba" aoClicar={() => window.open(estado.url, '_blank', 'noopener')}>
                <ExternalLink className="h-5 w-5" />
              </BotaoBarra>
              <span className="mx-1 h-5 w-px bg-white/20" aria-hidden />
            </>
          ) : null}
          <BotaoBarra titulo="Fechar (Esc)" aoClicar={aoFechar}>
            <X className="h-5 w-5" />
          </BotaoBarra>
        </div>
      </div>

      <div className="relative flex flex-1 items-center justify-center overflow-hidden px-4 pb-4">
        {estado.fase === 'carregando' ? (
          <Loader2 className="h-8 w-8 animate-spin text-white/70" />
        ) : estado.fase === 'erro' ? (
          <p className="flex items-center gap-2 text-sm text-white/80">
            <FileWarning className="h-5 w-5" /> Não foi possível abrir o arquivo.
          </p>
        ) : estado.mimeType === 'application/pdf' ? (
          <iframe title={titulo} src={estado.url} className="h-full w-full rounded bg-white" />
        ) : (
          <div className="text-center text-sm text-white/80">
            <p>Este tipo de arquivo não tem pré-visualização.</p>
            <button
              type="button"
              onClick={() => baixar(estado.url, estado.nomeArquivo)}
              className="mt-3 inline-flex items-center gap-1.5 rounded-md bg-white/15 px-3 py-1.5 hover:bg-white/25"
            >
              <Download className="h-4 w-4" /> Baixar
            </button>
          </div>
        )}
      </div>

      {acoes ? (
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-white/10 bg-black/60 px-4 py-3">
          {acoes}
        </div>
      ) : null}
    </TelaCheia>,
    document.body,
  );
}

function TelaCheia({ children, aoFechar }: { children: ReactNode; aoFechar: () => void }) {
  useEffect(() => {
    function aoTeclar(e: KeyboardEvent) {
      if (e.key === 'Escape') aoFechar();
    }
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, [aoFechar]);

  return (
    <div className="fixed inset-0 z-[60] flex flex-col bg-black/90 backdrop-blur-sm" role="dialog" aria-modal="true">
      {children}
    </div>
  );
}

function BotaoBarra({ children, titulo, aoClicar }: { children: ReactNode; titulo: string; aoClicar: () => void }) {
  return (
    <button
      type="button"
      title={titulo}
      aria-label={titulo}
      onClick={aoClicar}
      className="rounded-md p-2 text-white/90 transition hover:bg-white/15"
    >
      {children}
    </button>
  );
}

function baixar(href: string, nome: string) {
  const a = document.createElement('a');
  a.href = href;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
}

/** Botão de ação no padrão da barra escura do visualizador. */
export function AcaoVisualizador({
  children,
  aoClicar,
  destaque,
  perigo,
  disabled,
}: {
  children: ReactNode;
  aoClicar: () => void;
  destaque?: boolean;
  perigo?: boolean;
  disabled?: boolean;
}) {
  const cor = destaque
    ? 'bg-emerald-600 text-white hover:bg-emerald-500'
    : perigo
      ? 'bg-white/10 text-red-200 hover:bg-red-600 hover:text-white'
      : 'bg-white/10 text-white hover:bg-white/20';
  return (
    <button
      type="button"
      onClick={aoClicar}
      disabled={disabled}
      className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition disabled:opacity-50 ${cor}`}
    >
      {children}
    </button>
  );
}
