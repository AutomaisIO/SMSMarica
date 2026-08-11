using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

public sealed record IncidenteIdentidadeDto(
    Guid Id,
    string StudyInstanceUID,
    Guid? ExameImagemId,
    string? PacienteSuspeitoNome,
    string Motivo,
    StatusIncidenteIdentidade Status,
    bool Automatico,
    DateTime CriadoEm,
    string? ResolucaoNota);

public sealed record AbrirIncidenteRequest(string StudyInstanceUID, string Motivo);

/// <summary>
/// Quarentena de exame com identidade suspeita. Ver <see cref="ExameIncidenteIdentidade"/>.
///
/// <para>O ponto desta camada é o <see cref="EmQuarentenaAsync"/>: é ele que os gates de laudo,
/// de envio ao paciente e de conciliação consultam. Abrir o alarme é barato e reversível
/// (descartar como falso alarme); o custo de deixar um exame trocado circular não é.</para>
/// </summary>
public interface IQuarentenaIdentidadeService
{
    /// <summary>Há incidente ABERTO para o estudo? É o gate que os demais serviços consultam.</summary>
    Task<bool> EmQuarentenaAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>Dos estudos informados, quais estão em quarentena (consulta em lote, para listagens).</summary>
    Task<IReadOnlySet<string>> EmQuarentenaAsync(
        IReadOnlyCollection<string> studyInstanceUIDs, CancellationToken cancellationToken = default);

    /// <summary>Congela o exame. Idempotente: um segundo alarme sobre o mesmo estudo devolve o
    /// incidente que já está aberto em vez de duplicar a fila.</summary>
    Task<IncidenteIdentidadeDto> AbrirAsync(
        AbrirIncidenteRequest request, bool automatico = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidenteIdentidadeDto>> ListarAsync(
        StatusIncidenteIdentidade? status, CancellationToken cancellationToken = default);

    /// <summary>Falso alarme: levanta a quarentena sem corrigir nada.</summary>
    Task DescartarAsync(Guid id, string nota, CancellationToken cancellationToken = default);

    /// <summary>Fecha o incidente do estudo como resolvido — chamado pela correção de identidade.
    /// Não lança se não houver incidente aberto (nem toda correção começa por um alarme).</summary>
    Task ResolverPorEstudoAsync(string studyInstanceUID, string nota, CancellationToken cancellationToken = default);
}

public sealed class QuarentenaIdentidadeService(
    SmsMaricaDbContext db,
    Pacientes.Fhir.IPacienteResolver pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<QuarentenaIdentidadeService> logger) : IQuarentenaIdentidadeService
{
    public async Task<bool> EmQuarentenaAsync(string studyInstanceUID, CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace(studyInstanceUID)
        && await db.ExameIncidentesIdentidade.AsNoTracking().AnyAsync(
            i => i.StudyInstanceUID == studyInstanceUID && i.Status == StatusIncidenteIdentidade.Aberto,
            cancellationToken);

    public async Task<IReadOnlySet<string>> EmQuarentenaAsync(
        IReadOnlyCollection<string> studyInstanceUIDs, CancellationToken cancellationToken = default)
    {
        if (studyInstanceUIDs.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var achados = await db.ExameIncidentesIdentidade.AsNoTracking()
            .Where(i => studyInstanceUIDs.Contains(i.StudyInstanceUID)
                        && i.Status == StatusIncidenteIdentidade.Aberto)
            .Select(i => i.StudyInstanceUID)
            .ToListAsync(cancellationToken);
        return achados.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IncidenteIdentidadeDto> AbrirAsync(
        AbrirIncidenteRequest request, bool automatico = false, CancellationToken cancellationToken = default)
    {
        var uid = (request.StudyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0)
            throw new ValidacaoException("incidente.study_obrigatorio", "StudyInstanceUID é obrigatório.");
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 5)
            throw new ValidacaoException("incidente.motivo_obrigatorio",
                "Descreva o que levou a desconfiar — sem isso, quem for corrigir não sabe o que conferir.");

        var aberto = await db.ExameIncidentesIdentidade
            .FirstOrDefaultAsync(i => i.StudyInstanceUID == uid && i.Status == StatusIncidenteIdentidade.Aberto,
                cancellationToken);
        if (aberto is not null) return await MontarAsync(aberto, cancellationToken);

        // Snapshot de a quem o estudo pertencia no momento do alarme — depois da correção, o
        // vínculo muda e essa informação some.
        var exame = await db.ExamesImagem.AsNoTracking().Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.StudyInstanceUID == uid && e.ExcluidoEm == null, cancellationToken);
        Guid? exameId = exame?.Id;
        Guid? pacienteId = exame?.Solicitacao?.PacienteId;
        if (exame is null)
        {
            var assoc = await db.ExameAssociacoes.AsNoTracking()
                .FirstOrDefaultAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, cancellationToken);
            exameId = assoc?.ExameImagemId;
            pacienteId = assoc?.PacienteId;
        }

        var incidente = new ExameIncidenteIdentidade
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            ExameImagemId = exameId,
            PacienteSuspeitoId = pacienteId,
            Motivo = motivo,
            Status = StatusIncidenteIdentidade.Aberto,
            Automatico = automatico,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.ExameIncidentesIdentidade.Add(incidente);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Exame em QUARENTENA por suspeita de identidade — estudo {Uid}{Auto}. Motivo: {Motivo}",
            uid, automatico ? " (detector automático)" : string.Empty, motivo);

        return await MontarAsync(incidente, cancellationToken);
    }

