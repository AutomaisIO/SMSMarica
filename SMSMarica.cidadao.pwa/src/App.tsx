import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '@/store/auth';
import { AppShell } from '@/components/AppShell';
import { ConsentGate } from '@/components/ConsentGate';
import { Login } from '@/pages/Login';
import { Otp } from '@/pages/Otp';
import { Home } from '@/pages/Home';
import { Perfil } from '@/pages/Perfil';
import { Atendimentos } from '@/pages/Atendimentos';
import { Exames } from '@/pages/Exames';
import { Laudos } from '@/pages/Laudos';
import { Transporte } from '@/pages/Transporte';
import { ConsultasAgendadas, ExamesAgendados } from '@/pages/Agendados';
import { Documento } from '@/pages/Documento';

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
        {/* Link público de download (uso único) enviado ao paciente — sem autenticação. */}
        <Route path="/documento/:token" element={<Documento />} />

        <Route
          element={
            <Protegida>
              <ConsentGate>
                <AppShell />
              </ConsentGate>
            </Protegida>
          }
        >
          <Route path="/" element={<Home />} />
          <Route path="/atendimentos" element={<Atendimentos />} />
          <Route path="/agendados/consultas" element={<ConsultasAgendadas />} />
          <Route path="/agendados/exames" element={<ExamesAgendados />} />
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
