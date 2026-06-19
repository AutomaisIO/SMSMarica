import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/app/layout/Layout';
import { InicioPage } from '@/app/pages/InicioPage';
import { NaoEncontradoPage } from '@/app/pages/NaoEncontradoPage';
import { RotaProtegida } from '@/app/router/RotaProtegida';
import { AvaliacoesPage } from '@/features/avaliacoes/pages/AvaliacoesPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { TrocarSenhaPage } from '@/features/auth/pages/TrocarSenhaPage';
import { IaPage } from '@/features/ia/pages/IaPage';
import { IaConfiguracaoPage } from '@/features/ia/pages/IaConfiguracaoPage';
import { IaMelhoriasPage } from '@/features/ia/pages/IaMelhoriasPage';
import { MedicoDetalhePage } from '@/features/medicos/pages/MedicoDetalhePage';
import { MedicosPage } from '@/features/medicos/pages/MedicosPage';
import { ProfissionaisPage } from '@/features/medicos/pages/ProfissionaisPage';
import { MotoristaDetalhePage } from '@/features/motoristas/pages/MotoristaDetalhePage';
import { MotoristasPage } from '@/features/motoristas/pages/MotoristasPage';
import { PacienteDetalhePage } from '@/features/pacientes/pages/PacienteDetalhePage';
import { PacienteFormPage } from '@/features/pacientes/pages/PacienteFormPage';
import { PacientesPage } from '@/features/pacientes/pages/PacientesPage';
import { LaudoEditorPage } from '@/features/laudos/pages/LaudoEditorPage';
import { LaudosListagemPage } from '@/features/laudos/pages/LaudosListagemPage';
import { LaudoTemplateEditorPage } from '@/features/laudo-templates/pages/LaudoTemplateEditorPage';
import { LaudoTemplatesListagemPage } from '@/features/laudo-templates/pages/LaudoTemplatesListagemPage';
import { PacsListagemPage } from '@/features/pacs/pages/PacsListagemPage';
import { PacsViewerPage } from '@/features/pacs/pages/PacsViewerPage';
import { ProcedimentosSigtapPage } from '@/features/procedimentos-sigtap/pages/ProcedimentosSigtapPage';
import { AnamnesePage } from '@/features/anamnese/pages/AnamnesePage';
import { SolicitacaoExameDetalhePage } from '@/features/solicitacoes-exame/pages/SolicitacaoExameDetalhePage';
import { SolicitacaoExameFormPage } from '@/features/solicitacoes-exame/pages/SolicitacaoExameFormPage';
import { SolicitacoesExamePage } from '@/features/solicitacoes-exame/pages/SolicitacoesExamePage';
import { TipoExameFormPage } from '@/features/tipos-exame/pages/TipoExameFormPage';
import { TiposExamePage } from '@/features/tipos-exame/pages/TiposExamePage';
import { PerfisPage } from '@/features/perfis/pages/PerfisPage';
import { RastreamentoPage } from '@/features/rastreamento/pages/RastreamentoPage';
import { MapaFrotaPage } from '@/features/rastreamento/pages/MapaFrotaPage';
import { TiposTratamentoPage } from '@/features/tiposTratamento/pages/TiposTratamentoPage';
import { TransladoDetalhePage } from '@/features/translados/pages/TransladoDetalhePage';
import { TransladoFormPage } from '@/features/translados/pages/TransladoFormPage';
import { TransladosPage } from '@/features/translados/pages/TransladosPage';
import { TratamentoDetalhePage } from '@/features/tratamentos/pages/TratamentoDetalhePage';
import { TratamentoFormPage } from '@/features/tratamentos/pages/TratamentoFormPage';
import { TratamentosPage } from '@/features/tratamentos/pages/TratamentosPage';
import { UnidadeDetalhePage } from '@/features/unidades/pages/UnidadeDetalhePage';
import { UnidadeFormPage } from '@/features/unidades/pages/UnidadeFormPage';
import { UnidadesPage } from '@/features/unidades/pages/UnidadesPage';
import { AlterarMinhaSenhaPage } from '@/features/usuarios/pages/AlterarMinhaSenhaPage';
import { MeuPerfilPage } from '@/features/usuarios/pages/MeuPerfilPage';
import { UsuariosPage } from '@/features/usuarios/pages/UsuariosPage';
import { VeiculoDetalhePage } from '@/features/veiculos/pages/VeiculoDetalhePage';
import { VeiculosPage } from '@/features/veiculos/pages/VeiculosPage';
import { EspecialidadesPage } from '@/features/especialidades/pages/EspecialidadesPage';
import { EquipamentosPage } from '@/features/equipamentos/pages/EquipamentosPage';
import { AgendasPage } from '@/features/agendamentos/pages/AgendasPage';
import { AgendaDetalhePage } from '@/features/agendamentos/pages/AgendaDetalhePage';
import { MarcarConsultaPage } from '@/features/agendamentos/pages/MarcarConsultaPage';
import { SisregConsultaPage } from '@/features/sisreg/pages/SisregConsultaPage';
import { SisregConfiguracaoPage } from '@/features/sisreg/pages/SisregConfiguracaoPage';
import { PepSincronizacaoPage } from '@/features/pep-sincronizacao/pages/PepSincronizacaoPage';
import { ApiTokensPage } from '@/features/api-tokens/pages/ApiTokensPage';
import { useAuth } from '@/shared/auth/authStore';

