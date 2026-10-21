import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';

/** As regulações com bloco "Avisos ao paciente" na própria tela de Configuração. */
export type SistemaComAvisos = 'sisreg' | 'ser' | 'sernit' | 'esussg';

/** Espelha `AvisosPacienteRegulacaoDto`. */
export type AvisosPacienteRegulacao = {
  sistema: SistemaComAvisos;
  lembreteHabilitado: boolean;
  lembreteDiasAntes: number;
  avisoCancelamentoHabilitado: boolean;
  /** Quando o aviso de cancelamento foi ligado — o que foi desmarcado antes não é avisado. */
  avisoCancelamentoLigadoEm: string | null;
  /** Só leitura: o aviso do agendamento é ligado em outro lugar (Mensageria / unidade). */
  avisoAgendamentoHabilitado: boolean;
  /** Só no SISREG: o aviso de cancelamento depende da conciliação (Mensageria → Regras). */
  conciliacaoCancelamentoHabilitada: boolean | null;
  atualizadoEm: string | null;
};

export type SalvarAvisosPacienteRegulacao = Pick<
  AvisosPacienteRegulacao,
  'lembreteHabilitado' | 'lembreteDiasAntes' | 'avisoCancelamentoHabilitado'
>;

/** Cada regulação responde na rota de Configuração dela (e com a permissão dela). */
const ROTA: Record<SistemaComAvisos, string> = {
  sisreg: '/sisreg/configuracao/avisos-paciente',
  ser: '/regulacao/ser/configuracao/avisos-paciente',
  sernit: '/regulacao/sernit/configuracao/avisos-paciente',
  esussg: '/regulacao/esussg/configuracao/avisos-paciente',
};

export async function obterAvisosPaciente(sistema: SistemaComAvisos): Promise<AvisosPacienteRegulacao> {
  const { data } = await http.get<AvisosPacienteRegulacao>(ROTA[sistema]);
  return data;
}

export async function salvarAvisosPaciente(
  sistema: SistemaComAvisos,
  payload: SalvarAvisosPacienteRegulacao,
): Promise<AvisosPacienteRegulacao> {
  const { data } = await http.put<AvisosPacienteRegulacao>(ROTA[sistema], payload);
  return data;
}

const raiz = ['regulacao', 'avisos-paciente'] as const;

export function useAvisosPaciente(sistema: SistemaComAvisos) {
  return useQuery({ queryKey: [...raiz, sistema], queryFn: () => obterAvisosPaciente(sistema) });
}

export function useSalvarAvisosPaciente(sistema: SistemaComAvisos) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarAvisosPacienteRegulacao) => salvarAvisosPaciente(sistema, payload),
    onSuccess: (dados) => {
      qc.setQueryData([...raiz, sistema], dados);
      // O SISREG grava nas mesmas chaves que a Mensageria mostra: a tela de lá relê.
      if (sistema === 'sisreg') void qc.invalidateQueries({ queryKey: ['mensageria', 'configuracao-confirmacao'] });
    },
  });
}
