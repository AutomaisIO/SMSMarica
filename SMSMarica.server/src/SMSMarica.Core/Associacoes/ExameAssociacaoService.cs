using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Associacoes.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
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
            {
                // AUTO-REPARO: uma falha entre o commit da associação e a promoção deixaria a
                // solicitação presa sem "Realizada" (e sem o zap). Reaplicar aqui é idempotente
                // (MarcarComoRealizada tem guard por status; revínculo de laudos é no-op quando ok).
                var dataEstudoReparo = await ObterDataEstudoSeguroAsync(uid, cancellationToken);
                await solicitacoes.MarcarComoRealizadaAsync(solicitacao.Id, DateTime.UtcNow, dataEstudoReparo, cancellationToken);
                await AtualizarPacienteDosLaudosAsync(uid, solicitacao.PacienteId, DateTime.UtcNow, cancellationToken);
                return await MontarDtoAsync(existente, cancellationToken);
            }
            throw new ConflitoException("associacao.ja_associado",
                "Este exame já está associado a outra solicitação. Desassocie antes de reassociar.");
        }

        if (validarNoPacs && !await consultaStudy.StudyExistePorStudyUidAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.study_inexistente", "Estudo não encontrado no PACS.");

        // Data/hora REAL do exame vem do DICOM (StudyDate/StudyTime) do study associado —
        // fonte da verdade. Buscada ANTES da transação (nada de HTTP segurando lock);
        // falha do PACS não derruba a associação (null → fallback na exibição).
        var dataEstudo = await ObterDataEstudoSeguroAsync(uid, cancellationToken);

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

        // TRANSAÇÃO: associação + promoção (com enfileiramento do zap) + revínculo de laudos
        // são um único fato — parcial aqui deixava a solicitação presa em "JaConciliada" sem
        // nunca notificar o paciente. Falhou? Nada persiste e a próxima passagem refaz tudo.
        await using (var tx = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            db.ExameAssociacoes.Add(assoc);
            await db.SaveChangesAsync(cancellationToken);

            // Exame confirmado presente → promove a solicitação (no-op se já adiante).
            await solicitacoes.MarcarComoRealizadaAsync(solicitacao.Id, agora, dataEstudo, cancellationToken);

            // Mantém a cadeia consistente: o(s) laudo(s) deste estudo passam a apontar para o
            // paciente da solicitação associada (laudo assinado já foi barrado acima).
            await AtualizarPacienteDosLaudosAsync(uid, solicitacao.PacienteId, agora, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }

        logger.LogInformation(
            "Exame {Uid} associado à solicitação {Accession} (origem {Origem}).", uid, accession, origem);
        return await MontarDtoAsync(assoc, cancellationToken);
    }

    /// <summary>StudyDate/StudyTime do DICOM, blindado: falha do PACS vira null (fallback na exibição).</summary>
    private async Task<DateTime?> ObterDataEstudoSeguroAsync(string uid, CancellationToken ct)
    {
        try
        {
            return await consultaStudy.ObterDataHoraEstudoAsync(uid, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao obter StudyDate/StudyTime do DICOM para {Uid} — segue sem DataEstudo.", uid);
            return null;
        }
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

                    // A promoção desta associação enfileirou o zap "Exame Liberado". Se ainda
                    // NÃO saiu (Pendente), remove — o exame não aconteceu para este pedido, e a
                    // linha (única por solicitação×finalidade) calaria a notificação do exame
                    // real no futuro. Se já saiu, não há des-envio: só registra no log.
                    var comunicacoes = await db.ComunicacoesPaciente
                        .Where(c => c.SolicitacaoExameId == sol.Id
                                    && c.Finalidade == FinalidadeComunicacao.ExameLiberado)
                        .ToListAsync(cancellationToken);
                    var pendentes = comunicacoes.Where(c => c.Status == StatusComunicacao.Pendente).ToList();
                    db.ComunicacoesPaciente.RemoveRange(pendentes);
                    if (comunicacoes.Count > pendentes.Count)
                        logger.LogWarning(
                            "Desassociação da solicitação {Accession}: zap 'Exame Liberado' já havia sido enviado ao paciente.",
                            sol.AccessionNumber);
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

    /// <summary>Chave mínima de uma solicitação aberta para a conciliação PACS-driven.</summary>
    private sealed record SolicitacaoChave(Guid Id, string AccessionNumber, string StudyInstanceUID);

    /// <inheritdoc />
    public async Task<ResultadoConciliacao> ConciliarStudyAsync(
        EstudoPacsRecente estudo, CancellationToken cancellationToken = default)
    {
        var uid = (estudo.StudyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return ResultadoConciliacao.SemSolicitacao;

        // Idempotência (o poller repassa a mesma janela a cada 30s): study já vinculado
        // explicitamente, ou worklist já consumada (vínculo implícito por StudyUID de uma
        // solicitação já promovida) → nada a fazer. (No caminho em lote estes dois checks
        // são pré-filtrados em 2 queries — ConciliarLoteAsync chama o núcleo direto.)
        if (await db.ExameAssociacoes.AsNoTracking()
                .AnyAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, cancellationToken))
            return ResultadoConciliacao.JaConciliada;
        if (await db.SolicitacoesExame.AsNoTracking().AnyAsync(
                s => s.StudyInstanceUID == uid && s.ExcluidoEm == null
                     && (s.Status == StatusSolicitacaoExame.Realizada || s.Status == StatusSolicitacaoExame.Laudada),
                cancellationToken))
            return ResultadoConciliacao.JaConciliada;

        return await ConciliarNucleoAsync(uid, estudo, cancellationToken);
    }

    /// <summary>
    /// Núcleo da conciliação — pressupõe study SEM vínculo ativo (o chamador garantiu).
    /// Resolve a solicitação pelas chaves duráveis e promove (worklist) ou associa (explícito).
    /// </summary>
    private async Task<ResultadoConciliacao> ConciliarNucleoAsync(
        string uid, EstudoPacsRecente estudo, CancellationToken cancellationToken)
    {
        var acc = (estudo.AccessionNumber ?? string.Empty).Trim();
        var patId = (estudo.PatientId ?? string.Empty).Trim();

        // Chaves de junção duráveis, em ordem de confiança: AccessionNumber do DICOM
        // (worklist leva o nº SMS), PatientID = nº SMS (técnico digitou o número no campo
        // do paciente) e o StudyInstanceUID pré-gerado (worklist que voltou sem accession).
        // NENHUMA depende de quando a solicitação foi criada — o pedido pode ser antigo.
        // Realizada TAMBÉM resolve (2ª aquisição do mesmo pedido vincula ao mesmo exame);
        // Laudada/Cancelada nunca (laudo pode estar assinado; cancelada não vincula).
        var sol = await ResolverConciliavelPorAccessionAsync(acc, cancellationToken)
               ?? await ResolverConciliavelPorAccessionAsync(patId, cancellationToken)
               ?? await ResolverConciliavelPorStudyUidAsync(uid, cancellationToken);
        if (sol is null) return ResultadoConciliacao.SemSolicitacao;

        if (string.Equals(sol.StudyInstanceUID, uid, StringComparison.Ordinal))
        {
            // Worklist genuíno: o study herdou o StudyInstanceUID pré-gerado → o vínculo já
            // é IMPLÍCITO (por StudyUID). Só promove a Realizada, com a data real do DICOM.
            var dataEstudo = await ObterDataEstudoSeguroAsync(uid, cancellationToken);
            await solicitacoes.MarcarComoRealizadaAsync(sol.Id, DateTime.UtcNow, dataEstudo, cancellationToken);
            logger.LogInformation(
                "Conciliação worklist: solicitação {Accession} promovida a Realizada (study {Uid}).", sol.AccessionNumber, uid);
            return ResultadoConciliacao.Conciliada;
        }

        // TOMBSTONE: vínculo DESFEITO por humano é definitivo para o motor. As chaves DICOM
        // continuam gravadas no study; sem esta trava o poller recriaria a associação errada
        // em ≤30s e o desassociar seria impossível de sustentar. O study vira órfão (a tela
        // de conferência decide). Reassociar o MESMO par continua possível — manualmente.
        if (await db.ExameAssociacoes.AsNoTracking().AnyAsync(
                a => a.StudyInstanceUID == uid && a.SolicitacaoExameId == sol.Id && a.ExcluidoEm != null,
                cancellationToken))
            return ResultadoConciliacao.SemSolicitacao;

        // Exame sem worklist (ou com StudyUID próprio da máquina): cria vínculo EXPLÍCITO.
        // AssociarAsync promove a Realizada e revincula laudos. validarNoPacs:false — veio DO PACS.
        await AssociarAsync(
            new AssociarExameRequest(uid, sol.AccessionNumber, acc.Length > 0 ? acc : null),
            OrigemAssociacaoExame.Automatica, validarNoPacs: false, cancellationToken);
        return ResultadoConciliacao.Conciliada;
    }

    /// <inheritdoc />
    public async Task<ConciliacaoLoteResultado> ConciliarLoteAsync(
        IReadOnlyList<EstudoPacsRecente> estudos, CancellationToken cancellationToken = default)
    {
        // Normaliza e deduplica por UID (a paginação QIDO pode repetir linhas na borda).
        var validos = (estudos ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e.StudyInstanceUID))
            .Select(e => e with { StudyInstanceUID = e.StudyInstanceUID.Trim() })
            .DistinctBy(e => e.StudyInstanceUID, StringComparer.Ordinal)
            .ToList();
        if (validos.Count == 0) return new ConciliacaoLoteResultado(0, 0, 0, 0);

        // Pré-filtro em LOTE (2 queries) do caso dominante em regime: study já conciliado.
        // Evita 2+ queries POR STUDY a cada passagem do poller de 30s (DB compartilhado).
        var uids = validos.Select(e => e.StudyInstanceUID).ToArray();
        var jaAssociados = (await db.ExameAssociacoes.AsNoTracking()
            .Where(a => uids.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .Select(a => a.StudyInstanceUID)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var worklistConsumada = (await db.SolicitacoesExame.AsNoTracking()
            .Where(s => uids.Contains(s.StudyInstanceUID) && s.ExcluidoEm == null
                        && (s.Status == StatusSolicitacaoExame.Realizada || s.Status == StatusSolicitacaoExame.Laudada))
            .Select(s => s.StudyInstanceUID)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        int conciliadas = 0, jaConciliadas = 0, semSolicitacao = 0, falhas = 0;
        foreach (var estudo in validos)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var uid = estudo.StudyInstanceUID;
            if (jaAssociados.Contains(uid) || worklistConsumada.Contains(uid)) { jaConciliadas++; continue; }
            try
            {
                // Núcleo direto: o pré-filtro acima já fez os checks de idempotência.
                switch (await ConciliarNucleoAsync(uid, estudo, cancellationToken))
                {
                    case ResultadoConciliacao.Conciliada: conciliadas++; break;
                    case ResultadoConciliacao.JaConciliada: jaConciliadas++; break;
                    default: semSolicitacao++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                // Entidade deixada no ChangeTracker por um SaveChanges falho envenenaria os
                // SaveChanges dos próximos studies (ou flusharia promoção sem o zap junto) —
                // o DbContext é compartilhado pelo lote inteiro.
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "Falha ao conciliar study {Uid}.", uid);
            }
        }

        // Loga só quando algo aconteceu — o poller roda a cada 30s.
        if (conciliadas > 0 || falhas > 0)
            logger.LogInformation(
                "Conciliação: {Tot} studies — {Conc} conciliados, {Ja} já ok, {Sem} órfãos, {Falha} falhas.",
                validos.Count, conciliadas, jaConciliadas, semSolicitacao, falhas);

        return new ConciliacaoLoteResultado(conciliadas, jaConciliadas, semSolicitacao, falhas);
    }

    /// <summary>
    /// Solicitação CONCILIÁVEL (qualquer status exceto Laudada/Cancelada — Realizada entra,
    /// para a 2ª aquisição do mesmo pedido) cujo AccessionNumber == <paramref name="accession"/>.
    /// </summary>
    private async Task<SolicitacaoChave?> ResolverConciliavelPorAccessionAsync(string accession, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accession)) return null;
        return await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.AccessionNumber == accession && s.ExcluidoEm == null
                        && s.Status != StatusSolicitacaoExame.Laudada
                        && s.Status != StatusSolicitacaoExame.Cancelada)
            .Select(s => new SolicitacaoChave(s.Id, s.AccessionNumber, s.StudyInstanceUID))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Solicitação conciliável cujo StudyInstanceUID pré-gerado == <paramref name="uid"/>.</summary>
    private async Task<SolicitacaoChave?> ResolverConciliavelPorStudyUidAsync(string uid, CancellationToken ct) =>
        await db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.StudyInstanceUID == uid && s.ExcluidoEm == null
                        && s.Status != StatusSolicitacaoExame.Laudada
                        && s.Status != StatusSolicitacaoExame.Cancelada)
            .Select(s => new SolicitacaoChave(s.Id, s.AccessionNumber, s.StudyInstanceUID))
            .FirstOrDefaultAsync(ct);

    // Janela ampla (dias de StudyDate) do resync manual — mais larga que a do poller.
    private const int JanelaResyncDias = 30;
    // Teto de studies varridos por resync. 2000 cobre com folga 30 dias do CDT (~30-70/dia);
    // acima disso o DTO sinaliza LimiteAtingido e o front avisa a truncagem.
    private const int TetoResincronizacao = 2000;

    public async Task<ResincronizacaoResultadoDto> ResincronizarAsync(CancellationToken cancellationToken = default)
    {
        // PACS-driven: varre os studies recentes (pela DATA DO EXAME — StudyDate) e concilia.
        var hojeBr = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var estudos = await consultaStudy.BuscarStudiesPorDataAsync(
            hojeBr.AddDays(-JanelaResyncDias), hojeBr.AddDays(1), TetoResincronizacao, cancellationToken);

        var r = await ConciliarLoteAsync(estudos, cancellationToken);

        // Contrato do DTO (front): Candidatas/Varridas = studies varridos; Associadas =
        // conciliados nesta passada; SemExameNoPacs = órfãos (sem solicitação aberta).
        return new ResincronizacaoResultadoDto(
            estudos.Count, estudos.Count, r.Conciliadas, r.SemSolicitacao, r.Falhas,
            LimiteAtingido: estudos.Count >= TetoResincronizacao);
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
