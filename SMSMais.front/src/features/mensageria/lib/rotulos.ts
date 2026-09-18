import { useEffect, useState } from 'react';
import type { FinalidadeComunicacao, StatusConfirmacao, StatusNotificacao } from '@/features/mensageria/types';

/** Rótulos e classes de badge compartilhados por Mensageria e Confirmações (uma fonte só). */

export const ROTULO_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'Na fila',
  Enviada: 'Enviada',
  Entregue: 'Entregue',
  Lida: 'Lida',
  Falha: 'Falha',
  SemTelefoneValido: 'Sem celular válido',
  AguardandoTelefoneVerificado: 'Aguardando contato verificado',
  AguardandoVerificacaoCadastral: 'Aguardando o paciente se identificar',
  AguardandoCorrecaoContato: 'Número inválido (não é do paciente)',
  SubstituidaPorAtendente: 'Atendida por pessoa',
};

export const CLASSE_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'badge-gray',
  Enviada: 'badge-info',
  Entregue: 'badge-info',
  Lida: 'badge-success',
  Falha: 'badge-danger',
  SemTelefoneValido: 'badge-warning',
  AguardandoTelefoneVerificado: 'badge-warning',
  AguardandoVerificacaoCadastral: 'badge-warning',
  AguardandoCorrecaoContato: 'badge-danger',
  SubstituidaPorAtendente: 'badge-gray',
};

export const ROTULO_RESPOSTA: Record<StatusConfirmacao, string> = {
  Pendente: 'Sem resposta',
  Confirmada: 'Confirmou',
  Cancelada: 'Não vai',
};

export const CLASSE_RESPOSTA: Record<StatusConfirmacao, string> = {
  Pendente: 'badge-gray',
  Confirmada: 'badge-success',
  Cancelada: 'badge-danger',
};

export const ROTULO_CANAL: Record<string, string> = {
  'whatsapp-link': 'Link do WhatsApp',
  'whatsapp-quickreply': 'Botões do WhatsApp',
  'whatsapp-robo': 'Robô do WhatsApp',
  app: 'App do cidadão',
  presencial: 'Presencial (recepção)',
  atendente: 'Atendente (Confirmações)',
  sandbox: 'Sandbox',
};

export const ROTULO_FINALIDADE: Record<FinalidadeComunicacao, string> = {
  ConfirmacaoAgendamento: 'Confirmação de agendamento',
  ExameLiberado: 'Exame liberado',
  LaudoPronto: 'Laudo pronto',
};

export function rotuloCanal(canal: string | null | undefined): string {
  if (!canal) return '—';
  return ROTULO_CANAL[canal] ?? canal;
}

export function useDebounce<T>(valor: T, ms = 400): T {
  const [v, setV] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setV(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return v;
}

export function dataHora(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function hora(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

/** dd/mm (seg) — o dia como quem lê a agenda enxerga. */
export function diaLegivel(iso: string): string {
  const d = new Date(`${iso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', weekday: 'short' });
}

export function hojeMais(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}