function RedirecionamentoRaiz() {
  const usuario = useAuth((s) => s.usuario);
  return <Navigate to={usuario ? '/app' : '/login'} replace />;
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<RedirecionamentoRaiz />} />
      <Route path="/login" element={<LoginPage />} />

      {/* Compat: redireciona rotas antigas /operador e /gestor para /app. */}
      <Route path="/operador/*" element={<Navigate to="/app" replace />} />
      <Route path="/gestor/*" element={<Navigate to="/app" replace />} />

      <Route element={<RotaProtegida />}>
        <Route path="/trocar-senha" element={<TrocarSenhaPage />} />
        {/* Janela separada do PACS — fullscreen, sem sidebar/header do app. */}
        <Route path="/pacs/janela" element={<PacsViewerPage janela />} />
      </Route>

      <Route element={<RotaProtegida />}>
        <Route path="/app" element={<Layout />}>
          <Route index element={<InicioPage />} />
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
          <Route path="medicos" element={<MedicosPage />} />
          <Route path="medicos/:id" element={<MedicoDetalhePage />} />
          <Route path="profissionais" element={<ProfissionaisPage />} />
          <Route path="profissionais/:id" element={<MedicoDetalhePage />} />
          <Route path="usuarios" element={<UsuariosPage />} />
          <Route path="meu-perfil" element={<MeuPerfilPage />} />
          <Route path="alterar-senha" element={<AlterarMinhaSenhaPage />} />
          <Route path="tipos-tratamento" element={<TiposTratamentoPage />} />
          <Route path="perfis" element={<PerfisPage />} />
          <Route path="tratamentos" element={<TratamentosPage />} />
          <Route path="tratamentos/novo" element={<TratamentoFormPage />} />
          <Route path="tratamentos/:id" element={<TratamentoDetalhePage />} />
          <Route path="translados" element={<TransladosPage />} />
          <Route path="translados/novo" element={<TransladoFormPage />} />
          <Route path="translados/:id" element={<TransladoDetalhePage />} />
          <Route path="translados/:id/editar" element={<TransladoFormPage />} />
          <Route path="rastreamento" element={<RastreamentoPage />} />
          <Route path="rastreamento/mapa" element={<MapaFrotaPage />} />
          <Route path="avaliacoes" element={<AvaliacoesPage />} />
          <Route path="pacs" element={<PacsListagemPage />} />
          <Route path="laudos" element={<LaudosListagemPage />} />
          <Route path="laudos/novo" element={<LaudoEditorPage />} />
          <Route path="laudos/:id" element={<LaudoEditorPage />} />
          <Route path="laudo-templates" element={<LaudoTemplatesListagemPage />} />
          <Route path="laudo-templates/novo" element={<LaudoTemplateEditorPage />} />
          <Route path="laudo-templates/:id" element={<LaudoTemplateEditorPage />} />
          <Route path="solicitacoes-exame" element={<SolicitacoesExamePage />} />
          <Route path="solicitacoes-exame/novo" element={<SolicitacaoExameFormPage />} />
          <Route path="solicitacoes-exame/:id" element={<SolicitacaoExameDetalhePage />} />
          <Route path="solicitacoes-exame/:id/editar" element={<SolicitacaoExameFormPage />} />
          <Route path="anamnese" element={<AnamnesePage />} />
          <Route path="tipos-exame" element={<TiposExamePage />} />
          <Route path="tipos-exame/novo" element={<TipoExameFormPage />} />
          <Route path="tipos-exame/:id" element={<TipoExameFormPage />} />
          <Route path="procedimentos-sigtap" element={<ProcedimentosSigtapPage />} />
          <Route path="ia" element={<IaPage />} />
          <Route path="ia/configuracao" element={<IaConfiguracaoPage />} />
          <Route path="ia/melhorias" element={<IaMelhoriasPage />} />
          <Route path="especialidades" element={<EspecialidadesPage />} />
          <Route path="equipamentos" element={<EquipamentosPage />} />
          <Route path="agendas" element={<AgendasPage />} />
          <Route path="agendas/:id" element={<AgendaDetalhePage />} />
          <Route path="agendamentos/marcar" element={<MarcarConsultaPage />} />
          <Route path="sisreg" element={<SisregConsultaPage />} />
          <Route path="sisreg/configuracao" element={<SisregConfiguracaoPage />} />
          <Route path="pep-sincronizacao" element={<PepSincronizacaoPage />} />
          <Route path="api-tokens" element={<ApiTokensPage />} />
        </Route>
      </Route>

      <Route path="*" element={<NaoEncontradoPage />} />
    </Routes>
  );
}
