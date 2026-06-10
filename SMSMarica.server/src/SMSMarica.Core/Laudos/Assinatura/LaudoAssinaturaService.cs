using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
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

        var chave = GerarChave();
        var expira = DateTime.UtcNow.AddMinutes(_opt.ChaveExpiraMinutos);

        // Reutiliza um job pendente (re-clicou "Assinar") com chave nova.
        var pendente = existentes.FirstOrDefault(a =>
            a.Status is StatusAssinatura.Iniciada or StatusAssinatura.AguardandoAssinatura);
        if (pendente is not null)
        {
            pendente.ChaveAgente = chave;
            pendente.ChaveExpiraEm = expira;
            pendente.AtualizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new IniciarAssinaturaResultado(pendente.Id, chave);
        }

        var job = new LaudoAssinatura
        {
            Id = Guid.CreateVersion7(),
            LaudoId = laudoId,
            MedicoId = medico.Id,
            Status = StatusAssinatura.Iniciada,
            ChaveAgente = chave,
            ChaveExpiraEm = expira,
            AssinadoPorUsuarioId = usuarioId,
            CriadoEm = DateTime.UtcNow,
        };
        db.LaudoAssinaturas.Add(job);
        await db.SaveChangesAsync(cancellationToken);
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
        return new ReivindicarResultado(job.Id, SoDigitos(cpf) is { Length: 11 } d ? d : null, titulo);
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

        var prep = await assinador.PrepararAsync(pdfSemTarja, cadeiaCertificado, visual, cancellationToken);

        job.TransferState = prep.TransferState;
        job.HashParaAssinar = prep.ToSignHash;
        job.CertThumbprint = ThumbprintDe(cadeiaCertificado[0]);
        job.Status = StatusAssinatura.AguardandoAssinatura;
        job.EntregueEm = DateTime.UtcNow;
        job.AtualizadoEm = job.EntregueEm;
        await db.SaveChangesAsync(cancellationToken);

        return new PrepararJobResultadoDto(Convert.ToBase64String(prep.ToSignHash), prep.AlgoritmoHash);
    }

    public async Task ConcluirAsync(string chave, byte[] rawSignature, CancellationToken cancellationToken = default)
    {
        var job = await BuscarPorChaveAsync(chave, cancellationToken);
        if (job.Status != StatusAssinatura.AguardandoAssinatura || job.TransferState is null)
            throw new ConflitoException("assinatura.job_estado_invalido", "Job não está aguardando assinatura.");
        if (rawSignature is null || rawSignature.Length == 0)
            throw new ValidacaoException("assinatura.sem_assinatura", "Assinatura crua não informada.");

        var resultado = await assinador.ConcluirAsync(job.TransferState, rawSignature, cancellationToken);

        // Defesa em profundidade: o CPF do certificado precisa bater com o médico autor.
        var cpfAutor = await ResolverCpfMedicoAsync(job.MedicoId, cancellationToken);
        var cpfCert = SoDigitos(resultado.CpfTitular);
        if (!string.IsNullOrEmpty(cpfAutor) && cpfCert.Length == 11 && SoDigitos(cpfAutor) != cpfCert)
        {
            job.Status = StatusAssinatura.Falhou;
            LimparTransitorios(job);
            await db.SaveChangesAsync(cancellationToken);
            throw new ValidacaoException("assinatura.cpf_diverge",
                "O CPF do certificado não corresponde ao médico autor do laudo.");
        }

        job.PdfAssinado = resultado.PdfAssinado;
        job.PdfHashSha256 = SHA256.HashData(resultado.PdfAssinado);
        job.AssinadoPorCpf = cpfCert.Length == 11 ? cpfCert : null;
        job.CertificadoTitular = resultado.CertificadoTitular;
        job.CertificadoEmissor = resultado.CertificadoEmissor;
        job.Formato = resultado.Formato;
        job.AssinadoEm = DateTime.UtcNow;
        job.AtualizadoEm = job.AssinadoEm;
        job.Status = StatusAssinatura.Concluida;
        LimparTransitorios(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- helpers ----------------

    /// <summary>Resolve o job pela chave de uso único, validando existência e expiração.</summary>
    private async Task<LaudoAssinatura> BuscarPorChaveAsync(string chave, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ValidacaoException("assinatura.chave_invalida", "Chave não informada.");

        var job = await db.LaudoAssinaturas.FirstOrDefaultAsync(a => a.ChaveAgente == chave, ct);
        if (job is null || job.ChaveExpiraEm is null || job.ChaveExpiraEm < DateTime.UtcNow)
            throw new ValidacaoException("assinatura.chave_invalida", "Chave inválida ou expirada.");

        return job;
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
}
