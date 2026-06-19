import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '@/store/auth';
import { AppShell } from '@/components/AppShell';
import { Login } from '@/pages/Login';
import { Otp } from '@/pages/Otp';
import { Home } from '@/pages/Home';
import { Perfil } from '@/pages/Perfil';
import { Atendimentos } from '@/pages/Atendimentos';
import { Exames } from '@/pages/Exames';
import { Laudos } from '@/pages/Laudos';
import { Transporte } from '@/pages/Transporte';

function Protegida({ children }: { children: React.ReactNode }) {
  const token = useAuth((s) => s.token);
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/login/codigo" element={<Otp />} />

        <Route
          element={
            <Protegida>
              <AppShell />
            </Protegida>
          }
        >
          <Route path="/" element={<Home />} />
          <Route path="/atendimentos" element={<Atendimentos />} />
          <Route path="/exames" element={<Exames />} />
          <Route path="/laudos" element={<Laudos />} />
          <Route path="/transporte" element={<Transporte />} />
          <Route path="/perfil" element={<Perfil />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
