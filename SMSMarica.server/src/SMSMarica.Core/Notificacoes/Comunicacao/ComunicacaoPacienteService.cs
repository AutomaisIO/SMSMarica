using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Notificacoes.Comunicacao;

/// <summary>
/// Fila de comunicações ao paciente. Os gatilhos (import, Realizada, laudo assinado) só
/// ENFILEIRAM (<see cref="EnfileirarAsync"/>); o worker (<see cref="EnviadorComunicacaoService"/>)
/// processa com ritmo e retentativas: valida celular BR, gera magic link com destino por
/// finalidade e envia o template correspondente.
/// </summary>
public interface IComunicacaoPacienteService
{
    /// <summary>Enfileira a comunicação (idempotente por solicitação × finalidade). Para
    /// ConfirmacaoAgendamento exige DataAgendada futura (senão não faz nada). NÃO salva —
    /// participa do SaveChanges do chamador.</summary>
    Task EnfileirarAsync(Solicitacao solicitacao, FinalidadeComunicacao finalidade, CancellationToken ct = default);

    /// <summary>Processa UMA tentativa de envio. Nunca lança — falha vira backoff/estado terminal.</summary>
    Task ProcessarTentativaEnvioAsync(Guid comunicacaoId, CancellationToken ct = default);

    /// <summary>
    /// Reenvio MANUAL (operador): REVOGA todos os magic links ativos da solicitação (e derruba
    /// as sessões do cidadão se algum link foi usado — quem recebeu errado perde o acesso) e
    /// reconstrói o envio do zero com os dados ATUAIS do paciente (telefone certo, link novo).
    /// Envia imediatamente, sem esperar o worker.
    /// </summary>
    Task ReenviarAsync(Guid solicitacaoExameId, Guid comunicacaoId, CancellationToken ct = default);

    /// <summary>
    /// Envio MANUAL (operador clicou "Enviar exame/laudo"): cria a comunicação se ainda não existe
    /// (ou reconstrói a existente) para a finalidade, marca origem=Manual + quem enviou, e dispara
    /// na hora. <paramref name="assumirRisco"/> ignora o gate de telefone verificado (o operador
    /// assume o risco de mandar o resultado para um número não verificado). A validação de prontidão
    /// (exame realizado / laudo assinado) é feita pelo chamador.
    /// </summary>
    Task EnviarManualAsync(
        Guid solicitacaoExameId, FinalidadeComunicacao finalidade, bool assumirRisco, CancellationToken ct = default);
}

