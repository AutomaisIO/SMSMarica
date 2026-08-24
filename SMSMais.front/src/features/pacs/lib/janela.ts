// Compatibilidade: a função foi promovida para shared/lib porque outras features
// (laudos) também abrem popups. Reexporta aqui pra não quebrar callers antigos.
export { abrirJanelaSolta } from '@/shared/lib/janela';
