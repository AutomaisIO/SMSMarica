using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Comunicacao;

/// <summary>
/// Fila de comunicações ao paciente. Os gatilhos (import, Realizada, laudo assinado) só
/// ENFILEIRAM (<see cref="EnfileirarAsync"/>); o worker (<see cref="EnviadorComunicacaoService"/>)
/// processa com ritmo e retentativas: valida celular BR, gera magic link com destino por
/// finalidade e envia o template correspondente.
/// </summary>
public interface IComunicacaoPacienteService
{
    /// <summary>Enfileira a comunicação (idempotente por solicitação × finalidade). Para
    /// ConfirmacaoAgendamento exige DataAgendada futura (senão não faz nada). CancelamentoAgendamento
    /// só entra com o aviso ligado e para cancelamento posterior ao momento em que foi ligado.
    /// NÃO salva — participa do SaveChanges do chamador.</summary>
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

    /// <summary>
    /// Avisa o paciente do CANCELAMENTO agora, sem esperar o worker.
    ///
    /// <para>É o único aviso que sai na hora, e por um motivo concreto: quem clicou em cancelar
    /// está com o paciente na linha ou acabou de falar com ele. Esperar o próximo ciclo faria a
    /// pessoa ser avisada por mensagem de algo que ela já ouviu — ou pior, ouvir primeiro do
    /// posto.</para>
    ///
    /// <para>A comunicação fica registrada como enviada, e é isso que impede o motor periódico de
    /// conciliação de avisar de novo quando encontrar este mesmo cancelamento na tela do SISREG
    /// minutos depois. Nunca lança: falha de WhatsApp não desfaz cancelamento.</para>
    /// </summary>
    /// <returns><c>true</c> se a mensagem saiu agora.</returns>
    Task<bool> AvisarCancelamentoAgoraAsync(Solicitacao solicitacao, CancellationToken ct = default);

    /// <summary>
    /// Corta o acesso do paciente ao que já foi enviado desta solicitação: expira TODOS os magic
    /// links ainda ativos e derruba as sessões de quem chegou a usar algum. NÃO salva — participa
    /// do <c>SaveChanges</c> do chamador (exceto a revogação de sessões, que é <c>ExecuteUpdate</c>).
    /// <para>Usado pelo reenvio manual e pela <b>correção de identidade</b>: quando um exame muda de
    /// dono, quem recebeu o link por engano precisa perder o acesso na mesma operação.</para>
    /// </summary>
    Task<IReadOnlyList<CidadaoLoginLink>> RevogarAcessosAsync(
        Guid solicitacaoId, DateTime agora, CancellationToken ct = default);
}

