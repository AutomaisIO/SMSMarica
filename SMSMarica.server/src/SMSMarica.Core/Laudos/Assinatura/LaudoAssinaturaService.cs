using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Assinatura.Dtos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Core.Medicos.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Laudos.Assinatura;

public sealed class LaudoAssinaturaService(
    SmsMaricaDbContext db,
    ILaudoPdfRenderer pdf,
    IAssinadorPdfPades assinador,
    IPractitionerFhirClient practitionerFhir,
    ILogger<LaudoAssinaturaService> logger,
    IOptions<AssinaturaOptions> options) : ILaudoAssinaturaService
{
    private readonly AssinaturaOptions _opt = options.Value;

    // ---------------- Fluxo do médico ----------------

    public async Task<IniciarAssinaturaResultado> IniciarAsync(
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

        var existentes = await db.LaudoAssinaturas
            .Where(a => a.LaudoId == laudoId)
            .ToListAsync(cancellationToken);

        if (existentes.Any(a => a.Status == StatusAssinatura.Concluida))
            throw new ConflitoException("assinatura.ja_assinado", "Este laudo já foi assinado digitalmente.");

        // Housekeeping: jobs pendentes com chave já vencida viram Cancelada (não reaproveita
        // estado morto e dá uso ao enum Cancelada, em vez de acumular linhas órfãs).
        var agora = DateTime.UtcNow;
        foreach (var morto in existentes.Where(a =>
            a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura
            && a.ChaveExpiraEm is not null && a.ChaveExpiraEm < agora))
        {
            morto.Status = StatusAssinatura.Cancelada;
            morto.AtualizadoEm = agora;
            LimparTransitorios(morto);
        }

        var chave = GerarChave();
        var expira = agora.AddMinutes(_opt.ChaveExpiraMinutos);

        // Reutiliza um job pendente AINDA VÁLIDO (re-clicou "Assinar") com chave nova.
        var pendente = existentes.FirstOrDefault(a =>
            a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura);
        if (pendente is not null)
        {
            pendente.ChaveAgente = HashChave(chave);
            pendente.ChaveExpiraEm = expira;
            pendente.AtualizadoEm = agora;
            await db.SaveChangesAsync(cancellationToken);
            return new IniciarAssinaturaResultado(pendente.Id, chave);
        }

        var job = new LaudoAssinatura
        {
            Id = Guid.CreateVersion7(),
            LaudoId = laudoId,
            MedicoId = medico.Id,
            Status = StatusAssinatura.Iniciada,
            ChaveAgente = HashChave(chave),
            ChaveExpiraEm = expira,
            AssinadoPorUsuarioId = usuarioId,
            CriadoEm = agora,
        };
        db.LaudoAssinaturas.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Assinatura: job {JobId} iniciado (laudo {LaudoId}, médico {MedicoId}, usuário {UsuarioId}). Aguardando o agente.",
            job.Id, laudoId, medico.Id, usuarioId);
        return new IniciarAssinaturaResultado(job.Id, chave);
    }

    public async Task<AssinaturaStatusDto> ObterStatusAsync(Guid laudoId, CancellationToken cancellationToken = default)
    {
        var a = await db.LaudoAssinaturas.AsNoTracking()
            .Where(x => x.LaudoId == laudoId)
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        return a is null
            ? new AssinaturaStatusDto(null, "NaoIniciada", null, null, null)
            : new AssinaturaStatusDto(a.Id, a.Status.ToString(), a.AssinadoEm, a.CertificadoTitular, a.Formato);
    }

    public async Task<PdfDownloadDto> ObterPdfParaDownloadAsync(Guid laudoId, CancellationToken cancellationToken = default)
    {
        var assinado = await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.Concluida && a.PdfAssinado != null)
            .Select(a => a.PdfAssinado)
            .FirstOrDefaultAsync(cancellationToken);

        if (assinado is not null)
            return new PdfDownloadDto(assinado, true);

        var bytes = await pdf.GerarAsync(laudoId, incluirTarja: true, cancellationToken);
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

        var laudo = await db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == job.LaudoId && !l.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), job.LaudoId);

        var pdfSemTarja = await pdf.GerarAsync(job.LaudoId, incluirTarja: false, cancellationToken);
        var visual = new DadosVisualAssinatura(
            laudo.MedicoNomeSnapshot ?? string.Empty,
            laudo.MedicoCrmSnapshot ?? string.Empty,
            laudo.MedicoUfCrmSnapshot ?? string.Empty,
            laudo.MedicoRqeSnapshot,
            _opt.TextoCarimbo);

        PreparacaoAssinatura prep;
        try
        {
            prep = await assinador.PrepararAsync(pdfSemTarja, cadeiaCertificado, visual, cancellationToken);
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
            "Assinatura: job {JobId} preparado (laudo {LaudoId}, {QtdCerts} cert(s) na cadeia, thumbprint {Thumbprint}, hash {Algo}). Aguardando assinatura do agente.",
            job.Id, job.LaudoId, cadeiaCertificado.Count, job.CertThumbprint, prep.AlgoritmoHash);
        return new PrepararJobResultadoDto(Convert.ToBase64String(prep.ToSignHash), prep.AlgoritmoHash);
    }

    public async Task ConcluirAsync(string chave, byte[] rawSignature, CancellationToken cancellationToken = default)
    {
        var job = await BuscarPorChaveAsync(chave, cancellationToken);
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
        job.Status = StatusAssinatura.Concluida;
        LimparTransitorios(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assinatura: job {JobId} CONCLUÍDO (laudo {LaudoId}, formato {Formato}, titular '{Titular}', cpf {Cpf}, pdf {Bytes} bytes).",
            job.Id, job.LaudoId, resultado.Formato, resultado.CertificadoTitular, MascararCpf(cpfCert), resultado.PdfAssinado.Length);
    }

    // ---------------- helpers ----------------

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
    }

    private static string GerarChave() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

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

    /// <summary>Mascara CPF para log (LGPD): "123******89" — mantém prefixo/sufixo p/ diagnóstico.</summary>
    private static string MascararCpf(string? cpf)
    {
        var d = SoDigitos(cpf);
        if (d.Length != 11) return string.IsNullOrEmpty(d) ? "(vazio)" : $"({d.Length} dígitos)";
        return $"{d[..3]}******{d[9..]}";
    }
}
