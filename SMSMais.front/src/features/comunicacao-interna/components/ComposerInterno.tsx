import { useEffect, useRef, useState } from 'react';
import { FileText, Mic, Paperclip, Send, Smile, Trash2, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { TETO_ANEXO_BYTES } from '@/features/comunicacao-interna/api/comunicacaoInternaApi';
import { useEnviarMensagemInterna } from '@/features/comunicacao-interna/api/queries';
import { avisarDigitando } from '@/features/comunicacao-interna/hooks/useInternoHub';
import { useGravadorAudio, type AudioGravado } from '@/features/comunicacao-interna/hooks/useGravadorAudio';
import { duracaoFmt, tamanhoFmt } from '@/features/comunicacao-interna/lib/formatar';
import { codigoSelo, type Selo } from '@/features/comunicacao-interna/lib/selos';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import { SeletorEmoji } from '@/features/comunicacao-interna/components/SeletorEmoji';

/**
 * Caixa de escrever da Comunicação interna: texto (Enter envia, Shift+Enter quebra linha), emojis
 * e selos, anexos (clipe ou colar imagem; vários arquivos = várias mensagens, o texto vai junto do
 * primeiro, como no WhatsApp) e áudio gravado no navegador.
 */
export function ComposerInterno({ conversaId }: { conversaId: string }) {
  const [texto, setTexto] = useState('');
  const [pendentes, setPendentes] = useState<File[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [emojiAberto, setEmojiAberto] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const enviar = useEnviarMensagemInterna();
  const taRef = useRef<HTMLTextAreaElement>(null);
  const arquivoRef = useRef<HTMLInputElement>(null);

  async function enviarAudio(a: AudioGravado) {
    setErro(null);
    try {
      await enviar.mutateAsync({ conversaId, arquivo: a.arquivo, audio: { duracaoSegundos: a.duracaoSegundos } });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }
  const gravador = useGravadorAudio((a) => void enviarAudio(a));

  // Troca de conversa: limpa o rascunho e (se a pessoa abriu de propósito) põe o cursor aqui. Se o
  // painel abriu SOZINHO, não rouba o foco do campo em que ela estava digitando na tela.
  useEffect(() => {
    setTexto('');
    setPendentes([]);
    setErro(null);
    if (!useInterno.getState().abertoSozinho) taRef.current?.focus();
  }, [conversaId]);

  function adicionarArquivos(lista: FileList | File[]) {
    const novos: File[] = [];
    const grandes: string[] = [];
    for (const f of Array.from(lista)) {
      if (f.size > TETO_ANEXO_BYTES) grandes.push(f.name);
      else novos.push(f);
    }
    setErro(grandes.length ? `Acima de 25 MB, não vai: ${grandes.join(', ')}` : null);
    if (novos.length) setPendentes((p) => [...p, ...novos]);
  }

  async function aoEnviar() {
    const t = texto.trim();
    if (!t && pendentes.length === 0) return;
    setErro(null);
    setEnviando(true);
    try {
      if (pendentes.length === 0) {
        await enviar.mutateAsync({ conversaId, texto: t });
      } else {
        // Um arquivo por mensagem; a legenda vai com o primeiro. Sai da fila o que já foi.
        for (let i = 0; i < pendentes.length; i++) {
          await enviar.mutateAsync({ conversaId, arquivo: pendentes[i], texto: i === 0 && t ? t : undefined });
          setPendentes((p) => p.slice(1));
          if (i === 0) setTexto('');
        }
      }
      setTexto('');
      setPendentes([]);
      taRef.current?.focus();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  async function enviarSelo(selo: Selo) {
    setEmojiAberto(false);
    setErro(null);
    try {
      await enviar.mutateAsync({ conversaId, texto: codigoSelo(selo.chave) });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function inserirEmoji(e: string) {
    const ta = taRef.current;
    if (!ta) {
      setTexto((t) => t + e);
      return;
    }
    const ini = ta.selectionStart ?? texto.length;
    const fim = ta.selectionEnd ?? texto.length;
    const novo = texto.slice(0, ini) + e + texto.slice(fim);
    setTexto(novo);
    requestAnimationFrame(() => {
      ta.focus();
      const pos = ini + e.length;
      ta.setSelectionRange(pos, pos);
    });
  }

  if (gravador.estado === 'gravando') {
    return (
      <div className="flex items-center gap-3 border-t border-gray-200 bg-white px-3 py-2.5">
        <button
          type="button"
          onClick={() => void gravador.parar(false)}
          className="flex h-9 w-9 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 hover:text-red-600"
          title="Descartar o áudio"
          aria-label="Descartar o áudio"
        >
          <Trash2 className="h-4 w-4" />
        </button>
        <span className="flex flex-1 items-center gap-2 text-sm text-gray-700">
          <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-red-500" />
          Gravando… <span className="font-mono tabular-nums">{duracaoFmt(gravador.segundos)}</span>
        </span>
        <button
          type="button"
          onClick={() =>
            void gravador.parar(true).then((a) => {
              if (a) void enviarAudio(a);
            })
          }
          className="flex h-9 w-9 items-center justify-center rounded-full bg-orange-500 text-white hover:bg-orange-600"
          title="Enviar o áudio"
          aria-label="Enviar o áudio"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    );
  }

  const temConteudo = texto.trim().length > 0 || pendentes.length > 0;
  const ocupado = enviando || enviar.isPending;

  return (
    <div className="border-t border-gray-200 bg-white p-2">
      {(erro || gravador.erro) && (
        <p className="px-1 pb-1 text-xs text-red-600">{erro ?? gravador.erro}</p>
      )}

      {pendentes.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-1.5 px-1">
          {pendentes.map((f, i) => (
            <span
              key={`${f.name}-${i}`}
              className="flex max-w-[14rem] items-center gap-1.5 rounded-md bg-orange-50 py-1 pl-2 pr-1 text-xs text-orange-900 ring-1 ring-orange-200"
            >
              <FileText className="h-3.5 w-3.5 shrink-0" />
              <span className="truncate" title={f.name}>
                {f.name}
              </span>
              <span className="shrink-0 text-orange-700/70">{tamanhoFmt(f.size)}</span>
              <button
                type="button"
                onClick={() => setPendentes((p) => p.filter((_, j) => j !== i))}
                disabled={ocupado}
                className="rounded p-0.5 hover:bg-orange-100"
                aria-label={`Tirar ${f.name}`}
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
        </div>
      )}

      <div className="relative flex items-end gap-1">
        {emojiAberto && (
          <SeletorEmoji
            aoEscolherEmoji={inserirEmoji}
            aoEscolherSelo={(s) => void enviarSelo(s)}
            aoFechar={() => setEmojiAberto(false)}
          />
        )}
        <button
          type="button"
          onClick={() => setEmojiAberto((v) => !v)}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 hover:text-orange-600"
          title="Emojis e selos"
          aria-label="Emojis e selos"
        >
          <Smile className="h-5 w-5" />
        </button>
        <button
          type="button"
          onClick={() => arquivoRef.current?.click()}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 hover:text-orange-600"
          title="Anexar arquivo (até 25 MB)"
          aria-label="Anexar arquivo"
        >
          <Paperclip className="h-5 w-5" />
        </button>
        <input
          ref={arquivoRef}
          type="file"
          multiple
          className="hidden"
          onChange={(e) => {
            if (e.target.files) adicionarArquivos(e.target.files);
            e.target.value = '';
          }}
        />
        <textarea
          ref={taRef}
          value={texto}
          rows={1}
          onChange={(e) => {
            setTexto(e.target.value);
            if (e.target.value.trim()) avisarDigitando(conversaId);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault();
              if (!ocupado) void aoEnviar();
            }
          }}
          onPaste={(e) => {
            // Print colado (Ctrl+V) vira anexo pendente; texto colado segue o normal.
            const arquivos = Array.from(e.clipboardData.files ?? []);
            if (arquivos.length > 0) {
              e.preventDefault();
              adicionarArquivos(arquivos);
            }
          }}
          placeholder="Escreva uma mensagem…  (Enter envia, Shift+Enter quebra linha)"
          className="max-h-32 min-h-[2.25rem] flex-1 resize-none overflow-y-auto rounded-2xl border border-gray-300 px-3 py-2 text-sm outline-none [field-sizing:content] focus:border-orange-400"
        />
        {temConteudo ? (
          <button
            type="button"
            onClick={() => void aoEnviar()}
            disabled={ocupado}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-orange-500 text-white hover:bg-orange-600 disabled:opacity-50"
            title="Enviar"
            aria-label="Enviar"
          >
            <Send className="h-4 w-4" />
          </button>
        ) : (
          <button
            type="button"
            onClick={() => void gravador.iniciar()}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-orange-500 text-white hover:bg-orange-600"
            title="Gravar áudio"
            aria-label="Gravar áudio"
          >
            <Mic className="h-4 w-4" />
          </button>
        )}
      </div>
    </div>
  );
}
