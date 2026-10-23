import { useCallback, useEffect, useRef, useState } from 'react';

/** Gravação para depois de 10 min: áudio de recado, não de reunião (e o anexo tem teto de 25 MB). */
const DURACAO_MAXIMA_S = 600;

export type AudioGravado = { arquivo: File; duracaoSegundos: number };

type Estado = 'parado' | 'gravando';

function mimePreferido(): string | undefined {
  if (typeof MediaRecorder === 'undefined') return undefined;
  for (const t of ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/mp4']) {
    if (MediaRecorder.isTypeSupported(t)) return t;
  }
  return undefined;
}

function extensao(mime: string): string {
  if (mime.includes('ogg')) return 'ogg';
  if (mime.includes('mp4')) return 'm4a';
  return 'webm';
}

/**
 * Grava áudio do microfone no navegador (MediaRecorder; no Chrome sai webm/opus). `parar(true)`
 * devolve o arquivo; `parar(false)` descarta. O microfone é liberado ao terminar — o ícone de
 * gravação do navegador some junto.
 */
export function useGravadorAudio(aoAtingirTeto?: (a: AudioGravado) => void) {
  const tetoRef = useRef(aoAtingirTeto);
  useEffect(() => {
    tetoRef.current = aoAtingirTeto;
  }, [aoAtingirTeto]);
  const [estado, setEstado] = useState<Estado>('parado');
  const [segundos, setSegundos] = useState(0);
  const [erro, setErro] = useState<string | null>(null);
  const recRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const pedacosRef = useRef<Blob[]>([]);
  const inicioRef = useRef(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | undefined>(undefined);
  const resolverRef = useRef<((a: AudioGravado | null) => void) | null>(null);
  const manterRef = useRef(false);

  const liberar = useCallback(() => {
    clearInterval(timerRef.current);
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    recRef.current = null;
  }, []);

  useEffect(() => () => {
    manterRef.current = false;
    if (recRef.current && recRef.current.state !== 'inactive') recRef.current.stop();
    liberar();
  }, [liberar]);

  const parar = useCallback((manter: boolean): Promise<AudioGravado | null> => {
    const rec = recRef.current;
    if (!rec || rec.state === 'inactive') return Promise.resolve(null);
    manterRef.current = manter;
    return new Promise((resolve) => {
      resolverRef.current = resolve;
      rec.stop();
    });
  }, []);

  const iniciar = useCallback(async () => {
    setErro(null);
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      setErro('Este navegador não grava áudio.');
      return;
    }
    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      setErro('Sem acesso ao microfone. Libere o microfone para este site no navegador.');
      return;
    }
    const mime = mimePreferido();
    const rec = mime ? new MediaRecorder(stream, { mimeType: mime }) : new MediaRecorder(stream);
    streamRef.current = stream;
    recRef.current = rec;
    pedacosRef.current = [];
    rec.ondataavailable = (e) => {
      if (e.data.size > 0) pedacosRef.current.push(e.data);
    };
    rec.onstop = () => {
      const duracao = Math.max(1, Math.round((Date.now() - inicioRef.current) / 1000));
      const tipo = (rec.mimeType || mime || 'audio/webm').split(';')[0];
      const blob = new Blob(pedacosRef.current, { type: tipo });
      liberar();
      setEstado('parado');
      setSegundos(0);
      const resolver = resolverRef.current;
      resolverRef.current = null;
      if (!manterRef.current || blob.size === 0) {
        resolver?.(null);
        return;
      }
      const carimbo = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
      const audio = {
        arquivo: new File([blob], `audio-${carimbo}.${extensao(tipo)}`, { type: tipo }),
        duracaoSegundos: duracao,
      };
      // Sem quem esperasse (parou sozinho no teto): entrega pelo callback.
      if (resolver) resolver(audio);
      else tetoRef.current?.(audio);
    };
    inicioRef.current = Date.now();
    rec.start(250);
    setEstado('gravando');
    setSegundos(0);
    timerRef.current = setInterval(() => {
      const s = Math.round((Date.now() - inicioRef.current) / 1000);
      setSegundos(s);
      // No teto, para e GUARDA (quem está falando não perde o que gravou).
      if (s >= DURACAO_MAXIMA_S && recRef.current?.state === 'recording') {
        manterRef.current = true;
        recRef.current.stop();
      }
    }, 250);
  }, [liberar]);

  return { estado, segundos, erro, iniciar, parar, limparErro: () => setErro(null) };
}