    public async Task<IReadOnlyList<IncidenteIdentidadeDto>> ListarAsync(
        StatusIncidenteIdentidade? status, CancellationToken cancellationToken = default)
    {
        var q = db.ExameIncidentesIdentidade.AsNoTracking();
        if (status is { } s) q = q.Where(i => i.Status == s);
        var linhas = await q.OrderByDescending(i => i.CriadoEm).Take(200).ToListAsync(cancellationToken);

        var lista = new List<IncidenteIdentidadeDto>(linhas.Count);
        foreach (var i in linhas) lista.Add(await MontarAsync(i, cancellationToken));
        return lista;
    }

    public async Task DescartarAsync(Guid id, string nota, CancellationToken cancellationToken = default)
    {
        var incidente = await db.ExameIncidentesIdentidade.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Incidente", id.ToString());
        if (incidente.Status != StatusIncidenteIdentidade.Aberto)
            throw new ConflitoException("incidente.nao_aberto", "Este incidente já foi encerrado.");

        Encerrar(incidente, StatusIncidenteIdentidade.Descartado, nota);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolverPorEstudoAsync(
        string studyInstanceUID, string nota, CancellationToken cancellationToken = default)
    {
        var incidente = await db.ExameIncidentesIdentidade.FirstOrDefaultAsync(
            i => i.StudyInstanceUID == studyInstanceUID && i.Status == StatusIncidenteIdentidade.Aberto,
            cancellationToken);
        if (incidente is null) return; // corrigir sem alarme prévio é caminho normal

        Encerrar(incidente, StatusIncidenteIdentidade.Resolvido, nota);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void Encerrar(ExameIncidenteIdentidade incidente, StatusIncidenteIdentidade status, string nota)
    {
        incidente.Status = status;
        incidente.ResolvidoEm = DateTime.UtcNow;
        incidente.ResolvidoPor = usuarioAtual.UsuarioId;
        incidente.ResolucaoNota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        incidente.AtualizadoEm = DateTime.UtcNow;
        incidente.AtualizadoPor = usuarioAtual.UsuarioId;
    }

    private async Task<IncidenteIdentidadeDto> MontarAsync(ExameIncidenteIdentidade i, CancellationToken ct)
    {
        string? nome = null;
        if (i.PacienteSuspeitoId is { } p && p != Guid.Empty)
            nome = (await pacientes.ResolverAsync(p, ct))?.Nome;
        return new IncidenteIdentidadeDto(
            i.Id, i.StudyInstanceUID, i.ExameImagemId, nome, i.Motivo, i.Status, i.Automatico,
            i.CriadoEm, i.ResolucaoNota);
    }
}
