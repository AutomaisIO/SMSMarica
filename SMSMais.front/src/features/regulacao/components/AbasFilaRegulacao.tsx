import type { ResumoFilaRegulacao, StatusRegulacao } from '../tiposSolicitacao';

/**
 * As abas da fila. Cada uma agrupa os status que, para quem atende, são a mesma espera.
 *
 * <p><b>Por que agrupar:</b> são 12 status e ninguém raciocina em 12 caixas.
 * `EnviandoAoSistema`/`EnviadaAoSistema`/`FalhaEnvio` são "acabou de sair daqui" — inclusive a
 * falha, que é o caso que mais precisa de olho e ficaria escondido numa aba própria quase sempre
 * vazia.</p>
 */
export type AbaFila = {
  id: string;
  rotulo: string;
  status: StatusRegulacao[];
};

export const ABAS_FILA: AbaFila[] = [
  { id: 'rascunhos', rotulo: 'Rascunhos', status: ['Rascunho'] },
  { id: 'pre', rotulo: 'Pré-regulação', status: ['PendenteRegulacao'] },
  { id: 'analise', rotulo: 'Em análise', status: ['EmAnalise'] },
  { id: 'devolvidas', rotulo: 'Devolvidas', status: ['Devolvida'] },
  {
    id: 'enviadas',
    rotulo: 'Enviadas',
    status: ['EnviandoAoSistema', 'EnviadaAoSistema', 'FalhaEnvio'],
  },
  { id: 'externa', rotulo: 'No sistema', status: ['EmFilaExterna', 'Agendada'] },
  { id: 'encerradas', rotulo: 'Encerradas', status: ['Concluida', 'Cancelada', 'Recusada'] },
];

export function contarAba(resumo: ResumoFilaRegulacao | undefined, aba: AbaFila): number {
  if (!resumo) return 0;
  return aba.status.reduce((total, s) => total + (resumo.porStatus[s] ?? 0), 0);
}

export function AbasFilaRegulacao({
  ativa,
  aoTrocar,
  resumo,
}: {
  ativa: string;
  aoTrocar: (id: string) => void;
  resumo: ResumoFilaRegulacao | undefined;
}) {
  return (
    <div className="flex flex-wrap gap-1 border-b border-slate-200">
      {ABAS_FILA.map((aba) => {
        const total = contarAba(resumo, aba);
        const selecionada = aba.id === ativa;
        return (
          <button
            key={aba.id}
            type="button"
            onClick={() => aoTrocar(aba.id)}
            className={`-mb-px flex items-center gap-1.5 border-b-2 px-3 py-2 text-sm ${
              selecionada
                ? 'border-red-600 font-medium text-red-700'
                : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            {aba.rotulo}
            {total > 0 && (
              <span
                className={`rounded-full px-1.5 text-xs ${
                  selecionada ? 'bg-red-100 text-red-800' : 'bg-slate-100 text-slate-600'
                }`}
              >
                {total}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
