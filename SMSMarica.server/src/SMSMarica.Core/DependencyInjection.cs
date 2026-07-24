using FluentValidation;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using SMSMarica.Core.ApiTokens;
using SMSMarica.Core.Avaliacoes;
using SMSMarica.Core.EstudoAnotacoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.LaudoTemplates;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Motoristas;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacs;
using SMSMarica.Core.Perfis;
using SMSMarica.Core.Procedimentos;
using SMSMarica.Core.Rastreamento;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.TiposExame;
using SMSMarica.Core.TiposTratamento;
using SMSMarica.Core.Translado;
using SMSMarica.Core.Tratamentos;
using SMSMarica.Core.Unidades;
using SMSMarica.Core.Veiculos;
using SMSMarica.Core.Worklist;
using SMSMarica.Core.Worklist.Background;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPacientesService, PacientesService>();
        services.AddScoped<Pacientes.Fhir.IPacienteResolver, Pacientes.Fhir.PacienteResolver>();
        services.AddScoped<Pacientes.Promocao.IPromocaoBlobService, Pacientes.Promocao.PromocaoBlobService>();
        services.AddScoped<Auditoria.IAuditoriaService, Auditoria.AuditoriaService>();
        services.AddScoped<Erros.IRegistroErroService, Erros.RegistroErroService>();
        services.AddScoped<ITratamentosService, TratamentosService>();
        services.AddScoped<IUnidadesService, UnidadesService>();
        services.AddScoped<IVeiculosService, VeiculosService>();
        services.AddScoped<IMotoristasService, MotoristasService>();
        services.AddScoped<IMedicosService, MedicosService>();
        services.AddScoped<Medicos.Assinatura.IAssinaturaMedicoService, Medicos.Assinatura.AssinaturaMedicoService>();
        services.AddScoped<ITransladoService, TransladoService>();
        services.AddScoped<IRastreamentoService, RastreamentoService>();
        services.AddScoped<IRastreamentoNotificador, NotificadorRastreamentoNulo>();
        services.AddScoped<IAvaliacoesService, AvaliacoesService>();
        services.AddScoped<Tickets.ITicketService, Tickets.TicketService>();
        services.AddScoped<IEstudoAnotacoesService, EstudoAnotacoesService>();
        services.AddScoped<IIdentidadeService, IdentidadeService>();
        services.AddScoped<IApiTokensService, ApiTokensService>();
        services.AddScoped<IPerfisService, PerfisService>();
        services.AddScoped<ITiposTratamentoService, TiposTratamentoService>();
        services.AddScoped<ILaudoTemplatesService, LaudoTemplatesService>();
        services.AddScoped<ILaudosService, LaudosService>();
        // Resolução preguiçosa p/ quebrar o ciclo de DI Laudos ↔ SolicitacoesExame.
        services.AddScoped(sp => new Lazy<ISolicitacoesExameService>(sp.GetRequiredService<ISolicitacoesExameService>));
        services.AddScoped<ILaudoPdfRenderer, LaudoPdfRenderer>();
        services.AddScoped<Laudos.Configuracao.ILaudoConfiguracaoService, Laudos.Configuracao.LaudoConfiguracaoService>();

        // Armazenamento genérico de imagens/binários no banco (reutilizável).
        services.AddScoped<Midias.IMidiasService, Midias.MidiasService>();

        // ---- Assinatura digital de laudos (PAdES via Automais.Assinador) ----
        services.Configure<Laudos.Assinatura.AssinaturaOptions>(
            configuration.GetSection(Laudos.Assinatura.AssinaturaOptions.SecaoConfig));
        services.AddScoped<Laudos.Assinatura.ILaudoAssinaturaService, Laudos.Assinatura.LaudoAssinaturaService>();
        // Resolução preguiçosa: SolicitacoesExame entra no subsistema de Laudos por
        // aqui; sem o Lazy o grafo de DI fecha ciclo (via Laudos → SolicitacoesExame,
        // direto e via ExameAssociacao).
        services.AddScoped(sp => new Lazy<Laudos.Assinatura.ILaudoAssinaturaService>(
            sp.GetRequiredService<Laudos.Assinatura.ILaudoAssinaturaService>));
        services.AddSingleton<Laudos.Assinatura.ICarimboAssinaturaRenderer, Laudos.Assinatura.CarimboAssinaturaRenderer>();
        var assinadorBaseUrl = configuration["Assinatura:AssinadorBaseUrl"] ?? "http://localhost:5082/";
        var assinadorToken = configuration["Assinatura:AssinadorToken"];
        services
            .AddHttpClient<Laudos.Assinatura.IAssinadorPdfPades, Laudos.Assinatura.AssinadorPdfHttpClient>(client =>
            {
                client.BaseAddress = new Uri(assinadorBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
                if (!string.IsNullOrWhiteSpace(assinadorToken))
                    client.DefaultRequestHeaders.Add("X-Assinador-Token", assinadorToken);
            });

        // ---- Solicitação de Exames + Worklist + Notificações ----
        services.AddScoped<IProcedimentosSigtapService, ProcedimentosSigtapService>();
        services.AddScoped<ITiposExameService, TiposExameService>();
        services.AddScoped<ISolicitacoesExameService, SolicitacoesExameService>();
        services.AddScoped<ISolicitacaoHistoricoService, SolicitacaoHistoricoService>();
        services.AddScoped<Consultas.IConsultasService, Consultas.ConsultasService>();
        services.AddScoped<Mapeamento.IMapeamentoSigtapService, Mapeamento.MapeamentoSigtapService>();
        // Backfill de data_estudo (DICOM) — depende só de DbContext + IConsultaStudyClient (sem ciclo).
        services.AddScoped<SolicitacoesExame.IBackfillDataEstudoService, SolicitacoesExame.BackfillDataEstudoService>();
        services.AddScoped<SolicitacoesExame.Declaracao.IDeclaracaoComparecimentoService,
            SolicitacoesExame.Declaracao.DeclaracaoComparecimentoService>();
        services.AddScoped<Downloads.IDownloadTokenService, Downloads.DownloadTokenService>();
        services.AddScoped<Associacoes.IExameAssociacaoService, Associacoes.ExameAssociacaoService>();
        services.AddScoped<Anamneses.IAnamnesesService, Anamneses.AnamnesesService>();

        // ---- Anexos de exame (ponte QR → PWA "Arquivos Saúde Maricá") ----
        // PDFs de exame SEMPRE em DigitalOcean Spaces (S3) — sem disco local. A config vem
        // da credencial "digitalocean_spaces" (Integrações). Se indisponível, o upload falha
        // com ArmazenamentoIndisponivelException (alerta o usuário) — nunca grava local.
        services.AddScoped<Armazenamento.ArmazenamentoSpaces>();
        services.AddScoped<Armazenamento.IArmazenamentoArquivos>(sp =>
            sp.GetRequiredService<Armazenamento.ArmazenamentoSpaces>());
        services.Configure<Anexos.AnexosOptions>(configuration.GetSection(Anexos.AnexosOptions.Secao));
        services.AddScoped<Anexos.IAnexosService, Anexos.AnexosService>();
        services.AddScoped<IGeradorIdentificadores, GeradorIdentificadores>();
        services.AddScoped<INotificadorExame, NotificadorExameLog>();

        services.Configure<Dcm4cheeMwlOptions>(configuration.GetSection(Dcm4cheeMwlOptions.SecaoConfig));
        services.AddScoped<IResolvedorEstacaoWorklist, ResolvedorEstacaoWorklist>();
        var worklistBaseUrl = configuration["Pacs:Dcm4chee:WorklistBaseUrl"]
            ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/WORKLIST/rs/";
        services
            .AddHttpClient<IDcm4cheeMwlClient, Dcm4cheeMwlClient>(client =>
            {
                client.BaseAddress = new Uri(worklistBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            });

        services
            .AddHttpClient<IConsultaStudyClient, ConsultaStudyClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Pacs:Dcm4chee:RsBaseUrl"]
                    ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/PACS-CDT/rs/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        services.Configure<EnviadorWorklistOptions>(configuration.GetSection(EnviadorWorklistOptions.SecaoConfig));
        services.AddHostedService<EnviadorWorklistService>();

        services.Configure<SincronizadorExamesOptions>(configuration.GetSection(SincronizadorExamesOptions.SecaoConfig));
        services.AddHostedService<SincronizadorExamesService>();

        // Pré-materialização do PDF de imagens quando o exame fica pronto (1 por vez, de
        // fundo) — o clique do paciente/operador sai do cache S3 sem tocar o dcm4chee.
        services.Configure<Exames.Background.PreparadorImagensOptions>(
            configuration.GetSection(Exames.Background.PreparadorImagensOptions.SecaoConfig));
        services.AddHostedService<Exames.Background.PreparadorImagensExameService>();

        // Notificação WhatsApp de agendamentos (fila alimentada pelo import + worker de envio).
        services.Configure<Notificacoes.Comunicacao.ComunicacaoPacienteOptions>(
            configuration.GetSection(Notificacoes.Comunicacao.ComunicacaoPacienteOptions.SecaoConfig));
        services.AddScoped<Notificacoes.Comunicacao.IComunicacaoPacienteService,
            Notificacoes.Comunicacao.ComunicacaoPacienteService>();
        services.AddScoped<Notificacoes.Comunicacao.IComunicacaoGestaoService,
            Notificacoes.Comunicacao.ComunicacaoGestaoService>();
        // Resolução preguiçosa: quebra o ciclo Solicitacoes → Comunicacao → LoginLink → Solicitacoes.
        services.AddScoped(sp => new Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService>(
            sp.GetRequiredService<Notificacoes.Comunicacao.IComunicacaoPacienteService>));
        services.AddScoped<Sandbox.ISandboxService, Sandbox.SandboxService>();
        services.AddHostedService<Notificacoes.Comunicacao.EnviadorComunicacaoService>();

        // Sanitizador de HTML compartilhado (whitelist explícita das tags TipTap).
        services.AddSingleton<IHtmlSanitizer>(_ =>
        {
            var s = new HtmlSanitizer();
            s.AllowedTags.Clear();
            foreach (var tag in new[] { "p", "br", "h1", "h2", "h3", "ul", "ol", "li",
                                        "strong", "b", "em", "i", "u",
                                        "table", "thead", "tbody", "tr", "th", "td",
                                        // Cabeçalho/rodapé institucional: imagens + wrappers do TipTap.
                                        "img", "span", "div" })
            {
                s.AllowedTags.Add(tag);
            }
            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("colspan");
            s.AllowedAttributes.Add("rowspan");
            s.AllowedAttributes.Add("src");
            s.AllowedAttributes.Add("alt");
            s.AllowedAttributes.Add("width");
            s.AllowedAttributes.Add("height");
            s.AllowedAttributes.Add("style");
            s.AllowedAttributes.Add("class");

            // CSS restrito: só alinhamento e dimensões (Ganss já bloqueia url()/expression maliciosos).
            s.AllowedCssProperties.Clear();
            foreach (var prop in new[] { "text-align", "width", "height", "font-weight",
                                         "font-style", "text-decoration" })
            {
                s.AllowedCssProperties.Add(prop);
            }
            return s;
        });

        services.Configure<LaudosPdfOptions>(configuration.GetSection(LaudosPdfOptions.SecaoConfig));
        QuestPDF.Settings.License = LicenseType.Community;

        // PasswordHasher do ASP.NET Identity (PBKDF2-HMAC-SHA512 / 100k iterações).
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        // Motores de proxy (CPF/CEP) com fallback configurável. O timeout é controlado por
        // tentativa no orquestrador (CTS), então os HttpClients dos motores usam timeout
        // infinito — quem corta é o ProxyExecutor com base no TimeoutSegundos de cada motor.
        services.AddScoped<Integracoes.Proxy.Configuracao.IProxyMotorConfiguracaoService,
            Integracoes.Proxy.Configuracao.ProxyMotorConfiguracaoService>();
        services.AddScoped<Integracoes.Proxy.IConsultaCpfService, Integracoes.Proxy.ConsultaCpfService>();
        services.AddScoped<Integracoes.Proxy.IConsultaCepService, Integracoes.Proxy.ConsultaCepService>();

        services
            .AddHttpClient<Integracoes.Proxy.IMotorCpf, Integracoes.Proxy.Motores.HubDoDesenvolvedorMotorCpf>(client =>
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan);
        services
            .AddHttpClient<Integracoes.Proxy.IMotorCep, Integracoes.Proxy.Motores.HubDoDesenvolvedorMotorCep>(client =>
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan);
        // Fallback do proxy CPF via CADSUS/SISREG — usa a sessão SISREG (singleton com
        // relogin), não um HttpClient tipado próprio.
        services.AddScoped<Integracoes.Proxy.IMotorCpf, Integracoes.Proxy.Motores.SisregCadsusMotorCpf>();

        // ---- Indicadores contratuais do HMCML (motor = SQL cadastrado) — ADR-0022 ----
        services.AddScoped<Indicadores.IIndicadoresService, Indicadores.IndicadoresService>();

        // ---- Agendamento (Especialidade/Equipamento → Agenda → Agendamento) — ADR-0012/0013 ----
        services.AddScoped<Especialidades.IEspecialidadesService, Especialidades.EspecialidadesService>();
        services.AddScoped<Equipamentos.IEquipamentosService, Equipamentos.EquipamentosService>();
        services.AddScoped<Agendamentos.IAgendaService, Agendamentos.AgendaService>();
        services.AddScoped<Agendamentos.IAgendamentoService, Agendamentos.AgendamentoService>();

        // ---- Credenciais de provedores OAuth (Microsoft/Facebook/Google), cifradas ----
        services.AddScoped<Integracoes.Credenciais.IIntegracaoCredencialService, Integracoes.Credenciais.IntegracaoCredencialService>();

        // ---- SISREG III (web scraping): consulta de paciente por CNS (CADSUS) ----
        // Sessão única por operador → cliente HTTP com cookies persistentes (singleton) que
        // reloga sozinho quando a sessão cai. Credencial cifrada no store de Integrações ("sisreg").
        services.AddSingleton<Integracoes.SisregWeb.ISisregWebSessao, Integracoes.SisregWeb.SisregWebSessao>();
        services.AddScoped<Integracoes.SisregWeb.IConsultaCnsService, Integracoes.SisregWeb.ConsultaCnsService>();

        // Importação de agendamentos (scraping cons_marcados_reg) → SolicitacaoExame.
        services.AddScoped<Integracoes.SisregWeb.Importacao.IMarcadosRegScraper, Integracoes.SisregWeb.Importacao.MarcadosRegScraper>();
        services.AddScoped<Integracoes.SisregWeb.Importacao.IImportacaoSisregService, Integracoes.SisregWeb.Importacao.ImportacaoSisregService>();

        // Importação SISREG em LOTE (vários arquivos / zip) — processada no servidor, fora da
        // request: fechar a aba não mata a importação e os contadores do rastreio são confiáveis.
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.ISisregImportacaoFila, Integracoes.SisregWeb.Importacao.Background.SisregImportacaoFila>();
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.SisregImportacaoEstadoVivo>();
        services.AddScoped<Integracoes.SisregWeb.Importacao.IImportacaoLoteService, Integracoes.SisregWeb.Importacao.ImportacaoLoteService>();
        services.AddHostedService<Integracoes.SisregWeb.Importacao.Background.SisregImportacaoRunner>();

        // ---- Integração SISREG (feed de leitura DATASUS) — ADR-0012 ----
        // BaseUrl e credenciais vêm do banco (tela de configuração), não do registro de DI.
        services.AddScoped<Integracoes.Sisreg.Configuracao.ISisregConfiguracaoService, Integracoes.Sisreg.Configuracao.SisregConfiguracaoService>();
        services.AddScoped<Integracoes.Sisreg.ISisregConsultaService, Integracoes.Sisreg.SisregConsultaService>();
        services.AddHttpClient<Integracoes.Sisreg.ISisregClient, Integracoes.Sisreg.SisregClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var pacsBaseUrl = configuration["Pacs:Dcm4chee:RsBaseUrl"]
            ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/PACS-CDT/rs/";
        services
            .AddHttpClient<IPacsProxyService, PacsProxyService>(client =>
            {
                client.BaseAddress = new Uri(pacsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // ---- Cache em disco (LRU) + pré-aquecimento de imagens imutáveis do PACS ----
        services.Configure<PacsCacheOptions>(configuration.GetSection(PacsCacheOptions.SecaoConfig));
        services.AddSingleton<IPacsCache, PacsCache>();
        services.AddScoped<IPacsWarmupService, PacsWarmupService>();

        // ---- Transcode JPEG-LS Lossless (flag Pacs:Compressao:Habilitado, default false) ----
        // HttpClient próprio (sem BaseAddress: monta URLs WADO-URI absolutas) com timeout
        // maior por baixar a instância COMPLETA (~53MB) antes de comprimir (~533ms/frame).
        services.AddHttpClient<IPacsTranscodeService, PacsTranscodeService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Clientes do hub FHIR (Automais.Fhir) — paciente e médico vivem só no hub.
        var fhirBaseUrl = configuration["Fhir:BaseUrl"] ?? "http://localhost:5081/";
        services
            .AddHttpClient<Pacientes.Fhir.IPacienteFhirClient, Pacientes.Fhir.PacienteFhirClient>(client =>
            {
                client.BaseAddress = new Uri(fhirBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        services
            .AddHttpClient<Medicos.Fhir.IPractitionerFhirClient, Medicos.Fhir.PractitionerFhirClient>(client =>
            {
                client.BaseAddress = new Uri(fhirBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        services
            .AddHttpClient<Atendimentos.Fhir.IEncounterFhirClient, Atendimentos.Fhir.EncounterFhirClient>(client =>
            {
                client.BaseAddress = new Uri(fhirBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        services.AddScoped<Atendimentos.IAtendimentosService, Atendimentos.AtendimentosService>();

        // ---- Sincronização de PEPs (importação Salux/outros → hub FHIR) — ADR-0014 ----
        services.AddHttpClient<Integracoes.Pep.Fhir.IHubFhirEscritor, Integracoes.Pep.Fhir.HubFhirEscritor>(client =>
        {
            client.BaseAddress = new Uri(fhirBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<Integracoes.Pep.Background.IPepSincronizacaoFila, Integracoes.Pep.Background.PepSincronizacaoFila>();
        services.AddSingleton<Integracoes.Pep.Progresso.PepSincronizacaoEstadoVivo>();
        services.AddScoped<Integracoes.Pep.Estrategias.IEstrategiaImportacaoPep, Integracoes.Pep.Estrategias.Salux.SaluxImportacaoStrategy>();
        services.AddScoped<Integracoes.Pep.IPepSincronizacaoService, Integracoes.Pep.PepSincronizacaoService>();
        services.AddHostedService<Integracoes.Pep.Background.PepSincronizacaoRunner>();

        // ---- Módulo IA (consulta em linguagem natural) ----
        var anthropicBaseUrl = configuration["Ia:Anthropic:BaseUrl"] ?? "https://api.anthropic.com/";
        services.AddHttpClient<Inteligencia.Provedores.IProvedorIa, Inteligencia.Provedores.ClaudeProvedorIa>(client =>
        {
            client.BaseAddress = new Uri(anthropicBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<Inteligencia.Provedores.IServicoEmbeddings, Inteligencia.Provedores.VoyageEmbeddings>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<Inteligencia.Fontes.IFonteDadosFactory, Inteligencia.Fontes.FonteDadosFactory>();

        // Registro de agentes proxy conectados por WSS — singleton (as conexões vivem em memória,
        // processo único). Ver ADR-0023.
        services.AddSingleton<Inteligencia.Fontes.Agente.IAgenteSqlRegistry,
            Inteligencia.Fontes.Agente.AgenteSqlRegistry>();
        services.AddScoped<Inteligencia.Conhecimento.IConhecimentoService, Inteligencia.Conhecimento.ConhecimentoService>();
        services.AddScoped<Inteligencia.Conhecimento.IRecuperadorContexto, Inteligencia.Conhecimento.RecuperadorContexto>();
        services.AddScoped<Inteligencia.IIaService, Inteligencia.IaService>();
        services.AddScoped<Inteligencia.Configuracao.IIaConfiguracaoService, Inteligencia.Configuracao.IaConfiguracaoService>();
        services.AddScoped<Inteligencia.Configuracao.IIaFonteService, Inteligencia.Configuracao.IaFonteService>();
        services.AddScoped<Inteligencia.Governanca.IIaGovernancaService, Inteligencia.Governanca.IaGovernancaService>();

        // ---- Módulo TFD: configuração de integrações + geocodificação (FT0/FT1) — ADR-0017 ----
        services.AddMemoryCache();
        services.AddScoped<Cidadao.IPacienteAuthService, Cidadao.PacienteAuthService>();
        services.AddScoped<Cidadao.ICidadaoSessaoService, Cidadao.CidadaoSessaoService>();
        services.AddScoped<Cidadao.ICidadaoLoginLinkService, Cidadao.CidadaoLoginLinkService>();
        services.AddScoped<Cidadao.IConsentimentoCidadaoService, Cidadao.ConsentimentoCidadaoService>();
        // Leitura clínica do app do cidadão (exames + docs escaneados + imagens PACS + laudos + agendamentos).
        services.AddScoped<Cidadao.ICidadaoClinicoService, Cidadao.CidadaoClinicoService>();
        services.AddScoped<Exames.IExamePacsImagensReader, Exames.ExamePacsImagensReader>();
        services.AddScoped<Exames.IExameImagensPdfService, Exames.ExameImagensPdfService>();
        services.AddScoped<Exames.IExameCompletoPdfService, Exames.ExameCompletoPdfService>();
        services.AddScoped<Tfd.Configuracao.ITfdConfigService, Tfd.Configuracao.TfdConfigService>();
        services.AddScoped<Geo.IGeocodificadorService, Geo.GeocodificadorService>();
        services.AddScoped<Geo.IDistanciaService, Geo.DistanciaService>();
        services.AddHttpClient<Geo.Google.IGoogleGeocodingClient, Geo.Google.GoogleGeocodingClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<Geo.Google.IGoogleDistanceMatrixClient, Geo.Google.GoogleDistanceMatrixClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<Geo.Google.IGoogleRoutesClient, Geo.Google.GoogleRoutesClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        // FT3: distribuição inteligente de pacientes nos veículos via Claude (config cifrada do módulo IA).
        services.AddHttpClient<Translado.Geracao.IA.IDistribuidorIa, Translado.Geracao.IA.DistribuidorIa>(client =>
        {
            client.BaseAddress = new Uri(anthropicBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // Validação de telefone por OTP (WhatsApp) — registro global do número validado.
        services.AddScoped<Telefones.ITelefoneValidacaoService, Telefones.TelefoneValidacaoService>();

        // WhatsApp (Meta Cloud API) — cliente de envio + webhook de recebimento (FT6).
        services.AddScoped<Notificacoes.WhatsApp.IWhatsAppWebhookService, Notificacoes.WhatsApp.WhatsAppWebhookService>();
        services.AddScoped<Notificacoes.WhatsApp.IWhatsAppNotificador, Notificacoes.WhatsApp.WhatsAppNotificador>();
        services.AddHttpClient<Notificacoes.WhatsApp.IWhatsAppCliente, Notificacoes.WhatsApp.WhatsAppCliente>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // ---- Módulo Conversas (chat WhatsApp multi-operador, transversal) ----
        services.Configure<Conversas.ConversasOptions>(
            configuration.GetSection(Conversas.ConversasOptions.SecaoConfig));
        services.AddScoped<Conversas.IConversaService, Conversas.ConversaService>();
        services.AddScoped<Conversas.RespostasRapidas.IRespostaRapidaService,
            Conversas.RespostasRapidas.RespostaRapidaService>();
        services.AddScoped<Conversas.IUsuarioUnidadeService, Conversas.UsuarioUnidadeService>();
        // No-op por padrão (testes/console/background); a Api sobrescreve com o SignalR.
        services.AddScoped<Conversas.IConversaNotificador, Conversas.NotificadorConversaNulo>();
        // Manipuladores de mensagem inbound (o webhook aplica todos, ordenados).
        services.AddScoped<Notificacoes.WhatsApp.Manipuladores.IManipuladorMensagemWhatsApp,
            Notificacoes.WhatsApp.Manipuladores.AcompanhanteWhatsAppHandler>();
        services.AddScoped<Notificacoes.WhatsApp.Manipuladores.IManipuladorMensagemWhatsApp,
            Notificacoes.WhatsApp.Manipuladores.ConfirmacaoAgendamentoWhatsAppHandler>();

        // Faturamento SUS/BPA (FT10): contabilização proporcional + relatórios.
        services.AddScoped<Faturamento.IFaturamentoService, Faturamento.FaturamentoService>();

        // Motor de geração de translado (FT3): distribuição + sequenciamento de rotas.
        services.AddScoped<Translado.Geracao.IGeradorDeTransladoService, Translado.Geracao.GeradorDeTransladoService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
