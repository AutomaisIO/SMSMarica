using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Anexos;
using SMSMarica.Core.Anexos.Dtos;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Exames;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMarica.Core.Laudos.Assinatura.Dtos;
using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Cidadao;

public sealed class CidadaoClinicoService(
    SmsMaricaDbContext db,
    IAnexosService anexos,
    ILaudosService laudos,
    ILaudoAssinaturaService assinatura,
    IExameImagensPdfService imagensPdf) : ICidadaoClinicoService
{
    public async Task<IReadOnlyList<ExameResumoDto>> ListarExamesAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var exames = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null
                && (s.Status == StatusSolicitacaoExame.Realizada || s.Status == StatusSolicitacaoExame.Laudada))
            .OrderByDescending(s => s.RealizadoEm ?? s.CriadoEm)
            .Select(s => new
            {
                s.Id,
                s.StudyInstanceUID,
                s.Status,
                s.DataEstudo,
                s.RealizadoEm,
                s.CriadoEm,
                Nome = s.TipoExame != null ? s.TipoExame.Nome : "Exame de imagem",
            })
            .ToListAsync(cancellationToken);

        if (exames.Count == 0) return [];

        var ids = exames.Select(e => e.Id).ToList();

        var docs = await db.DocumentosExame.AsNoTracking()
            .Where(d => ids.Contains(d.SolicitacaoExameId) && d.ExcluidoEm == null
                && d.Status == StatusDocumentoExame.Salvo)
            .OrderByDescending(d => d.CriadoEm)
            .Select(d => new { d.Id, d.SolicitacaoExameId, d.Nome, d.TamanhoBytes, d.Paginas })
            .ToListAsync(cancellationToken);
        var docsPorExame = docs
            .GroupBy(d => d.SolicitacaoExameId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var studyUids = exames
            .Where(e => !string.IsNullOrEmpty(e.StudyInstanceUID))
            .Select(e => e.StudyInstanceUID)
            .Distinct()
            .ToList();
        var laudosPorStudy = studyUids.Count > 0
            ? (await laudos.ListarPorStudyUidsAsync(studyUids, cancellationToken))
                .GroupBy(l => l.StudyInstanceUID)
                .ToDictionary(g => g.Key, g => g.First())
            : [];
        IReadOnlySet<Guid> assinados = laudosPorStudy.Count > 0
            ? await assinatura.QuaisAssinadosAsync([.. laudosPorStudy.Values.Select(l => l.LaudoId)], cancellationToken)
            : new HashSet<Guid>();

        return exames.Select(e =>
        {
            docsPorExame.TryGetValue(e.Id, out var ds);
            LaudoPorStudyDto? laudo = null;
            if (!string.IsNullOrEmpty(e.StudyInstanceUID))
                laudosPorStudy.TryGetValue(e.StudyInstanceUID, out laudo);
            var laudoAssinado = laudo is not null && assinados.Contains(laudo.LaudoId);

            return new ExameResumoDto(
                e.Id,
                // Data do exame = DICOM (StudyDate/StudyTime) como fonte da verdade; só cai
                // para a hora de detecção (RealizadoEm) e, por fim, CriadoEm.
                e.DataEstudo ?? e.RealizadoEm ?? e.CriadoEm,
                e.Nome,
                DescreverStatusExame(e.Status),
                string.IsNullOrEmpty(e.StudyInstanceUID) ? null : e.StudyInstanceUID,
                TemImagens: !string.IsNullOrEmpty(e.StudyInstanceUID),
                Documentos: ds is null
                    ? []
                    : [.. ds.Select(d => new AnexoResumoDto(d.Id, d.Nome, d.TamanhoBytes, d.Paginas))],
                LaudoId: laudoAssinado ? laudo!.LaudoId : null,
                LaudoAssinado: laudoAssinado);
        }).ToList();
    }

    public async Task<AnexoConteudo?> ObterAnexoAsync(
        Guid pacienteId, Guid anexoId, CancellationToken cancellationToken = default)
    {
        var pertence = await (
            from d in db.DocumentosExame.AsNoTracking()
            join s in db.SolicitacoesExame.AsNoTracking() on d.SolicitacaoExameId equals s.Id
            where d.Id == anexoId && d.ExcluidoEm == null && d.Status == StatusDocumentoExame.Salvo
                  && s.PacienteId == pacienteId && s.ExcluidoEm == null
            select d.Id).AnyAsync(cancellationToken);

        return pertence ? await anexos.ObterConteudoAsync(anexoId, cancellationToken) : null;
    }

    public async Task<byte[]?> ObterImagensPdfAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var pertence = await db.SolicitacoesExame.AsNoTracking()
            .AnyAsync(s => s.Id == solicitacaoExameId && s.PacienteId == pacienteId && s.ExcluidoEm == null,
                cancellationToken);

        return pertence ? await imagensPdf.GerarOuObterAsync(solicitacaoExameId, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<LaudoResumoDto>> ListarLaudosAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var lista = await laudos.ListarAsync(
            new FiltroLaudosDto(PacienteId: pacienteId, Limite: 200), cancellationToken);

        return [.. lista
            .Where(l => l.Assinado)
            .Select(l => new LaudoResumoDto(l.Id, l.FinalizadoEm ?? l.CriadoEm, l.Titulo, "Assinado"))];
    }

    public async Task<PdfDownloadDto?> ObterLaudoPdfAsync(
        Guid pacienteId, Guid laudoId, CancellationToken cancellationToken = default)
    {
        var dono = await db.Laudos.AsNoTracking()
            .Where(l => l.Id == laudoId && !l.Excluido)
            .Select(l => l.PacienteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (dono != pacienteId) return null;
        if (!await assinatura.EstaAssinadoAsync(laudoId, cancellationToken)) return null;

        return await assinatura.ObterPdfParaDownloadAsync(laudoId, cancellationToken);
    }

    public async Task<IReadOnlyList<AgendamentoResumoDto>> ListarAgendamentosAsync(
        Guid pacienteId, string? tipo, CancellationToken cancellationToken = default)
    {
        var hoje = DateTime.UtcNow.Date;

        var query = db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId && a.ExcluidoEm == null
                && a.Status != StatusAgendamento.Cancelado
                && a.InicioEm >= hoje);

        var filtro = tipo?.Trim().ToLowerInvariant();
        if (filtro == "exame") query = query.Where(a => a.TipoExameId != null);
        else if (filtro == "consulta") query = query.Where(a => a.TipoExameId == null);

        var linhas = await query
            .OrderBy(a => a.InicioEm)
            .Select(a => new
            {
                a.Id,
                a.InicioEm,
                a.FimEm,
                a.Status,
                a.TipoExameId,
                EspecialidadeNome = a.Agenda!.Especialidade != null ? a.Agenda.Especialidade.Nome : null,
                TipoExameNome = a.TipoExame != null ? a.TipoExame.Nome : null,
                MedicoNome = a.Agenda!.MedicoNome,
                UnidadeNome = a.Agenda!.Unidade != null ? a.Agenda.Unidade.Nome : null,
            })
            .ToListAsync(cancellationToken);

        return linhas.Select(l =>
        {
            var ehExame = l.TipoExameId != null;
            return new AgendamentoResumoDto(
                l.Id,
                l.InicioEm,
                l.FimEm,
                ehExame ? "Exame" : "Consulta",
                (ehExame ? l.TipoExameNome : l.EspecialidadeNome) ?? (ehExame ? "Exame" : "Consulta"),
                l.MedicoNome,
                l.UnidadeNome,
                DescreverStatusAgendamento(l.Status));
        }).ToList();
    }

    private static string DescreverStatusExame(StatusSolicitacaoExame status) => status switch
    {
        StatusSolicitacaoExame.Laudada => "Laudado",
        StatusSolicitacaoExame.Realizada => "Realizado",
        _ => status.ToString(),
    };

    private static string DescreverStatusAgendamento(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Agendado => "Agendado",
        StatusAgendamento.Confirmado => "Confirmado",
        StatusAgendamento.Realizado => "Realizado",
        StatusAgendamento.Faltou => "Faltou",
        StatusAgendamento.Cancelado => "Cancelado",
        _ => status.ToString(),
    };
}
