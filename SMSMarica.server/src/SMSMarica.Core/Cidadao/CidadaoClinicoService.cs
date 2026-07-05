using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Anexos;
using SMSMarica.Core.Common.Tempo;
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
        if (!pertence) return null;

        // Abrir o exame no app = VISUALIZOU o "Exame liberado" (✓✓ azul). Best-effort/idempotente.
        await MarcarVisualizadoAsync(solicitacaoExameId, FinalidadeComunicacao.ExameLiberado, cancellationToken);

        return await imagensPdf.GerarOuObterAsync(solicitacaoExameId, cancellationToken);
    }

    /// <summary>Estampa VisualizadoEm na comunicação (solicitação × finalidade). Nunca lança.</summary>
    private async Task MarcarVisualizadoAsync(
        Guid solicitacaoExameId, FinalidadeComunicacao finalidade, CancellationToken ct)
    {
        try
        {
            await db.ComunicacoesPaciente
                .Where(c => c.SolicitacaoExameId == solicitacaoExameId
                    && c.Finalidade == finalidade && c.VisualizadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.VisualizadoEm, DateTime.UtcNow), ct);
        }
        catch
        {
            /* marcador é telemetria — nunca falha o request do cidadão */
        }
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
        var laudo = await db.Laudos.AsNoTracking()
            .Where(l => l.Id == laudoId && !l.Excluido)
            .Select(l => new { l.PacienteId, l.StudyInstanceUID })
            .FirstOrDefaultAsync(cancellationToken);

        if (laudo?.PacienteId != pacienteId) return null;
        if (!await assinatura.EstaAssinadoAsync(laudoId, cancellationToken)) return null;

        // Abrir o laudo no app = VISUALIZOU o "Laudo pronto" (✓✓ azul). Resolve a solicitação
        // pelo study (direto ou associação); best-effort.
        var solicitacaoId = await ResolverSolicitacaoPorStudyAsync(laudo.StudyInstanceUID, cancellationToken);
        if (solicitacaoId is { } sid)
            await MarcarVisualizadoAsync(sid, FinalidadeComunicacao.LaudoPronto, cancellationToken);

        return await assinatura.ObterPdfParaDownloadAsync(laudoId, cancellationToken);
    }

    private async Task<Guid?> ResolverSolicitacaoPorStudyAsync(string? studyUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studyUid)) return null;
        var direto = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.StudyInstanceUID == studyUid && s.ExcluidoEm == null)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (direto is not null) return direto;
        return await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.StudyInstanceUID == studyUid && a.ExcluidoEm == null)
            .Select(a => (Guid?)a.SolicitacaoExameId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AgendamentoResumoDto>> ListarAgendamentosAsync(
        Guid pacienteId, string? tipo, CancellationToken cancellationToken = default)
    {
        // Agendamento.InicioEm é "timestamp without time zone" (wall-clock Brasília): comparar com
        // um DateTime Kind=Utc quebra no Npgsql. Usamos a data local de Brasília como Unspecified.
        var hojeLocal = DateTime.SpecifyKind(FusoBrasilia.ParaExibicao(DateTime.UtcNow).Date, DateTimeKind.Unspecified);
        // SolicitacaoExame.DataAgendada é "timestamp with time zone" (Kind=Utc).
        var inicioHojeUtc = DateTime.UtcNow.Date;

        var query = db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId && a.ExcluidoEm == null
                && a.Status != StatusAgendamento.Cancelado
                && a.InicioEm >= hojeLocal);

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

        var resultado = linhas.Select(l =>
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

        // Exames importados (SISREG) vivem em SolicitacaoExame, não na agenda local — o card
        // deles traz a confirmação de presença (magic link / quick reply / botões do app).
        if (filtro is null or "exame")
        {
            var solicitacoes = await db.SolicitacoesExame.AsNoTracking()
                .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null
                    && s.Status != StatusSolicitacaoExame.Cancelada
                    && s.DataAgendada != null && s.DataAgendada >= inicioHojeUtc)
                .OrderBy(s => s.DataAgendada)
                .Select(s => new
                {
                    s.Id,
                    InicioEm = s.DataAgendada!.Value,
                    TipoExameNome = s.TipoExame != null ? s.TipoExame.Nome : null,
                    UnidadeNome = s.Unidade != null ? s.Unidade.Nome : null,
                    s.StatusConfirmacao,
                })
                .ToListAsync(cancellationToken);

            resultado.AddRange(solicitacoes.Select(s => new AgendamentoResumoDto(
                s.Id,
                s.InicioEm,
                s.InicioEm,
                "Exame",
                s.TipoExameNome ?? "Exame",
                null,
                s.UnidadeNome,
                DescreverStatusConfirmacao(s.StatusConfirmacao),
                SolicitacaoExameId: s.Id,
                StatusConfirmacao: s.StatusConfirmacao.ToString(),
                PodeResponder: s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)));
            resultado.Sort((a, b) => a.InicioEm.CompareTo(b.InicioEm));
        }

        return resultado;
    }

    public async Task<AgendamentoExameDetalheDto?> ObterExameAgendadoAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var s = await db.SolicitacoesExame.AsNoTracking()
            .Include(x => x.TipoExame)
            .Include(x => x.Unidade!).ThenInclude(u => u.Endereco)
            .Include(x => x.UnidadeSolicitante)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoExameId && x.ExcluidoEm == null, cancellationToken);
        if (s is null || s.PacienteId != pacienteId) return null;

        return new AgendamentoExameDetalheDto(
            s.Id,
            s.TipoExame?.Nome ?? "Exame",
            s.DataAgendada,
            s.DataSolicitacao,
            s.DataRegulacao,
            s.Unidade?.Nome,
            FormatarEndereco(s.Unidade?.Endereco),
            s.Unidade?.Telefone,
            s.UnidadeSolicitante?.Nome,
            string.IsNullOrWhiteSpace(s.SolicitanteNome) ? null : s.SolicitanteNome,
            s.AccessionNumber,
            s.CodigoSolicitacao,
            s.Prioridade.ToString(),
            s.Observacoes,
            s.StatusConfirmacao.ToString(),
            s.ConfirmadoEm,
            s.ConfirmadoCanal,
            s.ConfirmacaoCanceladaEm,
            s.MotivoCancelamentoPaciente);
    }

    private static string? FormatarEndereco(Data.Entities.Endereco? e)
    {
        if (e is null) return null;
        var partes = new[]
        {
            string.Join(", ", new[] { e.Logradouro, e.Numero }.Where(p => !string.IsNullOrWhiteSpace(p))),
            e.Bairro,
            string.Join("/", new[] { e.Cidade, e.Uf }.Where(p => !string.IsNullOrWhiteSpace(p))),
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        var texto = string.Join(" · ", partes);
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    public async Task ConfirmarExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var s = await ObterSolicitacaoDoPacienteAsync(pacienteId, solicitacaoExameId, cancellationToken);
        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.ja_respondida", "Este agendamento já foi respondido.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "app";
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, string motivo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new Common.Excecoes.ValidacaoException(
                "confirmacao.motivo_obrigatorio", "Informe o motivo para avisar que não poderá comparecer.");

        var s = await ObterSolicitacaoDoPacienteAsync(pacienteId, solicitacaoExameId, cancellationToken);
        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.ja_respondida", "Este agendamento já foi respondido.");

        var texto = motivo.Trim();
        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "app";
        s.MotivoCancelamentoPaciente = texto.Length <= 500 ? texto : texto[..500];
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Data.Entities.SolicitacaoExame> ObterSolicitacaoDoPacienteAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken ct)
    {
        var s = await db.SolicitacoesExame.FirstOrDefaultAsync(
            x => x.Id == solicitacaoExameId && x.ExcluidoEm == null, ct);
        // 404 também quando não é do paciente (não vaza existência).
        if (s is null || s.PacienteId != pacienteId)
            throw new Common.Excecoes.NaoEncontradoException("solicitacao.nao_encontrada", "Agendamento não encontrado.");
        if (s.DataAgendada is not { } da || da <= DateTime.UtcNow)
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.exame_passado", "Este exame já aconteceu ou não tem data futura.");
        return s;
    }

    private static string DescreverStatusConfirmacao(StatusConfirmacaoAgendamento status) => status switch
    {
        StatusConfirmacaoAgendamento.Confirmada => "Confirmado",
        StatusConfirmacaoAgendamento.Cancelada => "Aguardando remarcação",
        _ => "Agendado",
    };

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
