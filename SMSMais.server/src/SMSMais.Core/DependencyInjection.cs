using FluentValidation;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using SMSMais.Core.ApiTokens;
using SMSMais.Core.Avaliacoes;
using SMSMais.Core.EstudoAnotacoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Pdf;
using SMSMais.Core.LaudoTemplates;
using SMSMais.Core.Medicos;
using SMSMais.Core.Motoristas;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacs;
using SMSMais.Core.Perfis;
using SMSMais.Core.Procedimentos;
using SMSMais.Core.Rastreamento;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.SolicitacoesExame.Identificadores;
using SMSMais.Core.TiposExame;
using SMSMais.Core.TiposTratamento;
using SMSMais.Core.Translado;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Unidades;
using SMSMais.Core.Veiculos;
using SMSMais.Core.Worklist;
using SMSMais.Core.Worklist.Background;
using SMSMais.Data.Entities;

namespace SMSMais.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPacientesService, PacientesService>();
        services.AddScoped<IAgendamentosPacienteService, AgendamentosPacienteService>();
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

        // Identidade da instituição desta instância (ADR-0043): nome, marca, domínios e
        // contatos legais. Substitui os textos de Maricá que viviam fixos no código.
        services.AddScoped<Institucional.IInstituicaoService, Institucional.InstituicaoService>();


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
        // Painel da tela de início (read model; ADR-0033).
        services.AddScoped<PainelInicio.IPainelInicioService, PainelInicio.PainelInicioService>();
        services.AddScoped<Mapeamento.IMapeamentoSigtapService, Mapeamento.MapeamentoSigtapService>();
        // Backfill de data_estudo (DICOM) — depende só de DbContext + IConsultaStudyClient (sem ciclo).
        services.AddScoped<SolicitacoesExame.IBackfillDataEstudoService, SolicitacoesExame.BackfillDataEstudoService>();
        services.AddScoped<SolicitacoesExame.Declaracao.IDeclaracaoComparecimentoService,
            SolicitacoesExame.Declaracao.DeclaracaoComparecimentoService>();
        services.AddScoped<Downloads.IDownloadTokenService, Downloads.DownloadTokenService>();
        services.AddScoped<Associacoes.IExameAssociacaoService, Associacoes.ExameAssociacaoService>();
        services.AddScoped<Pacs.IResolvedorIdentidadeDicom, Pacs.ResolvedorIdentidadeDicom>();
        services.AddScoped<Associacoes.ICorrecaoIdentidadeExameService, Associacoes.CorrecaoIdentidadeExameService>();
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

        // Reescrita de identidade do estudo: baixa, reescreve e re-armazena o objeto inteiro.
        // Timeout largo — uma mamografia tem 4 instâncias que podem passar de 50 MB cada.
        services
            .AddHttpClient<Pacs.IPacsReescritorEstudoClient, Pacs.PacsReescritorEstudoClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["Pacs:Dcm4chee:RsBaseUrl"]
                    ?? "http://pacs.marica.automais.cloud:8080/dcm4chee-arc/aets/PACS-CDT/rs/");
                client.Timeout = TimeSpan.FromMinutes(10);
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
                                        "colgroup", "col",
                                        // Cabeçalho/rodapé institucional: imagens + wrappers do TipTap.
                                        "img", "span", "div" })
            {
                s.AllowedTags.Add(tag);
            }
            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("colspan");
            s.AllowedAttributes.Add("rowspan");
            // Largura de coluna que o TipTap grava ao redimensionar a tabela — sem
            // isso o ajuste some no save e a tabela volta a colunas iguais.
            s.AllowedAttributes.Add("colwidth");
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

        // Equipamento SOBREVIVEU a remocao da agenda local (05/09/2026): ele e recurso de imagem
        // usado por PACS/worklist, nao residuo daquele modulo.
        services.AddScoped<Equipamentos.IEquipamentosService, Equipamentos.EquipamentosService>();

        // ---- Credenciais de provedores OAuth (Microsoft/Facebook/Google), cifradas ----
        services.AddScoped<Integracoes.Credenciais.IIntegracaoCredencialService, Integracoes.Credenciais.IntegracaoCredencialService>();

        // ---- SISREG III (web scraping): consulta de paciente por CNS (CADSUS) ----
        // Sessão única por operador → um cliente HTTP com cookies próprios POR OPERADOR
        // (singleton), que reloga sozinho quando a sessão cai. A credencial é a global do store
        // de Integrações ("sisreg") e enxerga todas as unidades.
        // Aviso por WhatsApp de falha de sincronismo (SISREG/SER/SERNIT). Os motores rodam de
        // madrugada e sozinhos: sem isto, um CAPTCHA para a importação da noite e ninguém sabe até
        // alguém reparar que a agenda do dia não entrou.
        services.AddScoped<
            Notificacoes.Sincronismo.INotificadorSincronismo,
            Notificacoes.Sincronismo.NotificadorSincronismo>();

        // Orçamento anti-robô COMPARTILHADO: um contador rolante de 60 min por onde toda ida ao
        // SISREG passa (a sessão o alimenta). Sem ele cada motor tinha o próprio teto e nenhum
        // enxergava o gasto do outro — e o CAPTCHA aparecia sem ninguém ter "estourado" nada.
        services.Configure<Integracoes.SisregWeb.SisregOrcamentoOpcoes>(
            configuration.GetSection(Integracoes.SisregWeb.SisregOrcamentoOpcoes.Secao));
        services.AddSingleton<Integracoes.SisregWeb.SisregOrcamentoRequisicoes>();

        services.AddSingleton<Integracoes.SisregWeb.ISisregWebSessao, Integracoes.SisregWeb.SisregWebSessao>();
        services.AddScoped<Integracoes.SisregWeb.IConsultaCnsService, Integracoes.SisregWeb.ConsultaCnsService>();

        // ---- CADSUS por porta configurável (SISREG × SER) ----
        // Quem importa pede o cadastro pelo roteador, não pela porta: a do SISREG tem orçamento
        // anti-robô (~500 req/h → CAPTCHA → unidade travada 24h) e um lote grande a estoura.
        services.AddScoped<Integracoes.Cadastro.ISerCadastroPacienteService, Integracoes.Cadastro.SerCadastroPacienteService>();
        services.AddScoped<Integracoes.Cadastro.ICadastroPacienteService, Integracoes.Cadastro.CadastroPacienteRoteador>();

        // Cache por EXECUÇÃO (scoped, morre com a varredura) + a pré-carga que o alimenta em
        // sessões paralelas do SER. Ver PreCargaCadastroSerService: a sessão do SER é stateful e
        // serializada, então escala se faz com N sessões, não com N threads na mesma.
        services.AddScoped<Integracoes.Cadastro.CacheCadastroSer>();
        services.AddScoped<Integracoes.Cadastro.IPreCargaCadastroSerService, Integracoes.Cadastro.PreCargaCadastroSerService>();

        // Fluxos que exigem UMA unidade selecionada (mapeamento e varredura).
        services.AddScoped<Integracoes.SisregWeb.ISisregUnidadeAtual, Integracoes.SisregWeb.SisregUnidadeAtual>();

        // Mapeamento: a "verdade" do SISREG (profissionais × procedimentos) com habilita/desabilita
        // que define o custo de cada varredura de agenda.
        services.AddScoped<
            Integracoes.SisregWeb.Mapeamento.ISisregMapeamentoService,
            Integracoes.SisregWeb.Mapeamento.SisregMapeamentoService>();

        // Catálogo de unidades DO SISREG (combo `ups` do cons_agendas): descobre a rede inteira em
        // uma requisição e cria aqui as unidades que faltam. É o primeiro passo do "sincroniza
        // tudo" — antes dele o botão só revisitava as unidades já mapeadas à mão.
        services.AddScoped<
            Integracoes.SisregWeb.Unidades.ISisregCatalogoUnidadesService,
            Integracoes.SisregWeb.Unidades.SisregCatalogoUnidadesService>();

        // De-para do código de procedimento do SISREG (o `pa`) para o SIGTAP oficial: a agenda não
        // informa SIGTAP, e sem ele a solicitação nasceria sem categoria e sem worklist.
        services.AddScoped<
            Integracoes.SisregWeb.Varredura.Sigtap.IMapeadorSigtapSisreg,
            Integracoes.SisregWeb.Varredura.Sigtap.MapeadorSigtapSisreg>();

        // Procedimento do SISREG → tipo de exame, criando na hora. É o que faz o exame de imagem
        // entrar já com o nome certo, sem operador no meio (ver ResolvedorTipoExameSisreg).
        services.AddScoped<
            Integracoes.SisregWeb.Importacao.IResolvedorTipoExameSisreg,
            Integracoes.SisregWeb.Importacao.ResolvedorTipoExameSisreg>();

        // Motor de varredura da agenda (cons_agendas). Uma varredura por vez em toda a instalação:
        // as unidades saem para o SISREG pelo mesmo IP, então paralelizar só aproxima o CAPTCHA.
        services.Configure<Integracoes.SisregWeb.Varredura.VarreduraSisregOpcoes>(
            configuration.GetSection(Integracoes.SisregWeb.Varredura.VarreduraSisregOpcoes.Secao));
        services.AddSingleton<
            Integracoes.SisregWeb.Varredura.Background.IVarreduraSisregFila,
            Integracoes.SisregWeb.Varredura.Background.VarreduraSisregFila>();
        services.AddSingleton<Integracoes.SisregWeb.Varredura.Background.VarreduraSisregEstadoVivo>();
        services.AddScoped<
            Integracoes.SisregWeb.Varredura.IVarreduraAgendaService,
            Integracoes.SisregWeb.Varredura.VarreduraAgendaService>();

        // Import PONTUAL (botão por procedimento na tela de mapeamento): consulta o cons_agendas
        // direto (não bloqueado por horário, ao contrário do expo) para o "não dá para esperar as
        // 15h". Síncrono, escopo de um par, entra pelo mesmo núcleo de ImportarMarcacoesAsync.
        services.AddScoped<
            Integracoes.SisregWeb.Importacao.AgendaPontual.IImportacaoAgendaPontualService,
            Integracoes.SisregWeb.Importacao.AgendaPontual.ImportacaoAgendaPontualService>();

        services.AddHostedService<Integracoes.SisregWeb.Varredura.Background.VarreduraSisregRunner>();
        services.AddHostedService<Integracoes.SisregWeb.Varredura.Background.VarreduraSisregScheduler>();

        // Motor "sincroniza tudo" (#118): reconcilia o mapeamento (profissionais/procedimentos +
        // vínculo FHIR) de TODAS as unidades configuradas em sequência, com botão manual e flag
        // diário. Sequencial e com throttle: divide o orçamento anti-robô do SISREG com a varredura.
        services.Configure<Integracoes.SisregWeb.MapeamentoLote.SisregMapeamentoLoteOpcoes>(
            configuration.GetSection(Integracoes.SisregWeb.MapeamentoLote.SisregMapeamentoLoteOpcoes.Secao));
        services.AddSingleton<
            Integracoes.SisregWeb.MapeamentoLote.Background.IMapeamentoLoteFila,
            Integracoes.SisregWeb.MapeamentoLote.Background.MapeamentoLoteFila>();
        services.AddSingleton<Integracoes.SisregWeb.MapeamentoLote.Background.MapeamentoLoteEstadoVivo>();
        services.AddScoped<
            Integracoes.SisregWeb.MapeamentoLote.ISisregMapeamentoLoteService,
            Integracoes.SisregWeb.MapeamentoLote.SisregMapeamentoLoteService>();
        services.AddHostedService<Integracoes.SisregWeb.MapeamentoLote.Background.MapeamentoLoteRunner>();
        services.AddHostedService<Integracoes.SisregWeb.MapeamentoLote.Background.MapeamentoLoteScheduler>();

        // ---- SER (Sistema Estadual de Regulação, SES-RJ) — ADR-0042 ----
        // Sessão ÚNICA por operador, como no SISREG: um cliente HTTP singleton com cookies
        // próprios (JSESSIONID + SERVERID do balanceador) e o módulo Ambulatório ativo na conversa
        // Seam. Duas varreduras concorrentes se derrubariam, por isso a sessão serializa por
        // semáforo interno.
        services.AddSingleton<Integracoes.SerWeb.ISerWebSessao, Integracoes.SerWeb.SerWebSessao>();

        // Leitor (fala "SER", não conhece o banco) + varredor (bisecção por Data da Solicitação,
        // que é o eixo imutável) + sincronizador (espelha, faz diff e gera gatilhos).
        services.AddScoped<
            Integracoes.SerWeb.Varredura.ISerLeitorService,
            Integracoes.SerWeb.Varredura.SerLeitorService>();
        // Leitor por EXPORT (tela de Histórico de Consulta/Exame): 500 registros por lote num .xls,
        // com aviso explícito de corte. É o caminho das SEIS situações que aquele combo oferece.
        services.AddScoped<
            Integracoes.SerWeb.Varredura.Export.ISerExportLeitor,
            Integracoes.SerWeb.Varredura.Export.SerExportLeitor>();

        // Leitor por EXPORT da tela de Solicitação: 100 por lote e sem aviso de corte, mas é a
        // ÚNICA que oferece a situação ALTA. Também por arquivo — a leitura paginada foi aposentada
        // em 08/08/2026 por perder 853 registros de ALTA declarando cobertura completa.
        services.AddScoped<
            Integracoes.SerWeb.Varredura.Export.ISerExportSolicitacaoLeitor,
            Integracoes.SerWeb.Varredura.Export.SerExportSolicitacaoLeitor>();
        services.AddScoped<Integracoes.SerWeb.Varredura.Export.VarredorSerPorExport>();

        services.AddScoped<
            Integracoes.SerWeb.Varredura.ISerSincronizacaoService,
            Integracoes.SerWeb.Varredura.SerSincronizacaoService>();

        // A rodada leva de 15 min a ~1 h — nunca pode rodar dentro de um POST. Fila de
        // capacidade 1 que RECUSA em vez de esperar: a sessão do SER é única por operador.
        services.AddSingleton<
            Integracoes.SerWeb.Varredura.Background.IVarreduraSerFila,
            Integracoes.SerWeb.Varredura.Background.VarreduraSerFila>();
        services.AddHostedService<Integracoes.SerWeb.Varredura.Background.VarreduraSerRunner>();

        // Disparo diário. Vem DESLIGADO por padrão (Ser:Varredura:Ativo): ligar sozinho num
        // ambiente novo derrubaria a sessão do operador do SER sem ninguém entender por quê.
        services.Configure<Integracoes.SerWeb.Varredura.Background.VarreduraSerOpcoes>(
            configuration.GetSection(Integracoes.SerWeb.Varredura.Background.VarreduraSerOpcoes.Secao));
        services.AddHostedService<Integracoes.SerWeb.Varredura.Background.VarreduraSerScheduler>();

        // Consumo pelas telas: a busca lê o ESPELHO (nosso banco), não o SER.
        services.AddScoped<Ser.ISerConsultaService, Ser.SerConsultaService>();
        services.AddScoped<Ser.ISerMotorService, Ser.SerMotorService>();

        // Notificações: o primeiro consumidor da fila de gatilhos. Marcar como visto é o que
        // esvazia `ser_gatilho` — até aqui a fila só crescia.
        services.AddScoped<Ser.ISerNotificacaoService, Ser.SerNotificacaoService>();

        // ESCRITA no SER (FollowUP). Duas identidades diferentes, de propósito: a sessão acima é
        // de SINCRONISMO (credencial do banco) e só lê; escrever usa a sessão do OPERADOR, que é
        // singleton por viver em memória e por sessão de usuário — nunca em banco. Sem isso, toda
        // ação do município sairia assinada pela mesma pessoa na trilha do Estado.
        services.AddSingleton<Ser.Sessao.ISerSessaoOperadorStore, Ser.Sessao.SerSessaoOperadorStore>();
        services.AddScoped<Ser.ISerEscritaService, Ser.SerEscritaService>();

        // Config do disparo diário em BANCO: mudar a hora não pode exigir deploy.
        services.AddScoped<Ser.ISerVarreduraConfigService, Ser.SerVarreduraConfigService>();

        // Formulário de nova solicitação, lido ao vivo do SER (somente leitura — não envia).
        services.AddScoped<Ser.ISerNovaSolicitacaoService, Ser.SerNovaSolicitacaoService>();

        // Catálogo do SER espelhado: a tela monta o formulário DAQUI, offline. O sync é quem
        // copia (leitura longa, sob demanda); o service de leitura não toca no SER.
        services.AddScoped<Ser.ISerCatalogoService, Ser.SerCatalogoService>();
        services.AddScoped<Ser.ISerCatalogoSyncService, Ser.SerCatalogoSyncService>();

        // A cópia leva ~10 min: roda FORA do request. Dentro dele, o proxy desistia e o
        // CancellationToken da conexão abortada matava a cópia no meio (10/08/2026).
        services.AddSingleton<Ser.Background.ISerCatalogoSyncFila, Ser.Background.SerCatalogoSyncFila>();
        services.AddHostedService<Ser.Background.SerCatalogoSyncRunner>();

        // Rascunhos: pedidos montados e guardados aqui, com anexos, até serem autorizados.
        services.AddScoped<Ser.ISerRascunhoService, Ser.SerRascunhoService>();

        // Conciliação do paciente do SER com o hub FHIR (ADR-0009/0020/0041).
        services.AddScoped<Ser.Pacientes.ISerConciliacaoPacienteService,
            Ser.Pacientes.SerConciliacaoPacienteService>();
        services.AddScoped<Ser.Pacientes.ISerBackfillPacientesService,
            Ser.Pacientes.SerBackfillPacientesService>();
        services.AddHostedService<Ser.Background.SerConciliacaoPacienteRunner>();

        // Consulta DIRETA ao SER: a bancada de testes da integração. Exercita login, módulo,
        // ViewState, busca, paginação e parser em segundos, sem gravar nada.
        services.AddScoped<Ser.ISerConsultaDiretaService, Ser.SerConsultaDiretaService>();

        // ---- SERNIT (SER de Niterói) — subsistema irmão do SER-RJ (ADR-0042), tabelas sernit_* ----
        // Sessão ÚNICA por operador (login por Referer; cookies JSESSIONID + UCID). Difere do
        // SER-RJ: a grade é lida por PAGINAÇÃO (o SERNIT não tem Exportar).
        services.AddSingleton<Integracoes.SernitWeb.ISernitWebSessao, Integracoes.SernitWeb.SernitWebSessao>();
        services.AddScoped<
            Integracoes.SernitWeb.Varredura.ISernitLeitorService,
            Integracoes.SernitWeb.Varredura.SernitLeitorService>();
        services.AddScoped<Integracoes.SernitWeb.Varredura.VarredorSernitPorPaginacao>();
        services.AddScoped<
            Integracoes.SernitWeb.Varredura.ISernitSincronizacaoService,
            Integracoes.SernitWeb.Varredura.SernitSincronizacaoService>();

        services.AddSingleton<
            Integracoes.SernitWeb.Varredura.Background.IVarreduraSernitFila,
            Integracoes.SernitWeb.Varredura.Background.VarreduraSernitFila>();
        services.AddHostedService<Integracoes.SernitWeb.Varredura.Background.VarreduraSernitRunner>();

        services.Configure<Integracoes.SernitWeb.Varredura.Background.VarreduraSernitOpcoes>(
            configuration.GetSection(Integracoes.SernitWeb.Varredura.Background.VarreduraSernitOpcoes.Secao));
        services.AddHostedService<Integracoes.SernitWeb.Varredura.Background.VarreduraSernitScheduler>();

        // Config do disparo diário do SERNIT em BANCO (mudar a hora não pode exigir deploy).
        services.AddScoped<Sernit.ISernitVarreduraConfigService, Sernit.SernitVarreduraConfigService>();

        // Consumo pelas telas: a busca lê o ESPELHO (nosso banco); o motor opera a rodada.
        services.AddScoped<Sernit.ISernitConsultaService, Sernit.SernitConsultaService>();
        services.AddScoped<Sernit.ISernitMotorService, Sernit.SernitMotorService>();

        // ESCRITA no SERNIT (FollowUP + telefones), assinada pelo OPERADOR: a sessão de escrita é
        // singleton em memória e por sessão de usuário (jti), nunca em banco — a de sincronismo só lê.
        services.AddSingleton<Sernit.Sessao.ISernitSessaoOperadorStore, Sernit.Sessao.SernitSessaoOperadorStore>();
        services.AddScoped<Sernit.ISernitEscritaService, Sernit.SernitEscritaService>();

        // Notificações: consumidor da fila sernit_gatilho (marcar visto tira da fila).
        services.AddScoped<Sernit.ISernitNotificacaoService, Sernit.SernitNotificacaoService>();

        // Conciliação do paciente do SERNIT com o hub FHIR (ADR-0009/0020/0041), pelo upsert canônico.
        services.AddScoped<Sernit.Pacientes.ISernitConciliacaoPacienteService,
            Sernit.Pacientes.SernitConciliacaoPacienteService>();
        services.AddScoped<Sernit.Pacientes.ISernitBackfillPacientesService,
            Sernit.Pacientes.SernitBackfillPacientesService>();
        services.AddHostedService<Sernit.Background.SernitConciliacaoPacienteRunner>();

        // Catálogo espelhado + nova solicitação (leitura ao vivo) + rascunhos.
        services.AddScoped<Sernit.ISernitCatalogoService, Sernit.SernitCatalogoService>();
        services.AddScoped<Sernit.ISernitCatalogoSyncService, Sernit.SernitCatalogoSyncService>();
        services.AddScoped<Sernit.ISernitNovaSolicitacaoService, Sernit.SernitNovaSolicitacaoService>();
        services.AddScoped<Sernit.ISernitRascunhoService, Sernit.SernitRascunhoService>();
        services.AddSingleton<Sernit.Background.ISernitCatalogoSyncFila, Sernit.Background.SernitCatalogoSyncFila>();
        services.AddHostedService<Sernit.Background.SernitCatalogoSyncRunner>();

        // Importação de agendamentos → Solicitacao. A leitura do SISREG é a varredura da agenda
        // do executante (cons_agendas); o scraper de cons_marcados_reg foi aposentado por mirar a
        // visão do solicitante e custar 1 requisição de ficha POR agendamento — sozinho estouraria
        // o limite anti-robô do SISREG.
        services.AddScoped<Integracoes.SisregWeb.Importacao.IImportacaoSisregService, Integracoes.SisregWeb.Importacao.ImportacaoSisregService>();
        services.AddScoped<
            Integracoes.SisregWeb.Importacao.IBackfillExecutanteService,
            Integracoes.SisregWeb.Importacao.BackfillExecutanteService>();

        // Sincronismo de ESCALAS (a oferta). Mesmo quinteto dos demais motores do SISREG.
        services.Configure<Integracoes.SisregWeb.Escalas.EscalasSincronizacaoOpcoes>(
            configuration.GetSection(Integracoes.SisregWeb.Escalas.EscalasSincronizacaoOpcoes.Secao));
        services.AddSingleton<
            Integracoes.SisregWeb.Escalas.Background.IEscalasSincronizacaoFila,
            Integracoes.SisregWeb.Escalas.Background.EscalasSincronizacaoFila>();
        services.AddSingleton<Integracoes.SisregWeb.Escalas.Background.EscalasSincronizacaoEstadoVivo>();
        services.AddScoped<
            Integracoes.SisregWeb.Escalas.IEscalasSincronizacaoService,
            Integracoes.SisregWeb.Escalas.EscalasSincronizacaoService>();
        services.AddHostedService<Integracoes.SisregWeb.Escalas.Background.EscalasSincronizacaoRunner>();
        services.AddHostedService<Integracoes.SisregWeb.Escalas.Background.EscalasSincronizacaoScheduler>();

        // Histórico da agenda: anda para trás, uma fatia por tick, dirigido pela cobertura gravada
        // em sisreg_varredura_agenda. Sem fila e sem runner de propósito — não há execução longa a
        // orquestrar; cada fatia é uma varredura por período do motor que já existe.
        services.Configure<Integracoes.SisregWeb.Historico.HistoricoOpcoes>(
            configuration.GetSection(Integracoes.SisregWeb.Historico.HistoricoOpcoes.Secao));
        services.AddHostedService<Integracoes.SisregWeb.Historico.HistoricoAgendaScheduler>();

        services.AddScoped<
            Integracoes.SisregWeb.Alteracoes.IAlteracoesAgendaService,
            Integracoes.SisregWeb.Alteracoes.AlteracoesAgendaService>();

        services.AddScoped<AgendaRegulacao.IAgendaAnaliseService, AgendaRegulacao.AgendaAnaliseService>();
        services.AddScoped<AgendaRegulacao.IAgendaDemandaService, AgendaRegulacao.AgendaDemandaService>();

        // Importação SISREG em LOTE (vários arquivos / zip) — processada no servidor, fora da
        // request: fechar a aba não mata a importação e os contadores do rastreio são confiáveis.
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.ISisregImportacaoFila, Integracoes.SisregWeb.Importacao.Background.SisregImportacaoFila>();
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.SisregImportacaoEstadoVivo>();
        services.AddScoped<Integracoes.SisregWeb.Importacao.IImportacaoLoteService, Integracoes.SisregWeb.Importacao.ImportacaoLoteService>();
        services.AddHostedService<Integracoes.SisregWeb.Importacao.Background.SisregImportacaoRunner>();

        // "Resolver todas as pendências": mesmo desenho (fila de 1 + estado vivo + runner), fila
        // própria — é outro trabalho na mesma tela, e compartilhar o estado faria um aparecer no
        // lugar do outro.
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.IResolucaoPendenciasFila, Integracoes.SisregWeb.Importacao.Background.ResolucaoPendenciasFila>();
        services.AddSingleton<Integracoes.SisregWeb.Importacao.Background.ResolucaoPendenciasEstadoVivo>();
        services.AddScoped<Integracoes.SisregWeb.Importacao.IResolucaoPendenciasService, Integracoes.SisregWeb.Importacao.ResolucaoPendenciasService>();
        services.AddHostedService<Integracoes.SisregWeb.Importacao.Background.ResolucaoPendenciasRunner>();

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

        // Escopo de unidade traduzido para AE de origem — recorte da LISTAGEM do PACS.
        services.AddScoped<IEscopoEstudosPacs, EscopoEstudosPacs>();
        services.AddScoped<IOrigemEstudoService, OrigemEstudoService>();
        services.AddScoped<IListagemEstudosService, ListagemEstudosService>();

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
        services.AddScoped<PesquisasSatisfacao.IPesquisasSatisfacaoService, PesquisasSatisfacao.PesquisasSatisfacaoService>();
        services.AddHostedService<PesquisasSatisfacao.PesquisaSatisfacaoScheduler>();

        // ---- Sincronização de PEPs (importação Salux/outros → hub FHIR) — ADR-0014 ----
        services.AddHttpClient<Integracoes.Pep.Fhir.IHubFhirEscritor, Integracoes.Pep.Fhir.HubFhirEscritor>(client =>
        {
            client.BaseAddress = new Uri(fhirBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<Integracoes.Pep.Background.IPepSincronizacaoFila, Integracoes.Pep.Background.PepSincronizacaoFila>();
        services.AddSingleton<Integracoes.Pep.Progresso.PepSincronizacaoEstadoVivo>();
        services.AddScoped<Integracoes.Pep.Estrategias.IEstrategiaImportacaoPep, Integracoes.Pep.Estrategias.Salux.SaluxImportacaoStrategy>();
        services.AddScoped<Integracoes.Pep.Estrategias.IEstrategiaImportacaoPep, Integracoes.Pep.Estrategias.Klinikos.KlinikosImportacaoStrategy>();
        // Árbitro das divergências de identidade: usa a cadeia de motores de CPF (Receita/CADSUS).
        services.AddScoped<Integracoes.Pep.Divergencias.IVerificadorDivergenciasPep, Integracoes.Pep.Divergencias.VerificadorDivergenciasPep>();
        services.AddScoped<Integracoes.Pep.IPepSincronizacaoService, Integracoes.Pep.PepSincronizacaoService>();
        services.AddHostedService<Integracoes.Pep.Background.PepSincronizacaoRunner>();
        services.AddHostedService<Integracoes.Pep.Background.PepSincronizacaoScheduler>();
        // Arbitragem das divergências de identidade: job PRÓPRIO, desacoplado do run — depende
        // de serviço externo pago e não pode segurar o fechamento de um ciclo de importação.
        services.AddHostedService<Integracoes.Pep.Background.VerificadorDivergenciasScheduler>();

        // ---- Módulo IA ----
        // A consulta conversável usa o motor local (feature consulta-inteligente / IaChatController +
        // aiengine modo `dados`). O antigo provedor de "perguntar" via API metrada (IProvedorIa/
        // ClaudeProvedorIa) foi removido. O anthropicBaseUrl ainda serve o DistribuidorIa (FT3).
        var anthropicBaseUrl = configuration["Ia:Anthropic:BaseUrl"] ?? "https://api.anthropic.com/";
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
        services.AddScoped<Inteligencia.Conhecimento.Gestao.IConhecimentoGestaoService,
            Inteligencia.Conhecimento.Gestao.ConhecimentoGestaoService>();
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
        // Robô de atendimento na Messages API (ADR-0050) — mesma chave cifrada do módulo IA.
        // Timeout menor que o do FT3: um turno de WhatsApp que passa disso já perdeu a conversa.
        services.AddHttpClient<RoboAtendimento.Runtime.RoboAtendimentoMotorApi>(client =>
        {
            client.BaseAddress = new Uri(anthropicBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Validação de telefone por OTP (WhatsApp) — registro global do número validado.
        services.AddScoped<Telefones.ITelefoneValidacaoService, Telefones.TelefoneValidacaoService>();

        // Dispensa de verificação: quem não tem celular (ou não consegue confirmar o código)
        // consente em não validar, com motivo — é o que destrava a autorização na recepção.
        services.AddScoped<Telefones.IDispensaContatoService, Telefones.DispensaContatoService>();

        // WhatsApp via Automais.Zap (ADR-0044) — cliente de envio + webhook assinado pelo relay (FT6).
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
        services.AddScoped<Conversas.IConversasDoPacienteService, Conversas.ConversasDoPacienteService>();
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
        // Verificação cadastral determinística (dígitos CPF → nascimento → nome) — SEM LLM.
        services.AddScoped<Notificacoes.WhatsApp.Manipuladores.IManipuladorMensagemWhatsApp,
            Notificacoes.WhatsApp.Manipuladores.VerificacaoCadastralWhatsAppHandler>();

        // ---- Robô de atendimento (assuntos cadastráveis + comandos por assunto) ----
        services.AddScoped<RoboAtendimento.IRoboAssuntoService, RoboAtendimento.RoboAssuntoService>();
        services.AddScoped<RoboAtendimento.IRoboConfiguracaoService, RoboAtendimento.RoboConfiguracaoService>();
        services.AddScoped<RoboAtendimento.IRoboErroService, RoboAtendimento.RoboErroService>();
        services.AddScoped<PendenciasCadastro.IPendenciaCadastroService, PendenciasCadastro.PendenciaCadastroService>();
        services.AddScoped<PendenciasCadastro.IVarreduraContatoNegadoService, PendenciasCadastro.VarreduraContatoNegadoService>();

        // Runtime do robô: motor (aiengine), classificador, processador (1 tarefa) e o worker (fila).
        // Dois motores registrados; quem responde é decidido a cada turno pelo seletor, lendo
        // RoboConfiguracao.Motor (rollback da migração para a API sem deploy — ADR-0050).
        services.AddScoped<RoboAtendimento.Runtime.RoboAtendimentoMotorHttp>();
        services.AddScoped<RoboAtendimento.Runtime.IRoboAtendimentoMotor, RoboAtendimento.Runtime.RoboAtendimentoMotorSeletor>();
        // Ensaio de um turno sem falar com o cidadão (tela do módulo Robô).
        services.AddScoped<RoboAtendimento.Runtime.IRoboSimulacaoService, RoboAtendimento.Runtime.RoboSimulacaoService>();
        services.AddScoped<RoboAtendimento.Runtime.IRoboClassificador, RoboAtendimento.Runtime.RoboClassificador>();
        services.AddScoped<RoboAtendimento.Runtime.IRoboAtendimentoProcessador, RoboAtendimento.Runtime.RoboAtendimentoProcessador>();
        services.AddScoped<Notificacoes.WhatsApp.Manipuladores.IManipuladorMensagemWhatsApp,
            Notificacoes.WhatsApp.Manipuladores.RoboAtendimentoWhatsAppHandler>();
        services.AddHostedService<RoboAtendimento.Runtime.RoboAtendimentoWorker>();

        // ---- Treinamento do robô (crítica do atendente → análise adversarial → correção) ----
        // Cliente próprio da Messages API: o agente treinador roda no Fable, com turnos longos —
        // o timeout do motor de atendimento (60s) mataria a análise no meio.
        services.AddHttpClient<RoboAtendimento.Treinamento.ClienteAnthropicTreinamento>(client =>
        {
            client.BaseAddress = new Uri(anthropicBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddScoped<RoboAtendimento.Treinamento.IRoboBriefingService,
            RoboAtendimento.Treinamento.RoboBriefingService>();
        services.AddScoped<RoboAtendimento.Treinamento.RoboTreinamentoAplicador>();
        services.AddScoped<RoboAtendimento.Treinamento.IRoboTreinadorAgente,
            RoboAtendimento.Treinamento.RoboTreinadorAgente>();
        services.AddScoped<RoboAtendimento.Treinamento.IRoboTreinamentoSimulador,
            RoboAtendimento.Treinamento.RoboTreinamentoSimulador>();
        services.AddScoped<RoboAtendimento.Treinamento.IRoboTreinamentoService,
            RoboAtendimento.Treinamento.RoboTreinamentoService>();
        services.AddHostedService<RoboAtendimento.Treinamento.RoboTreinamentoWorker>();

        // Comandos do robô (o "guichê" tipado) + dispatcher (habilitação por assunto + trilha).
        services.AddScoped<RoboAtendimento.Comandos.IRoboComandoDispatcher, RoboAtendimento.Comandos.RoboComandoDispatcher>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.RegistrarNumeroErradoComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.EncaminharParaHumanoComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.IniciarCancelamentoComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConfirmarPresencaComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.VerificarCadastroComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.InformarHorarioAtendimentoComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConsultarStatusExameRecenteComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConsultarCadastroComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConsultarUnidadesComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConsultarAgendamentosComando>();
        services.AddScoped<RoboAtendimento.Comandos.IRoboComando, RoboAtendimento.Comandos.ConsultarPosicaoRegulacaoComando>();

        // ---- Estatísticas de atendimento (retrato do WhatsApp) — dashboard gerencial ----
        services.AddScoped<Estatisticas.IEstatisticasService, Estatisticas.EstatisticasService>();

        // Faturamento SUS/BPA (FT10): contabilização proporcional + relatórios.
        services.AddScoped<Faturamento.IFaturamentoService, Faturamento.FaturamentoService>();

        // Motor de geração de translado (FT3): distribuição + sequenciamento de rotas.
        services.AddScoped<Translado.Geracao.IGeradorDeTransladoService, Translado.Geracao.GeradorDeTransladoService>();

        // ---- REGULAÇÃO → SOLICITAÇÕES (ADR-0052) ----
        services.AddScoped<Regulacao.Configuracao.IRegulacaoConfiguracaoService, Regulacao.Configuracao.RegulacaoConfiguracaoService>();
        services.AddScoped<Regulacao.Catalogo.IRegulacaoCatalogoService, Regulacao.Catalogo.RegulacaoCatalogoService>();
        services.AddScoped<Regulacao.Catalogo.IRegulacaoProcedimentoBuscaService, Regulacao.Catalogo.RegulacaoProcedimentoBuscaService>();
        services.AddScoped<Regulacao.Anexos.IArquivoExigenciaStore, Regulacao.Anexos.ArquivoExigenciaStoreSpaces>();
        services.AddScoped<Regulacao.Anexos.IRegulacaoExigenciaService, Regulacao.Anexos.RegulacaoExigenciaService>();
        services.AddScoped<Regulacao.Pacientes.IRegulacaoPacienteService, Regulacao.Pacientes.RegulacaoPacienteService>();
        services.AddScoped<Regulacao.Formularios.IRegulacaoFormularioService, Regulacao.Formularios.RegulacaoFormularioService>();
        services.AddScoped<Regulacao.Solicitacoes.IRegulacaoEventoService, Regulacao.Solicitacoes.RegulacaoEventoService>();
        services.AddScoped<Regulacao.Solicitacoes.IRegulacaoSolicitacaoService, Regulacao.Solicitacoes.RegulacaoSolicitacaoService>();
        services.AddScoped<Regulacao.Legado.IRascunhoLegadoGate, Regulacao.Legado.RascunhoLegadoGate>();
        services.AddScoped<Regulacao.Legado.IMigradorRascunhosLegadosService, Regulacao.Legado.MigradorRascunhosLegadosService>();
        // Singleton: o cache de vetores de consulta só vale se sobreviver entre requisições.
        services.AddSingleton<Regulacao.Catalogo.CacheVetorConsulta>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
