using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Associacoes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Pacs;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Associacoes;

public sealed class ExameAssociacaoService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver,
    IConsultaStudyClient consultaStudy,
    ISolicitacoesExameService solicitacoes,
    Pacs.IPacsReescritorEstudoClient reescritor,
    Pacs.IResolvedorIdentidadeDicom identidades,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<ExameAssociacaoService> logger) : IExameAssociacaoService
{
    /// <summary>
    /// Corrige a identidade DENTRO do objeto DICOM do estudo "voando" e devolve o UID novo.
    ///
    /// <para>O estudo que chega sem worklist carrega o que a técnica digitou no equipamento —
    /// PatientID inventado, AccessionNumber vazio ou errado. Até 2026-08-11 a associação resolvia
    /// isso só do nosso lado e o objeto ficava como veio; o sistema mostrava certo e o arquivo
    /// continuava errado. Agora o objeto é reescrito com a identidade do pedido.</para>
    ///
    /// <para><b>Só no caminho MANUAL.</b> A conciliação automática roda a cada 30s sobre estudos
    /// recentes e pode pegar um estudo que o equipamento AINDA está enviando — reescrever ali
    /// (que apaga o original e re-armazena) perderia as instâncias que chegassem depois. No manual
    /// quem decide é uma pessoa, com o exame já terminado.</para>
    /// </summary>
    private async Task<EstudoReescrito> ReescreverDicomAsync(string uid, Guid exameImagemId, CancellationToken ct)
    {
        var identidade = await identidades.ObterAsync(exameImagemId, ct);
        var resultado = await reescritor.ReescreverIdentidadeAsync(uid, identidade, ct);
        logger.LogInformation(
            "Associação manual reescreveu o estudo {Antigo} → {Novo} com a identidade do pedido.",
            uid, resultado.StudyInstanceUIDNovo);
        return resultado;
    }

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

        // "solicitacao" aqui é o exame de imagem (id público); regulação via .Solicitacao.
        var solicitacao = await db.ExamesImagem.AsNoTracking().Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.AccessionNumber == accession && e.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação", accession);

        if (solicitacao.Status == StatusSolicitacaoExame.Cancelada)
            throw new ConflitoException("associacao.solicitacao_cancelada", "A solicitação informada está cancelada.");
        if (solicitacao.Solicitacao!.PacienteId == Guid.Empty)
            throw new ConflitoException("associacao.sem_paciente", "A solicitação não tem paciente vinculado.");
        var pacienteId = solicitacao.Solicitacao!.PacienteId;

        // Laudo ASSINADO trava o exame: a associação (e o paciente do laudo) não muda mais.
        if (await ExisteLaudoAssinadoAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.laudo_assinado",
                "Há laudo assinado para este exame. A associação não pode ser alterada.");

        // Já associado? Idempotente para o mesmo exame; conflito para outro.
        if (await ResolverJaAssociadoAsync(uid, solicitacao.Id, pacienteId, cancellationToken) is { } jaFeito)
            return jaFeito;

        // O estudo é o ORIGINAL de uma reescrita anterior? Então o que aparece dele na lista é a
        // sobra: a nota de rejeição, ou instâncias que o equipamento mandou depois. Reescrever de
        // novo criava uma cópia a cada clique (8 estudos para o accession 260811143 em 16–17/09).
        // Para o mesmo exame, anexa ao estudo que já existe; para outro, é conflito.
        if (origem == OrigemAssociacaoExame.Manual
            && await AnexarSobraDeReescritaAsync(uid, solicitacao.Id, pacienteId, cancellationToken) is { } anexado)
            return anexado;

        if (validarNoPacs && !await consultaStudy.StudyExistePorStudyUidAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.study_inexistente", "Estudo não encontrado no PACS.");

        // Data/hora REAL do exame vem do DICOM (StudyDate/StudyTime). Buscada ANTES da reescrita,
        // que troca o UID; falha do PACS não derruba a associação (null → fallback na exibição).
        var dataEstudo = await ObterDataEstudoSeguroAsync(uid, cancellationToken);

        // Integridade: o objeto passa a carregar a identidade do pedido, não a que foi digitada
        // no equipamento. Só no manual — ver ReescreverDicomAsync. Falha aqui ABORTA a associação:
        // meia correção (banco certo, arquivo errado) é justamente o que estamos eliminando.
        var uidDicomOriginal = uid;
        EstudoReescrito? reescrita = null;
        if (origem == OrigemAssociacaoExame.Manual)
        {
            reescrita = await ReescreverDicomAsync(uid, solicitacao.Id, cancellationToken);
            uid = reescrita.StudyInstanceUIDNovo;
            // O UID mudou: laudos que apontavam para o estudo antigo seguem o objeto, senão
            // ficariam órfãos apontando para um estudo que não existe mais.
            await db.Laudos
                .Where(l => l.StudyInstanceUID == uidDicomOriginal && !l.Excluido)
                .ExecuteUpdateAsync(u => u.SetProperty(l => l.StudyInstanceUID, uid), cancellationToken);

            // CORRIDA COM O CONCILIADOR: o estudo reescrito entra no PACS já com o accession
            // FINAL, então a varredura de 30s o reconhece e pode associá-lo antes de nós — foi o
            // que aconteceu em 11/08 e devolveu 409 ao operador, apesar de o resultado estar certo.
            // Chegar ao mesmo destino por outro caminho é SUCESSO, não conflito.
            if (await ResolverJaAssociadoAsync(uid, solicitacao.Id, pacienteId, cancellationToken) is { } peloMotor)
                return peloMotor;
        }

        var agora = DateTime.UtcNow;
        // Guarda o status atual SE a associação for promovê-lo a Realizada — para o desassociar
        // reverter ao ponto anterior. Null se já estava adiante.
        var promovel = solicitacao.Status is not (StatusSolicitacaoExame.Realizada
            or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada);
        var assoc = new ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            ExameImagemId = solicitacao.Id,
            PacienteId = pacienteId,
            AccessionNumberDicomOriginal = string.IsNullOrWhiteSpace(request.AccessionNumberDicomOriginal)
                ? null : request.AccessionNumberDicomOriginal!.Trim(),
            // Trilha da reescrita: como o estudo chegou e sob qual UID. Só existe no caminho manual,
            // que é o único que reescreve. Sem isto, associar para o paciente errado não teria volta.
            PatientIdDicomOriginal = reescrita?.PatientIdOriginal,
            NomePacienteDicomOriginal = reescrita?.PatientNameOriginal,
            StudyInstanceUidOriginal = reescrita is null ? null : uidDicomOriginal,
            Origem = origem,
            StatusSolicitacaoAnterior = promovel ? solicitacao.Status : null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };

        // TRANSAÇÃO: associação + promoção (com enfileiramento do zap) + revínculo de laudos são um
        // único fato — parcial aqui deixava a solicitação presa em "JaConciliada" sem notificar.
        // ExecutionStrategy: obrigatório com EnableRetryOnFailure — transação manual fora dela lança.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            db.ExameAssociacoes.Add(assoc);
            await db.SaveChangesAsync(cancellationToken);

            // Exame confirmado presente → promove a solicitação (no-op se já adiante).
            await solicitacoes.MarcarComoRealizadaAsync(solicitacao.Id, agora, dataEstudo, cancellationToken);

            // Mantém a cadeia consistente: o(s) laudo(s) deste estudo passam a apontar para o
            // paciente da solicitação associada (laudo assinado já foi barrado acima).
            await AtualizarPacienteDosLaudosAsync(uid, pacienteId, agora, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });

        logger.LogInformation(
            "Exame {Uid} associado à solicitação {Accession} (origem {Origem}).", uid, accession, origem);
        return await MontarDtoAsync(assoc, cancellationToken);
    }

    /// <summary>
    /// Trata a associação de um estudo que já foi reescrito antes (é o <c>StudyInstanceUidOriginal</c>
    /// de uma associação ativa). <c>null</c> = não é o caso, segue o fluxo normal.
    /// </summary>
    private async Task<ExameAssociacaoDto?> AnexarSobraDeReescritaAsync(
        string uidOriginal, Guid exameImagemId, Guid pacienteId, CancellationToken ct)
    {
        // A mais antiga: antes desta correção o mesmo original podia ter sido reescrito várias vezes,
        // e as reescritas seguintes eram as cópias parciais — a primeira é a que tem as imagens.
        var anterior = await db.ExameAssociacoes
            .Where(a => a.StudyInstanceUidOriginal == uidOriginal && a.ExcluidoEm == null)
            .OrderBy(a => a.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (anterior is null) return null;

        if (anterior.ExameImagemId != exameImagemId)
            throw new ConflitoException("associacao.ja_associado",
                "As imagens deste estudo já foram associadas a outra solicitação. Desassocie antes de reassociar.");

        var identidade = await identidades.ObterAsync(exameImagemId, ct);
        var anexo = await reescritor.AnexarAoEstudoAsync(uidOriginal, anterior.StudyInstanceUID, identidade, ct);
        logger.LogInformation(
            "Associação manual do original {Original}: {N} instância(s) anexada(s) a {Destino}, sem estudo novo.",
            uidOriginal, anexo.InstanciasReescritas, anterior.StudyInstanceUID);

        // Mesmo destino, mesma associação: o reparo idempotente cobre promoção e laudos.
        return await ResolverJaAssociadoAsync(anterior.StudyInstanceUID, exameImagemId, pacienteId, ct);
    }

    /// <summary>
    /// Devolve a associação quando o estudo JÁ está vinculado — ao mesmo exame (idempotente,
    /// com auto-reparo da promoção) — e lança quando está vinculado a outro. <c>null</c> = livre.
    ///
    /// <para>O auto-reparo existe porque uma falha entre o commit da associação e a promoção
    /// deixaria o exame preso sem "Realizada" (e sem o aviso ao paciente); reaplicar é inofensivo.</para>
    /// </summary>
    private async Task<ExameAssociacaoDto?> ResolverJaAssociadoAsync(
        string uid, Guid exameImagemId, Guid pacienteId, CancellationToken ct)
    {
        var existente = await db.ExameAssociacoes
            .FirstOrDefaultAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, ct);
        if (existente is null) return null;

        if (existente.ExameImagemId != exameImagemId)
            throw new ConflitoException("associacao.ja_associado",
                "Este exame já está associado a outra solicitação. Desassocie antes de reassociar.");

        var dataEstudoReparo = await ObterDataEstudoSeguroAsync(uid, ct);
        await solicitacoes.MarcarComoRealizadaAsync(exameImagemId, DateTime.UtcNow, dataEstudoReparo, ct);
        await AtualizarPacienteDosLaudosAsync(uid, pacienteId, DateTime.UtcNow, ct);
        return await MontarDtoAsync(existente, ct);
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

        // Regra: só o laudo ASSINADO trava. Finalizado-mas-não-assinado pode ser desassociado.
        if (await ExisteLaudoAssinadoAsync(uid, cancellationToken))
            throw new ConflitoException("associacao.laudo_assinado",
                "Há laudo assinado para este exame. Não é possível desassociar.");

        var agora = DateTime.UtcNow;
        assoc.ExcluidoEm = agora;
        assoc.ExcluidoPor = usuarioAtual.UsuarioId;
        assoc.AtualizadoEm = agora;
        assoc.AtualizadoPor = usuarioAtual.UsuarioId;

        // Reverte o status ao ponto anterior à associação — desde que tenha sido ESTA associação a
        // promovê-lo (StatusSolicitacaoAnterior setado), ainda esteja em Realizada e não haja OUTRA
        // associação ativa segurando-o.
        if (assoc.StatusSolicitacaoAnterior is { } anterior)
        {
            var temOutra = await db.ExameAssociacoes.AnyAsync(
                a => a.ExameImagemId == assoc.ExameImagemId && a.ExcluidoEm == null && a.Id != assoc.Id,
                cancellationToken);
            if (!temOutra)
            {
                var sol = await db.ExamesImagem.Include(e => e.Solicitacao)
                    .FirstOrDefaultAsync(e => e.Id == assoc.ExameImagemId && e.ExcluidoEm == null, cancellationToken);
                if (sol is not null && sol.Status == StatusSolicitacaoExame.Realizada)
                {
                    sol.Status = anterior;
                    sol.RealizadoEm = null;
                    sol.AtualizadoEm = agora;
                    sol.AtualizadoPor = usuarioAtual.UsuarioId;
                    // Espinha volta de Realizada para o estado de regulação anterior.
                    var reg = sol.Solicitacao!;
                    reg.Status = reg.DataAgendada != null ? StatusSolicitacao.Agendada : StatusSolicitacao.Solicitada;
                    reg.AtualizadoEm = agora;

                    // A promoção enfileirou o zap "Exame Liberado". Se ainda NÃO saiu (Pendente),
                    // remove — o exame não aconteceu para este pedido. Comunicação é ancorada na
                    // espinha, então filtra pelo SolicitacaoId. Se já saiu, só registra no log.
                    var comunicacoes = await db.ComunicacoesPaciente
                        .Where(c => c.SolicitacaoId == sol.SolicitacaoId
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
            .Select(a => new VinculoExame(a.ExameImagemId, a.PacienteId))
            .FirstOrDefaultAsync(cancellationToken);
        if (explicita is not null) return explicita;

        // Fallback: exame de worklist casa implicitamente pelo StudyInstanceUID.
        // Solicitação cancelada NÃO vincula (espelha o bloqueio do caminho explícito).
        return await db.ExamesImagem.AsNoTracking()
            .Where(e => e.StudyInstanceUID == uid && e.ExcluidoEm == null
                        && e.Status != StatusSolicitacaoExame.Cancelada)
            .Select(e => new VinculoExame(e.Id, e.Solicitacao!.PacienteId))
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
        // Fallback implícito (worklist): exame cujo StudyInstanceUID é o do estudo.
        var implicitas = await db.ExamesImagem.AsNoTracking()
            .Where(e => faltam.Contains(e.StudyInstanceUID) && e.ExcluidoEm == null
                        && e.Status != StatusSolicitacaoExame.Cancelada)
            .Select(e => new
            {
                e.StudyInstanceUID,
                e.Id,
                e.AccessionNumber,
                e.Solicitacao!.PacienteId,
                e.Solicitacao!.Prioridade,
                TipoNome = e.TipoExame!.Nome,
                Modalidade = (ModalidadeDicom?)e.TipoExame!.ModalidadeDicom,
                Executante = e.Solicitacao!.UnidadeExecutante!.Nome,
                Solicitante = e.Solicitacao!.UnidadeSolicitante!.Nome,
                e.Solicitacao!.CodigoSolicitacao,
            })
            .ToListAsync(cancellationToken);

        // Accession + Prioridade dos exames das associações explícitas.
        // OBS: projetar com Select (traduzido pro SQL — a navegação Solicitacao vira JOIN) ANTES
        // de virar dicionário. Passar a navegação no elementSelector do ToDictionaryAsync a
        // avaliaria em memória sobre a entidade materializada (Solicitacao não carregada) => NRE.
        var solIds = explicitas.Select(a => a.ExameImagemId).Distinct().ToArray();
        var solDados = solIds.Length == 0
            ? new Dictionary<Guid, ContextoExame>()
            : (await db.ExamesImagem.AsNoTracking()
                .Where(e => solIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Id,
                    e.AccessionNumber,
                    e.Solicitacao!.Prioridade,
                    TipoNome = e.TipoExame!.Nome,
                    Modalidade = (ModalidadeDicom?)e.TipoExame!.ModalidadeDicom,
                    Executante = e.Solicitacao!.UnidadeExecutante!.Nome,
                    Solicitante = e.Solicitacao!.UnidadeSolicitante!.Nome,
                    e.Solicitacao!.CodigoSolicitacao,
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id, x => new ContextoExame(
                    x.AccessionNumber, x.Prioridade, x.TipoNome, x.Modalidade,
                    x.Executante, x.Solicitante, x.CodigoSolicitacao));

        // Nomes de paciente em lote (1 chamada ao hub por id distinto).
        var pacienteIds = explicitas.Select(a => a.PacienteId).Concat(implicitas.Select(i => i.PacienteId));
        var nomes = await pacienteResolver.ResolverManyAsync(pacienteIds, cancellationToken);
        string? Nome(Guid id) => nomes.TryGetValue(id, out var r) ? r.Nome : null;

        // Exames que já têm anamnese preenchida (alimenta o gate de iniciar laudo no front).
        var todasSolIds = solIds.Concat(implicitas.Select(i => i.Id)).Distinct().ToArray();
        var comAnamnese = todasSolIds.Length == 0
            ? []
            : (await db.Anamneses.AsNoTracking()
                .Where(an => todasSolIds.Contains(an.ExameImagemId))
                .Select(an => an.ExameImagemId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var resultado = new List<ExameAssociacaoDto>(explicitas.Count + implicitas.Count);
        foreach (var a in explicitas)
        {
            var dados = solDados.GetValueOrDefault(a.ExameImagemId);
            resultado.Add(new ExameAssociacaoDto(
                a.StudyInstanceUID, a.ExameImagemId,
                dados?.Accession ?? string.Empty,
                a.PacienteId, Nome(a.PacienteId), Explicita: true, a.Origem,
                dados?.Prioridade ?? PrioridadeSolicitacao.Eletiva, comAnamnese.Contains(a.ExameImagemId),
                dados?.TipoExameNome, dados?.Modalidade, dados?.UnidadeExecutante, dados?.UnidadeSolicitante,
                dados?.CodigoSolicitacao));
        }
        foreach (var i in implicitas)
        {
            resultado.Add(new ExameAssociacaoDto(
                i.StudyInstanceUID, i.Id, i.AccessionNumber,
                i.PacienteId, Nome(i.PacienteId), Explicita: false, Origem: null, i.Prioridade,
                comAnamnese.Contains(i.Id),
                i.TipoNome, i.Modalidade, i.Executante, i.Solicitante, i.CodigoSolicitacao));
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
    /// Aponta todos os laudos (não-excluídos) do estudo para o paciente informado — toda a cadeia
    /// de versões fica consistente com a solicitação associada. Ao definir o vínculo, limpa o rótulo
    /// temporário do DICOM (<see cref="Laudo.PacienteNomeDicom"/>). Só chamado quando NÃO há laudo
    /// assinado (assinado é imutável). No-op quando já está tudo correto.
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

    /// <summary>Chave mínima de um exame aberto para a conciliação PACS-driven.</summary>
    private sealed record SolicitacaoChave(Guid Id, string AccessionNumber, string StudyInstanceUID);

    /// <summary>
    /// O contexto do PEDIDO que a listagem mostra junto do estudo. Nada disso existe no DICOM:
    /// o equipamento escreve uma StudyDescription genérica e não conhece nem o procedimento do
    /// SISREG nem as unidades envolvidas.
    /// </summary>
    private sealed record ContextoExame(
        string Accession,
        PrioridadeSolicitacao Prioridade,
        string? TipoExameNome,
        ModalidadeDicom? Modalidade,
        string? UnidadeExecutante,
        string? UnidadeSolicitante,
        string? CodigoSolicitacao);

    /// <inheritdoc />
    public async Task<ResultadoConciliacao> ConciliarStudyAsync(
        EstudoPacsRecente estudo, CancellationToken cancellationToken = default)
    {
        var uid = (estudo.StudyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return ResultadoConciliacao.SemSolicitacao;

        // Idempotência: study já vinculado explicitamente, ou worklist já consumada (vínculo
        // implícito por StudyUID de um exame já promovido) → nada a fazer.
        if (await db.ExameAssociacoes.AsNoTracking()
                .AnyAsync(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null, cancellationToken))
            return ResultadoConciliacao.JaConciliada;
        if (await db.ExamesImagem.AsNoTracking().AnyAsync(
                e => e.StudyInstanceUID == uid && e.ExcluidoEm == null
                     && (e.Status == StatusSolicitacaoExame.Realizada || e.Status == StatusSolicitacaoExame.Laudada),
                cancellationToken))
            return ResultadoConciliacao.JaConciliada;

        return await ConciliarNucleoAsync(uid, estudo, cancellationToken);
    }

    /// <summary>
    /// Núcleo da conciliação — pressupõe study SEM vínculo ativo (o chamador garantiu).
    /// Resolve o exame pelas chaves duráveis e promove (worklist) ou associa (explícito).
    /// </summary>
    private async Task<ResultadoConciliacao> ConciliarNucleoAsync(
        string uid, EstudoPacsRecente estudo, CancellationToken cancellationToken)
    {
        var acc = (estudo.AccessionNumber ?? string.Empty).Trim();
        var patId = (estudo.PatientId ?? string.Empty).Trim();

        // Chaves de junção duráveis, em ordem de confiança: AccessionNumber do DICOM (worklist leva
        // o nº SMS), PatientID = nº SMS e o StudyInstanceUID pré-gerado. Realizada TAMBÉM resolve (2ª
        // aquisição do mesmo pedido); Laudada/Cancelada nunca.
        var sol = await ResolverConciliavelPorAccessionAsync(acc, cancellationToken)
               ?? await ResolverConciliavelPorAccessionAsync(patId, cancellationToken)
               ?? await ResolverConciliavelPorStudyUidAsync(uid, cancellationToken);
        if (sol is null) return ResultadoConciliacao.SemSolicitacao;

        if (string.Equals(sol.StudyInstanceUID, uid, StringComparison.Ordinal))
        {
            // Worklist genuíno: o study herdou o StudyInstanceUID pré-gerado → vínculo IMPLÍCITO.
            // Só promove a Realizada, com a data real do DICOM.
            var dataEstudo = await ObterDataEstudoSeguroAsync(uid, cancellationToken);
            await solicitacoes.MarcarComoRealizadaAsync(sol.Id, DateTime.UtcNow, dataEstudo, cancellationToken);
            logger.LogInformation(
                "Conciliação worklist: solicitação {Accession} promovida a Realizada (study {Uid}).", sol.AccessionNumber, uid);
            return ResultadoConciliacao.Conciliada;
        }

        // TOMBSTONE: vínculo DESFEITO por humano é definitivo para o motor. Sem esta trava o poller
        // recriaria a associação errada em ≤30s. O study vira órfão (a tela de conferência decide).
        if (await db.ExameAssociacoes.AsNoTracking().AnyAsync(
                a => a.StudyInstanceUID == uid && a.ExameImagemId == sol.Id && a.ExcluidoEm != null,
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
        var uids = validos.Select(e => e.StudyInstanceUID).ToArray();
        var jaAssociados = (await db.ExameAssociacoes.AsNoTracking()
            .Where(a => uids.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .Select(a => a.StudyInstanceUID)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var worklistConsumada = (await db.ExamesImagem.AsNoTracking()
            .Where(e => uids.Contains(e.StudyInstanceUID) && e.ExcluidoEm == null
                        && (e.Status == StatusSolicitacaoExame.Realizada || e.Status == StatusSolicitacaoExame.Laudada))
            .Select(e => e.StudyInstanceUID)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        int conciliadas = 0, jaConciliadas = 0, semSolicitacao = 0, falhas = 0;
        foreach (var estudo in validos)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var uid = estudo.StudyInstanceUID;
            if (jaAssociados.Contains(uid) || worklistConsumada.Contains(uid)) { jaConciliadas++; continue; }
            try
            {
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
                // Entidade deixada no ChangeTracker por um SaveChanges falho envenenaria os próximos.
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "Falha ao conciliar study {Uid}.", uid);
            }
        }

        if (conciliadas > 0 || falhas > 0)
            logger.LogInformation(
                "Conciliação: {Tot} studies — {Conc} conciliados, {Ja} já ok, {Sem} órfãos, {Falha} falhas.",
                validos.Count, conciliadas, jaConciliadas, semSolicitacao, falhas);

        return new ConciliacaoLoteResultado(conciliadas, jaConciliadas, semSolicitacao, falhas);
    }

    /// <summary>
    /// Exame CONCILIÁVEL (qualquer status exceto Laudada/Cancelada — Realizada entra, para a 2ª
    /// aquisição do mesmo pedido) cujo AccessionNumber == <paramref name="accession"/>.
    /// </summary>
    private async Task<SolicitacaoChave?> ResolverConciliavelPorAccessionAsync(string accession, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accession)) return null;
        return await db.ExamesImagem.AsNoTracking()
            .Where(e => e.AccessionNumber == accession && e.ExcluidoEm == null
                        && e.Status != StatusSolicitacaoExame.Laudada
                        && e.Status != StatusSolicitacaoExame.Cancelada)
            .Select(e => new SolicitacaoChave(e.Id, e.AccessionNumber, e.StudyInstanceUID))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Exame conciliável cujo StudyInstanceUID pré-gerado == <paramref name="uid"/>.</summary>
    private async Task<SolicitacaoChave?> ResolverConciliavelPorStudyUidAsync(string uid, CancellationToken ct) =>
        await db.ExamesImagem.AsNoTracking()
            .Where(e => e.StudyInstanceUID == uid && e.ExcluidoEm == null
                        && e.Status != StatusSolicitacaoExame.Laudada
                        && e.Status != StatusSolicitacaoExame.Cancelada)
            .Select(e => new SolicitacaoChave(e.Id, e.AccessionNumber, e.StudyInstanceUID))
            .FirstOrDefaultAsync(ct);

    // Janela ampla (dias de StudyDate) do resync manual — mais larga que a do poller.
    private const int JanelaResyncDias = 30;
    // Teto de studies varridos por resync. 2000 cobre com folga 30 dias do CDT (~30-70/dia).
    private const int TetoResincronizacao = 2000;

    public async Task<ResincronizacaoResultadoDto> ResincronizarAsync(CancellationToken cancellationToken = default)
    {
        // PACS-driven: varre os studies recentes (pela DATA DO EXAME — StudyDate) e concilia.
        var hojeBr = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var estudos = await consultaStudy.BuscarStudiesPorDataAsync(
            hojeBr.AddDays(-JanelaResyncDias), hojeBr.AddDays(1), TetoResincronizacao, cancellationToken);

        var r = await ConciliarLoteAsync(estudos, cancellationToken);

        return new ResincronizacaoResultadoDto(
            estudos.Count, estudos.Count, r.Conciliadas, r.SemSolicitacao, r.Falhas,
            LimiteAtingido: estudos.Count >= TetoResincronizacao);
    }

    private async Task<ExameAssociacaoDto> MontarDtoAsync(ExameAssociacao assoc, CancellationToken ct)
    {
        var sol = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == assoc.ExameImagemId)
            .Select(e => new
            {
                e.AccessionNumber,
                e.Solicitacao!.Prioridade,
                TipoNome = e.TipoExame!.Nome,
                Modalidade = (ModalidadeDicom?)e.TipoExame!.ModalidadeDicom,
                Executante = e.Solicitacao!.UnidadeExecutante!.Nome,
                Solicitante = e.Solicitacao!.UnidadeSolicitante!.Nome,
                e.Solicitacao!.CodigoSolicitacao,
            })
            .FirstOrDefaultAsync(ct);
        var paciente = await pacienteResolver.ResolverAsync(assoc.PacienteId, ct);
        var temAnamnese = await db.Anamneses.AsNoTracking()
            .AnyAsync(an => an.ExameImagemId == assoc.ExameImagemId, ct);
        return new ExameAssociacaoDto(
            assoc.StudyInstanceUID, assoc.ExameImagemId, sol?.AccessionNumber ?? string.Empty,
            assoc.PacienteId, paciente?.Nome, Explicita: true, assoc.Origem,
            sol?.Prioridade ?? PrioridadeSolicitacao.Eletiva, temAnamnese,
            sol?.TipoNome, sol?.Modalidade, sol?.Executante, sol?.Solicitante, sol?.CodigoSolicitacao);
    }
}
