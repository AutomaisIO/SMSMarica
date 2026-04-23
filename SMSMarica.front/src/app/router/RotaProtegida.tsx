import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth, type Perfil } from '@/shared/auth/authStore';

export function RotaProtegida({ perfil }: { perfil?: Perfil }) {
  const usuario = useAuth((s) => s.usuario);
  const location = useLocation();

  if (!usuario) {
    return <Navigate to="/login" replace state={{ de: location.pathname }} />;
  }

  if (perfil && usuario.perfil !== perfil) {
    const destino = usuario.perfil === 'operador' ? '/operador' : '/gestor';
    return <Navigate to={destino} replace />;
  }

  return <Outlet />;
}
