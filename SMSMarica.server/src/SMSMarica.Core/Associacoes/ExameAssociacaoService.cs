using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

public sealed class ExameAssociacaoService(
    SmsMaricaDbContext db,
    IPacienteResolver pacienteResolver,
    IConsultaStudyClient consultaStudy,
    ISolicitacoesExameService solicitacoes,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<ExameAssociacaoService> logger) : IExameAssociacaoService
{
    public async Task<ExameAssociacaoDto> AssociarAsync(
        AssociarExameRequest request,
        OrigemAssociacaoExame origem = OrigemAssociacaoExame.Manual,
        bool validarNoPacs = true,
        CancellationToken cancellationToken = default)
    {
        var uid = (request.StudyInstanceUID ?? string.Empty).Trim();
        var accession = (request.AccessionNumber ?? string.Empty).Trim();
        if (uid.Length == 0)
            throw new ValidacaoException("associacao.study_obrigatorio", "StudyInstanceUID é obrigatório.");
        if (accession.Length == 0)
            throw new ValidacaoException("associacao.accession_obrigatorio", "Número da solicitação é obrigatório.");

        var solicitacao = await db.SolicitacoesExame.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccessionNumber == accession && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação", accession);

        if (solicitacao.Status == StatusSolicitacaoExame.Cancelada)
            throw new ConflitoException("associacao.solicitacao_cancelada", "A solicitação informada está cancelada.");
        if (solicitacao.PacienteId == Guid.Empty)
            throw new ConflitoException("associacao.sem_paciente", "A solicitação não tem paciente vinculado.");

        // Já associado? Idempotente para a mesma solicitação; conflito para outra.
        var existente = await db.ExameAssociacoes
            .FirstOrDefaultAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, cancellationToken);
        if (existente is not null)
        {
            if (existente.SolicitacaoExameId == solicitacao.Id)
                return await MontarDtoAsync(existente, cancellationToken);
            throw new ConflitoException("associacao.ja_associado",
                "Este exame já está associado a outra solicitação. Desassocie antes de reassociar.");
        }

        if (validarNoPacs && !await consultaStudy.StudyExistePorStudyUidAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.study_inexistente", "Estudo não encontrado no PACS.");

        var agora = DateTime.UtcNow;
        var assoc = new ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            SolicitacaoExameId = solicitacao.Id,
            PacienteId = solicitacao.PacienteId,
            AccessionNumberDicomOriginal = string.IsNullOrWhiteSpace(request.AccessionNumberDicomOriginal)
                ? null : request.AccessionNumberDicomOriginal!.Trim(),
            Origem = origem,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.ExameAssociacoes.Add(assoc);
        await db.SaveChangesAsync(cancellationToken);

        // Exame confirmado presente → promove a solicitação (no-op se já adiante).
        await solicitacoes.MarcarComoRealizadaAsync(solicitacao.Id, agora, cancellationToken);

        logger.LogInformation(
            "Exame {Uid} associado à solicitação {Accession} (origem {Origem}).", uid, accession, origem);
        return await MontarDtoAsync(assoc, cancellationToken);
    }

    public async Task DesassociarAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var uid = (studyInstanceUID ?? string.Empty).Trim();
        var assoc = await db.ExameAssociacoes
            .FirstOrDefaultAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Associação de exame", uid);

        // Regra: depois de fechar o laudo, não desassocia (só excluindo o laudo).
        var laudoFinalizado = await db.Laudos.AsNoTracking()
            .AnyAsync(l => l.StudyInstanceUID == uid && l.Status == StatusLaudo.Finalizado && !l.Excluido, cancellationToken);
        if (laudoFinalizado)
            throw new ConflitoException("associacao.laudo_finalizado",
                "Há laudo finalizado para este exame. Exclua o laudo antes de desassociar.");

        var agora = DateTime.UtcNow;
        assoc.ExcluidoEm = agora;
        assoc.ExcluidoPor = usuarioAtual.UsuarioId;
        assoc.AtualizadoEm = agora;
        assoc.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Exame {Uid} desassociado.", uid);
    }

    public async Task<VinculoExame?> ResolverVinculoAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var uid = (studyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return null;

        var explicita = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null)
            .Select(a => new VinculoExame(a.SolicitacaoExameId, a.PacienteId))
            .FirstOrDefaultAsync(cancellationToken);
        if (explicita is not null) return explicita;

        // Fallback: exame de worklist casa implicitamente pelo StudyInstanceUID.
        // Solicitação cancelada NÃO vincula (espelha o bloqueio do caminho explícito).
        return await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.StudyInstanceUID == uid && s.ExcluidoEm == null
                        && s.Status != StatusSolicitacaoExame.Cancelada)
            .Select(s => new VinculoExame(s.Id, s.PacienteId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExameAssociacaoDto>> ObterPorStudyUidsAsync(
        IReadOnlyList<string> studyInstanceUIDs, CancellationToken cancellationToken = default)
    {
        var uids = (studyInstanceUIDs ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct()
            .ToArray();
        if (uids.Length == 0) return [];

        var explicitas = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => uids.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .ToListAsync(cancellationToken);
        var comExplicita = explicitas.Select(a => a.StudyInstanceUID).ToHashSet();

        var faltam = uids.Where(u => !comExplicita.Contains(u)).ToArray();
        // Fallback implícito (worklist): solicitação cujo StudyInstanceUID é o do estudo.
        // Com faltam vazio o EF gera WHERE falso e retorna lista vazia — sem ternário.
        var implicitas = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => faltam.Contains(s.StudyInstanceUID) && s.ExcluidoEm == null
                        && s.Status != StatusSolicitacaoExame.Cancelada)
            .Select(s => new { s.StudyInstanceUID, s.Id, s.AccessionNumber, s.PacienteId })
            .ToListAsync(cancellationToken);

        // Accession das solicitações das associações explícitas.
        var solIds = explicitas.Select(a => a.SolicitacaoExameId).Distinct().ToArray();
        var accessions = solIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.SolicitacoesExame.AsNoTracking()
                .Where(s => solIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.AccessionNumber, cancellationToken);

        // Nomes de paciente em lote (1 chamada ao hub por id distinto).
        var pacienteIds = explicitas.Select(a => a.PacienteId).Concat(implicitas.Select(i => i.PacienteId));
        var nomes = await pacienteResolver.ResolverManyAsync(pacienteIds, cancellationToken);
        string? Nome(Guid id) => nomes.TryGetValue(id, out var r) ? r.Nome : null;

        var resultado = new List<ExameAssociacaoDto>(explicitas.Count + implicitas.Count);
        foreach (var a in explicitas)
        {
            resultado.Add(new ExameAssociacaoDto(
                a.StudyInstanceUID, a.SolicitacaoExameId,
                accessions.GetValueOrDefault(a.SolicitacaoExameId, string.Empty),
                a.PacienteId, Nome(a.PacienteId), Explicita: true, a.Origem));
        }
        foreach (var i in implicitas)
        {
            resultado.Add(new ExameAssociacaoDto(
                i.StudyInstanceUID, i.Id, i.AccessionNumber,
                i.PacienteId, Nome(i.PacienteId), Explicita: false, Origem: null));
        }
        return resultado;
    }

    private async Task<ExameAssociacaoDto> MontarDtoAsync(ExameAssociacao assoc, CancellationToken ct)
    {
        var accession = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.Id == assoc.SolicitacaoExameId)
            .Select(s => s.AccessionNumber)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var paciente = await pacienteResolver.ResolverAsync(assoc.PacienteId, ct);
        return new ExameAssociacaoDto(
            assoc.StudyInstanceUID, assoc.SolicitacaoExameId, accession,
            assoc.PacienteId, paciente?.Nome, Explicita: true, assoc.Origem);
    }
}
