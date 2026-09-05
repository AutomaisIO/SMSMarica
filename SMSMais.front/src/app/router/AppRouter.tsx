import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/app/layout/Layout';
import { InicioPage } from '@/app/pages/InicioPage';
import { MenuHubPage } from '@/app/pages/MenuHubPage';
import { NaoEncontradoPage } from '@/app/pages/NaoEncontradoPage';
import { RotaProtegida } from '@/app/router/RotaProtegida';
import { AvaliacoesPage } from '@/features/avaliacoes/pages/AvaliacoesPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { TrocarSenhaPage } from '@/features/auth/pages/TrocarSenhaPage';
import { ConsultaInteligentePage } from '@/features/consulta-inteligente/pages/ConsultaInteligentePage';
import { IaConfiguracaoPage } from '@/features/ia/pages/IaConfiguracaoPage';
import { IaMelhoriasPage } from '@/features/ia/pages/IaMelhoriasPage';
import { AgenteIaPage } from '@/features/agente-ia/pages/AgenteIaPage';
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
import { LaudoConfiguracaoPage } from '@/features/laudo-configuracao/pages/LaudoConfiguracaoPage';
import { PacsListagemPage } from '@/features/pacs/pages/PacsListagemPage';
import { PacsViewerPage } from '@/features/pacs/pages/PacsViewerPage';
import { ProcedimentosSigtapPage } from '@/features/procedimentos-sigtap/pages/ProcedimentosSigtapPage';
import { AnamnesePage } from '@/features/anamnese/pages/AnamnesePage';
import { ExamesAnterioresJanelaPage } from '@/features/exames-anteriores/pages/ExamesAnterioresJanelaPage';
import { SolicitacaoExameDetalhePage } from '@/features/solicitacoes-exame/pages/SolicitacaoExameDetalhePage';
import { SolicitacaoExameFormPage } from '@/features/solicitacoes-exame/pages/SolicitacaoExameFormPage';
import { SolicitacoesExamePage } from '@/features/solicitacoes-exame/pages/SolicitacoesExamePage';
import { ConsultasPage } from '@/features/consultas/pages/ConsultasPage';
import { ConsultaDetalhePage } from '@/features/consultas/pages/ConsultaDetalhePage';
import { MapeamentoSigtapPage } from '@/features/mapeamento-sigtap/pages/MapeamentoSigtapPage';
import { ImportacaoSisregPage } from '@/features/importacao-sisreg/pages/ImportacaoSisregPage';
import { TipoExameFormPage } from '@/features/tipos-exame/pages/TipoExameFormPage';
import { TiposExamePage } from '@/features/tipos-exame/pages/TiposExamePage';
import { PerfisPage } from '@/features/perfis/pages/PerfisPage';
import { RastreamentoPage } from '@/features/rastreamento/pages/RastreamentoPage';
import { MapaFrotaPage } from '@/features/rastreamento/pages/MapaFrotaPage';
import { TiposTratamentoPage } from '@/features/tiposTratamento/pages/TiposTratamentoPage';
import { TransladoDetalhePage } from '@/features/translados/pages/TransladoDetalhePage';
import { TransladoFormPage } from '@/features/translados/pages/TransladoFormPage';
import { TransladosPage } from '@/features/translados/pages/TransladosPage';
import { GerarTransladoPage } from '@/features/translados/pages/GerarTransladoPage';
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
import { IndicadoresAbaPage } from '@/features/indicadores/pages/IndicadoresAbaPage';
import { RedirecionaIndicadorLegado } from '@/features/indicadores/pages/RedirecionaIndicadorLegado';
import { EquipamentosPage } from '@/features/equipamentos/pages/EquipamentosPage';
import { SisregConsultaPage } from '@/features/sisreg/pages/SisregConsultaPage';
import { SisregConfiguracaoPage } from '@/features/sisreg/pages/SisregConfiguracaoPage';
import { SisregMapeamentoPage } from '@/features/sisreg-mapeamento/pages/SisregMapeamentoPage';
import { SerNotificacoesPage } from '@/features/ser/pages/SerNotificacoesPage';
import { SerNovaSolicitacaoPage } from '@/features/ser/pages/SerNovaSolicitacaoPage';
import { SerFilaPage } from '@/features/ser/pages/SerFilaPage';
import { SerSolicitacaoDetalhePage } from '@/features/ser/pages/SerSolicitacaoDetalhePage';
import { RegulacaoConfiguracaoPage } from '@/features/ser/pages/RegulacaoConfiguracaoPage';
import { SernitNotificacoesPage } from '@/features/sernit/pages/SernitNotificacoesPage';
import { SernitNovaSolicitacaoPage } from '@/features/sernit/pages/SernitNovaSolicitacaoPage';
import { SernitFilaPage } from '@/features/sernit/pages/SernitFilaPage';
import { SernitSolicitacaoDetalhePage } from '@/features/sernit/pages/SernitSolicitacaoDetalhePage';
import RegulacaoSernitConfiguracaoPage from '@/features/sernit/pages/RegulacaoSernitConfiguracaoPage';
import { PepSincronizacaoPage } from '@/features/pep-sincronizacao/pages/PepSincronizacaoPage';
import { ApiTokensPage } from '@/features/api-tokens/pages/ApiTokensPage';
import { IntegracoesPage } from '@/features/integracoes/pages/IntegracoesPage';
import { FaturamentoPage } from '@/features/faturamento/pages/FaturamentoPage';
import { AuditoriaPage } from '@/features/auditoria/pages/AuditoriaPage';
import { InstituicaoPage } from '@/features/instituicao/pages/InstituicaoPage';
import { ErrosPage } from '@/features/erros/pages/ErrosPage';
import { ConversasPage } from '@/features/conversas/pages/ConversasPage';
import { EstatisticasPage } from '@/features/estatisticas/pages/EstatisticasPage';
import { RelatoriosImagemPage } from '@/features/relatorios-imagem/pages/RelatoriosImagemPage';
import { ChatJanelaPage } from '@/features/conversas/pages/ChatJanelaPage';
import { MeusTicketsPage } from '@/features/tickets/pages/MeusTicketsPage';
import { TicketDetalhePage } from '@/features/tickets/pages/TicketDetalhePage';
import { GestaoTicketsPage } from '@/features/tickets/pages/GestaoTicketsPage';
import { NotificacoesAgendamentoPage } from '@/features/notificacoes-agendamento/pages/NotificacoesAgendamentoPage';
import { RespostasRapidasPage } from '@/features/respostas-rapidas/pages/RespostasRapidasPage';
import { RoboAtendimentoPage } from '@/features/robo-atendimento/pages/RoboAtendimentoPage';
import { AgendaPage } from '@/features/agenda/pages/AgendaPage';
import { AgendaAnalisePage } from '@/features/agenda/pages/AgendaAnalisePage';
import { AgendaDemandaPage } from '@/features/agenda/pages/AgendaDemandaPage';
import { AlteracoesAgendaPage } from '@/features/alteracoes-agenda/pages/AlteracoesAgendaPage';
import { PendenciasCadastroPage } from '@/features/pendencias-cadastro/pages/PendenciasCadastroPage';
import { SandboxPage } from '@/features/sandbox/pages/SandboxPage';
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
        {/* Janela solta da anamnese e dos exames anteriores (abertas do Laudar). */}
        <Route path="/anamnese/janela" element={<AnamnesePage janela />} />
        <Route path="/exames-anteriores/janela" element={<ExamesAnterioresJanelaPage />} />
        {/* Central de Atendimento em janela separada do navegador (ticket #18). */}
        <Route path="/chat/janela" element={<ChatJanelaPage />} />
      </Route>

      <Route element={<RotaProtegida />}>
        <Route path="/app" element={<Layout />}>
          <Route index element={<InicioPage />} />
          <Route path="menu/:secaoId" element={<MenuHubPage />} />
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
          <Route path="translados/gerar" element={<GerarTransladoPage />} />
          <Route path="translados/novo" element={<TransladoFormPage />} />
          <Route path="translados/:id" element={<TransladoDetalhePage />} />
          <Route path="translados/:id/editar" element={<TransladoFormPage />} />
          <Route path="rastreamento" element={<RastreamentoPage />} />
          <Route path="rastreamento/mapa" element={<MapaFrotaPage />} />
          <Route path="faturamento" element={<FaturamentoPage />} />
          <Route path="avaliacoes" element={<AvaliacoesPage />} />
          <Route path="pacs" element={<PacsListagemPage />} />
          <Route path="laudos" element={<LaudosListagemPage />} />
          <Route path="laudos/novo" element={<LaudoEditorPage />} />
          <Route path="laudos/:id" element={<LaudoEditorPage />} />
          <Route path="laudo-templates" element={<LaudoTemplatesListagemPage />} />
          <Route path="laudo-templates/novo" element={<LaudoTemplateEditorPage />} />
          <Route path="laudo-templates/:id" element={<LaudoTemplateEditorPage />} />
          <Route path="laudo-configuracao" element={<LaudoConfiguracaoPage />} />
          <Route path="solicitacoes-exame" element={<SolicitacoesExamePage />} />
          <Route path="solicitacoes-exame/novo" element={<SolicitacaoExameFormPage />} />
          <Route path="solicitacoes-exame/:id" element={<SolicitacaoExameDetalhePage />} />
          <Route path="solicitacoes-exame/:id/editar" element={<SolicitacaoExameFormPage />} />
          <Route path="importacao-sisreg" element={<ImportacaoSisregPage />} />
          <Route path="anamnese" element={<AnamnesePage />} />
          <Route path="tipos-exame" element={<TiposExamePage />} />
          <Route path="consultas" element={<ConsultasPage />} />
          <Route path="consultas/:id" element={<ConsultaDetalhePage />} />
          <Route path="mapeamento-sigtap" element={<MapeamentoSigtapPage />} />
          <Route path="tipos-exame/novo" element={<TipoExameFormPage />} />
          <Route path="tipos-exame/:id" element={<TipoExameFormPage />} />
          <Route path="procedimentos-sigtap" element={<ProcedimentosSigtapPage />} />
          <Route path="consulta-inteligente" element={<ConsultaInteligentePage />} />
          <Route path="ia/configuracao" element={<IaConfiguracaoPage />} />
          <Route path="ia/melhorias" element={<IaMelhoriasPage />} />
          <Route path="agente-ia" element={<AgenteIaPage />} />
          <Route path="indicadores" element={<Navigate to="/app/indicadores/conde/adulto" replace />} />
          <Route path="indicadores/conde" element={<Navigate to="/app/indicadores/conde/adulto" replace />} />
          <Route path="indicadores/conde/:aba" element={<IndicadoresAbaPage />} />
          {/* Compatibilidade com links antigos (aba direto sob /indicadores) */}
          <Route path="indicadores/:aba" element={<RedirecionaIndicadorLegado />} />
          <Route path="equipamentos" element={<EquipamentosPage />} />
          <Route path="regulacao/ser" element={<SerFilaPage />} />
          <Route path="regulacao/notificacoes" element={<SerNotificacoesPage />} />
          <Route path="regulacao/nova-solicitacao" element={<SerNovaSolicitacaoPage />} />
          <Route path="regulacao/ser/:id" element={<SerSolicitacaoDetalhePage />} />
          <Route path="regulacao/configuracao" element={<RegulacaoConfiguracaoPage />} />
          {/* SERNIT (SER de Niterói) — fila espelhada irmã do SER-RJ, sob /sernit para não colidir. */}
          <Route path="regulacao/sernit" element={<SernitFilaPage />} />
          <Route path="regulacao/sernit/notificacoes" element={<SernitNotificacoesPage />} />
          <Route path="regulacao/sernit/nova-solicitacao" element={<SernitNovaSolicitacaoPage />} />
          <Route path="regulacao/sernit/configuracao" element={<RegulacaoSernitConfiguracaoPage />} />
          <Route path="regulacao/sernit/:id" element={<SernitSolicitacaoDetalhePage />} />
          <Route path="sisreg" element={<SisregConsultaPage />} />
          <Route path="sisreg/configuracao" element={<SisregConfiguracaoPage />} />
          <Route path="sisreg/mapeamento" element={<SisregMapeamentoPage />} />
          <Route path="pep-sincronizacao" element={<PepSincronizacaoPage />} />
          <Route path="api-tokens" element={<ApiTokensPage />} />
          <Route path="integracoes" element={<IntegracoesPage />} />
          <Route path="auditoria" element={<AuditoriaPage />} />
          <Route path="instituicao" element={<InstituicaoPage />} />
          <Route path="erros" element={<ErrosPage />} />
          <Route path="conversas" element={<ConversasPage />} />
          <Route path="notificacoes-agendamento" element={<NotificacoesAgendamentoPage />} />
          <Route path="respostas-rapidas" element={<RespostasRapidasPage />} />
          <Route path="robo-atendimento" element={<RoboAtendimentoPage />} />
          <Route path="agenda" element={<AgendaPage />} />
          <Route path="agenda/analise" element={<AgendaAnalisePage />} />
          <Route path="agenda/demanda" element={<AgendaDemandaPage />} />
          <Route path="alteracoes-agenda" element={<AlteracoesAgendaPage />} />
          <Route path="pendencias-cadastro" element={<PendenciasCadastroPage />} />
          <Route path="estatisticas" element={<EstatisticasPage />} />
          <Route path="relatorios-imagem" element={<RelatoriosImagemPage />} />
          <Route path="sandbox" element={<SandboxPage />} />
          <Route path="tickets" element={<MeusTicketsPage />} />
          <Route path="tickets/gestao" element={<GestaoTicketsPage />} />
          <Route path="tickets/gestao/:id" element={<TicketDetalhePage gestao />} />
          <Route path="tickets/:id" element={<TicketDetalhePage />} />
        </Route>
      </Route>

      <Route path="*" element={<NaoEncontradoPage />} />
    </Routes>
  );
}
