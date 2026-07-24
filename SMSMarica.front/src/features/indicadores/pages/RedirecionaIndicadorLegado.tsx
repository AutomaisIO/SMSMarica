import { Navigate, useParams } from 'react-router-dom';

/**
 * Compatibilidade: as abas antes eram itens de menu (`/app/indicadores/{aba}`).
 * Agora a unidade é o item de menu e as abas viraram TABS dentro da tela da unidade.
 * Redireciona qualquer link antigo para a nova estrutura por unidade (Conde).
 */
export function RedirecionaIndicadorLegado() {
  const { aba } = useParams<{ aba: string }>();
  return <Navigate to={`/app/indicadores/conde/${aba ?? 'adulto'}`} replace />;
}