public sealed class ComunicacaoPacienteService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    ICidadaoLoginLinkService loginLinks,
    IWhatsAppCliente whatsApp,
    IOptions<ComunicacaoPacienteOptions> options,
    Identidade.IUsuarioAtualAccessor usuarioAtual,
    Telefones.IDispensaContatoService dispensasContato,
    IContatoComprometidoService contatosComprometidos,
    Confirmacoes.IConfirmacaoConfiguracaoService regrasConfirmacao,
    ILogger<ComunicacaoPacienteService> logger) : IComunicacaoPacienteService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Códigos de erro do WhatsApp que não adianta retentar (número inexistente/não autorizado).</summary>
    private static readonly string[] ErrosMetaPermanentes = ["131026", "131030"];

    /// <summary>
    /// O Automais.Zap devolve o erro como "{message} (code {code})"; o formato antigo, direto da
    /// Meta, era "({code}) {message}". Casa pelo código nos dois — senão número inexistente volta
    /// para a fila de retentativas em vez de falhar de vez.
    /// </summary>
    internal static bool ErroPermanente(string? erro)
        => erro is not null
           && ErrosMetaPermanentes.Any(c => erro.Contains($"({c})") || erro.Contains($"code {c}"));

    public async Task EnfileirarAsync(
        Solicitacao solicitacao, FinalidadeComunicacao finalidade, CancellationToken ct = default)
    {
        // Confirmação (e a régua que a reforça) só faz sentido antes do atendimento; as demais
        // finalidades valem sempre.
        if (EhDaConfirmacao(finalidade)
            && (solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow))
            return;

        // Aviso de cancelamento com a chave desligada NÃO entra na fila: desligado quer dizer "não
        // avisar", e não "avisar depois". Antes de 25/09/2026 a conciliação com o SISREG enfileirava
        // mesmo assim, e 217 avisos velhos se acumularam — ligar a chave soltaria todos de uma vez.
        // Assim, ligar vale daqui para frente (o "Cancelar" da tela de Confirmações já era assim).
        if (finalidade == FinalidadeComunicacao.CancelamentoAgendamento)
        {
            var regras = await RegrasAsync(ct);
            if (!regras.AvisoCancelamentoHabilitado) return;

            // Cancelamento feito no SISREG ANTES de o aviso ser ligado, e só conciliado depois (a
            // releitura do fechamento alcança dias para trás): também é retroativo.
            if (solicitacao.CanceladoEm is { } canceladoEm
                && (regras.AvisoCancelamentoLigadoEm is not { } ligadoEm || canceladoEm < ligadoEm))
                return;
        }

        // Por enquanto só se confirma agendamento do SISREG (regra do menu Confirmações): o
        // cadastrado à mão na recepção não gera mensagem.
        if (EhDaConfirmacao(finalidade)
            && (await RegrasAsync(ct)).SomenteSisreg
            && !Confirmacoes.OrigemAgendamento.EhDoSisreg(solicitacao))
            return;

        // Idempotência: uma comunicação por solicitação × finalidade (o índice único garante;
        // a checagem evita a exceção quando o gatilho re-dispara).
        var existente = await db.ComunicacoesPaciente
            .FirstOrDefaultAsync(c => c.SolicitacaoId == solicitacao.Id && c.Finalidade == finalidade, ct);
        if (existente is not null)
        {
            if (!await EstaObsoletaAsync(existente, ct)) return;

            // OBSOLETA: o aviso saiu ANTES do estudo que o exame carrega hoje — ou seja, falava de
            // OUTRO exame. Acontece depois de uma correção de identidade: o paciente recebeu o
            // resultado errado, o vínculo foi consertado e, sem isto, ele nunca seria avisado do
            // exame certo (a linha já existe, então o enfileiramento virava no-op). Foi o caso do
            // João Bento em 11/08/2026.
            await RevogarAcessosAsync(solicitacao.Id, DateTime.UtcNow, ct);
            RearmarParaNovoEnvio(existente);
            logger.LogInformation(
                "Comunicação {Id} ({Finalidade}) rearmada: o aviso anterior era de outro estudo.",
                existente.Id, finalidade);
            return;
        }

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

    public async Task<bool> AvisarCancelamentoAgoraAsync(Solicitacao solicitacao, CancellationToken ct = default)
    {
        // A chave mora no banco (menu Confirmações → Regras), não em arquivo: ligar e desligar
        // aviso ao paciente é decisão de operação e não pode depender de deploy.
        if (!(await RegrasAsync(ct)).AvisoCancelamentoHabilitado) return false;

        try
        {
            await EnfileirarAsync(solicitacao, FinalidadeComunicacao.CancelamentoAgendamento, ct);

            // Reserva: desde 25/09/2026 o enviador também manda avisos de cancelamento, e a linha
            // nasce com ProximaTentativaEm = agora. Se ele a pegasse enquanto este caminho envia,
            // sairiam duas mensagens e uma das pontas cairia em conflito de concorrência — o modal
            // diria "não avisado" a quem recebeu duas. O envio daqui não depende desse horário.
            foreach (var nova in db.ChangeTracker.Entries<ComunicacaoPaciente>()
                         .Where(e => e.State == EntityState.Added
                             && e.Entity.SolicitacaoId == solicitacao.Id
                             && e.Entity.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento))
                nova.Entity.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(10);

            await db.SaveChangesAsync(ct);

            var comunicacao = await db.ComunicacoesPaciente
                .Where(c => c.SolicitacaoId == solicitacao.Id
                    && c.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento)
                .Select(c => new { c.Id, c.Status })
                .FirstOrDefaultAsync(ct);

            // Já foi avisado antes (o motor pegou primeiro): não repete.
            if (comunicacao is null || comunicacao.Status != StatusComunicacao.Pendente) return false;

            await ProcessarTentativaEnvioAsync(comunicacao.Id, ct);

            // O que a tela diz à atendente tem de ser o que aconteceu: "avisado" só se a mensagem
            // saiu mesmo. Número sem WhatsApp, contato negado e janela fechada param aqui.
            return await db.ComunicacoesPaciente.AsNoTracking()
                .AnyAsync(c => c.Id == comunicacao.Id && c.EnviadoEm != null, ct);
        }
        catch (Exception ex)
        {
            // O cancelamento está feito nos dois sistemas; falhar o aviso é ruim, mas desfazer
            // seria pior. Fica o log e a comunicação na fila, que o worker retenta.
            logger.LogError(ex,
                "Falha ao avisar o cancelamento da solicitação {Solicitacao} na hora.", solicitacao.Id);
            return false;
        }
    }

    /// <summary>
    /// O aviso já enviado fala do estudo que o exame carrega HOJE? Se saiu antes de o exame ser
    /// dado como realizado com o vínculo atual, fala de outro — está obsoleto.
    /// <para>Só vale para aviso já ENVIADO: o que ainda está na fila sai com o link atual.</para>
    /// </summary>
    private async Task<bool> EstaObsoletaAsync(ComunicacaoPaciente c, CancellationToken ct)
    {
        if (c.EnviadoEm is not { } enviadoEm) return false;
        var realizadoEm = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.SolicitacaoId == c.SolicitacaoId && e.ExcluidoEm == null)
            .Select(e => e.RealizadoEm)
            .FirstOrDefaultAsync(ct);
        return realizadoEm is { } r && enviadoEm < r;
    }

    /// <summary>
    /// A confirmação e a régua que a reforça (reforço, orientação ao posto): falam de um
    /// agendamento que ainda vai acontecer e que o paciente ainda não respondeu. Por isso dividem
    /// as mesmas guardas — data futura, só SISREG, "já respondeu por outro canal".
    /// </summary>
    internal static bool EhDaConfirmacao(FinalidadeComunicacao f) =>
        f is FinalidadeComunicacao.ConfirmacaoAgendamento
            or FinalidadeComunicacao.ReforcoConfirmacao
            or FinalidadeComunicacao.OrientacaoPosto;

    /// <summary>
    /// Mensagem sobre a AGENDA (confirmação, lembrete e a régua de reforço): respeita a janela de
    /// horário e exige data futura. Resultado de exame e laudo não — são a resposta a algo que o
    /// paciente está esperando. O cancelamento sai na hora (quem cancela está com o paciente).
    /// </summary>
    internal static bool EhSobreAgendamento(FinalidadeComunicacao f) =>
        f is FinalidadeComunicacao.ConfirmacaoAgendamento
            or FinalidadeComunicacao.LembreteAgendamento
            or FinalidadeComunicacao.CancelamentoAgendamento
            or FinalidadeComunicacao.ReforcoConfirmacao
            or FinalidadeComunicacao.OrientacaoPosto;

    /// <summary>Por que uma comunicação não pode sair — ver <see cref="MotivoQueImpedeEnvio"/>.</summary>
    internal enum ImpedimentoEnvio
    {
        /// <summary>Solicitação excluída ou cancelada (exceto para o próprio aviso de cancelamento).</summary>
        SolicitacaoEncerrada,
        /// <summary>Mensagem sobre a agenda sem data futura.</summary>
        SemDataFutura,
        /// <summary>Lembrete de quem já avisou que não comparece.</summary>
        AvisouQueNaoVai,
        /// <summary>Confirmação de quem já respondeu por outro canal.</summary>
        JaRespondeu,
        /// <summary>Confirmação de agendamento que não veio do SISREG, com a regra "só SISREG" ligada.</summary>
        SomenteSisreg,
    }

    internal sealed record Impedimento(ImpedimentoEnvio Tipo, string Motivo);

    /// <summary>
    /// As guardas que fazem uma comunicação perder o sentido ANTES de sair — a solicitação acabou,
    /// a data passou, o paciente já respondeu, a regra "só SISREG". Ficam num lugar só porque duas
    /// pontas precisam dar a mesma resposta: o envio (<see cref="ProcessarTentativaEnvioAsync"/>),
    /// que encerra a linha com o motivo, e a liberação depois da identificação
    /// (<see cref="LiberacaoAposIdentificacao"/>), que não pode prometer "chegam em instantes" para
    /// uma mensagem que o envio vai barrar logo em seguida — foi o "confirma e não manda nada".
    /// </summary>
    /// <param name="somenteSisreg">A regra do menu Confirmações (só vale para a confirmação e a régua).</param>
    /// <param name="dataMinimaUtc">Até quando a data do agendamento conta como passada: o envio usa
    /// agora; a liberação dá uma folga (não se promete mensagem para um horário que já chegou).</param>
    internal static Impedimento? MotivoQueImpedeEnvio(
        FinalidadeComunicacao finalidade, Solicitacao? s, bool somenteSisreg, DateTime dataMinimaUtc)
    {
        // O aviso de CANCELAMENTO fala justamente de uma solicitação cancelada — sem esta
        // exceção ele se autoencerraria aqui, calado, e ninguém descobriria tão cedo.
        if (s is null || s.ExcluidoEm is not null
            || (s.Status == StatusSolicitacao.Cancelada && finalidade != FinalidadeComunicacao.CancelamentoAgendamento))
            return new(ImpedimentoEnvio.SolicitacaoEncerrada, "Solicitação excluída ou cancelada antes do envio.");

        // Mesma régua da conciliação ("só avisa o que ainda ia acontecer"), agora no envio: o aviso
        // de cancelamento que entrou na fila à noite e só pôde sair depois do horário do agendamento
        // já não é notícia — confunde quem já foi (ou não foi). Sem data, o aviso ainda sai.
        if (finalidade == FinalidadeComunicacao.CancelamentoAgendamento)
            return s.DataAgendada is { } dataCancelada && dataCancelada <= dataMinimaUtc
                ? new(ImpedimentoEnvio.SemDataFutura, "Agendamento já passou: aviso de cancelamento não enviado.")
                : null;

        if (EhSobreAgendamento(finalidade) && (s.DataAgendada is not { } dataAgendada || dataAgendada <= dataMinimaUtc))
            return new(ImpedimentoEnvio.SemDataFutura, "Exame sem data futura no momento do envio.");

        // Avisou que não vai depois de entrar na fila do lembrete: não se lembra quem já
        // respondeu que não comparece.
        if (finalidade == FinalidadeComunicacao.LembreteAgendamento
            && s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(ImpedimentoEnvio.AvisouQueNaoVai, "Paciente avisou que não poderá comparecer.");

        if (EhDaConfirmacao(finalidade) && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            return new(ImpedimentoEnvio.JaRespondeu, "Paciente já respondeu por outro canal.");

        if (EhDaConfirmacao(finalidade) && somenteSisreg && !Confirmacoes.OrigemAgendamento.EhDoSisreg(s))
            return new(ImpedimentoEnvio.SomenteSisreg,
                "Confirmação por WhatsApp está restrita a agendamentos do SISREG (menu Confirmações).");

        return null;
    }

    /// <summary>Zera a linha para um envio novo — mesmo saneamento do reenvio manual: telefone,
    /// link e recibos saem, porque todos se referem ao envio anterior.</summary>
    internal static void RearmarParaNovoEnvio(ComunicacaoPaciente n)
    {
        var agora = DateTime.UtcNow;
        n.IgnorarVerificacaoTelefone = false; // vale por envio, não para sempre
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
        n.IgnorarJanelaHorario = false;
        n.ProximaTentativaEm = agora;
        n.AtualizadoEm = agora;
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

            // As guardas que tornam o envio sem sentido (solicitação encerrada, data passada, já
            // respondeu, só SISREG) moram em MotivoQueImpedeEnvio: a liberação depois da
            // identificação aplica a MESMA régua antes de prometer "chegam em instantes".
            var somenteSisreg = s is not null && EhDaConfirmacao(n.Finalidade) && (await RegrasAsync(ct)).SomenteSisreg;
            if (MotivoQueImpedeEnvio(n.Finalidade, s, somenteSisreg, DateTime.UtcNow) is { } impedimento)
            {
                Terminal(n, StatusComunicacao.Falha, impedimento.Motivo);
            }
            else if (n.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento
                     && ((await RegrasAsync(ct)).AvisoCancelamentoLigadoEm is not { } ligadoEm
                         || n.CriadoEm < ligadoEm))
            {
                // Entrou na fila antes de o aviso ser ligado (ou num período em que esteve
                // desligado): ligar vale daqui para frente. Sem corte gravado, não arrisca.
                Terminal(n, StatusComunicacao.Falha,
                    "Aviso retroativo: entrou na fila antes de o aviso de cancelamento ser ligado.");
            }
            else if (EhSobreAgendamento(n.Finalidade)
                     && !n.IgnorarJanelaHorario
                     && await ForaDaJanelaAsync(ct) is { } abertura)
            {
                // Fora do horário (padrão 08h–18h): não é tentativa — fica EMPILHADA e sai quando a
                // janela abrir. Vale também para reenvio manual: a regra é sobre o paciente.
                n.Tentativas--;
                n.ProximaTentativaEm = abertura;
                n.MotivoFalha = null;
            }
            else if (Confirmacoes.ReguaReforcoConfirmacao.EhDaRegua(n.Finalidade)
                     && !n.IgnorarJanelaHorario
                     && Confirmacoes.ReguaReforcoConfirmacao.EhDomingo(DateTime.UtcNow))
            {
                // A régua não insiste no domingo. O que entrou na fila no sábado à noite espera a
                // abertura da janela na segunda — também sem gastar tentativa.
                n.Tentativas--;
                n.ProximaTentativaEm = Confirmacoes.ReguaReforcoConfirmacao.ProximaAberturaForaDoDomingo(
                    DateTime.UtcNow, TimeOnly.Parse((await RegrasAsync(ct)).HoraInicioEnvio));
                n.MotivoFalha = null;
            }
            else
            {
                // s não é nulo aqui: solicitação ausente é o primeiro impedimento de MotivoQueImpedeEnvio.
                await EnviarAsync(n, s!, ct);
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

    private Confirmacoes.Dtos.ConfirmacaoConfiguracaoDto? _regras;

    private async Task<Confirmacoes.Dtos.ConfirmacaoConfiguracaoDto> RegrasAsync(CancellationToken ct)
        => _regras ??= await regrasConfirmacao.ObterAsync(ct);

    /// <summary>Próxima abertura da janela (UTC) quando AGORA está fora dela; null se está dentro.</summary>
    private async Task<DateTime?> ForaDaJanelaAsync(CancellationToken ct)
    {
        var r = await RegrasAsync(ct);
        var inicio = TimeOnly.Parse(r.HoraInicioEnvio);
        var fim = TimeOnly.Parse(r.HoraFimEnvio);
        var agora = DateTime.UtcNow;
        return Confirmacoes.JanelaEnvioConfirmacao.Dentro(agora, inicio, fim)
            ? null
            : Confirmacoes.JanelaEnvioConfirmacao.ProximaAbertura(agora, inicio, fim);
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

        // 1-2. Revoga links ativos e derruba sessões de quem já clicou.
        var linksAtivos = await RevogarAcessosAsync(solicitacaoId, agora, ct);

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
        n.IgnorarJanelaHorario = false;
        n.ProximaTentativaEm = agora;
        n.AtualizadoEm = agora;
        // "Assumo o risco" era de UM envio, para UM número. Mantê-lo ligado fazia o reenvio pular
        // o desafio cadastral e mandar link de sessão para um número novo e não provado.
        n.IgnorarVerificacaoTelefone = false;

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
        var linksAtivos = await RevogarAcessosAsync(solicitacaoId, agora, ct);

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
        n.IgnorarJanelaHorario = false;
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
        FinalidadeComunicacao.LembreteAgendamento => "lembrete de agendamento",
        FinalidadeComunicacao.CancelamentoAgendamento => "aviso de cancelamento",
        FinalidadeComunicacao.ReforcoConfirmacao => "reforço da confirmação",
        FinalidadeComunicacao.OrientacaoPosto => "orientação ao posto",
        _ => f.ToString(),
    };

    /// <summary>Há pendência ABERTA de número errado para este destino? Comparação tolerante a
    /// DDI (sufixo), porque a pendência guarda o canônico da conversa ("55...") e o cadastro às
    /// vezes a forma nacional.</summary>
    private async Task<bool> NumeroTemPendenciaAbertaAsync(string telefone, CancellationToken ct)
    {
        var abertas = await db.PendenciasCadastro.AsNoTracking()
            .Where(p => p.Status == StatusPendenciaCadastro.Aberta
                && p.Tipo == TipoPendenciaCadastro.NumeroErrado)
            .Select(p => p.TelefoneCanonical)
            .ToListAsync(ct);
        return abertas.Any(t => Conversas.TelefoneWhatsApp.MesmoNumero(t, telefone));
    }

    private async Task EnviarAsync(ComunicacaoPaciente n, Solicitacao s, CancellationToken ct)
    {
        var paciente = await pacientes.ObterPorIdAsync(n.PacienteId, ct);

        // DADO CLÍNICO (imagem do exame, laudo) só vai para contato VERIFICADO: o link abre o
        // resultado, e o telefone do cadastro pode estar errado/desatualizado — foi o que
        // mandou laudo para destino desconhecido no lote de 2026-07. Confirmação de
        // agendamento continua indo para qualquer celular: não expõe resultado e é ela que
        // provoca o contato (a resposta do paciente é o que permite verificar o número).
        // Lembrete de quem JÁ confirmou fala da data e do procedimento: só vai para contato provado.
        // Lembrete de quem NÃO respondeu é a própria mensagem original repetida (decisão de
        // 20/09/2026) — e ela não expõe nada, é justamente o convite a se identificar. Para número
        // já provado (ou liberado na identificação) sai a confirmação completa (ver MontarEnvio).
        var lembreteDeQuemConfirmou = n.Finalidade == FinalidadeComunicacao.LembreteAgendamento
            && s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada;

        // CAMPANHA (ADR-0062): o local e o endereço vêm dela, não da unidade do SISREG. Com a
        // conferência desligada, a entrega é DIRETA — sem desafio e sem exigir número provado —
        // porque o que importa na campanha é a mensagem chegar. Em troca, o link não abre o app
        // (ver a geração do link abaixo).
        var campanha = n.Finalidade is FinalidadeComunicacao.ConfirmacaoAgendamento
                or FinalidadeComunicacao.LembreteAgendamento
            ? await Campanhas.CampanhaResolver.VigenteAsync(db, s.UnidadeExecutanteId, s.DataAgendada, ct)
            : null;
        var entregaDireta = campanha is { ExigirConferenciaCadastral: false };

        var exigeVerificado = n.Finalidade is FinalidadeComunicacao.ExameLiberado
            or FinalidadeComunicacao.LaudoPronto
            || (lembreteDeQuemConfirmou && !entregaDireta);

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
        // Número marcado como INVÁLIDO (quem atendeu disse que não conhece o paciente) nunca é
        // destino — nem de mensagem automática nem de reenvio. Só volta a valer quando o número é
        // verificado de novo ou a recepção dá a marcação por improcedente.
        bool Negado(string? t) => paciente.TelefoneNegado is { } neg && TelefoneWhatsApp.MesmoNumero(t, neg);
        var candidatos = new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal, paciente.TelefoneResidencial };
        var telefone = paciente.TelefoneVerificado
            ?? candidatos.FirstOrDefault(t => TelefoneWhatsApp.EhCelularBr(t) && !Negado(t));

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
        {
            if (candidatos.Any(t => TelefoneWhatsApp.EhCelularBr(t) && Negado(t)))
            {
                n.Status = StatusComunicacao.AguardandoCorrecaoContato;
                n.MotivoFalha = "Número marcado como INVÁLIDO: quem atende disse que não conhece o paciente. "
                    + "Atualize o telefone no cadastro para liberar o envio.";
                n.ProximaTentativaEm = null;
                return;
            }
            Terminal(n, StatusComunicacao.SemTelefoneValido, "Paciente sem número de celular válido.");
            // Marca no CADASTRO: o problema não é deste agendamento, é da ficha do paciente.
            await contatosComprometidos.MarcarAsync(
                n.PacienteId, null, MotivoContatoComprometido.SemCelular,
                "Nenhum celular brasileiro válido no cadastro.", n.Id, ct);
            return;
        }
        n.Telefone = TelefoneWhatsApp.NormalizarNonoDigito(telefone!);

        // O cadastro tem número utilizável: o que estiver marcado sobre um número velho (ou sobre
        // a falta de número) deixou de valer.
        await contatosComprometidos.ReconciliarAsync(n.PacienteId, n.Telefone, ct);

        // NÚMERO NEGADO: pendência aberta de "número errado" para o destino ⇒ quem atende já
        // disse que não é o paciente. Nenhum automático sai — nem o desafio cadastral (é
        // template do mesmo jeito, para a mesma pessoa errada). Fica retida; resolver/ignorar a
        // pendência solta (e o telefone é re-resolvido do cadastro, já corrigido).
        if (await NumeroTemPendenciaAbertaAsync(n.Telefone, ct))
        {
            n.Status = StatusComunicacao.AguardandoCorrecaoContato;
            n.MotivoFalha = "Número com pendência de contato errado (quem atende negou ser o "
                + "paciente). Corrija o cadastro em Pendências de Cadastro para liberar.";
            n.ProximaTentativaEm = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Régua de reforço (toques 2 e 3 da confirmação): ramo próprio, com reconferência — entre a
        // fila e a saída a pessoa pode ter respondido, alguém pode ter assumido, o cadastro mudou.
        if (Confirmacoes.ReguaReforcoConfirmacao.EhDaRegua(n.Finalidade))
        {
            await EnviarReguaAsync(n, s, paciente, ct);
            return;
        }

        // Confirmação para número NÃO verificado: em vez dos DADOS do agendamento, manda a PRIMEIRA
        // MENSAGEM — curta, sem pedir nada — e SEGURA a confirmação real (pendurada). A conversa é
        // conduzida pela MÁQUINA DE ESTADOS determinística do webhook
        // (VerificacaoCadastralWhatsAppHandler), independente do robô LLM estar ligado.
        //
        // Por que curta: a versão anterior pedia os 4 dígitos do CPF logo de cara e oferecia
        // "falar com atendente" como única alternativa — e era nele que as pessoas clicavam, em vez
        // de responder. Agora o pedido do CPF só vem depois de "Quero mais informações".
        // "Assumo o risco" (IgnorarVerificacaoTelefone) pula tudo isso.
        var repetePrimeiraMensagem = n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
            || (n.Finalidade == FinalidadeComunicacao.LembreteAgendamento
                && s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente);

        if (repetePrimeiraMensagem
            && !entregaDireta
            && !n.IgnorarVerificacaoTelefone
            && !TelefoneWhatsApp.EhCelularBr(paciente.TelefoneVerificado)
            && options.Value.VerificacaoCadastralHabilitada)
        {
            var optsDesafio = options.Value;
            var ehConsulta = n.Tipo == TipoAgendamento.Consulta;
            var modeloPrimeiraMsg = ehConsulta
                ? optsDesafio.TemplateConfirmacaoConsulta
                : optsDesafio.TemplateConfirmacaoExame;
            var tratamento = Tratamento(paciente.NomeCompleto, paciente.Sexo);
            // Conteúdo legível gravado na thread/histórico — é o que o operador vê que o cidadão recebeu.
            var textoDesafio =
                $"Olá {tratamento}, esse é o canal oficial do Alô Maricá da Secretaria Municipal de Saúde.\n\n"
                + $"{(ehConsulta ? "Sua consulta foi agendada" : "Seu exame foi agendado")}!\n"
                + "Para mais informações acesse: app.smsmarica.online";
            // Mesma conferência do resto: o modelo aprovado manda na quantidade de variáveis.
            var (paramsPrimeiraMsg, incompativelPrimeiraMsg) =
                await AjustarAoModeloAsync(modeloPrimeiraMsg, [tratamento], ct);
            if (incompativelPrimeiraMsg is not null)
            {
                Terminal(n, StatusComunicacao.Falha, incompativelPrimeiraMsg);
                return;
            }

            var desafio = await whatsApp.EnviarTemplateAsync(
                n.Telefone, modeloPrimeiraMsg, optsDesafio.Idioma, paramsPrimeiraMsg,
                pacienteId: n.PacienteId, conteudoLegivel: textoDesafio, ct: ct);

            if (desafio.Ok)
            {
                n.Status = StatusComunicacao.AguardandoVerificacaoCadastral;
                n.EnviadoEm = DateTime.UtcNow;
                // Liga a mensagem do desafio à comunicação: os recibos da Meta aparecem na fila e o
                // toque em "Não sou essa pessoa." é casado pelo contexto da resposta.
                if (desafio.WaMessageId is { } wamidDesafio)
                    n.MensagemWhatsAppId = await db.MensagensWhatsApp.AsNoTracking()
                        .Where(m => m.WaMessageId == wamidDesafio).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
                n.MotivoFalha = "Primeira mensagem entregue; aguardando o paciente se identificar "
                    + "(Quero mais informações → 4 dígitos do CPF → nascimento → nome).";
                n.ProximaTentativaEm = null;
                await AbrirEstadoVerificacaoAsync(n, ct);
                await db.SaveChangesAsync(ct);
                return;
            }
            if (ErroPermanente(desafio.Erro)) { Terminal(n, StatusComunicacao.Falha, desafio.Erro); return; }
            ReagendarOuFalhar(n, desafio.Erro);
            return;
        }

        // Magic link novo a cada tentativa (o anterior simplesmente expira sem uso). O link é
        // ancorado na ESPINHA (s.Id); o destino usa o id PÚBLICO do exame (ExameImagem.Id) para o
        // front achar o card em /exames.
        var exameIdPublico = s.ExameImagem?.Id ?? s.Id;
        // O aviso de CANCELAMENTO não leva botão de link, e a conciliação acabou de revogar os
        // acessos da solicitação: gerar um link novo aqui reabriria a porta que ela fechou — e, para
        // paciente sem CPF, a geração falha e derrubava o aviso inteiro.
        // Aviso de cancelamento mostra o agendamento inteiro só para contato PROVADO; para os
        // demais é anônimo ("sua consulta foi cancelada") e o detalhe espera a identificação.
        var contatoVerificado = TelefoneWhatsApp.EhCelularBr(paciente.TelefoneVerificado)
            && TelefoneWhatsApp.MesmoNumero(paciente.TelefoneVerificado, n.Telefone);

        var tokenLink = Guid.Empty;
        if (n.Finalidade != FinalidadeComunicacao.CancelamentoAgendamento)
        {
            // Campanha para número que não é o verificado do paciente: o link só CONFIRMA a
            // presença, não abre o app. A mensagem saiu sem conferir quem está do outro lado —
            // quem a recebeu por engano consegue, no máximo, confirmar.
            var abreSessao = campanha is null || contatoVerificado;
            var link = await loginLinks.GerarParaSolicitacaoAsync(
                exameIdPublico, Destino(n.Finalidade, exameIdPublico), ExigeCpf(n.Finalidade),
                abreSessao, ct);
            n.LoginLinkId = link.Token;
            tokenLink = link.Token;
        }

        var opts = options.Value;

        // IgnorarVerificacaoTelefone = quem está do outro lado acabou de se identificar (ou o
        // operador assumiu o risco): para o conteúdo, vale como contato provado. Sem isto, o
        // lembrete liberado na identificação saía de novo como a primeira mensagem curta — a
        // pessoa confirmava os dados e recebia "seu exame foi agendado" outra vez.
        var (template, parametros, botoes) = MontarEnvio(
            n.Finalidade, n.Tipo, s, paciente.NomeCompleto, paciente.Sexo, tokenLink, opts,
            contatoVerificado || n.IgnorarVerificacaoTelefone, campanha);

        // O modelo aprovado manda na quantidade de variáveis: mandar a mais é erro 132000 na Meta
        // e a mensagem não sai. Corta pela declaração do catálogo (cacheado) e avisa quando o
        // modelo pede MAIS do que o sistema monta — aí é o modelo que precisa de revisão.
        (parametros, var incompativel) = await AjustarAoModeloAsync(template, parametros, ct);
        if (incompativel is not null)
        {
            Terminal(n, StatusComunicacao.Falha, incompativel);
            return;
        }

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            n.Telefone, template, opts.Idioma, parametros, botoes,
            pacienteId: n.PacienteId, conteudoLegivel: ConteudoLegivel(template, opts, parametros), ct: ct);

        if (resultado.Ok)
        {
            n.Status = StatusComunicacao.Enviada;
            n.EnviadoEm = DateTime.UtcNow;
            n.MotivoFalha = null;
            n.ProximaTentativaEm = null; // recibos (entrega/leitura/falha) chegam pelo webhook
            if (resultado.WaMessageId is not null) n.MensagemWhatsAppId = await MensagemDoEnvioAsync(resultado, ct);
        }
        else
        {
            await TratarEnvioRecusadoAsync(n, resultado, ct);
        }
    }

    /// <summary>Id local da mensagem enviada (é por ela que os recibos da Meta acham a comunicação).</summary>
    private async Task<Guid?> MensagemDoEnvioAsync(EnvioWhatsAppResultado resultado, CancellationToken ct)
        => resultado.WaMessageId is { } wamid
            ? await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.WaMessageId == wamid).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct)
            : null;

    /// <summary>O que fazer quando o WhatsApp não aceitou o envio — a mesma régua para toda finalidade.</summary>
    private async Task TratarEnvioRecusadoAsync(
        ComunicacaoPaciente n, EnvioWhatsAppResultado resultado, CancellationToken ct)
    {
        // O Automais.Zap devolve "{message} (code {code})"; o formato antigo era "({code}) {message}".
        // Casar pelo codigo em ambos, senao numero inexistente volta para a fila em vez de falhar.
        if (resultado.Erro?.StartsWith(BloqueioEnvioWhatsApp.CodigoNumeroNegado, StringComparison.Ordinal) == true)
        {
            // A guarda central barrou: fica retida com o motivo, sem gastar tentativa.
            n.Status = StatusComunicacao.AguardandoCorrecaoContato;
            n.MotivoFalha = Truncar(resultado.Erro);
            n.ProximaTentativaEm = null;
            n.Tentativas--;
        }
        else if (ErroPermanente(resultado.Erro))
        {
            Terminal(n, StatusComunicacao.Falha, resultado.Erro);
            // A Meta disse que este número não recebe. É fato do cadastro, não desta mensagem:
            // sem a marca, toda solicitação futura do mesmo paciente redescobre o mesmo problema.
            await contatosComprometidos.MarcarAsync(
                n.PacienteId, n.Telefone, MotivoContatoComprometido.NaoEhWhatsApp,
                resultado.Erro, n.Id, ct);
        }
        else
        {
            ReagendarOuFalhar(n, resultado.Erro);
        }
    }

    /// <summary>
    /// Envio de uma linha da régua de reforço (reforço ou orientação ao posto). Tudo o que a
    /// colocou na fila é CONFERIDO DE NOVO — a régua é insistência, e insistência com informação
    /// velha é pior que silêncio. Chegando aqui, o telefone já foi resolvido pelo cadastro (e o
    /// negado já foi barrado); <c>n.Telefone</c> é o destino que o cadastro manda hoje.
    ///
    /// <para>O que pode acontecer, além de sair: a linha vira <see cref="StatusComunicacao.Dispensada"/>
    /// (coberta pela própria principal, que andou ou volta a sair), <see cref="StatusComunicacao.SubstituidaPorAtendente"/>
    /// (uma pessoa assumiu) ou espera o silêncio mínimo do número.</para>
    /// </summary>
    private async Task EnviarReguaAsync(
        ComunicacaoPaciente n, Solicitacao s, Pacientes.Dtos.PacienteDto paciente, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var opts = options.Value;
        var ehReforco = n.Finalidade == FinalidadeComunicacao.ReforcoConfirmacao;
        var superado = ehReforco
            ? Confirmacoes.ReguaReforcoConfirmacao.PrefixoSuperadoReforco
            : Confirmacoes.ReguaReforcoConfirmacao.PrefixoSuperadoOrientacao;

        // Uma pessoa está com a solicitação (menu Confirmações): o automático não entra por cima.
        if (await db.AtendimentosConfirmacao.AnyAsync(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null, ct))
        {
            Terminal(n, StatusComunicacao.SubstituidaPorAtendente, "Atendimento humano iniciado.");
            return;
        }

        // A principal ainda espera o paciente se identificar? É ela que a régua reforça.
        var principal = await db.ComunicacoesPaciente.FirstOrDefaultAsync(c =>
            c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        if (principal is null || principal.Status != StatusComunicacao.AguardandoVerificacaoCadastral
            || principal.Telefone is null || principal.EnviadoEm is not { } principalEnviadaEm)
        {
            Terminal(n, StatusComunicacao.Dispensada,
                $"{superado}: a primeira mensagem já não aguarda o paciente se identificar.");
            return;
        }
        if (principal.MotivoFalha?.StartsWith(Confirmacoes.ReguaReforcoConfirmacao.CarimboVaiAoPosto,
                StringComparison.Ordinal) == true)
        {
            Terminal(n, StatusComunicacao.Dispensada, $"{superado}: o paciente avisou que vai ao posto.");
            return;
        }

        // Verificou o contato por outro caminho (recepção, app, outro agendamento): não se insiste
        // em pedir identificação — libera a confirmação de verdade, com os dados do agendamento.
        if (TelefoneWhatsApp.EhCelularBr(paciente.TelefoneVerificado))
        {
            principal.Status = StatusComunicacao.Pendente;
            principal.ProximaTentativaEm = agora;
            principal.IgnorarVerificacaoTelefone = true;
            principal.Tentativas = 0;
            principal.MotivoFalha = null;
            principal.AtualizadoEm = agora;
            Terminal(n, StatusComunicacao.Dispensada,
                "O paciente verificou o contato por outro caminho: a confirmação com os dados do agendamento "
                + "foi liberada no lugar desta mensagem.");
            return;
        }

        // O telefone do cadastro mudou depois da primeira mensagem: reforçar no número velho é
        // insistir com quem talvez nem seja o paciente. A primeira mensagem volta a sair — agora
        // para o número novo — e a régua recomeça dela.
        if (!TelefoneWhatsApp.MesmoNumero(n.Telefone, principal.Telefone))
        {
            var estadoAntigo = await Confirmacoes.ReguaReforcoConfirmacao.EstadoDoNumeroAsync(db, principal.Telefone, ct);
            if (estadoAntigo is not null && estadoAntigo.ComunicacaoPacienteId == principal.Id
                && estadoAntigo.Etapa == EtapaVerificacaoCadastral.AguardandoInteresse)
                db.VerificacoesCadastraisEstado.Remove(estadoAntigo);
            RearmarParaNovoEnvio(principal);
            Terminal(n, StatusComunicacao.Dispensada,
                "O telefone do cadastro mudou depois da primeira mensagem: ela volta a sair, para o número novo.");
            return;
        }

        // O público de cada toque (ver ReguaReforcoConfirmacao): o reforço só para quem não mandou
        // nada; a orientação também para quem começou a se identificar e parou há dias.
        var houveEntrada = await Confirmacoes.ReguaReforcoConfirmacao.HouveEntradaDesdeAsync(
            db, n.Telefone!, principalEnviadaEm, ct);
        var estado = await Confirmacoes.ReguaReforcoConfirmacao.EstadoDoNumeroAsync(db, n.Telefone!, ct);
        if (ehReforco)
        {
            if (!Confirmacoes.ReguaReforcoConfirmacao.ReforcoAlcanca(houveEntrada, estado))
            {
                Terminal(n, StatusComunicacao.Dispensada,
                    $"{superado}: o número respondeu depois da primeira mensagem.");
                return;
            }
            // O lembrete ocupa o lugar do reforço — dois "toques 2" é insistência dobrada.
            if (await db.ComunicacoesPaciente.AnyAsync(c => c.SolicitacaoId == s.Id
                    && c.Finalidade == FinalidadeComunicacao.LembreteAgendamento && c.EnviadoEm != null, ct))
            {
                Terminal(n, StatusComunicacao.Dispensada, $"{superado}: o lembrete já cumpriu esse papel.");
                return;
            }
        }
        else if (!Confirmacoes.ReguaReforcoConfirmacao.OrientacaoAlcanca(
                     houveEntrada, estado, principal.Id, principal.PacienteId, agora))
        {
            Terminal(n, StatusComunicacao.Dispensada,
                $"{superado}: a conversa com o número andou depois da primeira mensagem.");
            return;
        }

        // Silêncio mínimo por NÚMERO: outro automático de agendamento saiu para ele há pouco (outro
        // paciente do mesmo número, um lote novo). Não é falha nem tentativa — espera e sai depois.
        var ultimo = await Confirmacoes.ReguaReforcoConfirmacao.UltimoAutomaticoDoNumeroAsync(db, n.Telefone!, n.Id, ct);
        if (ultimo is { } u && u > agora.AddHours(-opts.SilencioMinimoPorNumeroHoras))
        {
            n.Tentativas--;
            n.ProximaTentativaEm = u.AddHours(opts.SilencioMinimoPorNumeroHoras);
            n.MotivoFalha = "Aguardando o silêncio mínimo do número (outro aviso saiu para ele há pouco).";
            return;
        }

        // Modelo escolhido AGORA, pela leitura da principal e pelo catálogo de hoje.
        IReadOnlyList<TemplateWhatsApp> catalogo;
        try { catalogo = await whatsApp.ListarTemplatesAsync(ct); }
        catch { catalogo = []; }
        var envio = Confirmacoes.ReguaReforcoConfirmacao.MontarEnvio(
            n.Finalidade, n.Tipo, Tratamento(paciente.NomeCompleto, paciente.Sexo),
            principalLida: principal.LidoEm is not null, catalogo, opts);

        var (parametros, incompativel) = await AjustarAoModeloAsync(envio.Modelo, envio.Parametros, ct);
        if (incompativel is not null)
        {
            Terminal(n, StatusComunicacao.Falha, incompativel);
            return;
        }

        // O mesmo método da primeira mensagem: quick replies sem payload (a Meta devolve o texto do
        // botão) e origem Automático — a guarda de contato negado vale aqui também.
        var resultado = await whatsApp.EnviarTemplateAsync(
            n.Telefone!, envio.Modelo, opts.Idioma, parametros,
            pacienteId: n.PacienteId, conteudoLegivel: envio.ConteudoLegivel, ct: ct);
        if (!resultado.Ok)
        {
            await TratarEnvioRecusadoAsync(n, resultado, ct);
            return;
        }

        n.Status = StatusComunicacao.Enviada;
        n.EnviadoEm = agora;
        n.MotivoFalha = null;
        n.ProximaTentativaEm = null;
        if (resultado.WaMessageId is not null) n.MensagemWhatsAppId = await MensagemDoEnvioAsync(resultado, ct);

        // O toque reabre a porta da identificação: o estado do número ganha mais uma semana, SEM
        // mudar etapa nem contadores (quem parou no nascimento continua dali). A data de
        // atualização também fica: é por ela que se sabe há quanto tempo o diálogo está parado.
        // Sem estado, abre um esperando o "Quero mais informações", apontando para a PRINCIPAL.
        if (estado is null)
        {
            db.VerificacoesCadastraisEstado.Add(new Data.Entities.Notificacoes.VerificacaoCadastralEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = TelefoneWhatsApp.Canonizar(principal.Telefone),
                ComunicacaoPacienteId = principal.Id,
                Etapa = EtapaVerificacaoCadastral.AguardandoInteresse,
                ExpiraEm = agora.Add(Confirmacoes.ReguaReforcoConfirmacao.ValidadeEstado),
                CriadoEm = agora,
            });
        }
        else
        {
            estado.ExpiraEm = agora.Add(Confirmacoes.ReguaReforcoConfirmacao.ValidadeEstado);
        }

        // Na principal, o que aconteceu — é ela que a atendente vê na fila de Confirmações.
        principal.MotivoFalha = Confirmacoes.ReguaReforcoConfirmacao.CarimboNaPrincipal(n.Finalidade, agora);
        principal.AtualizadoEm = agora;
    }

    /// <summary>
    /// Ajusta os parâmetros ao modelo APROVADO na Meta: sobra é cortada (modelo mais curto do que
    /// o texto canônico), falta é erro — e erro que se explica, em vez de 132000 cru na fila.
    /// Catálogo indisponível (relay fora, modo simulado): segue com o que foi montado.
    /// </summary>
    private async Task<(string[] Parametros, string? Erro)> AjustarAoModeloAsync(
        string template, string[] parametros, CancellationToken ct)
    {
        IReadOnlyList<TemplateWhatsApp> catalogo;
        try { catalogo = await whatsApp.ListarTemplatesAsync(ct); }
        catch { return (parametros, null); }

        var modelo = catalogo.FirstOrDefault(t => string.Equals(t.Nome, template, StringComparison.OrdinalIgnoreCase));
        if (modelo is null) return (parametros, null); // não listado (ou catálogo vazio): tenta mesmo assim

        if (modelo.Parametros == parametros.Length) return (parametros, null);
        if (modelo.Parametros < parametros.Length) return (parametros[..modelo.Parametros], null);

        return (parametros,
            $"O modelo \"{template}\" aprovado na Meta espera {modelo.Parametros} variáveis e o "
            + $"sistema monta {parametros.Length}. Revise o modelo (ou avise quem cuida do fluxo) "
            + "antes de reenviar.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CidadaoLoginLink>> RevogarAcessosAsync(
        Guid solicitacaoId, DateTime agora, CancellationToken ct = default)
    {
        // Revoga TODOS os links ativos da solicitação, não só o da comunicação em questão:
        // qualquer link anterior pode ter ido para o número — ou para a pessoa — errada.
        // Expirar é a revogação: ninguém mais autentica com eles.
        var linksAtivos = await db.CidadaoLoginLinks
            .Where(l => l.SolicitacaoId == solicitacaoId && l.ExpiraEm > agora && l.RevogadoEm == null)
            .ToListAsync(ct);
        foreach (var l in linksAtivos)
        {
            l.ExpiraEm = agora;
            // Carimbo próprio: revogado não confirma presença nem devolve destino (o expirado
            // natural ainda confirma). Sem isto, um link cortado por engano de número continuava
            // valendo para confirmar.
            l.RevogadoEm = agora;
        }

        // Se algum link JÁ FOI USADO, derruba as sessões ativas do paciente daquele link — se quem
        // clicou foi a pessoa errada, ela perde o acesso ao app AGORA. O paciente certo reentra
        // com 1 clique no link novo.
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

        return linksAtivos;
    }

    /// <summary>Abre (ou renova) o estado da verificação cadastral determinística apontando para a
    /// comunicação PENDURADA. Um diálogo em ANDAMENTO (etapa &gt; CPF, não expirado) não é roubado —
    /// ao concluir, o handler emenda o próximo desafio pendente do telefone sozinho.</summary>
    private async Task AbrirEstadoVerificacaoAsync(ComunicacaoPaciente n, CancellationToken ct)
    {
        var telefone = Conversas.TelefoneWhatsApp.Canonizar(n.Telefone!);
        var agora = DateTime.UtcNow;
        var estado = await db.VerificacoesCadastraisEstado
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);
        if (estado is null)
        {
            db.VerificacoesCadastraisEstado.Add(new Data.Entities.Notificacoes.VerificacaoCadastralEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = telefone,
                ComunicacaoPacienteId = n.Id,
                // Nada foi pedido ainda: a primeira mensagem só avisa que há agendamento.
                Etapa = EtapaVerificacaoCadastral.AguardandoInteresse,
                ExpiraEm = agora.AddDays(7),
                CriadoEm = agora,
            });
            return;
        }
        var emAndamento = estado.Etapa is not (EtapaVerificacaoCadastral.AguardandoCpf
            or EtapaVerificacaoCadastral.AguardandoInteresse) && estado.ExpiraEm > agora;
        if (emAndamento) return;
        estado.ComunicacaoPacienteId = n.Id;
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoInteresse;
        estado.PacienteId = null;
        estado.CpfDigitosInformados = null;
        estado.TentativasErradas = 0;
        estado.Reorientacoes = 0;
        estado.ExpiraEm = agora.AddDays(7);
        estado.AtualizadoEm = agora;
    }

    private static string Destino(FinalidadeComunicacao finalidade, Guid solicitacaoId) => finalidade switch
    {
        FinalidadeComunicacao.ExameLiberado or FinalidadeComunicacao.LaudoPronto
            => $"/exames?exame={solicitacaoId}",
        _ => "/agendados/exames",
    };

    /// <summary>Quais links exigem o CPF do titular antes de virar sessão. Só os que carregam
    /// RESULTADO clínico: um link entregue à pessoa errada não pode abrir o prontuário alheio.
    /// Confirmação de agendamento fica de fora de propósito — não expõe resultado e depende de
    /// ser 1 clique.</summary>
    private static bool ExigeCpf(FinalidadeComunicacao finalidade) => finalidade
        is FinalidadeComunicacao.ExameLiberado or FinalidadeComunicacao.LaudoPronto;

    private static (string Template, string[] Parametros, BotaoTemplateWhatsApp[] Botoes) MontarEnvio(
        FinalidadeComunicacao finalidade, TipoAgendamento tipo, Solicitacao s, string? nomePaciente,
        Sexo sexo, Guid token, ComunicacaoPacienteOptions opts, bool contatoVerificado = false,
        Campanhas.CampanhaVigente? campanha = null)
    {
        var nome = PrimeiroNome(nomePaciente);
        // Nome do procedimento: exame de imagem tem TipoExame no satélite; consulta usa a
        // especialidade/procedimento em texto.
        var exame = s.ExameImagem?.TipoExame?.Nome ?? s.EspecialidadeTexto ?? s.ProcedimentoTexto ?? "exame";
        var url = new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, token.ToString());

        // CAMPANHA (ADR-0062): a confirmação — e o lembrete de quem ainda não respondeu, que é a
        // mesma mensagem repetida — sai no modelo da campanha, com o local e o endereço dela.
        // Com a conferência ligada, só chega aqui depois do desafio (número já provado).
        var mensagemDeCampanha = campanha is not null
            && (finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                || (finalidade == FinalidadeComunicacao.LembreteAgendamento
                    && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada
                    && !campanha.ExigirConferenciaCadastral));
        if (mensagemDeCampanha)
        {
            // confirmar_agendamento_urlapp (aprovado na Meta; era a confirmação até 08/07/2026):
            // "📆Olá *{{1}}*, você tem {{2}} de *{{3}}* agendado para o dia *{{4}}*, {{5}}📍, às
            // *{{6}}*. *Endereço:* {{7}} *É muito importante sua confirmação.*"
            // {{5}} leva o rótulo "local: " — o nome do local é livre e um "na"/"no" fixo erraria a
            // preposição. Botões na ordem do modelo: URL (0) e "Não poderei ir" (1, payload confirma:).
            var quando = FusoBrasilia.ParaExibicao(s.DataAgendada!.Value);
            return (
                opts.TemplateCampanha,
                [
                    nome,
                    tipo == TipoAgendamento.Consulta ? "uma consulta" : "um exame",
                    exame,
                    quando.ToString("dd/MM/yyyy", PtBr),
                    $"local: {campanha!.LocalNome}",
                    $"{quando.ToString("HH:mm", PtBr)}h",
                    campanha.LocalEndereco,
                ],
                [
                    url,
                    new BotaoTemplateWhatsApp(TipoBotaoTemplate.QuickReply, $"confirma:{s.Id}"),
                ]);
        }

        switch (finalidade)
        {
            // exame_liberado / laudo_disponivel: "seu exame de {{2}}, realizado {{3}}, está pronto…"
            case FinalidadeComunicacao.ExameLiberado:
                return (opts.TemplateExameLiberado, [nome, exame, DataRealizacao(s)], [url]);

            case FinalidadeComunicacao.LaudoPronto:
                return (opts.TemplateLaudoPronto, [nome, exame, DataRealizacao(s)], [url]);

            // agendamento_proximo: "Olá {{1}}, *{{2}}* está se aproximando! Não se esqueça que está
            // marcado dia *{{3}}*, às *{{4}}*. … Ainda está confirmado seu comparecimento?"
            //   {{1}} = "Sr. João" / "Sra. Maria"
            //   {{2}} = "Seu exame de Mamografia" / "Sua consulta de Cardiologia"
            //   {{3}} = "21/09/2026"
            //   {{4}} = "14:00h"
            // Botões (quick reply, sem payload — voltam como TEXTO): "Sim! Está confirmado!" e
            // "Não poderei ir." — tratados no ConfirmacaoAgendamentoWhatsAppHandler.
            // Modelo diferente para quem JÁ confirmou e para quem não respondeu (mesmo modelo
            // enquanto o segundo não existir).
            // agendamento_cancelado_anonimo: "Olá *{{1}}*, … Comunicamos que *{{2}}.* Para
            // maiores esclarecimentos, busque informações no posto que lhe atende."
            //   {{1}} = "Sr. João" / "Sra. Maria"
            //   {{2}} = a frase SEM ponto final — o modelo já fecha com ".*"
            // Botões "Não sou essa pessoa" e "Quero mais informações" (os mesmos da primeira
            // mensagem, tratados pelo VerificacaoCadastralWhatsAppHandler).
            case FinalidadeComunicacao.CancelamentoAgendamento:
            {
                var oQue = tipo == TipoAgendamento.Consulta ? "sua consulta" : "seu exame";
                var cancelado = tipo == TipoAgendamento.Consulta ? "foi cancelada" : "foi cancelado";

                // NÃO verificado: nada do agendamento. Nem procedimento, nem data, nem unidade —
                // quem está do outro lado pode não ser o paciente, e cancelamento é dado de saúde
                // como qualquer outro. O detalhe vem depois que ela se identificar.
                if (!contatoVerificado)
                    return (opts.TemplateCancelamento,
                        [Tratamento(nomePaciente, sexo), $"{oQue} {cancelado}"], []);

                // Verificado: o agendamento inteiro de uma vez.
                //   "sua consulta de Otorrinolaringologia marcada para o dia 22/12/2026 às 14:00h
                //    foi cancelada"
                // O MOTIVO não entra aqui e não entra em lugar nenhum que o paciente veja.
                return (opts.TemplateCancelamento,
                    [Tratamento(nomePaciente, sexo), $"{oQue} de {exame}{Marcada(s, tipo)} {cancelado}"],
                    []);
            }

            case FinalidadeComunicacao.LembreteAgendamento:
            {
                var oQue = tipo == TipoAgendamento.Consulta ? "Sua consulta" : "Seu exame";

                // NÃO respondeu ainda, para número NÃO provado: repete a mensagem ORIGINAL (curta,
                // com "Quero mais informações") — decisão de 20/09/2026. Insistir com data e hora em
                // quem nunca deu sinal não ajuda; o que falta é ela entrar na conversa.
                if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada && !contatoVerificado)
                {
                    return (
                        tipo == TipoAgendamento.Consulta
                            ? opts.TemplateConfirmacaoConsulta
                            : opts.TemplateConfirmacaoExame,
                        [Tratamento(nomePaciente, sexo)],
                        []);
                }

                // NÃO respondeu, mas o número está provado (ou a pessoa acabou de se identificar):
                // a mensagem curta não serve — ela já sabe que há agendamento e quer os dados. Vai
                // a confirmação completa, com data, guia e os botões de confirmar/não poderei ir.
                if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada)
                    return ConfirmacaoRegulacao(tipo, s, nome, exame, sexo, url, opts);

                var quando = FusoBrasilia.ParaExibicao(s.DataAgendada!.Value);
                return (
                    opts.TemplateLembreteConfirmado,
                    [
                        Tratamento(nomePaciente, sexo),
                        $"{oQue} de {exame}",
                        quando.ToString("dd/MM/yyyy", PtBr),
                        $"{quando.ToString("HH:mm", PtBr)}h",
                    ],
                    []);
            }

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
                return ConfirmacaoRegulacao(tipo, s, nome, exame, sexo, url, opts);
        }
    }

    /// <summary>A confirmação completa (<c>confirmacao_regulacao</c>): data, hora, onde retirar a
    /// guia e os botões. Usada pela confirmação e pelo lembrete de quem não respondeu mas já está
    /// com o número provado.</summary>
    private static (string Template, string[] Parametros, BotaoTemplateWhatsApp[] Botoes) ConfirmacaoRegulacao(
        TipoAgendamento tipo, Solicitacao s, string nome, string exame, Sexo sexo, BotaoTemplateWhatsApp url,
        ComunicacaoPacienteOptions opts)
    {
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

    /// <summary>
    /// Texto humano do template (variáveis preenchidas) para gravar na thread — o operador e o
    /// histórico veem O QUE o cidadão recebeu, não um marcador técnico. Só para modelos cujo corpo
    /// aprovado na Meta conhecemos; os demais mantêm o marcador <c>[template:...]</c>.
    /// </summary>
    private static string? ConteudoLegivel(string template, ComunicacaoPacienteOptions opts, IReadOnlyList<string> p)
    {
        if (template == opts.TemplateCampanha && p.Count == 7)
            // Corpo do confirmar_agendamento_urlapp (modelo da campanha, ADR-0062), com {{1}}..{{7}}.
            return $"📆Olá *{p[0]}*, você tem {p[1]} de *{p[2]}* agendado para o dia *{p[3]}*, {p[4]}📍, "
                + $"às *{p[5]}*.\n\n*Endereço:* {p[6]}\n\n*É muito importante sua confirmação.* 😊";
        if (template == opts.TemplateConfirmaAgendamento && p.Count == 7)
            // Corpo aprovado do confirmacao_regulacao (Complexo Regulador), com {{1}}..{{7}}.
            return $"Bom dia, {p[0]}. Este é o canal do *Alô Maricá* do Complexo Regulador do Município! "
                + $"Boas notícias! {p[1]} de {p[2]} *foi agendada para o dia {p[3]}*. Pedimos, por gentileza, "
                + $"que retire a guia no posto de saúde onde {p[4]}. Na sua guia constam todos os dados "
                + "necessários para a realização do procedimento: dia, hora, local e endereço da unidade "
                + $"executante. No dia {p[5]} é imprescindível que {p[6]} leve também o pedido médico, guia "
                + "do SISREG, comprovante de residência e cartão do SUS. Favor confirmar o seu comparecimento "
                + "clicando no link abaixo. Favor não enviar áudio. Atenciosamente, _*Complexo Regulador de Maricá*_";
        return null;
    }

    /// <summary>
    /// " marcada para o dia 22/12/2026 às 14:00h" — o trecho que situa o agendamento cancelado.
    /// Sem data no cadastro, devolve vazio: dizer "marcada para o dia" sem dia é pior que não dizer.
    /// </summary>
    private static string Marcada(Solicitacao s, TipoAgendamento tipo)
    {
        if (s.DataAgendada is not { } quando) return string.Empty;
        var local = FusoBrasilia.ParaExibicao(quando);
        var flexao = tipo == TipoAgendamento.Consulta ? "marcada" : "marcado";
        return $" {flexao} para o dia {local.ToString("dd/MM/yyyy", PtBr)} "
               + $"às {local.ToString("HH:mm", PtBr)}h";
    }

    /// <summary>"Sr. João" / "Sra. Maria" / só o primeiro nome quando o sexo não está no cadastro.</summary>
    private static string Tratamento(string? nomeCompleto, Sexo sexo)
    {
        var nome = PrimeiroNome(nomeCompleto);
        return sexo switch
        {
            Sexo.Masculino => $"Sr. {nome}",
            Sexo.Feminino => $"Sra. {nome}",
            _ => nome,
        };
    }

    private static string PrimeiroNome(string? nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "Paciente" : PtBr.TextInfo.ToTitleCase(partes[0].ToLowerInvariant());
    }

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
