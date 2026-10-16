import { Link, useLocation } from 'react-router-dom';
import { Siren, X } from 'lucide-react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { useRastreamentoHub } from '@/features/ocorrencias/hooks/useRastreamentoHub';
import { TIPOS_OCORRENCIA } from '@/features/ocorrencias/lib/rotulos';
import { useAlertasOcorrencia } from '@/features/ocorrencias/store/alertasOcorrencia';

const ROTA_OCORRENCIAS = '/app/rastreamento/ocorrencias';

/**
 * Aviso global da central (montado no Layout autenticado): cada REPORT de tablet que chega pelo
 * hub vira uma faixa vermelha no topo de QUALQUER tela, até o operador dispensar ou abrir a lista.
 * Mantém o hub /hubs/rastreamento ligado enquanto houver o módulo Rastreamento.
 */
export function AlertaOcorrencias() {
  const podeVer = useTemConsulta('Rastreamento');
  useRastreamentoHub(podeVer);
  const alertas = useAlertasOcorrencia((s) => s.alertas);
  const dispensar = useAlertasOcorrencia((s) => s.dispensar);
  const dispensarTodos = useAlertasOcorrencia((s) => s.dispensarTodos);
  const { pathname } = useLocation();

  if (!podeVer || alertas.length === 0) return null;

  return (
    <div className="fixed inset-x-0 top-0 z-50 space-y-1 p-2" role="alert" aria-live="assertive">
      {alertas.slice(0, 3).map((a) => {
        const tipo = TIPOS_OCORRENCIA[a.tipo] ?? TIPOS_OCORRENCIA.Outro;
        const Icone = tipo.icone;
        return (
          <div
            key={a.ocorrenciaId}
            className="mx-auto flex max-w-3xl items-center gap-3 rounded-lg bg-red-600 px-4 py-3 text-white shadow-lg"
          >
            <Siren className="h-6 w-6 shrink-0 animate-pulse" />
            <Icone className="h-5 w-5 shrink-0" />
            <div className="min-w-0 flex-1">
              <div className="font-semibold">Ocorrência: {tipo.rotulo}</div>
              <div className="text-sm text-red-50">
                Veículo {a.veiculoPlaca} ·{' '}
                {new Date(a.recebidoEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
              </div>
            </div>
            {pathname !== ROTA_OCORRENCIAS ? (
              <Link
                to={ROTA_OCORRENCIAS}
                onClick={dispensarTodos}
                className="rounded-md bg-white px-3 py-1.5 text-sm font-semibold text-red-700 hover:bg-red-50"
              >
                Ver ocorrências
              </Link>
            ) : null}
            <button
              type="button"
              onClick={() => dispensar(a.ocorrenciaId)}
              className="rounded-md p-1 hover:bg-red-700"
              aria-label="Dispensar aviso"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        );
      })}
      {alertas.length > 3 ? (
        <div className="mx-auto max-w-3xl text-center text-xs font-medium text-red-700">
          + {alertas.length - 3} ocorrência(s) — veja a lista
        </div>
      ) : null}
    </div>
  );
}
