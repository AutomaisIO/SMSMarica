import { useCallback, useEffect, useRef, useState } from 'react';
import { ClipboardPaste, ImagePlus, Loader2, X } from 'lucide-react';
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

/** Imagem vista na área de transferência, ainda não anexada nem dispensada. */
type Sugestao = { arquivo: File; url: string; assinatura: string };

/** Print colado chega como "image.png" — dá um nome que diga o que é. */
function nomearPrint(tipo: string): string {
  const ext = tipo.split('/')[1]?.replace('jpeg', 'jpg') || 'png';
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `print-${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}.${ext}`;
}

/** Identifica a mesma imagem entre leituras da área de transferência (não sugerir duas vezes). */
async function assinar(blob: Blob): Promise<string> {
  try {
    const hash = await crypto.subtle.digest('SHA-256', await blob.arrayBuffer());
    return Array.from(new Uint8Array(hash), (b) => b.toString(16).padStart(2, '0')).join('');
  } catch {
    return `${blob.type}:${blob.size}`; // crypto.subtle só existe em HTTPS/localhost
  }
}

function ehCampoDeTexto(el: EventTarget | null): boolean {
  return el instanceof HTMLElement && (el.isContentEditable || el instanceof HTMLTextAreaElement || el instanceof HTMLInputElement);
}

/** Lê a imagem da área de transferência — `null` se não há imagem ou o navegador não deixa. */
async function lerImagemDaAreaDeTransferencia(): Promise<File | null> {
  const itens = await navigator.clipboard.read();
  for (const item of itens) {
    const tipo = item.types.find((t) => t.startsWith('image/'));
    if (tipo) {
      const blob = await item.getType(tipo);
      return new File([blob], nomearPrint(tipo), { type: tipo });
    }
  }
  return null;
}

const podeLerAreaDeTransferencia = typeof navigator !== 'undefined' && !!navigator.clipboard?.read;

/**
 * Seleciona imagens (prints), envia ao backend e mantém a lista de referências.
 * Aceita também Ctrl+V de imagem e, quando o navegador já deu permissão, sugere sozinho o print
 * que está na área de transferência (inclusive ao voltar para a aba depois de tirar o print).
 */
export function AnexosInput({ anexos, aoMudar, disabled, escopoColar }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const blocoRef = useRef<HTMLDivElement>(null);
  const [enviando, setEnviando] = useState(false);
  const [sugestao, setSugestao] = useState<Sugestao | null>(null);
  // Imagens já anexadas ou dispensadas nesta tela — não voltam como sugestão.
  const vistas = useRef(new Set<string>());
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
            vistas.current.add(await assinar(arq));
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
      setSugestao(null);
      void enviarArquivos(imagens.map((f) => new File([f], nomearPrint(f.type), { type: f.type })));
    }
    document.addEventListener('paste', aoColar);
    return () => document.removeEventListener('paste', aoColar);
  }, [ocupado, escopoColar, enviarArquivos]);

  // ---- Sugestão automática (só quando a permissão de leitura já foi dada; nunca pede sozinha) ----
  const verificarAreaDeTransferencia = useCallback(async (pedirPermissao: boolean) => {
    if (!podeLerAreaDeTransferencia) return;
    try {
      if (!pedirPermissao) {
        const estado = await navigator.permissions?.query({ name: 'clipboard-read' as PermissionName });
        if (estado?.state !== 'granted' || !document.hasFocus()) return;
      }
      const arquivo = await lerImagemDaAreaDeTransferencia();
      if (!arquivo) {
        if (pedirPermissao) notificar('Não há imagem na área de transferência. Tire o print (Win+Shift+S) e tente de novo.', 'info');
        return;
      }
      const assinatura = await assinar(arquivo);
      if (vistas.current.has(assinatura)) {
        if (pedirPermissao) notificar('Essa imagem já foi anexada.', 'info');
        return;
      }
      if (pedirPermissao) {
        // Clique explícito em "Colar print": anexa direto, sem perguntar de novo.
        await enviarArquivos([arquivo]);
        return;
      }
      setSugestao((atual) => {
        if (atual?.assinatura === assinatura) return atual;
        return { arquivo, url: URL.createObjectURL(arquivo), assinatura };
      });
    } catch {
      if (pedirPermissao) notificar('O navegador não deixou ler a área de transferência. Use Ctrl+V.', 'info');
    }
  }, [enviarArquivos]);

  useEffect(() => {
    if (ocupado) return;
    void verificarAreaDeTransferencia(false);
    const aoFocar = () => void verificarAreaDeTransferencia(false);
    window.addEventListener('focus', aoFocar);
    return () => window.removeEventListener('focus', aoFocar);
  }, [ocupado, verificarAreaDeTransferencia]);

  useEffect(() => () => { if (sugestao) URL.revokeObjectURL(sugestao.url); }, [sugestao]);

  function aceitarSugestao() {
    if (!sugestao) return;
    const { arquivo } = sugestao;
    setSugestao(null);
    void enviarArquivos([arquivo]);
  }

  function dispensarSugestao() {
    if (!sugestao) return;
    vistas.current.add(sugestao.assinatura);
    setSugestao(null);
  }

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
        {podeLerAreaDeTransferencia && (
          <Button
            type="button"
            variante="outline"
            tamanho="sm"
            disabled={ocupado}
            onClick={() => void verificarAreaDeTransferencia(true)}
            title="Anexa a imagem que está na área de transferência (o print que você acabou de tirar)"
          >
            <ClipboardPaste className="h-4 w-4" />
            Colar print
          </Button>
        )}
        <span className="text-xs text-slate-400">ou cole com Ctrl+V</span>
      </div>

      {sugestao && !ocupado && (
        <div className="flex items-center gap-3 rounded-lg border border-sky-200 bg-sky-50 p-2">
          <img src={sugestao.url} alt="Imagem na área de transferência" className="h-14 w-14 rounded object-cover ring-1 ring-sky-200" />
          <p className="flex-1 text-sm text-sky-900">Há uma imagem na área de transferência. Anexar?</p>
          <Button type="button" tamanho="sm" onClick={aceitarSugestao}>Anexar</Button>
          <Button type="button" tamanho="sm" variante="ghost" onClick={dispensarSugestao}>Agora não</Button>
        </div>
      )}

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
