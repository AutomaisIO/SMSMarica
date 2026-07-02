using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

public sealed class SolicitacoesExameService(
    SmsMaricaDbContext db,
    IGeradorIdentificadores geradorIds,
    IDcm4cheeMwlClient mwlClient,
    INotificadorExame notificador,
    IUsuarioAtualAccessor usuarioAtual,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
    Lazy<Laudos.Assinatura.ILaudoAssinaturaService> assinaturas,
    ILogger<SolicitacoesExameService> logger)
    : ISolicitacoesExameService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IGeradorIdentificadores _geradorIds = geradorIds;
    private readonly IDcm4cheeMwlClient _mwlClient = mwlClient;
    private readonly INotificadorExame _notificador = notificador;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;
    private readonly Pacientes.Fhir.IPacienteResolver _pacienteResolver = pacienteResolver;
    // Lazy: ponto de entrada do subsistema de Laudos. Sem isso a construção do
    // SolicitacoesExameService puxa Assinatura → PdfRenderer → Laudos, que reentra
    // aqui por várias arestas (direta e via ExameAssociacao) — dependência circular.
    private readonly Lazy<Laudos.Assinatura.ILaudoAssinaturaService> _assinaturas = assinaturas;
    private readonly ILogger<SolicitacoesExameService> _logger = logger;

    // Resolve nome/CPF/CNS do paciente (hub FHIR) e embute nos DTOs.
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        var nomes = await _pacienteResolver.ResolverManyAsync(dtos.Select(d => d.PacienteId), ct);
        return [.. dtos.Select(d => nomes.TryGetValue(d.PacienteId, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }

    private async Task<SolicitacaoExameDto> EnriquecerAsync(SolicitacaoExameDto dto, CancellationToken ct)
    {
        var r = await _pacienteResolver.ResolverAsync(dto.PacienteId, ct);
        return r is null ? dto : dto with { PacienteNome = r.Nome, PacienteCpf = r.Cpf, PacienteCns = r.Cns };
    }

    // Marca, em cada linha, o laudo "atual" (maior versão finalizada) do estudo e se
    // ele já está ASSINADO digitalmente — o front habilita o botão "ver laudo" só nesse caso.
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerLaudosAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        var studies = dtos
            .Where(d => !string.IsNullOrEmpty(d.StudyInstanceUID))
            .Select(d => d.StudyInstanceUID)
            .Distinct()
            .ToArray();
        if (studies.Length == 0) return dtos;

        var laudos = await _db.Laudos.AsNoTracking()
            .Where(l => !l.Excluido && l.Status == StatusLaudo.Finalizado && studies.Contains(l.StudyInstanceUID))
            .Select(l => new { l.Id, l.StudyInstanceUID, l.Versao })
            .ToListAsync(ct);
        if (laudos.Count == 0) return dtos;

        var atualPorStudy = laudos
            .GroupBy(l => l.StudyInstanceUID)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Versao).First().Id);

        var assinados = await _assinaturas.Value.QuaisAssinadosAsync(atualPorStudy.Values.ToArray(), ct);

        return [.. dtos.Select(d =>
            atualPorStudy.TryGetValue(d.StudyInstanceUID, out var laudoId)
                ? d with { LaudoId = laudoId, LaudoAssinado = assinados.Contains(laudoId) }
                : d)];
    }

    public async Task<IReadOnlyList<SolicitacaoExameListItemDto>> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SolicitacaoExame> query = _db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Where(s => s.ExcluidoEm == null);

        if (filtro.Status.HasValue) query = query.Where(s => s.Status == filtro.Status);
        if (filtro.PacienteId.HasValue) query = query.Where(s => s.PacienteId == filtro.PacienteId);
        if (filtro.UnidadeId.HasValue) query = query.Where(s => s.UnidadeId == filtro.UnidadeId);
        if (filtro.TipoExameId.HasValue) query = query.Where(s => s.TipoExameId == filtro.TipoExameId);
        if (!string.IsNullOrWhiteSpace(filtro.AccessionNumber))
        {
            var a = filtro.AccessionNumber.Trim();
            query = query.Where(s => s.AccessionNumber == a);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            // Busca livre: nº do pedido (accession/código) por ILIKE local + nome/CPF/CNS
            // resolvidos no hub FHIR (ids). Hub fora do ar → só match local (nunca 500).
            var termo = filtro.Busca.Trim();
            var padrao = $"%{termo}%";
            var idsPaciente = (await _pacienteResolver.BuscarIdsPorTermoAsync(termo, cancellationToken)).ToArray();
            query = query.Where(s =>
                EF.Functions.ILike(s.AccessionNumber, padrao)
                || (s.CodigoSolicitacao != null && EF.Functions.ILike(s.CodigoSolicitacao, padrao))
                || idsPaciente.Contains(s.PacienteId));
        }
        if (filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.CriadoEm >= ini);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(s => s.CriadoEm <= fim);
        }

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        // Urgentes sempre no topo, independente da data (Prioridade: Urgente=3 > Prioritaria=2 > Eletiva=1).
        var lista = await query
            .OrderByDescending(s => s.Prioridade)
            .ThenByDescending(s => s.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.Select(SolicitacoesExameMapper.ParaListItem)], cancellationToken);
        return await EnriquecerLaudosAsync([.. dtos], cancellationToken);
    }

    public async Task<SolicitacaoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await CarregarCompletoAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);
        return await EnriquecerAsync(SolicitacoesExameMapper.ParaDto(s), cancellationToken);
    }

    public async Task<SolicitacaoExameDto?> ObterPorAccessionAsync(string accession, CancellationToken cancellationToken = default)
    {
        var a = (accession ?? string.Empty).Trim();
        if (a.Length == 0) return null;
        var s = await CarregarCompletoAsync(x => x.AccessionNumber == a, cancellationToken);
        return s is null ? null : await EnriquecerAsync(SolicitacoesExameMapper.ParaDto(s), cancellationToken);
    }

    public async Task<SolicitacaoExameDto?> ObterPorStudyAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var u = (studyInstanceUID ?? string.Empty).Trim();
        if (u.Length == 0) return null;

        // 1) Casamento direto (exame de worklist: study == StudyInstanceUID da solicitação).
        var s = await CarregarCompletoAsync(x => x.StudyInstanceUID == u, cancellationToken);

        // 2) Fallback: associação manual/automática — o study REAL do PACS difere do
        //    StudyInstanceUID pré-gerado da solicitação. Resolve pela tabela de associação.
        if (s is null)
        {
            var solicitacaoId = await _db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.StudyInstanceUID == u && a.ExcluidoEm == null)
                .Select(a => a.SolicitacaoExameId)
                .FirstOrDefaultAsync(cancellationToken);
            if (solicitacaoId != Guid.Empty)
                s = await CarregarCompletoAsync(x => x.Id == solicitacaoId, cancellationToken);
        }

        return s is null ? null : await EnriquecerAsync(SolicitacoesExameMapper.ParaDto(s), cancellationToken);
    }

    public async Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        await ValidarReferenciasAsync(request.PacienteId, request.TipoExameId, request.UnidadeId, request.UnidadeSolicitanteId, cancellationToken);

        // Solicitante médico é Practitioner no hub FHIR; os dados vão nos snapshots
        // (SolicitanteNome/Crm/UfCrm). Validação de papel Médico foi descontinuada.

        var accession = await _geradorIds.ProximoAccessionAsync(cancellationToken);
        var studyUid = _geradorIds.NovoStudyInstanceUid();
        var agora = DateTime.UtcNow;

        // Tipo com envio ao worklist desligado: cria a solicitação mas não enfileira
        // o envio ao PACS (ProximaTentativaEm = null → o worker não pega).
        var enviarParaWorklist = await _db.TiposExame.AsNoTracking()
            .Where(t => t.Id == request.TipoExameId)
            .Select(t => t.EnviarParaWorklist)
            .FirstOrDefaultAsync(cancellationToken);

        var solicitacao = new SolicitacaoExame
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = accession,
            StudyInstanceUID = studyUid,

            PacienteId = request.PacienteId,
            TipoExameId = request.TipoExameId,
            UnidadeId = request.UnidadeId,
            UnidadeSolicitanteId = request.UnidadeSolicitanteId,

            // Solicitante = só o nome (texto). Colunas de conselho/usuário mantidas no banco
            // com os defaults da entidade (num/uf vazios, conselho "CRM"), mas não usadas.
            SolicitanteNome = request.SolicitanteNome.Trim(),

            CodigoSolicitacao = NormalizaOpcional(request.CodigoSolicitacao),
            ChaveConfirmacao = NormalizaOpcional(request.ChaveConfirmacao),
            Justificativa = NormalizaOpcional(request.Justificativa),

            Status = StatusSolicitacaoExame.Solicitada,
            Prioridade = request.Prioridade,
            Observacoes = NormalizaOpcional(request.Observacoes),
            DataAgendada = request.DataAgendada,

            // Worker pega imediatamente no próximo tick (sem bloquear a resposta da API
            // esperando o PACS responder). Null quando o tipo não envia ao worklist.
            ProximaTentativaEm = enviarParaWorklist ? agora : null,

            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.SolicitacoesExame.Add(solicitacao);
        await _db.SaveChangesAsync(cancellationToken);

        return solicitacao.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status != StatusSolicitacaoExame.Solicitada)
        {
            throw new ConflitoException(
                "solicitacaoExame.imutavel",
                "Solicitação só pode ser editada enquanto está no status 'Solicitada'.");
        }

        await ValidarReferenciasAsync(s.PacienteId, request.TipoExameId, request.UnidadeId, request.UnidadeSolicitanteId, cancellationToken);

        s.TipoExameId = request.TipoExameId;
        s.UnidadeId = request.UnidadeId;
        s.UnidadeSolicitanteId = request.UnidadeSolicitanteId;
        s.SolicitanteNome = request.SolicitanteNome.Trim();
        s.CodigoSolicitacao = NormalizaOpcional(request.CodigoSolicitacao);
        s.ChaveConfirmacao = NormalizaOpcional(request.ChaveConfirmacao);
        s.Justificativa = NormalizaOpcional(request.Justificativa);
        s.Prioridade = request.Prioridade;
        s.Observacoes = NormalizaOpcional(request.Observacoes);
        s.DataAgendada = request.DataAgendada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancelarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_cancelavel",
                $"Solicitação no status '{s.Status}' não pode ser cancelada.");
        }

        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
        {
            throw new ValidacaoException("solicitacaoExame.motivo_obrigatorio", "Motivo do cancelamento é obrigatório.");
        }

        // Remove o item da worklist no dcm4chee (best-effort — não derruba o
        // cancelamento local se o PACS estiver fora; o item terminal não atrapalha).
        if (!string.IsNullOrEmpty(s.WorklistItemUid))
        {
            try
            {
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Falha ao remover MWL de {Accession} no cancelamento — segue cancelado localmente.",
                    s.AccessionNumber);
            }
        }

        var agora = DateTime.UtcNow;
        s.Status = StatusSolicitacaoExame.Cancelada;
        s.CanceladoEm = agora;
        s.CanceladoPorUsuarioId = _usuarioAtual.UsuarioId;
        s.MotivoCancelamento = motivo;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReenviarWorklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_reenviavel",
                $"Só é possível reenviar worklist em 'Solicitada', 'Enviada' ou 'Recebida'. Atual: {s.Status}.");
        }

        // Apenas agenda o worker pra tentar agora — ele faz o POST/GET e
        // atualiza o status. UX: o front mostra "tentativa em andamento"
        // depois do refresh e o worker resolve em ~30s no pior caso.
        s.ProximaTentativaEm = DateTime.UtcNow;
        s.ErroIntegracaoPacs = null;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, bool force, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        // Exame iniciado/realizado/laudado tem imagens — o pedido não se exclui por aqui.
        if (s.Status is StatusSolicitacaoExame.EmExecucao or StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_excluivel",
                $"Solicitação no status '{s.Status}' (exame iniciado/realizado) não pode ser excluída.");
        }

        // Regra anti-lixo: primeiro remove o item da worklist no dcm4chee e confirma;
        // só então apaga localmente. Se a remoção no PACS falhar e NÃO for 'force', aborta
        // com um código que o front reconhece para oferecer a exclusão forçada.
        if (force)
        {
            // Force: tenta remover do PACS best-effort, mas ignora falha e limpa só a base local.
            try
            {
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Exclusão forçada de {Accession} — falha ao remover MWL no dcm4chee; limpando só a base local.",
                    s.AccessionNumber);
            }
        }
        else
        {
            try
            {
                // 404 (já não existe) conta como removido — operação idempotente.
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
            }
            catch (ConflitoException)
            {
                throw new ConflitoException(
                    "solicitacaoExame.exclusao_pacs_falhou",
                    "Não foi possível remover o item da worklist no dcm4chee. Verifique o PACS e tente de novo, " +
                    "ou use a exclusão forçada (limpa apenas a base local).");
            }
        }

        var agora = DateTime.UtcNow;
        s.ExcluidoEm = agora;
        s.ExcluidoPor = _usuarioAtual.UsuarioId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;
        s.ProximaTentativaEm = null; // tira do worker
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoLaudadaAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var uid = (studyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return;

        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(
            x => x.StudyInstanceUID == uid && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return; // study sem solicitação amarrada — ok.

        if (s.Status is StatusSolicitacaoExame.Cancelada or StatusSolicitacaoExame.Laudada) return;

        s.Status = StatusSolicitacaoExame.Laudada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoRealizadaAsync(Guid id, DateTime realizadoEm, DateTime? dataEstudo, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return;
        if (s.Status is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada) return;

        s.Status = StatusSolicitacaoExame.Realizada;
        s.RealizadoEm = realizadoEm; // hora de detecção pelo servidor (auditoria)
        // Data REAL do exame vinda do DICOM (fonte da verdade). Só grava quando o PACS
        // trouxe a tag — não sobrescreve com null.
        if (dataEstudo is not null) s.DataEstudo = dataEstudo;
        s.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Notifica (whatsapp/push no futuro; log no MVP).
        await _notificador.NotificarRealizadoAsync(s, cancellationToken);
    }

    public async Task ProcessarTentativaEnvioAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        var s = await _db.SolicitacoesExame
            .Include(x => x.TipoExame).ThenInclude(t => t!.ProcedimentoSigtap)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, cancellationToken);

        if (s is null) return;

        // Só os estados de envio interessam ao worker (Recebida já é terminal).
        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada))
        {
            // Limpa o agendamento para não voltar.
            if (s.ProximaTentativaEm is not null)
            {
                s.ProximaTentativaEm = null;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var agora = DateTime.UtcNow;
        s.TentativasEnvio += 1;
        s.UltimaTentativaEm = agora;

        try
        {
            switch (s.Status)
            {
                case StatusSolicitacaoExame.Solicitada:
                    // 1) Cria o MWL item (resolve paciente no FHIR + registra no dcm4chee + POST /mwlitems).
                    var sps = await _mwlClient.CriarOuAtualizarMwlItemAsync(s, cancellationToken);
                    s.WorklistItemUid = sps;
                    s.Status = StatusSolicitacaoExame.Enviada;
                    s.ErroIntegracaoPacs = null;
                    s.ProximaTentativaEm = agora; // confirma no próximo tick
                    s.AtualizadoEm = agora;
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Solicitação {Accession} enviada ao PACS (MWL {Sps}) — aguardando confirmação.",
                        s.AccessionNumber, sps);
                    break;

                case StatusSolicitacaoExame.Enviada:
                    // 2) Confirma que o dcm4chee tem o item na worklist → Recebida (TERMINAL do envio).
                    if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
                    {
                        s.Status = StatusSolicitacaoExame.Recebida;
                        s.ErroIntegracaoPacs = null;
                        s.ProximaTentativaEm = null; // terminal — a worklist está disponível para a máquina
                        s.AtualizadoEm = agora;
                        await _db.SaveChangesAsync(cancellationToken);
                        await _notificador.NotificarAgendadoAsync(s, cancellationToken);
                        _logger.LogInformation("Solicitação {Accession} recebida pela worklist do PACS.", s.AccessionNumber);
                    }
                    else
                    {
                        _logger.LogWarning("MWL item de {Accession} não encontrado no PACS — voltando para Solicitada.", s.AccessionNumber);
                        s.Status = StatusSolicitacaoExame.Solicitada;
                        s.WorklistItemUid = null;
                        s.ErroIntegracaoPacs = "Item de worklist não encontrado no PACS — reenviando.";
                        s.ProximaTentativaEm = agora;
                        s.AtualizadoEm = agora;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falha temporária — agenda nova tentativa com backoff exponencial.
            var espera = CalcularBackoff(s.TentativasEnvio);
            s.ErroIntegracaoPacs = ex.Message;
            s.ProximaTentativaEm = agora.Add(espera);
            s.AtualizadoEm = agora;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex,
                "Tentativa {N} de envio de {Accession} falhou. Próxima em {Espera}.",
                s.TentativasEnvio, s.AccessionNumber, espera);
        }
    }

    // ---- helpers ----

    /// <summary>
    /// Backoff exponencial limitado: 30s, 1min, 2min, 5min, 15min, 30min, 1h, 2h (cap).
    /// </summary>
    private static TimeSpan CalcularBackoff(int tentativas)
    {
        return tentativas switch
        {
            <= 1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(2),
            4 => TimeSpan.FromMinutes(5),
            5 => TimeSpan.FromMinutes(15),
            6 => TimeSpan.FromMinutes(30),
            7 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(2),
        };
    }

    private async Task<SolicitacaoExame?> CarregarCompletoAsync(
        System.Linq.Expressions.Expression<Func<SolicitacaoExame, bool>> filtro,
        CancellationToken cancellationToken)
    {
        return await _db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .Include(s => s.UnidadeSolicitante)
            .Where(s => s.ExcluidoEm == null)
            .FirstOrDefaultAsync(filtro, cancellationToken);
    }

    private async Task ValidarReferenciasAsync(Guid pacienteId, Guid tipoExameId, Guid unidadeId, Guid? unidadeSolicitanteId, CancellationToken ct)
    {
        // PacienteId referencia o hub FHIR — validação de existência fica a cargo do hub.
        if (!await _db.TiposExame.AsNoTracking().AnyAsync(t => t.Id == tipoExameId && t.ExcluidoEm == null && t.Ativo, ct))
        {
            throw new NaoEncontradoException(nameof(TipoExame), tipoExameId);
        }

        if (!await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct))
        {
            throw new NaoEncontradoException(nameof(Unidade), unidadeId);
        }

        if (unidadeSolicitanteId is { } us && !await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == us, ct))
        {
            throw new NaoEncontradoException(nameof(Unidade), us);
        }
    }


    private static string? NormalizaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
