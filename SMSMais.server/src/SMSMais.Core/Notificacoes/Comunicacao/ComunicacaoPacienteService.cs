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
        // Confirmação só faz sentido antes do atendimento; as demais finalidades valem sempre.
        if (finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
            && (solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow))
            return;

        // Por enquanto só se confirma agendamento do SISREG (regra do menu Confirmações): o
        // cadastrado à mão na recepção não gera mensagem.
        if (finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
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

    /// <summary>Zera a linha para um envio novo — mesmo saneamento do reenvio manual: telefone,
    /// link e recibos saem, porque todos se referem ao envio anterior.</summary>
    private static void RearmarParaNovoEnvio(ComunicacaoPaciente n)
    {
        var agora = DateTime.UtcNow;
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
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && (await RegrasAsync(ct)).SomenteSisreg
                     && !Confirmacoes.OrigemAgendamento.EhDoSisreg(s))
            {
                Terminal(n, StatusComunicacao.Falha,
                    "Confirmação por WhatsApp está restrita a agendamentos do SISREG (menu Confirmações).");
            }
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && !n.IgnorarJanelaHorario
                     && await ForaDaJanelaAsync(ct) is { } abertura)
            {
                // Fora do horário (padrão 08h–18h): não é tentativa — fica EMPILHADA e sai quando a
                // janela abrir. Vale também para reenvio manual: a regra é sobre o paciente.
                n.Tentativas--;
                n.ProximaTentativaEm = abertura;
                n.MotivoFalha = null;
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
            return;
        }
        n.Telefone = TelefoneWhatsApp.NormalizarNonoDigito(telefone!);

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

        // Confirmação para número NÃO verificado: em vez de mandar os DADOS do agendamento, manda
        // o DESAFIO cadastral (validacao_cadastro) e SEGURA a confirmação real (pendurada). A
        // resposta é conduzida pela MÁQUINA DE ESTADOS determinística do webhook
        // (VerificacaoCadastralWhatsAppHandler: dígitos → nascimento → nome) — independente do
        // robô LLM estar ligado. "Assumo o risco" (IgnorarVerificacaoTelefone) pula o desafio.
        if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
            && !n.IgnorarVerificacaoTelefone
            && !TelefoneWhatsApp.EhCelularBr(paciente.TelefoneVerificado)
            && options.Value.VerificacaoCadastralHabilitada)
        {
            var optsDesafio = options.Value;
            var procedimento = s.ExameImagem?.TipoExame?.Nome ?? s.EspecialidadeTexto ?? s.ProcedimentoTexto ?? "seu atendimento";
            // Conteúdo legível gravado na thread/histórico: deixa CLARO que se pedem os 4 dígitos.
            var textoDesafio =
                $"Olá {PrimeiroNome(paciente.NomeCompleto)}! Este é o canal oficial da saúde. Temos uma "
                + $"informação sobre *{procedimento}*. Para sua segurança, confirme apenas os *4 primeiros "
                + "dígitos do CPF* do paciente para prosseguir.";
            var desafio = await whatsApp.EnviarTemplateAsync(
                n.Telefone, optsDesafio.TemplateValidacaoCadastro, optsDesafio.Idioma,
                [PrimeiroNome(paciente.NomeCompleto), procedimento],
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
                n.MotivoFalha = "Aguardando verificação cadastral (dígitos do CPF + nascimento + nome).";
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
        var link = await loginLinks.GerarParaSolicitacaoAsync(
            exameIdPublico, Destino(n.Finalidade, exameIdPublico), ExigeCpf(n.Finalidade), ct);
        n.LoginLinkId = link.Token;

        var opts = options.Value;
        var (template, parametros, botoes) = MontarEnvio(
            n.Finalidade, n.Tipo, s, paciente.NomeCompleto, paciente.Sexo, link.Token, opts);

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            n.Telefone, template, opts.Idioma, parametros, botoes,
            pacienteId: n.PacienteId, conteudoLegivel: ConteudoLegivel(template, opts, parametros), ct: ct);

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
        // O Automais.Zap devolve "{message} (code {code})"; o formato antigo era "({code}) {message}".
        // Casar pelo codigo em ambos, senao numero inexistente volta para a fila em vez de falhar.
        else if (ErroPermanente(resultado.Erro))
        {
            Terminal(n, StatusComunicacao.Falha, resultado.Erro);
        }
        else
        {
            ReagendarOuFalhar(n, resultado.Erro);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CidadaoLoginLink>> RevogarAcessosAsync(
        Guid solicitacaoId, DateTime agora, CancellationToken ct = default)
    {
        // Revoga TODOS os links ativos da solicitação, não só o da comunicação em questão:
        // qualquer link anterior pode ter ido para o número — ou para a pessoa — errada.
        // Expirar é a revogação: ninguém mais autentica com eles.
        var linksAtivos = await db.CidadaoLoginLinks
            .Where(l => l.SolicitacaoId == solicitacaoId && l.ExpiraEm > agora)
            .ToListAsync(ct);
        foreach (var l in linksAtivos) l.ExpiraEm = agora;

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
                Etapa = EtapaVerificacaoCadastral.AguardandoCpf,
                ExpiraEm = agora.AddDays(7),
                CriadoEm = agora,
            });
            return;
        }
        var emAndamento = estado.Etapa != EtapaVerificacaoCadastral.AguardandoCpf && estado.ExpiraEm > agora;
        if (emAndamento) return;
        estado.ComunicacaoPacienteId = n.Id;
        estado.Etapa = EtapaVerificacaoCadastral.AguardandoCpf;
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

    /// <summary>
    /// Texto humano do template (variáveis preenchidas) para gravar na thread — o operador e o
    /// histórico veem O QUE o cidadão recebeu, não um marcador técnico. Só para modelos cujo corpo
    /// aprovado na Meta conhecemos; os demais mantêm o marcador <c>[template:...]</c>.
    /// </summary>
    private static string? ConteudoLegivel(string template, ComunicacaoPacienteOptions opts, IReadOnlyList<string> p)
    {
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

    private static string PrimeiroNome(string? nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "Paciente" : PtBr.TextInfo.ToTitleCase(partes[0].ToLowerInvariant());
    }

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
