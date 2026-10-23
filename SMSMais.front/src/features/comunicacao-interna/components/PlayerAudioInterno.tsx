import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Captions, Loader2, Pause, Play } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTranscreverMensagemInterna } from '@/features/comunicacao-interna/api/queries';
import { duracaoFmt } from '@/features/comunicacao-interna/lib/formatar';
import type { AnexoInterno } from '@/features/comunicacao-interna/types';

const VELOCIDADES = [1, 1.5, 2] as const;

/**
 * Player do áudio gravado no painel. Não usa os controles nativos do navegador: o WebM que o
 * MediaRecorder grava não traz a duração no cabeçalho, e o player nativo mostrava "0:00" com a barra
 * parada — parecia que não tocava. Aqui a duração vem do servidor (`duracaoSegundos`), o play/pausa
 * avisa quando o navegador recusa, e embaixo fica a transcrição (pedida por quem quiser, guardada
 * para todos — ElevenLabs, a mesma dos áudios do WhatsApp).
 */
export function PlayerAudioInterno({
  mensagemId,
  anexo,
  url,
  carregando,
  erroDownload,
  botaoBaixar,
}: {
  mensagemId: string;
  anexo: AnexoInterno;
  url: string | null;
  carregando: boolean;
  erroDownload: boolean;
  botaoBaixar: React.ReactNode;
}) {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [tocando, setTocando] = useState(false);
  const [atual, setAtual] = useState(0);
  const [duracaoReal, setDuracaoReal] = useState<number | null>(null);
  const [velocidade, setVelocidade] = useState<(typeof VELOCIDADES)[number]>(1);
  const [erroTocar, setErroTocar] = useState(false);
  const transcrever = useTranscreverMensagemInterna();

  const total = anexo.duracaoSegundos ?? duracaoReal ?? 0;

  useEffect(() => {
    if (audioRef.current) audioRef.current.playbackRate = velocidade;
  }, [velocidade]);

  async function alternar() {
    const el = audioRef.current;
    if (!el) return;
    if (!el.paused) {
      el.pause();
      return;
    }
    setErroTocar(false);
    try {
      if (el.ended) el.currentTime = 0;
      await el.play();
    } catch {
      setErroTocar(true);
    }
  }

  const progresso = total > 0 ? Math.min(100, (atual / total) * 100) : 0;
  const falhou = erroDownload || erroTocar;

  return (
    <div className="flex min-w-[15rem] max-w-[18rem] flex-col gap-1.5">
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={() => void alternar()}
          disabled={!url}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-orange-500 text-white shadow-sm hover:bg-orange-600 disabled:bg-gray-300"
          aria-label={tocando ? 'Pausar o áudio' : 'Tocar o áudio'}
          title={tocando ? 'Pausar' : 'Tocar'}
        >
          {carregando ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : tocando ? (
            <Pause className="h-4 w-4" />
          ) : (
            <Play className="ml-0.5 h-4 w-4" />
          )}
        </button>
        <div className="min-w-0 flex-1">
          <div className="h-1.5 overflow-hidden rounded-full bg-black/10">
            <div className="h-full rounded-full bg-orange-500 transition-[width] duration-200" style={{ width: `${progresso}%` }} />
          </div>
          <div className="mt-1 flex items-center justify-between font-mono text-[11px] tabular-nums text-gray-500">
            <span>{duracaoFmt(Math.floor(atual))}</span>
            <span>{total > 0 ? duracaoFmt(total) : '—'}</span>
          </div>
        </div>
        <button
          type="button"
          onClick={() => setVelocidade((v) => VELOCIDADES[(VELOCIDADES.indexOf(v) + 1) % VELOCIDADES.length])}
          className="shrink-0 rounded-md bg-black/5 px-1.5 py-0.5 text-[11px] font-semibold text-gray-600 hover:bg-black/10"
          title="Velocidade"
        >
          {velocidade}×
        </button>
      </div>

      {url && (
        <audio
          ref={audioRef}
          src={url}
          preload="auto"
          className="hidden"
          onPlay={() => setTocando(true)}
          onPause={() => setTocando(false)}
          onEnded={() => {
            setTocando(false);
            setAtual(total);
          }}
          onTimeUpdate={(e) => setAtual(e.currentTarget.currentTime)}
          onLoadedMetadata={(e) => {
            const d = e.currentTarget.duration;
            if (Number.isFinite(d) && d > 0) setDuracaoReal(d);
          }}
          onError={() => setErroTocar(true)}
        />
      )}

      {falhou && (
        <div className="flex flex-wrap items-center gap-1.5 text-[11px] text-red-600">
          <AlertTriangle className="h-3.5 w-3.5" />
          {erroDownload ? 'Não foi possível baixar o áudio.' : 'O navegador não conseguiu tocar este áudio.'}
          {botaoBaixar}
        </div>
      )}

      {anexo.transcricao ? (
        <div className="rounded-md bg-black/5 px-2 py-1.5">
          <p className="mb-0.5 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-gray-500">
            <Captions className="h-3 w-3" /> Transcrição
          </p>
          <p className="whitespace-pre-wrap text-xs italic text-gray-700">{anexo.transcricao}</p>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            onClick={() => transcrever.mutate(mensagemId)}
            disabled={transcrever.isPending}
            className="flex items-center gap-1 rounded-md bg-black/5 px-2 py-1 text-[11px] font-medium text-gray-700 hover:bg-black/10 disabled:opacity-60"
            title="Transformar a fala em texto (fica guardado para todos da conversa)"
          >
            {transcrever.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Captions className="h-3.5 w-3.5" />}
            {transcrever.isPending ? 'Transcrevendo…' : 'Transcrever'}
          </button>
          {transcrever.isError && <span className="text-[11px] text-red-600">{extrairMensagemDeErro(transcrever.error)}</span>}
        </div>
      )}
    </div>
  );
}
