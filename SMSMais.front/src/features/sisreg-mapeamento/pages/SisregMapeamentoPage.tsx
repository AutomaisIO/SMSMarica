import { Navigate } from 'react-router-dom';
import { useAuth } from '@/shared/auth/authStore';

/**
 * APOSENTADA. O mapeamento do SISREG virou a aba <b>SISREG</b> do detalhe da unidade
 * (`/app/unidades/{id}`), junto com a credencial e o sincronismo diário — um lugar só, operando
 * a unidade da ROTA em vez do seletor do topo.
 *
 * A rota continua existindo só para não quebrar link salvo ou favorito: redireciona para a
 * unidade ativa, ou para a lista quando não há uma escolhida.
 */
export function SisregMapeamentoPage() {
  const unidadeAtivaId = useAuth((s) => s.unidadeAtivaId);

  return (
    <Navigate
      to={unidadeAtivaId ? `/app/unidades/${unidadeAtivaId}` : '/app/unidades'}
      replace
    />
  );
}
