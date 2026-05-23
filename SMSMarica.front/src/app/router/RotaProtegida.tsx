import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/shared/auth/authStore';

export function RotaProtegida() {
  const usuario = useAuth((s) => s.usuario);
  const location = useLocation();

  if (!usuario) {
    return <Navigate to="/login" replace state={{ de: location.pathname }} />;
  }

  return <Outlet />;
}
