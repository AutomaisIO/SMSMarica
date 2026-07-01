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

        // Laudo ASSINADO trava o exame: a associação (e o paciente do laudo) não muda mais.
        if (await ExisteLaudoAssinadoAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.laudo_assinado",
                "Há laudo assinado para este exame. A associação não pode ser alterada.");

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
        // Guarda o status atual SE a associação for promovê-lo a Realizada — para o
        // desassociar reverter ao ponto anterior. Null se já estava adiante.
        var promovel = solicitacao.Status is not (StatusSolicitacaoExame.Realizada
            or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada);
        var assoc = new ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            SolicitacaoExameId = solicitacao.Id,
            PacienteId = solicitacao.PacienteId,
            AccessionNumberDicomOriginal = string.IsNullOrWhiteSpace(request.AccessionNumberDicomOriginal)
                ? null : request.AccessionNumberDicomOriginal!.Trim(),
            Origem = origem,
            StatusSolicitacaoAnterior = promovel ? solicitacao.Status : null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.ExameAssociacoes.Add(assoc);
        await db.SaveChangesAsync(cancellationToken);

        // Data/hora REAL do exame vem do DICOM (StudyDate/StudyTime) do study associado —
        // fonte da verdade. Falha do PACS não derruba a associação (null → fallback na exibição).
        DateTime? dataEstudo = null;
        try
        {
            dataEstudo = await consultaStudy.ObterDataHoraEstudoAsync(uid, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao obter StudyDate/StudyTime do DICOM para {Uid} — segue sem DataEstudo.", uid);
        }

        // Exame confirmado presente → promove a solicitação (no-op se já adiante).
        await solicitacoes.MarcarComoRealizadaAsync(solicitacao.Id, agora, dataEstudo, cancellationToken);

        // Mantém a cadeia consistente: o(s) laudo(s) deste estudo passam a apontar para o
        // paciente da solicitação associada (laudo assinado já foi barrado acima).
        await AtualizarPacienteDosLaudosAsync(uid, solicitacao.PacienteId, agora, cancellationToken);

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

        // Regra: só o laudo ASSINADO trava. Laudo finalizado-mas-ainda-não-assinado pode ser
        // desassociado (e reassociado) — depois de assinado não há mais "jeito".
        if (await ExisteLaudoAssinadoAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.laudo_assinado",
                "Há laudo assinado para este exame. Não é possível desassociar.");

        var agora = DateTime.UtcNow;
        assoc.ExcluidoEm = agora;
        assoc.ExcluidoPor = usuarioAtual.UsuarioId;
        assoc.AtualizadoEm = agora;
        assoc.AtualizadoPor = usuarioAtual.UsuarioId;

        // Reverte o status da solicitação ao ponto anterior à associação — desde que
        // tenha sido ESTA associação a promovê-la (StatusSolicitacaoAnterior setado),
        // ela ainda esteja em Realizada e não haja OUTRA associação ativa segurando-a.
        if (assoc.StatusSolicitacaoAnterior is { } anterior)
        {
            var temOutra = await db.ExameAssociacoes.AnyAsync(
                a => a.SolicitacaoExameId == assoc.SolicitacaoExameId && a.ExcluidoEm == null && a.Id != assoc.Id,
                cancellationToken);
            if (!temOutra)
            {
                var sol = await db.SolicitacoesExame.FirstOrDefaultAsync(
                    s => s.Id == assoc.SolicitacaoExameId && s.ExcluidoEm == null, cancellationToken);
                if (sol is not null && sol.Status == StatusSolicitacaoExame.Realizada)
                {
                    sol.Status = anterior;
                    sol.RealizadoEm = null;
                    sol.AtualizadoEm = agora;
                    sol.AtualizadoPor = usuarioAtual.UsuarioId;
                }
            }
        }

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
            .Select(s => new { s.StudyInstanceUID, s.Id, s.AccessionNumber, s.PacienteId, s.Prioridade })
            .ToListAsync(cancellationToken);

        // Accession + Prioridade das solicitações das associações explícitas.
        var solIds = explicitas.Select(a => a.SolicitacaoExameId).Distinct().ToArray();
        var solDados = solIds.Length == 0
            ? new Dictionary<Guid, (string Accession, PrioridadeSolicitacao Prioridade)>()
            : await db.SolicitacoesExame.AsNoTracking()
                .Where(s => solIds.Contains(s.Id))
                .ToDictionaryAsync(
                    s => s.Id, s => (Accession: s.AccessionNumber, s.Prioridade), cancellationToken);

        // Nomes de paciente em lote (1 chamada ao hub por id distinto).
        var pacienteIds = explicitas.Select(a => a.PacienteId).Concat(implicitas.Select(i => i.PacienteId));
        var nomes = await pacienteResolver.ResolverManyAsync(pacienteIds, cancellationToken);
        string? Nome(Guid id) => nomes.TryGetValue(id, out var r) ? r.Nome : null;

        // Solicitações que já têm anamnese preenchida (alimenta o gate de iniciar laudo no front).
        var todasSolIds = solIds.Concat(implicitas.Select(i => i.Id)).Distinct().ToArray();
        var comAnamnese = todasSolIds.Length == 0
            ? []
            : (await db.Anamneses.AsNoTracking()
                .Where(an => todasSolIds.Contains(an.SolicitacaoExameId))
                .Select(an => an.SolicitacaoExameId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var resultado = new List<ExameAssociacaoDto>(explicitas.Count + implicitas.Count);
        foreach (var a in explicitas)
        {
            var dados = solDados.GetValueOrDefault(a.SolicitacaoExameId);
            resultado.Add(new ExameAssociacaoDto(
                a.StudyInstanceUID, a.SolicitacaoExameId,
                dados.Accession ?? string.Empty,
                a.PacienteId, Nome(a.PacienteId), Explicita: true, a.Origem,
                dados.Prioridade, comAnamnese.Contains(a.SolicitacaoExameId)));
        }
        foreach (var i in implicitas)
        {
            resultado.Add(new ExameAssociacaoDto(
                i.StudyInstanceUID, i.Id, i.AccessionNumber,
                i.PacienteId, Nome(i.PacienteId), Explicita: false, Origem: null, i.Prioridade,
                comAnamnese.Contains(i.Id)));
        }
        return resultado;
    }

    /// <summary>Existe laudo (não-excluído) deste estudo com assinatura concluída?</summary>
    private async Task<bool> ExisteLaudoAssinadoAsync(string uid, CancellationToken ct) =>
        await db.LaudoAssinaturas.AsNoTracking().AnyAsync(
            a => a.Status == StatusAssinatura.Concluida
                 && db.Laudos.Any(l => l.Id == a.LaudoId && l.StudyInstanceUID == uid && !l.Excluido),
            ct);

    /// <summary>
    /// Aponta todos os laudos (não-excluídos) do estudo para o paciente informado — toda a
    /// cadeia de versões fica consistente com a solicitação associada. Ao definir o vínculo,
    /// limpa o rótulo temporário do DICOM (<see cref="Laudo.PacienteNomeDicom"/>): agora há
    /// paciente confiável. Só chamado quando NÃO há laudo assinado (assinado é imutável).
    /// No-op quando já está tudo correto.
    /// </summary>
    private async Task AtualizarPacienteDosLaudosAsync(string uid, Guid pacienteId, DateTime agora, CancellationToken ct)
    {
        var laudos = await db.Laudos
            .Where(l => l.StudyInstanceUID == uid && !l.Excluido
                        && (l.PacienteId != pacienteId || l.PacienteNomeDicom != null))
            .ToListAsync(ct);
        if (laudos.Count == 0) return;
        foreach (var l in laudos)
        {
            l.PacienteId = pacienteId;
            l.PacienteNomeDicom = null; // vínculo definido → rótulo temporário do DICOM é removido
            l.AtualizadoEm = agora;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("{N} laudo(s) do estudo {Uid} revinculados ao paciente {Paciente}.",
            laudos.Count, uid, pacienteId);
    }

    // Teto de varredura: o conjunto de órfãos costuma ser pequeno; o cap só protege o
    // PACS de um sweep gigante (cada candidata = 1 consulta QIDO ao dcm4chee).
    private const int TetoResincronizacao = 500;

    public async Task<ResincronizacaoResultadoDto> ResincronizarAsync(CancellationToken cancellationToken = default)
    {
        // Solicitações abertas (não-terminais) SEM associação ativa — sem janela de data.
        var abertas = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && (s.Status == StatusSolicitacaoExame.Solicitada
                            || s.Status == StatusSolicitacaoExame.Enviada
                            || s.Status == StatusSolicitacaoExame.Recebida
                            || s.Status == StatusSolicitacaoExame.EmExecucao))
            .OrderByDescending(s => s.CriadoEm)
            .Select(s => new { s.Id, s.AccessionNumber, s.StudyInstanceUID })
            .ToListAsync(cancellationToken);

        var comAssociacao = (await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExcluidoEm == null)
            .Select(a => a.SolicitacaoExameId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var candidatas = abertas.Where(s => !comAssociacao.Contains(s.Id)).ToList();
        var limiteAtingido = candidatas.Count > TetoResincronizacao;
        var lote = limiteAtingido ? candidatas.Take(TetoResincronizacao).ToList() : candidatas;

        int varridas = 0, associadas = 0, semExame = 0, falhas = 0;

        foreach (var s in lote)
        {
            if (cancellationToken.IsCancellationRequested) break;
            varridas++;

            IReadOnlyList<EstudoPacsBasico> estudos;
            try
            {
                // Só casa quando o Patient ID do estudo == nº da solicitação (o que o técnico
                // digitou). Em exame de worklist real o Patient ID é o CPF → busca não casa (no-op).
                estudos = await consultaStudy.BuscarPorPatientIdAsync(s.AccessionNumber, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogWarning(ex, "Resync: falha ao consultar o PACS por Patient ID {Accession}.", s.AccessionNumber);
                continue;
            }

            // Ignora o próprio study pré-gerado da worklist — esse não é "o exame".
            var alvos = estudos
                .Where(e => !string.Equals(e.StudyInstanceUID, s.StudyInstanceUID, StringComparison.Ordinal))
                .ToList();
            if (alvos.Count == 0) { semExame++; continue; }

            foreach (var e in alvos)
            {
                try
                {
                    await AssociarAsync(
                        new AssociarExameRequest(e.StudyInstanceUID, s.AccessionNumber, e.AccessionNumber),
                        OrigemAssociacaoExame.Automatica, validarNoPacs: false, cancellationToken);
                    associadas++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Conflito (já associado a OUTRA solicitação) ou erro pontual — não derruba o lote.
                    falhas++;
                    logger.LogWarning(ex,
                        "Resync: falha ao associar study {Uid} à solicitação {Accession}.", e.StudyInstanceUID, s.AccessionNumber);
                }
            }
        }

        logger.LogInformation(
            "Resync manual: {Cand} candidatas, {Var} varridas, {Assoc} associadas, {Sem} sem exame no PACS, {Falha} falhas.",
            candidatas.Count, varridas, associadas, semExame, falhas);

        return new ResincronizacaoResultadoDto(candidatas.Count, varridas, associadas, semExame, falhas, limiteAtingido);
    }

    private async Task<ExameAssociacaoDto> MontarDtoAsync(ExameAssociacao assoc, CancellationToken ct)
    {
        var sol = await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.Id == assoc.SolicitacaoExameId)
            .Select(s => new { s.AccessionNumber, s.Prioridade })
            .FirstOrDefaultAsync(ct);
        var paciente = await pacienteResolver.ResolverAsync(assoc.PacienteId, ct);
        var temAnamnese = await db.Anamneses.AsNoTracking()
            .AnyAsync(an => an.SolicitacaoExameId == assoc.SolicitacaoExameId, ct);
        return new ExameAssociacaoDto(
            assoc.StudyInstanceUID, assoc.SolicitacaoExameId, sol?.AccessionNumber ?? string.Empty,
            assoc.PacienteId, paciente?.Nome, Explicita: true, assoc.Origem,
            sol?.Prioridade ?? PrioridadeSolicitacao.Eletiva, temAnamnese);
    }
}