public sealed class ComunicacaoPacienteService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    ICidadaoLoginLinkService loginLinks,
    IWhatsAppCliente whatsApp,
    IOptions<ComunicacaoPacienteOptions> options,
    Identidade.IUsuarioAtualAccessor usuarioAtual,
    Telefones.IDispensaContatoService dispensasContato,
    ILogger<ComunicacaoPacienteService> logger) : IComunicacaoPacienteService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Códigos de erro da Meta que não adianta retentar (número inexistente/não autorizado).</summary>
    private static readonly string[] ErrosMetaPermanentes = ["131026", "131030"];

    public async Task EnfileirarAsync(
        Solicitacao solicitacao, FinalidadeComunicacao finalidade, CancellationToken ct = default)
    {
        // Confirmação só faz sentido antes do atendimento; as demais finalidades valem sempre.
        if (finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
            && (solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow))
            return;

        // Idempotência: uma comunicação por solicitação × finalidade (o índice único garante;
        // a checagem evita a exceção quando o gatilho re-dispara).
        var jaExiste = await db.ComunicacoesPaciente
            .AnyAsync(c => c.SolicitacaoId == solicitacao.Id && c.Finalidade == finalidade, ct);
        if (jaExiste) return;

        db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = solicitacao.Categoria == CategoriaSolicitacao.Consulta
                ? TipoAgendamento.Consulta : TipoAgendamento.Exame,
            Finalidade = finalidade,
            SolicitacaoId = solicitacao.Id,
            PacienteId = solicitacao.PacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        });
    }

    public async Task ProcessarTentativaEnvioAsync(Guid comunicacaoId, CancellationToken ct = default)
    {
        var n = await db.ComunicacoesPaciente
            .Include(x => x.Solicitacao!).ThenInclude(s => s.ExameImagem!).ThenInclude(e => e.TipoExame)
            .FirstOrDefaultAsync(x => x.Id == comunicacaoId, ct);
        if (n is null || n.Status != StatusComunicacao.Pendente || n.ProximaTentativaEm is null)
            return;

        n.Tentativas++;
        n.UltimaTentativaEm = DateTime.UtcNow;
        n.AtualizadoEm = DateTime.UtcNow;

        try
        {
            var s = n.Solicitacao;

            if (s is null || s.ExcluidoEm is not null || s.Status == StatusSolicitacao.Cancelada)
            {
                Terminal(n, StatusComunicacao.Falha, "Solicitação excluída ou cancelada antes do envio.");
            }
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && (s.DataAgendada is not { } dataAgendada || dataAgendada <= DateTime.UtcNow))
            {
                Terminal(n, StatusComunicacao.Falha, "Exame sem data futura no momento do envio.");
            }
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            {
                Terminal(n, StatusComunicacao.Falha, "Paciente já respondeu por outro canal.");
            }
            else
            {
                await EnviarAsync(n, s, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar comunicação {Id} ({Finalidade}, tentativa {N}).",
                n.Id, n.Finalidade, n.Tentativas);
            ReagendarOuFalhar(n, ex.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ReenviarAsync(Guid solicitacaoExameId, Guid comunicacaoId, CancellationToken ct = default)
    {
        // Id público de um exame = ExameImagem.Id; traduz para o id da espinha (Solicitacao).
        // Consulta: o id público já é o da espinha (sem satélite).
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
            ?? solicitacaoExameId;

        var n = await db.ComunicacoesPaciente
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == comunicacaoId && x.SolicitacaoId == solicitacaoId, ct)
            ?? throw new Common.Excecoes.NaoEncontradoException(nameof(ComunicacaoPaciente), comunicacaoId);

        var agora = DateTime.UtcNow;
        var s = n.Solicitacao;

        // Guardas de coerência (evitam transformar um histórico OK em Falha no processamento).
        if (s is null || s.ExcluidoEm is not null || s.Status == StatusSolicitacao.Cancelada)
            throw new Common.Excecoes.ConflitoException(
                "reenvio.solicitacao_invalida", "A solicitação foi excluída ou cancelada — nada a reenviar.");
        if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
        {
            if (s.DataAgendada is not { } da || da <= agora)
                throw new Common.Excecoes.ConflitoException(
                    "reenvio.sem_data_futura", "O exame não tem data futura — a confirmação não pode ser reenviada.");
            if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
                throw new Common.Excecoes.ConflitoException(
                    "reenvio.ja_respondida", "O paciente já respondeu esta confirmação — nada a reenviar.");
        }

        // 1. REVOGA todos os magic links ainda ativos da solicitação (não só o desta comunicação:
        //    qualquer link anterior pode ter ido para o número errado). Expirar = ninguém mais
        //    autentica com eles.
        var linksAtivos = await db.CidadaoLoginLinks
            .Where(l => l.SolicitacaoId == solicitacaoId && l.ExpiraEm > agora)
            .ToListAsync(ct);
        foreach (var l in linksAtivos) l.ExpiraEm = agora;

        // 2. Se algum link da solicitação JÁ FOI USADO, derruba as sessões ativas do paciente do
        //    link — se quem clicou foi a pessoa errada, ela perde o acesso ao app AGORA. O
        //    paciente certo reentra com 1 clique no link novo (single-device, custo zero).
        var pacientesComLinkUsado = await db.CidadaoLoginLinks.AsNoTracking()
            .Where(l => l.SolicitacaoId == solicitacaoId && l.UsadoEm != null)
            .Select(l => l.PatientId)
            .Distinct()
            .ToListAsync(ct);
        if (pacientesComLinkUsado.Count > 0)
        {
            await db.CidadaoSessoes
                .Where(x => x.RevogadaEm == null && pacientesComLinkUsado.Contains(x.CidadaoAcesso.PatientId))
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.RevogadaEm, agora), ct);
        }

        // 3. Reconstrói o envio do zero: zera telefone/link/recibos — o processamento re-resolve
        //    o paciente (telefone ATUAL: verificado > celular > principal) e gera link novo.
        n.Status = StatusComunicacao.Pendente;
        n.MotivoFalha = null;
        n.Telefone = null;
        n.LoginLinkId = null;
        n.MensagemWhatsAppId = null;
        n.Tentativas = 0;
        n.EnviadoEm = null;
        n.EntregueEm = null;
        n.LidoEm = null;
        n.VisualizadoEm = null;
        n.ProximaTentativaEm = agora;
        n.AtualizadoEm = agora;

        // Trilha na linha do tempo: quem reenviou e o que foi revogado.
        db.ContatosRegistro.Add(new ContatoRegistro
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            PacienteId = n.PacienteId,
            Meio = MeioContato.WhatsApp,
            Resultado = ResultadoContato.Outro,
            Observacao = $"Reenvio manual da comunicação ({RotuloFinalidade(n.Finalidade)}): " +
                         $"{linksAtivos.Count} link(s) de acesso anterior(es) revogado(s); mensagem reconstruída com o contato atual.",
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        });

        await db.SaveChangesAsync(ct);

        // 4. Envia JÁ (mesmo caminho do worker; falha vira backoff normal, visível no histórico).
        await ProcessarTentativaEnvioAsync(n.Id, ct);
    }

    public async Task EnviarManualAsync(
        Guid solicitacaoExameId, FinalidadeComunicacao finalidade, bool assumirRisco, CancellationToken ct = default)
    {
        if (finalidade is not (FinalidadeComunicacao.ExameLiberado or FinalidadeComunicacao.LaudoPronto))
            throw new Common.Excecoes.ValidacaoException(
                "comunicacao.finalidade_invalida", "Envio manual só vale para exame liberado ou laudo pronto.");

        // Id público de um exame = ExameImagem.Id; traduz para o id da espinha (Solicitacao).
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
            ?? solicitacaoExameId;

        var s = await db.Solicitacoes
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, ct)
            ?? throw new Common.Excecoes.NaoEncontradoException(nameof(Solicitacao), solicitacaoId);
        if (s.ExcluidoEm is not null || s.Status == StatusSolicitacao.Cancelada)
            throw new Common.Excecoes.ConflitoException(
                "envio.solicitacao_invalida", "A solicitação foi excluída ou cancelada — nada a enviar.");

        var agora = DateTime.UtcNow;

        // Revoga links de acesso ainda ativos da solicitação (e derruba sessões de quem já usou um
        // link — proteção contra número errado); o envio reconstrói com o contato ATUAL.
        var linksAtivos = await db.CidadaoLoginLinks
            .Where(l => l.SolicitacaoId == solicitacaoId && l.ExpiraEm > agora)
            .ToListAsync(ct);
        foreach (var l in linksAtivos) l.ExpiraEm = agora;
        var pacientesComLinkUsado = await db.CidadaoLoginLinks.AsNoTracking()
            .Where(l => l.SolicitacaoId == solicitacaoId && l.UsadoEm != null)
            .Select(l => l.PatientId).Distinct().ToListAsync(ct);
        if (pacientesComLinkUsado.Count > 0)
            await db.CidadaoSessoes
                .Where(x => x.RevogadaEm == null && pacientesComLinkUsado.Contains(x.CidadaoAcesso.PatientId))
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.RevogadaEm, agora), ct);

        // Upsert da comunicação (solicitação × finalidade é único): cria se não existe, senão reusa.
        var n = await db.ComunicacoesPaciente
            .FirstOrDefaultAsync(c => c.SolicitacaoId == solicitacaoId && c.Finalidade == finalidade, ct);
        if (n is null)
        {
            n = new ComunicacaoPaciente
            {
                Id = Guid.CreateVersion7(),
                Tipo = s.Categoria == CategoriaSolicitacao.Consulta ? TipoAgendamento.Consulta : TipoAgendamento.Exame,
                Finalidade = finalidade,
                SolicitacaoId = solicitacaoId,
                PacienteId = s.PacienteId,
                CriadoEm = agora,
            };
            db.ComunicacoesPaciente.Add(n);
        }

        // Reconstrói o estado de envio (o processamento re-resolve o telefone e gera link novo).
        n.Status = StatusComunicacao.Pendente;
        n.MotivoFalha = null;
        n.Telefone = null;
        n.LoginLinkId = null;
        n.MensagemWhatsAppId = null;
        n.Tentativas = 0;
        n.EnviadoEm = null;
        n.EntregueEm = null;
        n.LidoEm = null;
        n.VisualizadoEm = null;
        n.ProximaTentativaEm = agora;
        n.AtualizadoEm = agora;
        n.Origem = OrigemComunicacao.Manual;
        n.EnviadoPor = usuarioAtual.UsuarioId;
        n.IgnorarVerificacaoTelefone = assumirRisco;

        db.ContatosRegistro.Add(new ContatoRegistro
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            PacienteId = s.PacienteId,
            Meio = MeioContato.WhatsApp,
            Resultado = ResultadoContato.Outro,
            Observacao = $"Envio manual — {RotuloFinalidade(finalidade)}."
                + (assumirRisco ? " Telefone NÃO verificado: risco assumido pelo operador." : "")
                + (linksAtivos.Count > 0 ? $" {linksAtivos.Count} link(s) anterior(es) revogado(s)." : ""),
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        });

        await db.SaveChangesAsync(ct);

        // Envia JÁ (mesmo caminho do worker; falha vira backoff/estado terminal, visível no histórico).
        await ProcessarTentativaEnvioAsync(n.Id, ct);
    }

    private static string RotuloFinalidade(FinalidadeComunicacao f) => f switch
    {
        FinalidadeComunicacao.ConfirmacaoAgendamento => "confirmação de agendamento",
        FinalidadeComunicacao.ExameLiberado => "exame liberado",
        FinalidadeComunicacao.LaudoPronto => "laudo pronto",
        _ => f.ToString(),
    };

    private async Task EnviarAsync(ComunicacaoPaciente n, Solicitacao s, CancellationToken ct)
    {
        var paciente = await pacientes.ObterPorIdAsync(n.PacienteId, ct);

        // DADO CLÍNICO (imagem do exame, laudo) só vai para contato VERIFICADO: o link abre o
        // resultado, e o telefone do cadastro pode estar errado/desatualizado — foi o que
        // mandou laudo para destino desconhecido no lote de 2026-07. Confirmação de
        // agendamento continua indo para qualquer celular: não expõe resultado e é ela que
        // provoca o contato (a resposta do paciente é o que permite verificar o número).
        var exigeVerificado = n.Finalidade is FinalidadeComunicacao.ExameLiberado
            or FinalidadeComunicacao.LaudoPronto;

        // Envio manual com "assumo o risco" (n.IgnorarVerificacaoTelefone): o operador decidiu
        // enviar o resultado mesmo sem número verificado — pula o gate e usa o melhor celular.
        if (exigeVerificado && !n.IgnorarVerificacaoTelefone
            && !TelefoneWhatsApp.EhCelularBr(paciente.TelefoneVerificado))
        {
            // Dispensa registrada na recepção: o paciente consentiu em não validar. Parte dos
            // motivos ainda tem um número utilizável ("é o celular da filha", "não consegue
            // digitar o código") — nesses o resultado segue para o cadastro. Os demais ("não tem
            // celular", "recusa") não têm para onde ir: continua retida e a entrega é presencial.
            var dispensa = await dispensasContato.ObterAtivaAsync(n.PacienteId, ct);
            if (dispensa is not { PermiteEnvio: true })
            {
                // NÃO é terminal: fica retida e sai sozinha quando a recepção verificar o contato.
                n.Status = StatusComunicacao.AguardandoTelefoneVerificado;
                n.MotivoFalha = dispensa is null
                    ? "Contato não verificado — resultado e laudo só vão para telefone verificado."
                    : $"Verificação dispensada ({dispensa.MotivoTexto}) — resultado e laudo devem ser "
                      + "entregues presencialmente.";
                n.ProximaTentativaEm = null;
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        // Telefone: contato VERIFICADO (marcador no telecom FHIR, já vem no DTO) > qualquer
        // CELULAR do cadastro (celular > principal > residencial — import às vezes guarda o
        // celular como "home").
        var telefone = paciente.TelefoneVerificado
            ?? new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal, paciente.TelefoneResidencial }
                .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
        {
            Terminal(n, StatusComunicacao.SemTelefoneValido, "Paciente sem número de celular válido.");
            return;
        }
        n.Telefone = TelefoneWhatsApp.NormalizarNonoDigito(telefone!);

        // Magic link novo a cada tentativa (o anterior simplesmente expira sem uso). O link é
        // ancorado na ESPINHA (s.Id); o destino usa o id PÚBLICO do exame (ExameImagem.Id) para o
        // front achar o card em /exames.
        var exameIdPublico = s.ExameImagem?.Id ?? s.Id;
        var link = await loginLinks.GerarParaSolicitacaoAsync(exameIdPublico, Destino(n.Finalidade, exameIdPublico), ct);
        n.LoginLinkId = link.Token;

        var opts = options.Value;
        var (template, parametros, botoes) = MontarEnvio(
            n.Finalidade, n.Tipo, s, paciente.NomeCompleto, paciente.Sexo, link.Token, opts);

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            n.Telefone, template, opts.Idioma, parametros, botoes, pacienteId: n.PacienteId, ct: ct);

        if (resultado.Ok)
        {
            n.Status = StatusComunicacao.Enviada;
            n.EnviadoEm = DateTime.UtcNow;
            n.MotivoFalha = null;
            n.ProximaTentativaEm = null; // recibos (entrega/leitura/falha) chegam pelo webhook
            if (resultado.WaMessageId is { } wamid)
                n.MensagemWhatsAppId = await db.MensagensWhatsApp.AsNoTracking()
                    .Where(m => m.WaMessageId == wamid).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        }
        else if (ErrosMetaPermanentes.Any(c => resultado.Erro?.Contains($"({c})") == true))
        {
            Terminal(n, StatusComunicacao.Falha, resultado.Erro);
        }
        else
        {
            ReagendarOuFalhar(n, resultado.Erro);
        }
    }

    /// <summary>Rota de chegada no app após o magic link, por finalidade. Exame liberado e
    /// laudo pronto caem em /exames com o CARD do exame já expandido (?exame={id}).</summary>
    private static string Destino(FinalidadeComunicacao finalidade, Guid solicitacaoId) => finalidade switch
    {
        FinalidadeComunicacao.ExameLiberado or FinalidadeComunicacao.LaudoPronto
            => $"/exames?exame={solicitacaoId}",
        _ => "/agendados/exames",
    };

    private static (string Template, string[] Parametros, BotaoTemplateWhatsApp[] Botoes) MontarEnvio(
        FinalidadeComunicacao finalidade, TipoAgendamento tipo, Solicitacao s, string? nomePaciente,
        Sexo sexo, Guid token, ComunicacaoPacienteOptions opts)
    {
        var nome = PrimeiroNome(nomePaciente);
        // Nome do procedimento: exame de imagem tem TipoExame no satélite; consulta usa a
        // especialidade/procedimento em texto.
        var exame = s.ExameImagem?.TipoExame?.Nome ?? s.EspecialidadeTexto ?? s.ProcedimentoTexto ?? "exame";
        var url = new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, token.ToString());

        switch (finalidade)
        {
            // exame_liberado / laudo_disponivel: "seu exame de {{2}}, realizado {{3}}, está pronto…"
            case FinalidadeComunicacao.ExameLiberado:
                return (opts.TemplateExameLiberado, [nome, exame, DataRealizacao(s)], [url]);

            case FinalidadeComunicacao.LaudoPronto:
                return (opts.TemplateLaudoPronto, [nome, exame, DataRealizacao(s)], [url]);

            // confirmacao_regulacao (modelo do Complexo Regulador; substitui o
            // confirmar_agendamento_urlapp desde 2026-07-08): "Bom dia, {{1}}. … Boas notícias!
            // {{2}} de {{3}} *foi agendada para o dia {{4}}* … retire a guia no posto de saúde
            // onde {{5}} … No dia {{6}} é imprescindível que {{7}} leve também o pedido médico,
            // guia do SISREG, comprovante de residência e cartão do SUS."
            // Flexiona gênero pelo cadastro (Sr./Sra.; sem sexo informado → "você").
            // ("foi agendada" é texto FIXO do template aprovado — a concordância com "O seu
            // exame" só se corrige submetendo nova versão à Meta.)
            // Botões na ordem do template: URL (0), "Não poderei ir!" (1, payload confirma:) e
            // "Falar com atendente" (2, payload atendente: — sem manipulador de propósito: a
            // resposta cai no módulo Conversas/Central de Atendimento).
            default:
                var local = FusoBrasilia.ParaExibicao(s.DataAgendada!.Value);
                var (tratamento, assistido, pronome) = sexo switch
                {
                    Sexo.Masculino => ($"Sr. {nome}", "o Sr. é assistido", "o Sr."),
                    Sexo.Feminino => ($"Sra. {nome}", "a Sra. é assistida", "a Sra."),
                    _ => (nome, "você é assistido(a)", "você"),
                };
                return (
                    opts.TemplateConfirmaAgendamento,
                    [
                        tratamento,
                        tipo == TipoAgendamento.Consulta ? "A sua consulta" : "O seu exame",
                        exame,
                        $"{local.ToString("dd/MM/yyyy", PtBr)} às {local.ToString("HH:mm", PtBr)}h",
                        assistido,
                        tipo == TipoAgendamento.Consulta ? "da sua consulta" : "do seu exame",
                        pronome,
                    ],
                    [
                        url,
                        new BotaoTemplateWhatsApp(TipoBotaoTemplate.QuickReply, $"confirma:{s.Id}"),
                        new BotaoTemplateWhatsApp(TipoBotaoTemplate.QuickReply, $"atendente:{s.Id}"),
                    ]);
        }
    }

    /// <summary>Data em que o exame foi feito (DICOM → detecção → criação), formatada dd/MM/aaaa.</summary>
    private static string DataRealizacao(Solicitacao s)
    {
        // Datas de execução vivem no satélite de imagem; DataEstudo é wall-clock do equipamento
        // (as-is), os demais são UTC → Brasília. Fallback final na criação da solicitação.
        var d = s.ExameImagem?.DataEstudo
            ?? (s.ExameImagem?.RealizadoEm is { } r ? FusoBrasilia.ParaExibicao(r) : FusoBrasilia.ParaExibicao(s.CriadoEm));
        return d.ToString("dd/MM/yyyy", PtBr);
    }

    private void ReagendarOuFalhar(ComunicacaoPaciente n, string? erro)
    {
        n.MotivoFalha = Truncar(erro);
        if (n.Tentativas >= Math.Max(1, options.Value.MaxTentativas))
        {
            n.Status = StatusComunicacao.Falha;
            n.ProximaTentativaEm = null;
            return;
        }
        // Backoff exponencial: 5min, 10min, 20min, 40min...
        n.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(5 * Math.Pow(2, n.Tentativas - 1));
    }

    private static void Terminal(ComunicacaoPaciente n, StatusComunicacao status, string? motivo)
    {
        n.Status = status;
        n.MotivoFalha = Truncar(motivo);
        n.ProximaTentativaEm = null;
    }

    private static string PrimeiroNome(string? nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "Paciente" : PtBr.TextInfo.ToTitleCase(partes[0].ToLowerInvariant());
    }

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
