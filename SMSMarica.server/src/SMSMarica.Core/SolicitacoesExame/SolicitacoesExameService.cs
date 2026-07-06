using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
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
    // Lazy: quebra o ciclo Solicitacoes → Comunicacao → LoginLink → Solicitacoes.
    Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> comunicacoes,
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
    private readonly Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> _comunicacoes = comunicacoes;
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
        var cpf = new string([.. (r?.Cpf ?? "").Where(char.IsDigit)]);
        var verificado = cpf.Length == 11
            && await _db.ContatosValidados.AsNoTracking().AnyAsync(c => c.Cpf == cpf, ct);
        var comVerificado = dto with { PacienteContatoVerificado = verificado };
        return r is null
            ? comVerificado
            : comVerificado with { PacienteNome = r.Nome, PacienteCpf = r.Cpf, PacienteCns = r.Cns };
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
        // O período filtra pela DATA DO AGENDAMENTO (data_agendada), não pela data da solicitação.
        // data_agendada é um instante UTC (timestamptz); a coluna é exibida no fuso de Brasília, então
        // os limites do dia (yyyy-mm-dd) são convertidos de Brasília para UTC (+3h) p/ casar com a exibição.
        // Registros sem data agendada ficam fora quando há filtro de período.
        if (filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada >= ini);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada <= fim);
        }

        var (queryEscopo, unidadeReferencia) = await AplicarEscopoUnidadeAsync(query, cancellationToken);
        query = queryEscopo;

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        // Urgentes sempre no topo, independente da data (Prioridade: Urgente=3 > Prioritaria=2 > Eletiva=1).
        var lista = await query
            .OrderByDescending(s => s.Prioridade)
            .ThenByDescending(s => s.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.Select(s => SolicitacoesExameMapper.ParaListItem(s, unidadeReferencia))], cancellationToken);
        var comLaudos = await EnriquecerLaudosAsync([.. dtos], cancellationToken);
        return await EnriquecerComunicacoesAsync([.. comLaudos], cancellationToken);
    }

    // Checks de comunicação na lista (✓/✓✓/✓✓azul/⚠): resume ExameLiberado e LaudoPronto de
    // cada solicitação da página. Uma query só para a página inteira.
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerComunicacoesAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        var ids = dtos.Select(d => d.Id).ToArray();

        var comunicacoes = await _db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoExameId != null && ids.Contains(c.SolicitacaoExameId.Value))
            .Select(c => new { c.SolicitacaoExameId, c.Finalidade, c.Status, c.VisualizadoEm, c.MotivoFalha })
            .ToListAsync(ct);
        if (comunicacoes.Count == 0) return dtos;

        var mapa = comunicacoes.ToDictionary(
            c => (c.SolicitacaoExameId!.Value, c.Finalidade),
            c => new ComunicacaoChipDto(c.Status.ToString(), c.VisualizadoEm != null, c.MotivoFalha));

        return [.. dtos.Select(d => d with
        {
            ChipConfirmacao = mapa.GetValueOrDefault((d.Id, FinalidadeComunicacao.ConfirmacaoAgendamento)),
            ChipExameLiberado = mapa.GetValueOrDefault((d.Id, FinalidadeComunicacao.ExameLiberado)),
            ChipLaudoPronto = mapa.GetValueOrDefault((d.Id, FinalidadeComunicacao.LaudoPronto)),
        })];
    }

    // Multitenancy por unidade: restringe a listagem às unidades vinculadas ao usuário
    // (usuario_unidade), casando tanto pela EXECUTORA quanto pela SOLICITANTE — a unidade vê
    // o que recebe para realizar e o que ela própria pediu. Regra de transição: usuário sem
    // vínculo (ou fora de contexto autenticado, ex.: background) continua vendo tudo. A unidade
    // ativa vem do header X-Unidade-Id e só vale se estiver entre os vínculos; caso contrário
    // degrada silenciosamente para o conjunto vinculado (nunca 403).
    //
    // Retorna também a "unidade de referência" (a ativa resolvida, ou null quando é a visão do
    // conjunto/admin-todas) — usada para marcar a direção (recebida/enviada) de cada linha.
    private async Task<(IQueryable<SolicitacaoExame> Query, Guid? UnidadeReferencia)> AplicarEscopoUnidadeAsync(
        IQueryable<SolicitacaoExame> query, CancellationToken ct)
    {
        var usuarioId = _usuarioAtual.UsuarioId;
        if (usuarioId is null) return (query, null);

        var ativa = _usuarioAtual.UnidadeAtivaId;

        // Global admin: vínculo implícito a TODAS as unidades — sem escopo obrigatório;
        // a unidade ativa (se enviada e válida) vira apenas um filtro de conveniência.
        if (usuarioId == IdentificadoresFixos.UsuarioAdminId)
        {
            if (ativa.HasValue &&
                await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == ativa.Value && u.Ativo, ct))
            {
                return (query.Where(s => s.UnidadeId == ativa.Value || s.UnidadeSolicitanteId == ativa.Value), ativa);
            }
            return (query, null);
        }

        var vinculos = await _db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.Unidade!.Ativo)
            .Select(v => v.UnidadeId)
            .ToArrayAsync(ct);
        if (vinculos.Length == 0) return (query, null);

        if (ativa.HasValue && vinculos.Contains(ativa.Value))
        {
            return (query.Where(s => s.UnidadeId == ativa.Value || s.UnidadeSolicitanteId == ativa.Value), ativa);
        }

        // Visão do conjunto (sem unidade ativa única): executora OU solicitante entre as
        // vinculadas. Sem referência única → sem seta de direção.
        return (
            query.Where(s => vinculos.Contains(s.UnidadeId)
                || (s.UnidadeSolicitanteId != null && vinculos.Contains(s.UnidadeSolicitanteId.Value))),
            null);
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

    public async Task AutorizarAsync(Guid id, string chaveConfirmacao, CancellationToken cancellationToken = default)
    {
        var chave = (chaveConfirmacao ?? string.Empty).Trim();
        if (chave.Length == 0)
            throw new ValidacaoException("autorizacao.chave_obrigatoria", "Informe a chave de autorização.");
        // Mesma régua da regulação usada no cadastro/edição (0000 emergencial ou ≥ 9999).
        if (!Validators.RegulacaoRegras.Valido(chave))
            throw new ValidacaoException("autorizacao.chave_invalida", Validators.RegulacaoRegras.MensagemInvalido);

        var s = await _db.SolicitacoesExame
            .Include(x => x.TipoExame)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), id);

        // Gate: paciente precisa ter um número VERIFICADO (contato_validado por CPF).
        var paciente = await _pacienteResolver.ResolverAsync(s.PacienteId, cancellationToken);
        var cpf = new string([.. (paciente?.Cpf ?? "").Where(char.IsDigit)]);
        var verificado = cpf.Length == 11
            && await _db.ContatosValidados.AsNoTracking().AnyAsync(c => c.Cpf == cpf, cancellationToken);
        if (!verificado)
            throw new ValidacaoException(
                "autorizacao.sem_numero_verificado",
                "O paciente ainda não tem um número de telefone verificado. Verifique o contato antes de autorizar.");

        var agora = DateTime.UtcNow;
        s.ChaveConfirmacao = chave;
        s.AutorizadoEm = agora;
        s.AutorizadoPor = _usuarioAtual.UsuarioId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Presença física vence qualquer estado anterior: sem resposta → confirma presencial;
        // tinha CANCELADO pelo WhatsApp/app mas compareceu → "revive" (volta a Confirmada,
        // canal presencial, e limpa o cancelamento — a linha deixa de ficar esmaecida).
        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada)
        {
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
            s.ConfirmadoEm = agora;
            s.ConfirmadoCanal = "presencial";
            s.ConfirmacaoCanceladaEm = null;
            s.MotivoCancelamentoPaciente = null;
        }

        // Só AGORA enfileira o envio ao PACS (se o tipo envia à worklist e ainda não foi enviado).
        if (s.Status == StatusSolicitacaoExame.Solicitada && (s.TipoExame?.EnviarParaWorklist ?? false))
        {
            s.ProximaTentativaEm = agora;
            s.ErroIntegracaoPacs = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        await ValidarReferenciasAsync(request.PacienteId, request.TipoExameId, request.UnidadeId, request.UnidadeSolicitanteId, cancellationToken);

        // Solicitante médico é Practitioner no hub FHIR; os dados vão nos snapshots
        // (SolicitanteNome/Crm/UfCrm). Validação de papel Médico foi descontinuada.

        var accession = await _geradorIds.ProximoAccessionAsync(cancellationToken);
        var studyUid = _geradorIds.NovoStudyInstanceUid();
        var agora = DateTime.UtcNow;

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
            DataSolicitacao = request.DataSolicitacao,
            DataAgendada = request.DataAgendada,

            // NADA vai ao PACS automaticamente: o envio só é enfileirado quando a recepção
            // AUTORIZA (AutorizarAsync). ProximaTentativaEm fica null até lá.
            ProximaTentativaEm = null,

            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.SolicitacoesExame.Add(solicitacao);
        // Notificação de confirmação por WhatsApp — igual ao import do SISREG (só enfileira;
        // o worker envia). Sem data agendada futura, o EnfileirarAsync não faz nada.
        await _comunicacoes.Value.EnfileirarAsync(
            solicitacao, Data.Entities.Enums.FinalidadeComunicacao.ConfirmacaoAgendamento, cancellationToken);
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
        s.DataSolicitacao = request.DataSolicitacao;
        s.DataAgendada = request.DataAgendada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Cobriu o caso "criou sem data, agendou depois": enfileira a confirmação por WhatsApp
        // se ainda não existe (idempotente por solicitação × finalidade).
        await _comunicacoes.Value.EnfileirarAsync(
            s, Data.Entities.Enums.FinalidadeComunicacao.ConfirmacaoAgendamento, cancellationToken);

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

        // Gate de autorização: uma solicitação que NUNCA foi ao PACS (Solicitada) só entra na
        // fila depois que a recepção autorizar com a chave. (Enviada/Recebida já estão no PACS —
        // o reenvio ali é manutenção/ressincronização, não um envio novo.)
        if (s.Status == StatusSolicitacaoExame.Solicitada && s.AutorizadoEm is null)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_autorizada",
                "Este exame ainda não foi autorizado pela recepção. Autorize com a chave de confirmação antes de enviar ao PACS.");
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

        // Exame chegou → enfileira o aviso "Exame liberado" (idempotente; o worker só envia
        // quando a chave EnviarExameLiberado estiver ligada — template aguardando a Meta).
        await _comunicacoes.Value.EnfileirarAsync(
            s, Data.Entities.Enums.FinalidadeComunicacao.ExameLiberado, cancellationToken);

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
