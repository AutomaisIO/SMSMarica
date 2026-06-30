import { Link, useLocation } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import {
  caminhoHub,
  encontrarSecaoPorPath,
  type ItemMenu,
} from '@/app/layout/menuConfig';
import { useAuth } from '@/shared/auth/authStore';

/**
 * Barra fina exibida acima do conteúdo quando a rota atual pertence a uma seção
 * com mais de uma opção. Dá o "Voltar ao menu" (página-hub dos botões), de onde
 * dá pra trocar a tela default da seção.
 */
export function MenuContextoBar() {
  const { pathname } = useLocation();
  const permissoes = useAuth((s) => s.permissoes);

  const secao = encontrarSecaoPorPath(pathname);
  if (!secao || !secao.titulo) return null;

  const temAcesso = (item: ItemMenu) =>
    !item.modulo || (permissoes[item.modulo] ?? []).includes('Consulta');

  // Só faz sentido voltar pra um hub quando há mais de uma opção visível.
  const visiveis = secao.itens.filter(temAcesso);
  if (visiveis.length < 2) return null;

  return (
    <div className="mb-4">
      <Link
        to={caminhoHub(secao.id)}
        className="inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-medium text-gray-600 transition-colors hover:bg-gray-100 hover:text-primary-700"
      >
        <ChevronLeft className="w-4 h-4" />
        <span>
          Voltar ao menu <strong className="font-semibold">{secao.titulo}</strong>
        </span>
      </Link>
    </div>
  );
}
