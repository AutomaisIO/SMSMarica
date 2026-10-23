let audioCtx: AudioContext | null = null;

/**
 * "Plim" da Comunicação interna: duas notas ascendentes (Mi5 → Lá5) com cauda suave. De propósito
 * DIFERENTE do bip seco de 880 Hz da Central de Atendimento — quem está nas duas precisa saber, pelo
 * ouvido, se é colega ou cidadão.
 */
export function tocarPlim() {
  try {
    const Ctx = window.AudioContext
      ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    audioCtx ??= new Ctx();
    const ctx = audioCtx;
    if (ctx.state === 'suspended') void ctx.resume().catch(() => {});
    const notas: [number, number][] = [
      [659.25, 0],
      [880, 0.13],
    ];
    for (const [freq, atraso] of notas) {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.value = freq;
      osc.connect(gain);
      gain.connect(ctx.destination);
      const t0 = ctx.currentTime + atraso;
      gain.gain.setValueAtTime(0.0001, t0);
      gain.gain.exponentialRampToValueAtTime(0.09, t0 + 0.015);
      gain.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.35);
      osc.start(t0);
      osc.stop(t0 + 0.36);
    }
  } catch {
    /* som é best-effort */
  }
}
