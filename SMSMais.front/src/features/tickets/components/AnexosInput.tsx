import { useCallback, useEffect, useRef, useState } from 'react';
import { ImagePlus, Loader2, X } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { VisualizadorImagem } from '@/shared/ui/VisualizadorImagem';
import { urlMidiaAbsoluta } from '@/shared/api/midiaApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { enviarAnexo } from '@/features/tickets/api/ticketsApi';
import type { AnexoRef } from '@/features/tickets/types';

type Props = {
  anexos: AnexoRef[];
  aoMudar: (anexos: AnexoRef[]) => void;
  disabled?: boolean;
  /**
   * Área onde o Ctrl+V de uma imagem vira anexo (o formulário inteiro, não só o botão).
   * Sem ela, vale o bloco do próprio componente.
   */
  escopoColar?: React.RefObject<HTMLElement | null>;
};

/** Print colado chega como "image.png" — dá um nome que diga o que é. */
function nomearPrint(tipo: string): string {
  const ext = tipo.split('/')[1]?.replace('jpeg', 'jpg') || 'png';
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `print-${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}.${ext}`;
}

function ehCampoDeTexto(el: EventTarget | null): boolean {
  return el instanceof HTMLElement && (el.isContentEditable || el instanceof HTMLTextAreaElement || el instanceof HTMLInputElement);
}

/**
 * Seleciona imagens (prints), envia ao backend e mantém a lista de referências.
 * Aceita também Ctrl+V de imagem (o print tirado com Win+Shift+S) — sem pedir permissão ao navegador.
 */
export function AnexosInput({ anexos, aoMudar, disabled, escopoColar }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const blocoRef = useRef<HTMLDivElement>(null);
  const [enviando, setEnviando] = useState(false);
  // A lista mais recente, para o Ctrl+V (listener de documento) não anexar sobre uma lista velha.
  const anexosRef = useRef(anexos);
  anexosRef.current = anexos;
  const ocupado = disabled || enviando;

  const enviarArquivos = useCallback(
    async (arquivos: File[]) => {
      if (arquivos.length === 0) return;
      setEnviando(true);
      try {
        // Commit incremental: cada upload bem-sucedido é vinculado na hora. Assim, uma falha
        // em um arquivo seguinte (rede, tipo, tamanho) NÃO descarta os que já subiram — evita
        // mídias órfãs (persistidas no servidor, mas sem referência no ticket).
        let atuais = anexosRef.current;
        for (const arq of arquivos) {
          if (!arq.type.startsWith('image/')) {
            notificar(`"${arq.name}" não é uma imagem.`, 'erro');
            continue;
          }
          try {
            const ref = await enviarAnexo(arq);
            atuais = [...atuais, ref];
            aoMudar(atuais);
          } catch (erro) {
            notificar(`Falha ao anexar "${arq.name}": ${extrairMensagemDeErro(erro)}`, 'erro');
          }
        }
      } finally {
        setEnviando(false);
      }
    },
    [aoMudar],
  );

  function aoSelecionar(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivos = Array.from(e.target.files ?? []);
    e.target.value = ''; // permite reenviar o mesmo arquivo
    void enviarArquivos(arquivos);
  }

  function remover(midiaId: string) {
    aoMudar(anexos.filter((a) => a.midiaId !== midiaId));
  }

  // ---- Ctrl+V ----
  useEffect(() => {
    if (ocupado) return;
    function aoColar(e: ClipboardEvent) {
      const escopo = escopoColar?.current ?? blocoRef.current;
      const alvo = e.target instanceof Node ? e.target : null;
      if (!escopo) return;
      if (alvo && alvo !== document.body) {
        if (!escopo.contains(alvo)) return; // colou em outro lugar da tela
      } else {
        // Nada focado: vale para quem está na frente (o modal aberto, se houver um).
        const dialogo = document.querySelector('[role="dialog"]');
        if (dialogo && !dialogo.contains(escopo)) return;
      }
      const dados = e.clipboardData;
      if (!dados) return;
      const imagens = Array.from(dados.files).filter((f) => f.type.startsWith('image/'));
      if (imagens.length === 0) return;
      // Copiar do Word/Excel traz texto E imagem: dentro de um campo de texto, quem cola quer o texto.
      if (dados.types.includes('text/plain') && ehCampoDeTexto(e.target)) return;
      e.preventDefault();
      void enviarArquivos(imagens.map((f) => new File([f], nomearPrint(f.type), { type: f.type })));
    }
    document.addEventListener('paste', aoColar);
    return () => document.removeEventListener('paste', aoColar);
  }, [ocupado, escopoColar, enviarArquivos]);

  return (
    <div ref={blocoRef} className="space-y-2">
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        multiple
        hidden
        onChange={aoSelecionar}
      />
      <div className="flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variante="outline"
          tamanho="sm"
          disabled={ocupado}
          onClick={() => inputRef.current?.click()}
        >
          {enviando ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
          {enviando ? 'Enviando…' : 'Anexar imagem'}
        </Button>
        <span className="text-xs text-slate-400">ou cole com Ctrl+V</span>
      </div>

      {anexos.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {anexos.map((a) => (
            <div key={a.midiaId} className="group relative">
              <img
                src={urlMidiaAbsoluta(a.midiaId)}
                alt={a.nomeArquivo}
                className="h-20 w-20 rounded-lg object-cover ring-1 ring-slate-200"
              />
              <button
                type="button"
                onClick={() => remover(a.midiaId)}
                className="absolute -right-1.5 -top-1.5 rounded-full bg-slate-800 p-0.5 text-white opacity-90 hover:bg-red-600"
                title="Remover"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/** Galeria só-leitura de anexos (miniaturas que abrem no visualizador com zoom). */
export function AnexosGaleria({ anexos }: { anexos: { midiaId: string; nomeArquivo: string }[] }) {
  const [aberto, setAberto] = useState<number | null>(null);
  if (anexos.length === 0) return null;
  return (
    <>
      <div className="flex flex-wrap gap-2">
        {anexos.map((a, i) => (
          <button
            key={a.midiaId}
            type="button"
            onClick={() => setAberto(i)}
            title={`${a.nomeArquivo} — clique para ampliar`}
            className="cursor-zoom-in"
          >
            <img
              src={urlMidiaAbsoluta(a.midiaId)}
              alt={a.nomeArquivo}
              className="h-24 w-24 rounded-lg object-cover ring-1 ring-slate-200 transition hover:ring-2 hover:ring-red-400"
            />
          </button>
        ))}
      </div>
      {aberto !== null ? (
        <VisualizadorImagem
          imagens={anexos.map((a) => ({
            url: urlMidiaAbsoluta(a.midiaId),
            legenda: a.nomeArquivo,
            nomeArquivo: a.nomeArquivo,
          }))}
          indiceInicial={aberto}
          aoFechar={() => setAberto(null)}
        />
      ) : null}
    </>
  );
}
