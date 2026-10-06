import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/app/layout/Layout';
import { InicioPage } from '@/app/pages/InicioPage';
import { MenuHubPage } from '@/app/pages/MenuHubPage';
import { NaoEncontradoPage } from '@/app/pages/NaoEncontradoPage';
import { RotaProtegida } from '@/app/router/RotaProtegida';
import { RotaComModulo } from '@/app/router/RotaComModulo';
import { AvaliacoesPage } from '@/features/avaliacoes/pages/AvaliacoesPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { PoliticaPrivacidadeExtensaoPage } from '@/features/extensao-navegador/pages/PoliticaPrivacidadeExtensaoPage';
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
import { UnificarPacientePage } from '@/features/pacientes/pages/UnificarPacientePage';
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
import { SolicitacaoDetalhePage } from '@/features/solicitacoes/pages/SolicitacaoDetalhePage';
import { SolicitacaoExameFormPage } from '@/features/solicitacoes/pages/SolicitacaoExameFormPage';
import { SolicitacoesPage } from '@/features/solicitacoes/pages/SolicitacoesPage';
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
import { UnidadesAtendimentoPage } from '@/features/unidades-atendimento/pages/UnidadesAtendimentoPage';
import { UnidadeAtendimentoFormPage } from '@/features/unidades-atendimento/pages/UnidadeAtendimentoFormPage';
import { UnidadeAtendimentoDetalhePage } from '@/features/unidades-atendimento/pages/UnidadeAtendimentoDetalhePage';
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
import { SisregMedicosPage } from '@/features/sisreg/pages/SisregMedicosPage';
import { SisregEstatisticasPage } from '@/features/sisreg-estatisticas/pages/SisregEstatisticasPage';
import { RegulacaoEstatisticasPage } from '@/features/regulacao-estatisticas/pages/RegulacaoEstatisticasPage';
import { IndicadoresRegulacaoPage } from '@/features/regulacao-indicadores/pages/IndicadoresRegulacaoPage';
import { SisregMapeamentoPage } from '@/features/sisreg-mapeamento/pages/SisregMapeamentoPage';
import { SerNotificacoesPage } from '@/features/ser/pages/SerNotificacoesPage';
import { SerNovaSolicitacaoPage } from '@/features/ser/pages/SerNovaSolicitacaoPage';
import { SerMedicosPage } from '@/features/ser/pages/SerMedicosPage';
import { SernitMedicosPage } from '@/features/sernit/pages/SernitMedicosPage';
import { SerFilaPage } from '@/features/ser/pages/SerFilaPage';
import { SerSolicitacaoDetalhePage } from '@/features/ser/pages/SerSolicitacaoDetalhePage';
import { RegulacaoConfiguracaoPage } from '@/features/ser/pages/RegulacaoConfiguracaoPage';
import { NovaSolicitacaoPage } from '@/features/regulacao/pages/NovaSolicitacaoPage';
import { MinhaFilaPage } from '@/features/regulacao/pages/MinhaFilaPage';
import { FilaRegulacaoPage } from '@/features/regulacao/pages/FilaRegulacaoPage';
import { AnaliseSolicitacaoPage } from '@/features/regulacao/pages/AnaliseSolicitacaoPage';
import { SolicitacaoDetalhePage as RegulacaoSolicitacaoDetalhePage } from '@/features/regulacao/pages/SolicitacaoDetalhePage';
import { NotificacoesRegulacaoPage } from '@/features/regulacao/pages/NotificacoesRegulacaoPage';
import { RegrasElegibilidadePage } from '@/features/regulacao/pages/RegrasElegibilidadePage';
import { SernitNotificacoesPage } from '@/features/sernit/pages/SernitNotificacoesPage';
import { SernitNovaSolicitacaoPage } from '@/features/sernit/pages/SernitNovaSolicitacaoPage';
import { SernitFilaPage } from '@/features/sernit/pages/SernitFilaPage';
import { SernitSolicitacaoDetalhePage } from '@/features/sernit/pages/SernitSolicitacaoDetalhePage';
import RegulacaoSernitConfiguracaoPage from '@/features/sernit/pages/RegulacaoSernitConfiguracaoPage';
import { EsusSgFilaPage } from '@/features/esussg/pages/EsusSgFilaPage';
import { EsusSgNotificacoesPage } from '@/features/esussg/pages/EsusSgNotificacoesPage';
import { EsusSgSolicitacaoDetalhePage } from '@/features/esussg/pages/EsusSgSolicitacaoDetalhePage';
import RegulacaoEsusSgConfiguracaoPage from '@/features/esussg/pages/RegulacaoEsusSgConfiguracaoPage';
import { PepSincronizacaoPage } from '@/features/pep-sincronizacao/pages/PepSincronizacaoPage';
import { FontesProntuarioPage } from '@/features/pep-sincronizacao/pages/FontesProntuarioPage';
import { ApiTokensPage } from '@/features/api-tokens/pages/ApiTokensPage';
import { IntegracoesPage } from '@/features/integracoes/pages/IntegracoesPage';
import { FaturamentoPage } from '@/features/faturamento/pages/FaturamentoPage';
import { AuditoriaPage } from '@/features/auditoria/pages/AuditoriaPage';
import { InstituicaoPage } from '@/features/instituicao/pages/InstituicaoPage';
import { ErrosPage } from '@/features/erros/pages/ErrosPage';
import { AutorizarComputadorPage } from '@/features/extensao-navegador/pages/AutorizarComputadorPage';
import { ExtensaoChromePage } from '@/features/extensao-navegador/pages/ExtensaoChromePage';
import { ExtensaoGerenciarPage } from '@/features/extensao-navegador/pages/ExtensaoGerenciarPage';
import { AvisosCelularPage } from '@/features/alertas-plataforma/pages/AvisosCelularPage';
import { ConversasPage } from '@/features/conversas/pages/ConversasPage';
import { EstatisticasPage } from '@/features/estatisticas/pages/EstatisticasPage';
import { RelatoriosImagemPage } from '@/features/relatorios-imagem/pages/RelatoriosImagemPage';
import { ChatJanelaPage } from '@/features/conversas/pages/ChatJanelaPage';
import { MeusTicketsPage } from '@/features/tickets/pages/MeusTicketsPage';
import { TicketDetalhePage } from '@/features/tickets/pages/TicketDetalhePage';
import { GestaoTicketsPage } from '@/features/tickets/pages/GestaoTicketsPage';
import { OuvidoriaFilaPage } from '@/features/ouvidoria/pages/OuvidoriaFilaPage';
import { RegistrarManifestacaoPage } from '@/features/ouvidoria/pages/RegistrarManifestacaoPage';
import { ManifestacaoDetalhePage } from '@/features/ouvidoria/pages/ManifestacaoDetalhePage';
import { MeuPontoPage } from '@/features/ouvidoria/pages/MeuPontoPage';
import { PontosRespostaPage } from '@/features/ouvidoria/pages/PontosRespostaPage';
import { AssuntosPage } from '@/features/ouvidoria/pages/AssuntosPage';
import { PainelOuvidoriaPage } from '@/features/ouvidoria/pages/PainelOuvidoriaPage';
import { ConfiguracaoOuvidoriaPage } from '@/features/ouvidoria/pages/ConfiguracaoOuvidoriaPage';
import { MensageriaPage } from '@/features/mensageria/pages/MensageriaPage';
import { ConfirmacoesPage } from '@/features/confirmacoes/pages/ConfirmacoesPage';
import { JanelaAtendimentoPage } from '@/features/confirmacoes/pages/JanelaAtendimentoPage';
import { ManualIndicePage } from '@/features/manual/pages/ManualIndicePage';
import { ManualArtigoPage } from '@/features/manual/pages/ManualArtigoPage';
import { RespostasRapidasPage } from '@/features/respostas-rapidas/pages/RespostasRapidasPage';
import { RoboAtendimentoPage } from '@/features/robo-atendimento/pages/RoboAtendimentoPage';
import { AgendaPage } from '@/features/agenda/pages/AgendaPage';
import { AgendaAnalisePage } from '@/features/agenda/pages/AgendaAnalisePage';
import { AgendaDemandaPage } from '@/features/agenda/pages/AgendaDemandaPage';
import { EstrategiasFilaPage } from '@/features/estrategias-fila/pages/EstrategiasFilaPage';
import { EstrategiaEditorPage } from '@/features/estrategias-fila/pages/EstrategiaEditorPage';
import { AlteracoesAgendaPage } from '@/features/alteracoes-agenda/pages/AlteracoesAgendaPage';
import { OfertasPage } from '@/features/sisreg/pages/OfertasPage';
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
      {/* Pública: a Chrome Web Store exige a política da extensão num endereço aberto. */}
      <Route path="/privacidade/extensao" element={<PoliticaPrivacidadeExtensaoPage />} />

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
        {/* Janela solta de confirmar/cancelar UM agendamento, aberta do chat. */}
        <Route path="/confirmacoes/janela/:solicitacaoId" element={<JanelaAtendimentoPage />} />
      </Route>

      <Route element={<RotaProtegida />}>
        <Route path="/app" element={<Layout />}>
          <Route index element={<InicioPage />} />
          <Route path="menu/:secaoId" element={<MenuHubPage />} />
          <Route path="pacientes" element={<PacientesPage />} />
          <Route path="pacientes/novo" element={<PacienteFormPage />} />
          <Route path="pacientes/unificar" element={<UnificarPacientePage />} />
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
          <Route element={<RotaComModulo modulo="UnidadesAtendimento" rotulo="Unidades de atendimento" />}>
            <Route path="unidades-atendimento" element={<UnidadesAtendimentoPage />} />
            <Route path="unidades-atendimento/novo" element={<UnidadeAtendimentoFormPage />} />
            <Route path="unidades-atendimento/:id" element={<UnidadeAtendimentoDetalhePage />} />
            <Route path="unidades-atendimento/:id/editar" element={<UnidadeAtendimentoFormPage />} />
          </Route>
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
          <Route path="solicitacoes" element={<SolicitacoesPage />} />
          <Route path="solicitacoes/novo" element={<SolicitacaoExameFormPage />} />
          <Route path="solicitacoes/:id" element={<SolicitacaoDetalhePage />} />
          <Route path="solicitacoes/:id/editar" element={<SolicitacaoExameFormPage />} />
          <Route path="importacao-sisreg" element={<ImportacaoSisregPage />} />
          <Route path="anamnese" element={<AnamnesePage />} />
          <Route path="tipos-exame" element={<TiposExamePage />} />
          {/* Exames e consultas viraram uma lista só: os endereços antigos (favoritos) caem nela. */}
          <Route path="solicitacoes-exame/*" element={<Navigate to="/app/solicitacoes" replace />} />
          <Route path="consultas/*" element={<Navigate to="/app/solicitacoes" replace />} />
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
          <Route path="regulacao/ser/medicos" element={<SerMedicosPage />} />
          <Route path="regulacao/sernit/medicos" element={<SernitMedicosPage />} />
          <Route path="regulacao/ser/:id" element={<SerSolicitacaoDetalhePage />} />
          <Route path="regulacao/configuracao" element={<RegulacaoConfiguracaoPage />} />
          {/* Regulação → Solicitações (ADR-0052): abertura pela unidade solicitante.
              Gate por módulo: esconder o item do menu não impede quem digita a URL de cair numa
              tela que só dispara 403 — aqui a pessoa lê o motivo. */}
          <Route element={<RotaComModulo modulo="Regulacao" rotulo="Regulação — Solicitações" />}>
            <Route path="regulacao/solicitacoes" element={<MinhaFilaPage />} />
            <Route path="regulacao/solicitacoes/nova" element={<NovaSolicitacaoPage />} />
            {/* O mesmo assistente, aberto numa solicitação que ainda é da unidade (rascunho ou
                devolvida): tudo editável, anexos inclusive. */}
            <Route path="regulacao/solicitacoes/:id/editar" element={<NovaSolicitacaoPage />} />
            <Route
              path="regulacao/solicitacoes/notificacoes"
              element={<NotificacoesRegulacaoPage />}
            />
            <Route path="regulacao/solicitacoes/:id" element={<RegulacaoSolicitacaoDetalhePage />} />
          </Route>
          {/* Gestão de fila: a visão de quem avalia e regula (48) — gate próprio, mais estreito.
              A lista e a análise de cada pedido ficam fora de `regulacao/solicitacoes/*`, que é o
              lado de quem pede. */}
          <Route
            element={<RotaComModulo modulo="RegulacaoTriagem" rotulo="Regulação — Gestão de fila" />}
          >
            <Route path="regulacao/gestao-fila" element={<FilaRegulacaoPage />} />
            <Route path="regulacao/gestao-fila/:id" element={<AnaliseSolicitacaoPage />} />
            {/* Endereço antigo da "Fila da regulação": favorito de quem já usava não quebra. */}
            <Route
              path="regulacao/solicitacoes/regulacao"
              element={<Navigate to="/app/regulacao/gestao-fila" replace />}
            />
          </Route>
          {/* Curadoria das regras: é configuração da regulação (51), não do solicitante. */}
          <Route
            element={<RotaComModulo modulo="RegulacaoConfiguracao" rotulo="Regulação — Configuração" />}
          >
            <Route path="regulacao/regras" element={<RegrasElegibilidadePage />} />
          </Route>
          {/* SERNIT (SER de Niterói) — fila espelhada irmã do SER-RJ, sob /sernit para não colidir. */}
          <Route path="regulacao/sernit" element={<SernitFilaPage />} />
          <Route path="regulacao/sernit/notificacoes" element={<SernitNotificacoesPage />} />
          <Route path="regulacao/sernit/nova-solicitacao" element={<SernitNovaSolicitacaoPage />} />
          <Route path="regulacao/sernit/configuracao" element={<RegulacaoSernitConfiguracaoPage />} />
          <Route path="regulacao/sernit/:id" element={<SernitSolicitacaoDetalhePage />} />
          {/* Estatísticas dos operadores: um módulo por sistema (68–70), desligados por padrão.
              Gate por módulo — esconder o item do menu não é barreira para quem digita a URL. */}
          <Route element={<RotaComModulo modulo="EstatisticaSer" rotulo="Estatísticas — SER" />}>
            <Route path="regulacao/ser/estatisticas" element={<RegulacaoEstatisticasPage fonte="ser" />} />
          </Route>
          <Route element={<RotaComModulo modulo="EstatisticaSernit" rotulo="Estatísticas — SERNIT" />}>
            <Route path="regulacao/sernit/estatisticas" element={<RegulacaoEstatisticasPage fonte="sernit" />} />
          </Route>
          {/* ESUS de São Gonçalo (ADR-0063) — espelho só leitura. Cada rota com o gate do seu
              módulo: a fila, o detalhe e as notificações pedem RegulacaoEsusSg (77); a
              configuração, RegulacaoConfiguracao; as estatísticas, EstatisticaEsusSg (78). As
              rotas literais vêm antes de `:id` só por legibilidade — o router ranqueia sozinho. */}
          <Route element={<RotaComModulo modulo="RegulacaoEsusSg" rotulo="Regulação — ESUS São Gonçalo" />}>
            <Route path="regulacao/esussg" element={<EsusSgFilaPage />} />
            <Route path="regulacao/esussg/notificacoes" element={<EsusSgNotificacoesPage />} />
            <Route path="regulacao/esussg/:id" element={<EsusSgSolicitacaoDetalhePage />} />
          </Route>
          <Route
            element={<RotaComModulo modulo="RegulacaoConfiguracao" rotulo="Regulação — Configuração" />}
          >
            <Route path="regulacao/esussg/configuracao" element={<RegulacaoEsusSgConfiguracaoPage />} />
          </Route>
          <Route element={<RotaComModulo modulo="EstatisticaEsusSg" rotulo="Estatísticas — ESUS São Gonçalo" />}>
            <Route path="regulacao/esussg/estatisticas" element={<RegulacaoEstatisticasPage fonte="esussg" />} />
          </Route>
          <Route path="sisreg" element={<SisregConsultaPage />} />
          <Route path="sisreg/configuracao" element={<SisregConfiguracaoPage />} />
          <Route path="sisreg/medicos" element={<SisregMedicosPage />} />
          <Route element={<RotaComModulo modulo="EstatisticaSisreg" rotulo="Estatísticas — SISREG" />}>
            <Route path="sisreg/estatisticas" element={<SisregEstatisticasPage />} />
          </Route>
          {/* Indicadores de Regulação (79): a série mensal de cada sistema — um módulo só para os
              quatro, só Consulta (que também libera o PDF). `/regulacao/indicadores` sozinho é o
              destino do "Abrir a tela" do manual: cai no SISREG. */}
          <Route element={<RotaComModulo modulo="IndicadoresRegulacao" rotulo="Indicadores — SISREG" />}>
            <Route
              path="regulacao/indicadores"
              element={<Navigate to="/app/regulacao/indicadores/sisreg" replace />}
            />
            <Route path="regulacao/indicadores/sisreg" element={<IndicadoresRegulacaoPage fonte="sisreg" />} />
          </Route>
          <Route element={<RotaComModulo modulo="IndicadoresRegulacao" rotulo="Indicadores — SER" />}>
            <Route path="regulacao/indicadores/ser" element={<IndicadoresRegulacaoPage fonte="ser" />} />
          </Route>
          <Route element={<RotaComModulo modulo="IndicadoresRegulacao" rotulo="Indicadores — SERNIT" />}>
            <Route path="regulacao/indicadores/sernit" element={<IndicadoresRegulacaoPage fonte="sernit" />} />
          </Route>
          <Route element={<RotaComModulo modulo="IndicadoresRegulacao" rotulo="Indicadores — ESUS São Gonçalo" />}>
            <Route path="regulacao/indicadores/esussg" element={<IndicadoresRegulacaoPage fonte="esussg" />} />
          </Route>
          <Route path="sisreg/mapeamento" element={<SisregMapeamentoPage />} />
          <Route path="pep-sincronizacao" element={<PepSincronizacaoPage />} />
          <Route path="pep-sincronizacao/fontes" element={<FontesProntuarioPage />} />
          <Route path="api-tokens" element={<ApiTokensPage />} />
          <Route path="integracoes" element={<IntegracoesPage />} />
          <Route path="auditoria" element={<AuditoriaPage />} />
          <Route path="instituicao" element={<InstituicaoPage />} />
          <Route path="erros" element={<ErrosPage />} />
          <Route path="avisos-celular" element={<AvisosCelularPage />} />
          {/* Extensão do Chrome (ADR-0064). Baixar o instalador e autorizar um computador são de
              qualquer usuário logado; o código da autorização vai NO CAMINHO porque a ida ao login
              guarda só o pathname. O gerenciador pede o módulo. */}
          <Route path="extensao" element={<ExtensaoChromePage />} />
          <Route path="extensao/autorizar/:codigo" element={<AutorizarComputadorPage />} />
          <Route element={<RotaComModulo modulo="ExtensaoNavegador" rotulo="Extensão Chrome — computadores e versões" />}>
            <Route path="extensao/gerenciar" element={<ExtensaoGerenciarPage />} />
          </Route>
          <Route path="conversas" element={<ConversasPage />} />
          <Route element={<RotaComModulo modulo="NotificacoesAgendamento" rotulo="Mensageria" />}>
            <Route path="mensageria" element={<MensageriaPage />} />
            {/* Rota antiga: quem tinha o link salvo cai na Mensageria. */}
            <Route path="notificacoes-agendamento" element={<MensageriaPage />} />
          </Route>
          <Route element={<RotaComModulo modulo="Confirmacoes" rotulo="Confirmações" />}>
            <Route path="confirmacoes" element={<ConfirmacoesPage />} />
          </Route>
          <Route path="respostas-rapidas" element={<RespostasRapidasPage />} />
          <Route path="robo-atendimento" element={<RoboAtendimentoPage />} />
          <Route path="agenda" element={<AgendaPage />} />
          <Route path="agenda/analise" element={<AgendaAnalisePage />} />
          <Route path="agenda/demanda" element={<AgendaDemandaPage />} />
          <Route element={<RotaComModulo modulo="EstrategiasFila" rotulo="Estratégias de fila" />}>
            <Route path="agenda/estrategias" element={<EstrategiasFilaPage />} />
            <Route path="agenda/estrategias/nova" element={<EstrategiaEditorPage />} />
            <Route path="agenda/estrategias/:id" element={<EstrategiaEditorPage />} />
          </Route>
          <Route path="alteracoes-agenda" element={<AlteracoesAgendaPage />} />
          <Route path="ofertas" element={<OfertasPage />} />
          <Route path="pendencias-cadastro" element={<PendenciasCadastroPage />} />
          <Route path="estatisticas" element={<EstatisticasPage />} />
          <Route path="relatorios-imagem" element={<RelatoriosImagemPage />} />
          <Route path="sandbox" element={<SandboxPage />} />
          {/* Ouvidoria (ADR-0060). O detalhe existe em dois caminhos porque `RotaComModulo` gateia um
              módulo só: a ouvidoria central abre por /ouvidoria/:id; o membro de ponto de resposta, por
              /ouvidoria/meu-ponto/:id (mesma página, sem card de manifestante). */}
          <Route element={<RotaComModulo modulo="Ouvidoria" rotulo="Ouvidoria" />}>
            <Route path="ouvidoria" element={<OuvidoriaFilaPage />} />
            <Route path="ouvidoria/registrar" element={<RegistrarManifestacaoPage />} />
            <Route path="ouvidoria/:id" element={<ManifestacaoDetalhePage />} />
          </Route>
          <Route element={<RotaComModulo modulo="OuvidoriaPontoResposta" rotulo="Ouvidoria — Meu ponto de resposta" />}>
            <Route path="ouvidoria/meu-ponto" element={<MeuPontoPage />} />
            <Route path="ouvidoria/meu-ponto/:id" element={<ManifestacaoDetalhePage modoPonto />} />
          </Route>
          <Route element={<RotaComModulo modulo="OuvidoriaGestao" rotulo="Ouvidoria — Gestão" />}>
            <Route path="ouvidoria/pontos-resposta" element={<PontosRespostaPage />} />
            <Route path="ouvidoria/assuntos" element={<AssuntosPage />} />
            <Route path="ouvidoria/painel" element={<PainelOuvidoriaPage />} />
            <Route path="ouvidoria/configuracao" element={<ConfiguracaoOuvidoriaPage />} />
          </Route>
          {/* Manual: sem gate de módulo — todo mundo que entra no painel pode aprender a usá-lo. */}
          <Route path="manual" element={<ManualIndicePage />} />
          <Route path="manual/:slug" element={<ManualArtigoPage />} />
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
