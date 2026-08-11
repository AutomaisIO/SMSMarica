import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '@/store/auth';
import { AppShell } from '@/components/AppShell';
import { ConsentGate } from '@/components/ConsentGate';
import { Login } from '@/pages/Login';
import { Otp } from '@/pages/Otp';
import { Verificacao } from '@/pages/Verificacao';
import { Home } from '@/pages/Home';
import { Perfil } from '@/pages/Perfil';
import { Atendimentos } from '@/pages/Atendimentos';
import { Exames } from '@/pages/Exames';
import { Chat } from '@/pages/Chat';
import { Transporte } from '@/pages/Transporte';
import { ConsultasAgendadas, ExamesAgendados } from '@/pages/Agendados';
import { TicketExame } from '@/pages/TicketExame';
import { Documento } from '@/pages/Documento';
import { Entrar } from '@/pages/Entrar';
import { Pesquisa } from '@/pages/Pesquisa';

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
        <Route path="/login/verificacao" element={<Verificacao />} />
        <Route path="/login/codigo" element={<Otp />} />
        {/* Link público de download (uso único) enviado ao paciente — sem autenticação. */}
        <Route path="/documento/:token" element={<Documento />} />
        {/* Magic-link: login em 1 clique a partir do WhatsApp. */}
        <Route path="/entrar/:token" element={<Entrar />} />
        {/* Pesquisa de satisfação pelo link do WhatsApp — token escopado, abre SÓ a pesquisa
            (nunca uma sessão), então vive fora da área protegida. */}
        <Route path="/pesquisa/:token" element={<Pesquisa />} />

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
          <Route path="/atendimentos/:id/pesquisa" element={<Pesquisa />} />
          <Route path="/agendados/consultas" element={<ConsultasAgendadas />} />
          <Route path="/agendados/exames" element={<ExamesAgendados />} />
          <Route path="/agendados/exames/:id" element={<TicketExame />} />
          <Route path="/exames" element={<Exames />} />
          {/* Laudo pertence ao exame (abre dentro do card em /exames); /laudos redireciona. */}
          <Route path="/laudos" element={<Navigate to="/exames" replace />} />
          <Route path="/chat" element={<Chat />} />
          <Route path="/transporte" element={<Transporte />} />
          <Route path="/perfil" element={<Perfil />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
