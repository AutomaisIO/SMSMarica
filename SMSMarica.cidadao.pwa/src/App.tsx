import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '@/store/auth';
import { Login } from '@/pages/Login';
import { Otp } from '@/pages/Otp';
import { Agenda } from '@/pages/Agenda';
import { Translado } from '@/pages/Translado';

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
          path="/"
          element={
            <Protegida>
              <Agenda />
            </Protegida>
          }
        />
        <Route
          path="/translado/:id"
          element={
            <Protegida>
              <Translado />
            </Protegida>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
