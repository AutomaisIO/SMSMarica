using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.Laudos.Assinatura.Dtos;
using SMSMais.Core.Laudos.Assinatura.Nuvem;
using SMSMais.Core.Laudos.Pdf;
using SMSMais.Core.Laudos.Verificacao;
using SMSMais.Core.Medicos;
using SMSMais.Core.Medicos.Dtos;
using SMSMais.Core.Medicos.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos.Assinatura;

public sealed class LaudoAssinaturaService(
    SmsMaisDbContext db,
    ILaudoPdfRenderer pdf,
    IAssinadorPdfPades assinador,
    IPractitionerFhirClient practitionerFhir,
    Medicos.Assinatura.IAssinaturaMedicoService assinaturaMedico,
    Medicos.IMedicosService medicos,
    Associacoes.IExameAssociacaoService associacao,
    ICarimboAssinaturaRenderer carimboRenderer,
    // Lazy: quebra o ciclo Assinatura → Comunicacao → LoginLink → Solicitacoes → Assinatura.
    Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> comunicacoes,
    ILaudoVerificacaoService verificacao,
    IIntegraIcpClient integraIcp,
    ICadeiaIcpBrasil cadeiaIcp,
    IProtetorSegredos protetor,
    IConfiguration configuration,
    ILogger<LaudoAssinaturaService> logger,
    IOptions<AssinaturaOptions> options,
    IOptions<IntegraIcpOptions> nuvemOptions) : ILaudoAssinaturaService
{
    private readonly AssinaturaOptions _opt = options.Value;
    private readonly IntegraIcpOptions _nuvem = nuvemOptions.Value;

    /// <summary>
    /// Formato gravado no job quando o laudo sai só com carimbo, sem ICP-Brasil (ADR-0061).
    /// É o que distingue, depois de Concluida, "assinado digitalmente" de "carimbado".
    /// </summary>
    public const string FormatoCarimboSemCertificado = "CARIMBO_SEM_ICP";

    public static bool EhCarimboSemCertificado(string? formato) =>
        string.Equals(formato, FormatoCarimboSemCertificado, StringComparison.Ordinal);

    // ---------------- Fluxo do médico ----------------

    public async Task<byte[]> ObterPdfBaseAsync(
        Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var laudo = await db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == laudoId && !l.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), laudoId);

        if (laudo.Status != StatusLaudo.Finalizado)
            throw new ConflitoException("assinatura.so_finalizado", "Apenas laudos finalizados podem ser assinados.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("assinatura.nao_e_autor", "Apenas o médico autor pode assinar o laudo.");

        // Mesmo layout que será assinado (sem tarja/marca d'água, com o selo de verificação)
        // — a médica posiciona o carimbo sobre ele (ADR-0049/0061).
        var modo = (await assinaturaMedico.ObterModoAsync(medico.Id, cancellationToken)).Modo;
        return await GerarPdfOficialAsync(laudoId, modo, cancellationToken);
    }

    public async Task<IniciarAssinaturaResultado> IniciarAsync(
        Guid laudoId, Guid usuarioId, CarimboPosicaoDto? posicao, CancellationToken cancellationToken = default)
    {
        ValidarPosicao(posicao);

        var laudo = await db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == laudoId && !l.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), laudoId);

        if (laudo.Status != StatusLaudo.Finalizado)
            throw new ConflitoException("assinatura.so_finalizado", "Apenas laudos finalizados podem ser assinados.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("assinatura.nao_e_autor", "Apenas o médico autor pode assinar o laudo.");

        // Regra DURA (não-configurável): assinar exige o exame associado a um pedido — sem
        // isso não há paciente confiável. Isso impede laudo assinado órfão, mesmo que a
        // configuração permita INICIAR o laudo sem associação.
        if (await associacao.ResolverVinculoAsync(laudo.StudyInstanceUID, cancellationToken) is null)
            throw new ConflitoException("assinatura.sem_associacao",
                "Associe o exame a um pedido antes de assinar o laudo.");

        var existentes = await db.LaudoAssinaturas
            .Where(a => a.LaudoId == laudoId)
            .ToListAsync(cancellationToken);

        if (existentes.Any(a => a.Status == StatusAssinatura.Concluida))
            throw new ConflitoException("assinatura.ja_assinado", "Este laudo já foi assinado digitalmente.");

        // Assinatura feita e aguardando a CONFERÊNCIA do médico: não abre outro job —
        // o painel mostra o documento para aprovar ou rejeitar.
        if (existentes.Any(a => a.Status == StatusAssinatura.AguardandoAprovacao))
            throw new ConflitoException("assinatura.aguardando_aprovacao",
                "Este laudo já está assinado e aguardando a sua conferência — aprove ou rejeite o documento.");

        // GATE fail-closed: sem rubrica cadastrada não há carimbo para estampar —
        // recusa cedo (antes de criar o job). Quem cadastra a rubrica é o administrador.
        await GarantirMedicoTemRubricaAsync(medico.Id, cancellationToken);

        // Modo do médico (ADR-0061), gravado no job: trocar no cadastro não muda job em curso.
        var modo = (await assinaturaMedico.ObterModoAsync(medico.Id, cancellationToken)).Modo;
        if (modo == ModoAssinaturaMedico.Nuvem && !_nuvem.Habilitado)
            throw new ConflitoException("assinatura.nuvem_desligada",
                "A assinatura em nuvem não está configurada nesta instância. Peça ao administrador para " +
                "configurar o canal IntegraICP ou trocar o modo de assinatura do médico.");

        // Housekeeping: jobs pendentes com chave já vencida viram Cancelada (não reaproveita
        // estado morto e dá uso ao enum Cancelada, em vez de acumular linhas órfãs). Pendente
        // de OUTRO modo também é cancelado: o médico trocou de caminho.
        var agora = DateTime.UtcNow;
        foreach (var morto in existentes.Where(a =>
            a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura
            && ((a.ChaveExpiraEm is not null && a.ChaveExpiraEm < agora)
                || (a.Modo ?? ModoAssinaturaMedico.Desktop) != modo)))
        {
            morto.Status = StatusAssinatura.Cancelada;
            morto.AtualizadoEm = agora;
            LimparTransitorios(morto);
        }

        // PDF-base FIXADO (ADR-0049): renderiza UMA vez o layout exato que será assinado
        // e guarda no job. O "preparar" reutiliza esses bytes em vez de re-renderizar,
        // eliminando drift de paginação entre o que a médica posicionou e o que é assinado.
        // Leva o selo de verificação (QR) no rodapé — ADR-0061.
        var pdfBase = await GerarPdfOficialAsync(laudoId, modo, cancellationToken);
        var pdfBaseHash = SHA256.HashData(pdfBase);

        // Reutiliza um job pendente AINDA VÁLIDO do mesmo modo (re-clicou "Assinar").
        var job = existentes.FirstOrDefault(a =>
            a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura);
        if (job is null)
        {
            job = new LaudoAssinatura
            {
                Id = Guid.CreateVersion7(),
                LaudoId = laudoId,
                MedicoId = medico.Id,
                Status = StatusAssinatura.Iniciada,
                AssinadoPorUsuarioId = usuarioId,
                CriadoEm = agora,
            };
            db.LaudoAssinaturas.Add(job);
        }
        else
        {
            // Volta a Iniciada: a posição e o PDF-base mudaram, o hash antigo não vale mais.
            job.Status = StatusAssinatura.Iniciada;
            job.TransferState = null;
            job.HashParaAssinar = null;
            job.AtualizadoEm = agora;
        }
        job.Modo = modo;
        AplicarBaseEPosicao(job, pdfBase, pdfBaseHash, posicao);

        switch (modo)
        {
            case ModoAssinaturaMedico.SemCertificado:
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Assinatura: job {JobId} iniciado SEM CERTIFICADO (laudo {LaudoId}, médico {MedicoId}, usuário {UsuarioId}). Carimbando.",
                    job.Id, laudoId, medico.Id, usuarioId);
                await CarimbarSemCertificadoAsync(job, cancellationToken);
                return new IniciarAssinaturaResultado(job.Id, null, modo);

            case ModoAssinaturaMedico.Nuvem:
                var url = await AbrirAutorizacaoNuvemAsync(job, agora, cancellationToken);
                return new IniciarAssinaturaResultado(job.Id, null, modo, url);

            default:
                var chave = GerarChave();
                job.ChaveAgente = HashChave(chave);
                job.ChaveExpiraEm = agora.AddMinutes(_opt.ChaveExpiraMinutos);
                job.NuvemStateHash = null;
                job.NuvemCodeVerifier = null;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Assinatura: job {JobId} iniciado (laudo {LaudoId}, médico {MedicoId}, usuário {UsuarioId}). Aguardando o agente.",
                    job.Id, laudoId, medico.Id, usuarioId);
                return new IniciarAssinaturaResultado(job.Id, chave, modo);
        }
    }

    public async Task<AssinaturaStatusDto> ObterStatusAsync(Guid laudoId, CancellationToken cancellationToken = default)
    {
        var a = await db.LaudoAssinaturas.AsNoTracking()
            .Where(x => x.LaudoId == laudoId)
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        return a is null
            ? new AssinaturaStatusDto(null, "NaoIniciada", null, null, null)
            : new AssinaturaStatusDto(a.Id, StatusVisivel(a).ToString(), a.AssinadoEm, a.CertificadoTitular, a.Formato, a.Modo);
    }

    /// <summary>
    /// Job pendente com a janela vencida (agente que não veio, aprovação no app que não
    /// aconteceu) aparece como Falhou: o painel volta a oferecer "Tentar de novo" em vez de
    /// esperar para sempre. O próximo "iniciar" o cancela de fato (housekeeping).
    /// </summary>
    private static StatusAssinatura StatusVisivel(LaudoAssinatura a) =>
        a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura
        && a.ChaveExpiraEm is not null && a.ChaveExpiraEm < DateTime.UtcNow
            ? StatusAssinatura.Falhou
            : a.Status;

    public async Task<PdfDownloadDto> ObterPdfParaDownloadAsync(Guid laudoId, CancellationToken cancellationToken = default)
    {
        var assinado = await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.Concluida && a.PdfAssinado != null)
            .Select(a => a.PdfAssinado)
            .FirstOrDefaultAsync(cancellationToken);

        if (assinado is not null)
            return new PdfDownloadDto(assinado, true);

        // Default = FinalizadoNaoAssinado; o renderer rebaixa para Rascunho (marca
        // d'água) sozinho quando o laudo ainda não está finalizado.
        var bytes = await pdf.GerarAsync(laudoId, cancellationToken: cancellationToken);
        return new PdfDownloadDto(bytes, false);
    }

    public async Task<bool> EstaAssinadoAsync(Guid laudoId, CancellationToken cancellationToken = default) =>
        await db.LaudoAssinaturas.AsNoTracking()
            .AnyAsync(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.Concluida, cancellationToken);

    public async Task<IReadOnlySet<Guid>> QuaisAssinadosAsync(
        IReadOnlyCollection<Guid> laudoIds, CancellationToken cancellationToken = default)
    {
        if (laudoIds is null || laudoIds.Count == 0) return new HashSet<Guid>();
        var assinados = await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => laudoIds.Contains(a.LaudoId) && a.Status == StatusAssinatura.Concluida)
            .Select(a => a.LaudoId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return assinados.ToHashSet();
    }

    // ---------------- Fluxo do agente (autenticado pela chave) ----------------

    public async Task<ReivindicarResultado> ReivindicarAsync(string chave, CancellationToken cancellationToken = default)
    {
        var job = await BuscarPorChaveAsync(chave, cancellationToken);
        if (job.Status is not (StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura))
            throw new ConflitoException("assinatura.job_estado_invalido", "Job não está aguardando assinatura.");

        var titulo = await db.Laudos.AsNoTracking()
            .Where(l => l.Id == job.LaudoId)
            .Select(l => l.Titulo)
            .FirstOrDefaultAsync(cancellationToken) ?? "Laudo";

        var cpf = await ResolverCpfMedicoAsync(job.MedicoId, cancellationToken);
        var cpfDigits = SoDigitos(cpf) is { Length: 11 } d ? d : null;
        logger.LogInformation(
            "Assinatura: job {JobId} reivindicado pelo agente (laudo {LaudoId}, médico {MedicoId}, cpfAutor {CpfAutor}).",
            job.Id, job.LaudoId, job.MedicoId, MascararCpf(cpfDigits));
        return new ReivindicarResultado(job.Id, cpfDigits, titulo);
    }

    public async Task<PrepararJobResultadoDto> PrepararAsync(
        string chave, IReadOnlyList<byte[]> cadeiaCertificado, CancellationToken cancellationToken = default)
    {
        if (cadeiaCertificado is null || cadeiaCertificado.Count == 0)
            throw new ValidacaoException("assinatura.sem_certificado", "Cadeia de certificado não informada.");

        var job = await BuscarPorChaveAsync(chave, cancellationToken);
        if (job.Status is not (StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura))
            throw new ConflitoException("assinatura.job_estado_invalido", "Job não está aguardando preparação.");

        var prep = await PrepararJobAsync(job, cadeiaCertificado, cancellationToken);
        return new PrepararJobResultadoDto(Convert.ToBase64String(prep.ToSignHash), prep.AlgoritmoHash);
    }

    /// <summary>
    /// Prepara o PAdES do job com a cadeia do signatário (agente ou nuvem): carimbo + posição
    /// no PDF-base fixado → hash a assinar. Deixa o job em AguardandoAssinatura.
    /// </summary>
    private async Task<PreparacaoAssinatura> PrepararJobAsync(
        LaudoAssinatura job, IReadOnlyList<byte[]> cadeiaCertificado, CancellationToken cancellationToken)
    {
        var laudo = await db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == job.LaudoId && !l.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), job.LaudoId);

        // GATE fail-closed (defesa em profundidade — espelha o IniciarAsync): sem
        // rubrica não há carimbo. Marca o job como falho e recusa.
        var rubrica = await assinaturaMedico.ObterAsync(job.MedicoId, cancellationToken);
        if (rubrica is null)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            throw new ConflitoException("assinatura.medico_sem_rubrica", MensagemSemRubrica);
        }

        // Reutiliza o PDF-base FIXADO no "iniciar" (ADR-0049) — os bytes exatos que a
        // médica posicionou. Só re-renderiza no fallback (jobs antigos sem base fixada).
        var pdfBase = job.PdfBaseFixado
            ?? await pdf.GerarAsync(job.LaudoId, ModoRodapeLaudo.PreparandoAssinatura, cancellationToken);

        var carimboPng = await MontarCarimboAsync(laudo, rubrica, assinaturaDigital: true, cancellationToken);
        var visual = new DadosVisualAssinatura(
            laudo.MedicoNomeSnapshot ?? string.Empty,
            laudo.MedicoCrmSnapshot ?? string.Empty,
            laudo.MedicoUfCrmSnapshot ?? string.Empty,
            laudo.MedicoRqeSnapshot,
            _opt.TextoCarimbo,
            CarimboPngBase64: Convert.ToBase64String(carimboPng),
            Posicao: PosicaoDoJob(job));

        PreparacaoAssinatura prep;
        try
        {
            prep = await assinador.PrepararAsync(pdfBase, cadeiaCertificado, visual, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Assinatura: job {JobId} falhou ao preparar no Automais.Assinador.", job.Id);
            await MarcarFalhaAsync(job, cancellationToken);
            throw;
        }

        job.TransferState = prep.TransferState;
        job.HashParaAssinar = prep.ToSignHash;
        job.CertThumbprint = ThumbprintDe(cadeiaCertificado[0]);
        job.Status = StatusAssinatura.AguardandoAssinatura;
        job.EntregueEm = DateTime.UtcNow;
        job.AtualizadoEm = job.EntregueEm;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assinatura: job {JobId} preparado (laudo {LaudoId}, modo {Modo}, {QtdCerts} cert(s) na cadeia, thumbprint {Thumbprint}, hash {Algo}).",
            job.Id, job.LaudoId, job.Modo, cadeiaCertificado.Count, job.CertThumbprint, prep.AlgoritmoHash);
        return prep;
    }

    public async Task ConcluirAsync(string chave, byte[] rawSignature, CancellationToken cancellationToken = default)
    {
        var job = await BuscarPorChaveAsync(chave, cancellationToken);
        await ConcluirJobAsync(job, rawSignature, cancellationToken);
    }

    /// <summary>
    /// Embute a assinatura crua no PAdES preparado, aplica a trava de autoria (CPF do
    /// certificado == CPF do autor) e deixa o job aguardando a conferência do médico.
    /// Comum ao agente (Desktop) e à nuvem.
    /// </summary>
    private async Task ConcluirJobAsync(LaudoAssinatura job, byte[] rawSignature, CancellationToken cancellationToken)
    {
        if (job.Status != StatusAssinatura.AguardandoAssinatura || job.TransferState is null)
            throw new ConflitoException("assinatura.job_estado_invalido", "Job não está aguardando assinatura.");
        if (rawSignature is null || rawSignature.Length == 0)
            throw new ValidacaoException("assinatura.sem_assinatura", "Assinatura crua não informada.");

        // Perdedor de corrida (duplo-clique/retry): se o laudo já foi assinado, sai cedo
        // sem pagar a montagem PAdES (chamada cara ao Automais.Assinador).
        if (await EstaAssinadoAsync(job.LaudoId, cancellationToken))
            throw new ConflitoException("assinatura.ja_assinado", "Este laudo já foi assinado digitalmente.");

        // Revalida o invariante do laudo no fechamento (simetria com Iniciar/Preparar).
        var laudoValido = await db.Laudos.AsNoTracking()
            .AnyAsync(l => l.Id == job.LaudoId && !l.Excluido && l.Status == StatusLaudo.Finalizado, cancellationToken);
        if (!laudoValido)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            throw new ConflitoException("assinatura.laudo_estado_invalido",
                "O laudo não está mais finalizado/disponível para assinatura.");
        }

        // Falha do assinador encerra a janela: marca Falhou e invalida a chave (não
        // deixa a chave válida para novas tentativas dentro do TTL).
        ResultadoAssinatura resultado;
        try
        {
            resultado = await assinador.ConcluirAsync(job.TransferState, rawSignature, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Assinatura: job {JobId} falhou ao concluir no Automais.Assinador.", job.Id);
            await MarcarFalhaAsync(job, cancellationToken);
            throw;
        }

        // Defesa em profundidade (FAIL-CLOSED): só conclui se o CPF do certificado
        // bater exatamente com o CPF do médico autor. Sem CPF de um lado = recusa.
        var cpfAutor = SoDigitos(await ResolverCpfMedicoAsync(job.MedicoId, cancellationToken));
        var cpfCert = SoDigitos(resultado.CpfTitular);
        logger.LogInformation(
            "Assinatura: job {JobId} validando autoria — cpfAutor {CpfAutor}, cpfCert {CpfCert}, titular '{Titular}', emissor '{Emissor}'.",
            job.Id, MascararCpf(cpfAutor), MascararCpf(cpfCert), resultado.CertificadoTitular, resultado.CertificadoEmissor);

        if (cpfAutor.Length != 11)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            logger.LogWarning("Assinatura: job {JobId} RECUSADO — médico autor sem CPF resolvível no FHIR.", job.Id);
            throw new ConflitoException("assinatura.autor_sem_cpf",
                "O médico autor não tem CPF resolvível no hub FHIR; não é possível validar a assinatura.");
        }
        if (cpfCert.Length != 11)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            logger.LogWarning("Assinatura: job {JobId} RECUSADO — CPF não extraído do certificado (titular '{Titular}').", job.Id, resultado.CertificadoTitular);
            throw new ValidacaoException("assinatura.cert_sem_cpf",
                "Não foi possível extrair o CPF (ICP-Brasil) do certificado de assinatura.");
        }
        if (cpfAutor != cpfCert)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            logger.LogWarning("Assinatura: job {JobId} RECUSADO — CPF do certificado ({CpfCert}) diverge do autor ({CpfAutor}).", job.Id, MascararCpf(cpfCert), MascararCpf(cpfAutor));
            throw new ValidacaoException("assinatura.cpf_diverge",
                "O CPF do certificado não corresponde ao médico autor do laudo.");
        }

        job.PdfAssinado = resultado.PdfAssinado;
        job.PdfHashSha256 = SHA256.HashData(resultado.PdfAssinado);
        job.AssinadoPorCpf = cpfCert;
        job.CertificadoTitular = resultado.CertificadoTitular;
        job.CertificadoEmissor = resultado.CertificadoEmissor;
        job.Formato = resultado.Formato;
        job.AssinadoEm = DateTime.UtcNow;
        job.AtualizadoEm = job.AssinadoEm;
        // Assinado criptograficamente, mas AINDA NÃO oficial: o médico confere o PDF
        // (carimbo/conteúdo) no painel e APROVA — só então vira Concluida e o aviso ao
        // paciente é enfileirado (AprovarAsync). Rejeitar cancela e libera re-assinar.
        job.Status = StatusAssinatura.AguardandoAprovacao;
        LimparTransitorios(job);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assinatura: job {JobId} assinado, AGUARDANDO APROVAÇÃO do médico (laudo {LaudoId}, formato {Formato}, titular '{Titular}', cpf {Cpf}, pdf {Bytes} bytes).",
            job.Id, job.LaudoId, resultado.Formato, resultado.CertificadoTitular, MascararCpf(cpfCert), resultado.PdfAssinado.Length);
    }

    public async Task<byte[]> ObterPdfAprovacaoAsync(Guid laudoId, CancellationToken cancellationToken = default)
    {
        var pdf = await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.AguardandoAprovacao && a.PdfAssinado != null)
            .OrderByDescending(a => a.AssinadoEm)
            .Select(a => a.PdfAssinado)
            .FirstOrDefaultAsync(cancellationToken);
        return pdf ?? throw new NaoEncontradoException("Assinatura aguardando aprovação", laudoId);
    }

    public async Task AprovarAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var job = await db.LaudoAssinaturas
            .FirstOrDefaultAsync(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.AguardandoAprovacao, cancellationToken)
            ?? throw new NaoEncontradoException("Assinatura aguardando aprovação", laudoId);

        job.Status = StatusAssinatura.Concluida;
        job.AtualizadoEm = DateTime.UtcNow;

        // Agora sim o laudo é oficialmente assinado → aviso "Laudo pronto" ao paciente.
        // Best-effort: falha aqui nunca impede a aprovação.
        try
        {
            await EnfileirarLaudoProntoAsync(job.LaudoId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enfileirar aviso de laudo pronto (laudo {LaudoId}).", job.LaudoId);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assinatura: job {JobId} APROVADO pelo usuário {UsuarioId} (laudo {LaudoId}) — laudo oficialmente assinado.",
            job.Id, usuarioId, job.LaudoId);
    }

    public async Task RejeitarAsync(Guid laudoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var job = await db.LaudoAssinaturas
            .FirstOrDefaultAsync(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.AguardandoAprovacao, cancellationToken)
            ?? throw new NaoEncontradoException("Assinatura aguardando aprovação", laudoId);

        // Cancelada (não excluída): o PDF assinado permanece na linha para auditoria.
        // O slot único é só de Concluida — o médico pode assinar de novo em seguida.
        job.Status = StatusAssinatura.Cancelada;
        job.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assinatura: job {JobId} REJEITADO na conferência pelo usuário {UsuarioId} (laudo {LaudoId}) — liberado para nova assinatura.",
            job.Id, usuarioId, job.LaudoId);
    }

    /// <summary>Resolve a solicitação pelo StudyInstanceUID do laudo (direto ou via associação)
    /// e enfileira a comunicação LaudoPronto. Sem solicitação amarrada → no-op.</summary>
    private async Task EnfileirarLaudoProntoAsync(Guid laudoId, CancellationToken ct)
    {
        var studyUid = await db.Laudos.AsNoTracking()
            .Where(l => l.Id == laudoId)
            .Select(l => l.StudyInstanceUID)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(studyUid)) return;

        var solicitacao = await db.ExamesImagem.Include(s => s.Solicitacao)
            .FirstOrDefaultAsync(s => s.StudyInstanceUID == studyUid && s.ExcluidoEm == null, ct);
        if (solicitacao is null)
        {
            var solicitacaoId = await db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.StudyInstanceUID == studyUid && a.ExcluidoEm == null)
                .Select(a => (Guid?)a.ExameImagemId)
                .FirstOrDefaultAsync(ct);
            if (solicitacaoId is { } sid)
                solicitacao = await db.ExamesImagem.Include(s => s.Solicitacao)
                    .FirstOrDefaultAsync(s => s.Id == sid && s.ExcluidoEm == null, ct);
        }
        if (solicitacao?.Solicitacao is null) return;

        await comunicacoes.Value.EnfileirarAsync(
            solicitacao.Solicitacao, FinalidadeComunicacao.LaudoPronto, ct);
    }

    // ---------------- Fluxo em nuvem (IntegraICP, ADR-0061) ----------------

    /// <summary>
    /// Abre a autorização na IntegraICP para o CPF do autor e devolve a URL que o médico abre.
    /// PKCE: o <c>code_verifier</c> fica cifrado no job; o <c>state</c> vai na URL de retorno e
    /// só o hash dele é guardado — é ele que amarra o retorno a este job.
    /// </summary>
    private async Task<string> AbrirAutorizacaoNuvemAsync(LaudoAssinatura job, DateTime agora, CancellationToken ct)
    {
        var cpf = SoDigitos(await ResolverCpfMedicoAsync(job.MedicoId, ct));
        if (cpf.Length != 11)
            throw new ConflitoException("assinatura.autor_sem_cpf",
                "O médico autor não tem CPF resolvível no hub FHIR; a assinatura em nuvem precisa do CPF.");

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = GerarChave();

        job.ChaveAgente = null;
        job.ChaveExpiraEm = agora.AddMinutes(_nuvem.JanelaAutorizacaoMinutos);
        job.NuvemStateHash = HashChave(state);
        job.NuvemCodeVerifier = protetor.Proteger(verifier);
        await db.SaveChangesAsync(ct);

        string url;
        try
        {
            url = await integraIcp.IniciarAutorizacaoAsync(cpf, MontarUrlRetornoNuvem(state), challenge, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Assinatura: job {JobId} não conseguiu abrir a autorização em nuvem.", job.Id);
            await MarcarFalhaAsync(job, ct);
            throw;
        }

        logger.LogInformation(
            "Assinatura: job {JobId} iniciado EM NUVEM (laudo {LaudoId}, médico {MedicoId}, cpf {Cpf}). Aguardando aprovação no app.",
            job.Id, job.LaudoId, job.MedicoId, MascararCpf(cpf));
        return url;
    }

    public async Task ConcluirNuvemAsync(string state, string credencialId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(credencialId))
            throw new ValidacaoException("assinatura.retorno_invalido", "Retorno da autorização incompleto.");

        var hash = HashChave(state);
        var job = await db.LaudoAssinaturas.FirstOrDefaultAsync(a => a.NuvemStateHash == hash, cancellationToken)
            ?? throw new ValidacaoException("assinatura.retorno_invalido",
                "Esta autorização não corresponde a nenhuma assinatura em andamento (já usada ou expirada).");

        if (job.Status != StatusAssinatura.Iniciada || job.Modo != ModoAssinaturaMedico.Nuvem
            || job.NuvemCodeVerifier is null)
            throw new ConflitoException("assinatura.job_estado_invalido", "Esta assinatura não está aguardando autorização.");
        if (job.ChaveExpiraEm is null || job.ChaveExpiraEm < DateTime.UtcNow)
        {
            await MarcarFalhaAsync(job, cancellationToken);
            throw new ConflitoException("assinatura.autorizacao_expirada",
                "A autorização demorou demais e expirou. Clique em Assinar de novo no painel.");
        }

        var verifier = protetor.Revelar(job.NuvemCodeVerifier);
        try
        {
            var certificado = await integraIcp.ObterCertificadoAsync(credencialId, verifier, cancellationToken);
            var cadeia = await cadeiaIcp.MontarAsync(certificado, cancellationToken);
            var prep = await PrepararJobAsync(job, cadeia, cancellationToken);
            var raw = await integraIcp.AssinarHashAsync(credencialId, verifier, prep.ToSignHash, cancellationToken);

            // O RAW da IntegraICP nunca foi exercitado com certificado real (a doc só diz que
            // "o hash é assinado diretamente"). O CMS do iText precisa de RSASSA-PKCS1-v1_5
            // sobre DigestInfo(SHA-256) — o mesmo que o agente faz. Confere ANTES de embutir:
            // se o provedor fizer outra coisa, falha aqui em vez de gerar um PDF que abre mas
            // é criptograficamente inválido.
            using var cert = X509CertificateLoader.LoadCertificate(certificado);
            using var rsa = cert.GetRSAPublicKey();
            if (rsa is null || !rsa.VerifyHash(prep.ToSignHash, raw, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                logger.LogError(
                    "Assinatura: job {JobId} RECUSADO — a assinatura RAW da nuvem não confere como PKCS#1 v1.5/SHA-256 ({Bytes} bytes).",
                    job.Id, raw.Length);
                throw new ConflitoException("assinatura.nuvem_raw_incompativel",
                    "O provedor devolveu uma assinatura em formato incompatível. Nada foi gravado. Avise o suporte.");
            }

            await ConcluirJobAsync(job, raw, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Qualquer etapa que falhe deixa o job morto: o médico recomeça pelo painel.
            // (As travas de CPF do ConcluirJobAsync já marcam Falhou antes de lançar.)
            logger.LogWarning(ex, "Assinatura: job {JobId} falhou no fluxo em nuvem.", job.Id);
            if (job.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura)
                await MarcarFalhaAsync(job, cancellationToken);
            throw;
        }
    }

    private string MontarUrlRetornoNuvem(string state)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(_nuvem.UrlPublicaApi)
                ? configuration["Publico:BaseUrl"] ?? "https://api.smsmarica.online"
                : _nuvem.UrlPublicaApi).TrimEnd('/');
        return $"{baseUrl}/assinatura/nuvem/retorno?state={Uri.EscapeDataString(state)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---------------- Sem certificado (ADR-0061) ----------------

    /// <summary>
    /// Médico sem certificado: estampa o carimbo ("Emitido em", não "Assinado em") no PDF-base
    /// fixado, sem criptografia, e deixa o documento aguardando a conferência — a partir daí
    /// o fluxo é o mesmo dos outros modos (aprovar oficializa e avisa o paciente).
    /// </summary>
    private async Task CarimbarSemCertificadoAsync(LaudoAssinatura job, CancellationToken ct)
    {
        try
        {
            var laudo = await db.Laudos.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == job.LaudoId && !l.Excluido, ct)
                ?? throw new NaoEncontradoException(nameof(Laudo), job.LaudoId);
            var rubrica = await assinaturaMedico.ObterAsync(job.MedicoId, ct)
                ?? throw new ConflitoException("assinatura.medico_sem_rubrica", MensagemSemRubrica);
            var pdfBase = job.PdfBaseFixado
                ?? throw new ConflitoException("assinatura.job_estado_invalido", "PDF-base não fixado.");

            var carimbo = await MontarCarimboAsync(laudo, rubrica, assinaturaDigital: false, ct);
            // Sem certificado não há o que assinar: o carimbo é desenhado aqui mesmo, sobre o
            // PDF-base fixado, sem passar pelo Automais.Assinador.
            var carimbado = CarimboPdf.Estampar(pdfBase, carimbo, PosicaoDoJob(job));

            job.PdfAssinado = carimbado;
            job.PdfHashSha256 = SHA256.HashData(carimbado);
            job.Formato = FormatoCarimboSemCertificado;
            job.AssinadoPorCpf = null;
            job.CertificadoTitular = null;
            job.CertificadoEmissor = null;
            job.AssinadoEm = DateTime.UtcNow;
            job.AtualizadoEm = job.AssinadoEm;
            job.Status = StatusAssinatura.AguardandoAprovacao;
            LimparTransitorios(job);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Assinatura: job {JobId} CARIMBADO sem certificado, AGUARDANDO APROVAÇÃO (laudo {LaudoId}, pdf {Bytes} bytes).",
                job.Id, job.LaudoId, carimbado.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Assinatura: job {JobId} falhou ao carimbar sem certificado.", job.Id);
            await MarcarFalhaAsync(job, ct);
            throw;
        }
    }

    // ---------------- helpers ----------------

    /// <summary>PDF-base oficial com o selo (QR) — a frase do rodapé depende do modo.</summary>
    private async Task<byte[]> GerarPdfOficialAsync(Guid laudoId, ModoAssinaturaMedico modo, CancellationToken ct)
    {
        var selo = await verificacao.ObterSeloAsync(
            laudoId, assinaturaDigital: modo != ModoAssinaturaMedico.SemCertificado, ct);
        return await pdf.GerarOficialAsync(laudoId, selo, ct);
    }

    /// <summary>Compõe o carimbo (rubrica + nome/CRM/RQE + data) em PNG.</summary>
    private async Task<byte[]> MontarCarimboAsync(
        Laudo laudo, Medicos.Assinatura.AssinaturaMedicoDto rubrica, bool assinaturaDigital, CancellationToken ct)
    {
        var rqe = laudo.MedicoRqeSnapshot;

        // Laudos finalizados ANTES do médico ter RQE cadastrado têm o snapshot nulo.
        // Como o signatário é o próprio autor, busca o RQE atual do médico como fallback
        // (não falsifica nada — é a credencial vigente). Falha não bloqueia a assinatura.
        if (string.IsNullOrWhiteSpace(rqe))
        {
            try { rqe = (await medicos.ObterPorIdAsync(laudo.MedicoId, ct)).Rqe; }
            catch (Exception ex) { logger.LogWarning(ex, "Não foi possível resolver o RQE atual do médico {Medico}.", laudo.MedicoId); }
        }

        return carimboRenderer.Renderizar(new CarimboDados(
            Rubrica: DecodificarImagem(rubrica.ImagemBase64),
            Formato: rubrica.Formato,
            Nome: laudo.MedicoNomeSnapshot ?? string.Empty,
            Crm: laudo.MedicoCrmSnapshot ?? string.Empty,
            UfCrm: laudo.MedicoUfCrmSnapshot ?? string.Empty,
            Rqe: rqe,
            // A assinatura criptográfica acontece segundos depois da preparação; este é o
            // instante exibido no carimbo.
            DataAssinatura: FusoBrasilia.ParaExibicao(DateTime.UtcNow),
            AssinaturaDigital: assinaturaDigital));
    }

    /// <summary>Posição escolhida pela médica no "iniciar" (ADR-0049); nula = padrão legado.</summary>
    private static CarimboPosicaoPdf? PosicaoDoJob(LaudoAssinatura job) =>
        job.CarimboPagina is { } pag
        && job.CarimboX is { } px && job.CarimboY is { } py
        && job.CarimboLargura is { } pw && job.CarimboAltura is { } ph
            ? new CarimboPosicaoPdf(pag, px, py, pw, ph)
            : null;

    internal const string MensagemSemRubrica =
        "O médico não possui rubrica de assinatura cadastrada. Solicite ao administrador " +
        "o cadastro da imagem da assinatura (formato 2:1 ou 1:1) antes de assinar.";

    /// <summary>Gate fail-closed: lança se o médico não tiver rubrica ativa cadastrada.</summary>
    private async Task GarantirMedicoTemRubricaAsync(Guid medicoId, CancellationToken ct)
    {
        var rubrica = await assinaturaMedico.ObterAsync(medicoId, ct);
        if (rubrica is null)
            throw new ConflitoException("assinatura.medico_sem_rubrica", MensagemSemRubrica);
    }

    /// <summary>Resolve o job pela chave de uso único, validando existência e expiração.</summary>
    private async Task<LaudoAssinatura> BuscarPorChaveAsync(string chave, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ValidacaoException("assinatura.chave_invalida", "Chave não informada.");

        // A chave é guardada apenas como hash (defesa: vazamento de dump/backup não
        // permite reivindicar jobs). O cliente sempre envia o valor em claro.
        var hash = HashChave(chave);
        var job = await db.LaudoAssinaturas.FirstOrDefaultAsync(a => a.ChaveAgente == hash, ct);
        if (job is null || job.ChaveExpiraEm is null || job.ChaveExpiraEm < DateTime.UtcNow)
            throw new ValidacaoException("assinatura.chave_invalida", "Chave inválida ou expirada.");

        return job;
    }

    /// <summary>Hash (SHA-256 hex) da chave de uso único para armazenamento/lookup. Entropia alta ⇒ sem salt.</summary>
    private static string HashChave(string chave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)));

    /// <summary>Marca o job como falho, limpa transitórios e persiste (fail-closed).</summary>
    private async Task MarcarFalhaAsync(LaudoAssinatura job, CancellationToken ct)
    {
        job.Status = StatusAssinatura.Falhou;
        job.AtualizadoEm = DateTime.UtcNow;
        LimparTransitorios(job);
        await db.SaveChangesAsync(ct);
    }

    private static void LimparTransitorios(LaudoAssinatura job)
    {
        job.TransferState = null;
        job.HashParaAssinar = null;
        job.ChaveAgente = null;
        job.ChaveExpiraEm = null;
        job.NuvemStateHash = null;
        job.NuvemCodeVerifier = null;
        // PDF-base fixado é pesado (bytea) e só serve entre iniciar→preparar; some ao
        // concluir/cancelar/falhar. A posição (carimbo_*) e o hash ficam para auditoria.
        job.PdfBaseFixado = null;
    }

    private static string GerarChave() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    /// <summary>
    /// Valida grosseiramente a posição do carimbo (ADR-0049): página ≥ 1 e retângulo em
    /// pontos PDF dentro de limites sãos (A4 ≈ 595×842pt). O clamp fino aos limites reais
    /// da página acontece no Automais.Assinador, que conhece o tamanho de cada página.
    /// </summary>
    private static void ValidarPosicao(CarimboPosicaoDto? p)
    {
        if (p is null) return;
        const double maxPt = 1000d, minLado = 10d;
        if (p.Pagina < 1
            || p.Largura < minLado || p.Altura < minLado
            || p.Largura > maxPt || p.Altura > maxPt
            || p.X < 0 || p.Y < 0 || p.X > maxPt || p.Y > maxPt)
        {
            throw new ValidacaoException("assinatura.posicao_invalida",
                "Posição do carimbo fora dos limites da página.");
        }
    }

    /// <summary>Grava o PDF-base fixado + hash + a posição escolhida no job (ADR-0049).</summary>
    private static void AplicarBaseEPosicao(
        LaudoAssinatura job, byte[] pdfBase, byte[] pdfBaseHash, CarimboPosicaoDto? posicao)
    {
        job.PdfBaseFixado = pdfBase;
        job.PdfBaseHash = pdfBaseHash;
        job.CarimboPagina = posicao?.Pagina;
        job.CarimboX = posicao?.X;
        job.CarimboY = posicao?.Y;
        job.CarimboLargura = posicao?.Largura;
        job.CarimboAltura = posicao?.Altura;
    }

    /// <summary>Resolve o médico (Practitioner) do usuário logado — espelha LaudosService.</summary>
    private async Task<MedicoDto> ResolverMedicoAsync(Guid usuarioId, CancellationToken ct)
    {
        var p = await practitionerFhir.ObterAsync(usuarioId, ct);
        if (p is null)
        {
            var cpf = await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Cpf)
                .FirstOrDefaultAsync(ct);
            var cpfDigits = SoDigitos(cpf);
            if (cpfDigits.Length == 11)
            {
                var bundle = await practitionerFhir.BuscarAsync(identifier: cpfDigits, ct: ct);
                p = bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Practitioner>().FirstOrDefault();
            }
        }

        if (p is null)
            throw new ConflitoException("assinatura.usuario_sem_papel_medico",
                "Apenas usuários médicos (Practitioner no hub FHIR) podem assinar laudos.");

        return MedicoFhirMapper.ParaDto(p);
    }

    private async Task<string?> ResolverCpfMedicoAsync(Guid medicoId, CancellationToken ct)
    {
        var p = await practitionerFhir.ObterAsync(medicoId, ct);
        return p is null ? null : MedicoFhirMapper.ParaDto(p).Cpf;
    }

    private static string ThumbprintDe(byte[] certDer)
    {
        using var cert = X509CertificateLoader.LoadCertificate(certDer);
        return cert.Thumbprint;
    }

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    /// <summary>Decodifica a rubrica (data URL "data:image/png;base64,..." ou base64 puro) em bytes.</summary>
    private static byte[] DecodificarImagem(string valor)
    {
        var dados = valor;
        var virgula = valor.IndexOf(',');
        if (valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && virgula > 0)
            dados = valor[(virgula + 1)..];
        return Convert.FromBase64String(dados);
    }

    /// <summary>Mascara CPF para log (LGPD): "123******89" — mantém prefixo/sufixo p/ diagnóstico.</summary>
    private static string MascararCpf(string? cpf)
    {
        var d = SoDigitos(cpf);
        if (d.Length != 11) return string.IsNullOrEmpty(d) ? "(vazio)" : $"({d.Length} dígitos)";
        return $"{d[..3]}******{d[9..]}";
    }
}
