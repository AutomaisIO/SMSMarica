import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/app/layout/Layout';
import { GestorInicioPage } from '@/app/pages/GestorInicioPage';
import { NaoEncontradoPage } from '@/app/pages/NaoEncontradoPage';
import { OperadorInicioPage } from '@/app/pages/OperadorInicioPage';
import { RotaProtegida } from '@/app/router/RotaProtegida';
import { AvaliacoesPage } from '@/features/avaliacoes/pages/AvaliacoesPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { MotoristaDetalhePage } from '@/features/motoristas/pages/MotoristaDetalhePage';
import { MotoristasPage } from '@/features/motoristas/pages/MotoristasPage';
import { PacsViewerPage } from '@/features/pacs/pages/PacsViewerPage';
import { PacienteDetalhePage } from '@/features/pacientes/pages/PacienteDetalhePage';
import { PacienteFormPage } from '@/features/pacientes/pages/PacienteFormPage';
import { PacientesPage } from '@/features/pacientes/pages/PacientesPage';
import { RastreamentoPage } from '@/features/rastreamento/pages/RastreamentoPage';
import { TransladoDetalhePage } from '@/features/translados/pages/TransladoDetalhePage';
import { TransladoFormPage } from '@/features/translados/pages/TransladoFormPage';
import { TransladosPage } from '@/features/translados/pages/TransladosPage';
import { TratamentoDetalhePage } from '@/features/tratamentos/pages/TratamentoDetalhePage';
import { TratamentoFormPage } from '@/features/tratamentos/pages/TratamentoFormPage';
import { TratamentosPage } from '@/features/tratamentos/pages/TratamentosPage';
import { UnidadeDetalhePage } from '@/features/unidades/pages/UnidadeDetalhePage';
import { UnidadeFormPage } from '@/features/unidades/pages/UnidadeFormPage';
import { UnidadesPage } from '@/features/unidades/pages/UnidadesPage';
import { UsuariosPage } from '@/features/usuarios/pages/UsuariosPage';
import { VeiculoDetalhePage } from '@/features/veiculos/pages/VeiculoDetalhePage';
import { VeiculosPage } from '@/features/veiculos/pages/VeiculosPage';
import { useAuth } from '@/shared/auth/authStore';

function RedirecionamentoRaiz() {
  const usuario = useAuth((s) => s.usuario);
  if (!usuario) return <Navigate to="/login" replace />;
  return <Navigate to={usuario.perfil === 'operador' ? '/operador' : '/gestor'} replace />;
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<RedirecionamentoRaiz />} />
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RotaProtegida perfil="operador" />}>
        <Route path="/operador" element={<Layout perfil="operador" />}>
          <Route index element={<OperadorInicioPage />} />
          <Route path="pacientes" element={<PacientesPage />} />
          <Route path="pacientes/novo" element={<PacienteFormPage />} />
          <Route path="pacientes/:id" element={<PacienteDetalhePage />} />
          <Route path="pacientes/:id/editar" element={<PacienteFormPage />} />
          <Route path="unidades" element={<UnidadesPage />} />
          <Route path="unidades/novo" element={<UnidadeFormPage />} />
          <Route path="unidades/:id" element={<UnidadeDetalhePage />} />
          <Route path="unidades/:id/editar" element={<UnidadeFormPage />} />
          <Route path="veiculos" element={<VeiculosPage />} />
          <Route path="veiculos/:id" element={<VeiculoDetalhePage />} />
          <Route path="motoristas" element={<MotoristasPage />} />
          <Route path="motoristas/:id" element={<MotoristaDetalhePage />} />
          <Route path="usuarios" element={<UsuariosPage />} />
          <Route path="tratamentos" element={<TratamentosPage />} />
          <Route path="tratamentos/novo" element={<TratamentoFormPage />} />
          <Route path="tratamentos/:id" element={<TratamentoDetalhePage />} />
          <Route path="translados" element={<TransladosPage />} />
          <Route path="translados/novo" element={<TransladoFormPage />} />
          <Route path="translados/:id" element={<TransladoDetalhePage />} />
          <Route path="translados/:id/editar" element={<TransladoFormPage />} />
          <Route path="rastreamento" element={<RastreamentoPage />} />
          <Route path="avaliacoes" element={<AvaliacoesPage />} />
          <Route path="pacs" element={<PacsViewerPage />} />
        </Route>
      </Route>

      <Route element={<RotaProtegida perfil="gestor" />}>
        <Route path="/gestor" element={<Layout perfil="gestor" />}>
          <Route index element={<GestorInicioPage />} />
          <Route path="pacs" element={<PacsViewerPage />} />
        </Route>
      </Route>

      <Route path="*" element={<NaoEncontradoPage />} />
    </Routes>
  );
}
