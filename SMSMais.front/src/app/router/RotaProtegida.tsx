import { useEffect, useRef } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/shared/auth/authStore';

export function RotaProtegida() {
  const usuario = useAuth((s) => s.usuario);
  const recarregarPermissoes = useAuth((s) => s.recarregarPermissoes);
  const location = useLocation();
  const recarregado = useRef(false);

  // Permissões ficam cacheadas em localStorage entre sessões. Quando um módulo
  // novo é adicionado (e o seeder do back amplia o perfil do usuário), o
  // localStorage stale faz o menu sumir. Refresca uma vez por carga do app.
  useEffect(() => {
    if (usuario && !recarregado.current) {
      recarregado.current = true;
      recarregarPermissoes().catch(() => {
        // 401/erro de rede → o interceptor do http client já desloga se for o caso.
      });
    }
  }, [usuario, recarregarPermissoes]);

  if (!usuario) {
    return <Navigate to="/login" replace state={{ de: location.pathname }} />;
  }

  // Troca de senha obrigatória bloqueia qualquer outra rota até ser concluída.
  if (usuario.deveTrocarSenha && location.pathname !== '/trocar-senha') {
    return <Navigate to="/trocar-senha" replace />;
  }

  return <Outlet />;
}
