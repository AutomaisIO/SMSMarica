using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Common.Unidades;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

/// <summary>
/// Serviço do ciclo de vida de EXAMES DE IMAGEM (ADR-0021). Opera sobre o satélite
/// <see cref="ExameImagem"/> (cujo <c>Id</c> é o id PÚBLICO do exame, preservado do modelo antigo)
/// e acessa a regulação pela navegação <see cref="ExameImagem.Solicitacao"/>. O status de EXECUÇÃO
/// (worklist/PACS) vive em <c>ExameImagem.Status</c>; a espinha <c>Solicitacao.Status</c> é o resumo
/// de regulação, sincronizado nas transições Cancelada/Realizada.
/// </summary>
public sealed class SolicitacoesExameService(
    SmsMaricaDbContext db,
    IGeradorIdentificadores geradorIds,
    IDcm4cheeMwlClient mwlClient,
    IResolvedorEstacaoWorklist estacaoWorklist,
    INotificadorExame notificador,
    IUsuarioAtualAccessor usuarioAtual,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
    Telefones.IDispensaContatoService dispensasContato,
    Lazy<Laudos.Assinatura.ILaudoAssinaturaService> assinaturas,
    // Lazy: quebra o ciclo Solicitacoes → Comunicacao → LoginLink → Solicitacoes.
    Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> comunicacoes,
    Erros.IRegistroErroService registroErros,
    Auditoria.IAuditoriaService auditoria,
    ILogger<SolicitacoesExameService> logger)
    : ISolicitacoesExameService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IGeradorIdentificadores _geradorIds = geradorIds;
    private readonly IDcm4cheeMwlClient _mwlClient = mwlClient;
    private readonly IResolvedorEstacaoWorklist _estacaoWorklist = estacaoWorklist;
    private readonly INotificadorExame _notificador = notificador;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;
    private readonly Pacientes.Fhir.IPacienteResolver _pacienteResolver = pacienteResolver;
    private readonly Telefones.IDispensaContatoService _dispensasContato = dispensasContato;
    // Lazy: ponto de entrada do subsistema de Laudos. Sem isso a construção do
    // SolicitacoesExameService puxa Assinatura → PdfRenderer → Laudos, que reentra
    // aqui por várias arestas (direta e via ExameAssociacao) — dependência circular.
    private readonly Lazy<Laudos.Assinatura.ILaudoAssinaturaService> _assinaturas = assinaturas;
    private readonly Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> _comunicacoes = comunicacoes;
    private readonly Erros.IRegistroErroService _registroErros = registroErros;
    private readonly Auditoria.IAuditoriaService _auditoria = auditoria;
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
        // Verificado = marcador no telecom do Patient FHIR (fonte única; sem tabela local).
        var comVerificado = dto with { PacienteContatoVerificado = r?.TelefoneVerificado is not null };
        var enriquecido = r is null
            ? comVerificado
            : comVerificado with { PacienteNome = r.Nome, PacienteCpf = r.Cpf, PacienteCns = r.Cns };

        // Dispensa de verificação: a recepção precisa ver POR QUE este exame pode ser autorizado
        // sem contato verificado — e se o resultado sairá por WhatsApp ou só presencialmente.
        // Só consulta quando não há verificado: com selo, a dispensa já caiu.
        if (!enriquecido.PacienteContatoVerificado
            && await _dispensasContato.ObterAtivaAsync(dto.PacienteId, ct) is { } dispensa)
        {
            enriquecido = enriquecido with
            {
                PacienteContatoDispensado = true,
                PacienteContatoDispensaMotivo = dispensa.MotivoTexto,
                PacienteContatoDispensaPermiteEnvio = dispensa.PermiteEnvio,
            };
        }

        // Ticket #30: rastro de QUEM confirmou a chave de acesso (autorização presencial).
        if (dto.AutorizadoPor is { } autorId)
        {
            var nome = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Id == autorId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(ct);
            if (nome is not null) enriquecido = enriquecido with { AutorizadoPorNome = nome };
        }
        return enriquecido;
    }

    /// <summary>Existe um laudo (última versão finalizada) ASSINADO para o estudo deste exame?
    /// Study = o do próprio exame + os das associações ativas (precedência da associação).</summary>
    private async Task<bool> ExameTemLaudoAssinadoAsync(Guid exameId, CancellationToken ct)
    {
        var studyProprio = await _db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == exameId).Select(e => e.StudyInstanceUID).FirstOrDefaultAsync(ct);
        var studyAssoc = await _db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExameImagemId == exameId && a.ExcluidoEm == null)
            .Select(a => a.StudyInstanceUID).ToListAsync(ct);
        var studies = studyAssoc.Append(studyProprio ?? string.Empty)
            .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToArray();
        if (studies.Length == 0) return false;

        var laudos = await _db.Laudos.AsNoTracking()
            .Where(l => !l.Excluido && l.Status == StatusLaudo.Finalizado && studies.Contains(l.StudyInstanceUID))
            .Select(l => new { l.Id, l.StudyInstanceUID, l.Versao })
            .ToListAsync(ct);
        if (laudos.Count == 0) return false;

        var atuais = laudos
            .GroupBy(l => l.StudyInstanceUID)
            .Select(g => g.OrderByDescending(x => x.Versao).First().Id)
            .ToArray();
        var assinados = await _assinaturas.Value.QuaisAssinadosAsync(atuais, ct);
        return assinados.Count > 0;
    }

    public async Task EnviarComunicacaoManualAsync(
        Guid solicitacaoExameId, FinalidadeComunicacao finalidade, bool assumirRisco,
        CancellationToken cancellationToken = default)
    {
        var exame = await _db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId && e.ExcluidoEm == null)
            .Select(e => new { e.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        switch (finalidade)
        {
            case FinalidadeComunicacao.ExameLiberado
                when exame.Status is not (StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada):
                throw new ConflitoException(
                    "envio.exame_nao_realizado", "O exame ainda não foi realizado — não há imagem para enviar.");
            case FinalidadeComunicacao.LaudoPronto
                when !await ExameTemLaudoAssinadoAsync(solicitacaoExameId, cancellationToken):
                throw new ConflitoException(
                    "envio.laudo_nao_assinado", "O laudo ainda não está assinado — não há laudo pronto para enviar.");
            case FinalidadeComunicacao.ExameLiberado:
            case FinalidadeComunicacao.LaudoPronto:
                break;
            default:
                throw new ValidacaoException(
                    "envio.finalidade_invalida", "Envio manual só vale para exame liberado ou laudo pronto.");
        }

        await _comunicacoes.Value.EnviarManualAsync(solicitacaoExameId, finalidade, assumirRisco, cancellationToken);
    }

    // Marca, em cada linha, o laudo "atual" (maior versão finalizada) do exame e se
    // ele já está ASSINADO digitalmente — o front habilita o botão "ver laudo" só nesse caso.
    // O study do exame pode ser o pré-gerado da solicitação (worklist consumada) OU o gerado
    // pela própria máquina e vinculado via ExameAssociacao — a associação tem precedência.
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerLaudosAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;

        // DTO.Id = ExameImagem.Id; a associação FK aponta direto para o exame (id preservado).
        var ids = dtos.Select(d => d.Id).ToArray();
        var associados = await _db.ExameAssociacoes.AsNoTracking()
            .Where(a => ids.Contains(a.ExameImagemId) && a.ExcluidoEm == null)
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => new { a.ExameImagemId, a.StudyInstanceUID })
            .ToListAsync(ct);
        var assocPorSolicitacao = associados
            .GroupBy(a => a.ExameImagemId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StudyInstanceUID).ToList());

        var studies = dtos
            .SelectMany(d => CandidatosStudy(d, assocPorSolicitacao))
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
        {
            var candidatos = CandidatosStudy(d, assocPorSolicitacao)
                .Where(atualPorStudy.ContainsKey)
                .Select(s => atualPorStudy[s])
                .ToList();
            if (candidatos.Count == 0) return d;
            // Mais de um exame com laudo na mesma solicitação: prevalece o já assinado.
            var laudoId = candidatos.FirstOrDefault(assinados.Contains);
            if (laudoId == default) laudoId = candidatos[0];
            return d with { LaudoId = laudoId, LaudoAssinado = assinados.Contains(laudoId) };
        })];
    }

    /// <summary>Studies candidatos da linha: os vinculados via associação explícita (mais
    /// recente primeiro) e por fim o StudyInstanceUID pré-gerado da própria solicitação.</summary>
    private static IEnumerable<string> CandidatosStudy(
        SolicitacaoExameListItemDto d, Dictionary<Guid, List<string>> assocPorSolicitacao)
    {
        if (assocPorSolicitacao.TryGetValue(d.Id, out var associados))
            foreach (var study in associados) yield return study;
        if (!string.IsNullOrEmpty(d.StudyInstanceUID)) yield return d.StudyInstanceUID;
    }

    public async Task<IReadOnlyList<SolicitacaoExameListItemDto>> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default)
    {
        // Exames de imagem = satélite; regulação vem por .Solicitacao (não-excluída dos dois lados).
        IQueryable<ExameImagem> query = _db.ExamesImagem.AsNoTracking()
            .Include(e => e.TipoExame)
            .Include(e => e.Solicitacao!).ThenInclude(so => so.UnidadeExecutante)
            .Where(e => e.ExcluidoEm == null && e.Solicitacao!.ExcluidoEm == null);

        if (filtro.Status.HasValue) query = query.Where(e => e.Status == filtro.Status);
        if (filtro.PacienteId.HasValue) query = query.Where(e => e.Solicitacao!.PacienteId == filtro.PacienteId);
        if (filtro.UnidadeId.HasValue) query = query.Where(e => e.Solicitacao!.UnidadeExecutanteId == filtro.UnidadeId);
        if (filtro.TipoExameId.HasValue) query = query.Where(e => e.TipoExameId == filtro.TipoExameId);
        if (!string.IsNullOrWhiteSpace(filtro.AccessionNumber))
        {
            var a = filtro.AccessionNumber.Trim();
            query = query.Where(e => e.AccessionNumber == a);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            // Busca livre: nº do pedido (accession/código) por ILIKE local + nome/CPF/CNS
            // resolvidos no hub FHIR (ids). Hub fora do ar → só match local (nunca 500).
            var termo = filtro.Busca.Trim();
            var padrao = $"%{termo}%";
            var idsPaciente = (await _pacienteResolver.BuscarIdsPorTermoAsync(termo, cancellationToken)).ToArray();
            query = query.Where(e =>
                EF.Functions.ILike(e.AccessionNumber, padrao)
                || (e.Solicitacao!.CodigoSolicitacao != null && EF.Functions.ILike(e.Solicitacao!.CodigoSolicitacao, padrao))
                || idsPaciente.Contains(e.Solicitacao!.PacienteId));
        }
        // Busca PONTUAL (nº do pedido ou termo livre) ignora o período: quem procura uma
        // solicitação específica quer encontrá-la mesmo fora do dia filtrado.
        var buscaPontual = !string.IsNullOrWhiteSpace(filtro.AccessionNumber)
                           || !string.IsNullOrWhiteSpace(filtro.Busca);

        // O período filtra pela DATA DO AGENDAMENTO (regulação). data_agendada é instante UTC; os
        // limites do dia (Brasília) são convertidos para UTC (+3h). Sem data agendada → fora do período.
        if (!buscaPontual && filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(e => e.Solicitacao!.DataAgendada != null && e.Solicitacao!.DataAgendada >= ini);
        }
        if (!buscaPontual && filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(e => e.Solicitacao!.DataAgendada != null && e.Solicitacao!.DataAgendada <= fim);
        }

        // Recorte do painel de início ("ver todos" de uma raia). Os predicados são os MESMOS de
        // PainelInicioService — se divergirem, o total do painel deixa de bater com a lista que
        // ele abre, que é o jeito mais rápido de perder a confiança do operador.
        if (filtro.Painel == RecortePainel.Cancelados)
        {
            query = query.Where(e =>
                e.Solicitacao!.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada
                && (e.Solicitacao!.Status == StatusSolicitacao.Solicitada
                    || e.Solicitacao!.Status == StatusSolicitacao.Agendada));
        }
        else if (filtro.Painel == RecortePainel.Aguardando)
        {
            var agora = DateTime.UtcNow;
            var limiteJanela = agora.AddDays(PainelInicio.JanelasPainel.AguardandoDias);
            query = query.Where(e =>
                e.Solicitacao!.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && (e.Solicitacao!.Status == StatusSolicitacao.Solicitada
                    || e.Solicitacao!.Status == StatusSolicitacao.Agendada)
                && e.Solicitacao!.DataAgendada != null
                && e.Solicitacao!.DataAgendada >= agora
                && e.Solicitacao!.DataAgendada <= limiteJanela);
        }

        var (queryEscopo, unidadeReferencia) = await AplicarEscopoUnidadeAsync(query, cancellationToken);
        query = queryEscopo;

        // Recorte por VISÃO (ticket #84): só quando há uma unidade de referência única (a ativa) e
        // NÃO é busca pontual. O escopo acima já limitou a executora OU solicitante entre as
        // vinculadas; aqui estreita para UM dos lados. Padrão = executante (o que a recepção
        // realiza); solicitante = o que a unidade pediu a outra. Sem referência única (VeTudo /
        // visão do conjunto) não há visão que faça sentido, e o comportamento anterior (os dois
        // lados) fica intacto. Busca pontual (nº do pedido/CPF/CNS) IGNORA a visão pelo mesmo motivo
        // que ignora o período: quem procura um pedido específico quer achá-lo mesmo do outro lado.
        if (unidadeReferencia is { } refVisao && !buscaPontual)
        {
            query = filtro.VisaoSolicitante
                ? query.Where(e => e.Solicitacao!.UnidadeSolicitanteId == refVisao)
                : query.Where(e => e.Solicitacao!.UnidadeExecutanteId == refVisao);
        }

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        // Urgentes sempre no topo, independente da data (Prioridade: Urgente=3 > Prioritaria=2 > Eletiva=1).
        var lista = await query
            .OrderByDescending(e => e.Solicitacao!.Prioridade)
            .ThenByDescending(e => e.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.Select(e => SolicitacoesExameMapper.ParaListItem(e, unidadeReferencia))], cancellationToken);
        var comLaudos = await EnriquecerLaudosAsync([.. dtos], cancellationToken);
        var comAnamnese = await EnriquecerAnamneseAsync([.. comLaudos], cancellationToken);
        return await EnriquecerComunicacoesAsync([.. comAnamnese], cancellationToken);
    }

    // Marca as linhas que já têm anamnese preenchida (muda a cor do botão Anamnese na lista).
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerAnamneseAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        // Anamnese FK = ExameImagemId (id preservado = DTO.Id).
        var ids = dtos.Select(d => d.Id).ToArray();
        var comAnamnese = (await _db.Anamneses.AsNoTracking()
            .Where(a => ids.Contains(a.ExameImagemId) && a.ExcluidoEm == null)
            .Select(a => a.ExameImagemId)
            .ToListAsync(ct)).ToHashSet();
        return [.. dtos.Select(d => comAnamnese.Contains(d.Id) ? d with { TemAnamnese = true } : d)];
    }

    // Checks de comunicação na lista (✓/✓✓/✓✓azul/⚠): resume ExameLiberado e LaudoPronto de
    // cada solicitação da página. As comunicações são ancoradas na ESPINHA (Solicitacao), então
    // traduzimos o id público (ExameImagem.Id) → id da espinha para o join.
    private async Task<IReadOnlyList<SolicitacaoExameListItemDto>> EnriquecerComunicacoesAsync(
        List<SolicitacaoExameListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        var ids = dtos.Select(d => d.Id).ToArray();

        // Mapa exame(id público) → solicitação(espinha).
        var exameParaSolic = await _db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.SolicitacaoId })
            .ToListAsync(ct);
        var solicPorExame = exameParaSolic.ToDictionary(x => x.Id, x => x.SolicitacaoId);
        var solicIds = exameParaSolic.Select(x => x.SolicitacaoId).ToArray();

        var comunicacoes = await _db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId != null && solicIds.Contains(c.SolicitacaoId.Value))
            .Select(c => new { c.SolicitacaoId, c.Finalidade, c.Status, c.VisualizadoEm, c.MotivoFalha })
            .ToListAsync(ct);
        if (comunicacoes.Count == 0) return dtos;

        var mapa = comunicacoes.ToDictionary(
            c => (c.SolicitacaoId!.Value, c.Finalidade),
            c => new ComunicacaoChipDto(c.Status.ToString(), c.VisualizadoEm != null, c.MotivoFalha));

        ComunicacaoChipDto? Chip(Guid exameId, FinalidadeComunicacao f) =>
            solicPorExame.TryGetValue(exameId, out var sid) ? mapa.GetValueOrDefault((sid, f)) : null;

        return [.. dtos.Select(d => d with
        {
            ChipConfirmacao = Chip(d.Id, FinalidadeComunicacao.ConfirmacaoAgendamento),
            ChipExameLiberado = Chip(d.Id, FinalidadeComunicacao.ExameLiberado),
            ChipLaudoPronto = Chip(d.Id, FinalidadeComunicacao.LaudoPronto),
        })];
    }

    // Multitenancy por unidade: restringe a listagem às unidades vinculadas ao usuário
    // (usuario_unidade), casando tanto pela EXECUTORA quanto pela SOLICITANTE. A cascata de
    // resolução vive em EscopoUnidade (ADR-0033); aqui só se aplica o filtro, porque o exame chega
    // à unidade navegando pela espinha. Sem vínculo nenhum ⇒ não vê nada (fail-closed, ADR-0037).
    // Retorna a "unidade de referência" (a ativa resolvida, ou null na visão do conjunto) — é ela
    // que marca a direção (recebida/enviada) de cada linha.
    private async Task<(IQueryable<ExameImagem> Query, Guid? UnidadeReferencia)> AplicarEscopoUnidadeAsync(
        IQueryable<ExameImagem> query, CancellationToken ct)
    {
        var escopo = await EscopoUnidade.ResolverAsync(_db, _usuarioAtual, ct);

        if (escopo.VeTudo) return (query, null);
        if (escopo.SemAcesso) return (query.Where(_ => false), null);

        if (escopo.UnidadeUnica is { } uma)
        {
            return (query.Where(e => e.Solicitacao!.UnidadeExecutanteId == uma
                || e.Solicitacao!.UnidadeSolicitanteId == uma), escopo.Referencia);
        }

        // Visão do conjunto: executora OU solicitante entre as vinculadas. Sem referência única → sem seta.
        var unidades = escopo.Unidades;
        return (
            query.Where(e => unidades.Contains(e.Solicitacao!.UnidadeExecutanteId)
                || (e.Solicitacao!.UnidadeSolicitanteId != null && unidades.Contains(e.Solicitacao!.UnidadeSolicitanteId.Value))),
            escopo.Referencia);
    }

    public async Task<SolicitacaoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await CarregarCompletoAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);
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

        // 1) Casamento direto (exame de worklist: study == StudyInstanceUID pré-gerado).
        var s = await CarregarCompletoAsync(x => x.StudyInstanceUID == u, cancellationToken);

        // 2) Fallback: associação manual/automática — o study REAL do PACS difere do pré-gerado.
        if (s is null)
        {
            var exameId = await _db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.StudyInstanceUID == u && a.ExcluidoEm == null)
                .Select(a => a.ExameImagemId)
                .FirstOrDefaultAsync(cancellationToken);
            if (exameId != Guid.Empty)
                s = await CarregarCompletoAsync(x => x.Id == exameId, cancellationToken);
        }

        return s is null ? null : await EnriquecerAsync(SolicitacoesExameMapper.ParaDto(s), cancellationToken);
    }

    public async Task AutorizarAsync(
        Guid id, string chaveConfirmacao, Guid? equipamentoId = null, CancellationToken cancellationToken = default)
    {
        var chave = (chaveConfirmacao ?? string.Empty).Trim();
        if (chave.Length == 0)
            throw new ValidacaoException("autorizacao.chave_obrigatoria", "Informe a chave de autorização.");
        // Mesma régua da regulação usada no cadastro/edição (0000 emergencial ou ≥ 9999).
        if (!Validators.RegulacaoRegras.Valido(chave))
            throw new ValidacaoException("autorizacao.chave_invalida", Validators.RegulacaoRegras.MensagemInvalido);

        var s = await _db.ExamesImagem
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);
        var reg = s.Solicitacao!;

        // Gate: paciente precisa ter um número VERIFICADO (marcador no telecom do Patient FHIR)
        // OU uma dispensa registrada. A dispensa existe porque o gate rígido travava o balcão:
        // quem não tem celular, ou não consegue confirmar o código, ficava sem caminho de saída.
        var paciente = await _pacienteResolver.ResolverAsync(reg.PacienteId, cancellationToken);
        if (paciente?.TelefoneVerificado is null
            && await _dispensasContato.ObterAtivaAsync(reg.PacienteId, cancellationToken) is null)
            throw new ValidacaoException(
                "autorizacao.sem_numero_verificado",
                "O paciente ainda não tem um número de telefone verificado. Verifique o contato — ou, " +
                "se ele não puder validar, registre a dispensa com o motivo antes de autorizar.");

        // Gate da estação: a recepção é quem sabe em qual sala o paciente vai entrar. Com duas ou
        // mais na modalidade, a escolha é EXIGIDA aqui — depois de autorizar, o envio é do worker
        // e não há mais ninguém para perguntar. Nenhum equipamento cadastrado NÃO bloqueia: isso é
        // cadastro de administrador, e travar a recepção deixaria o paciente parado no balcão; o
        // exame é autorizado e o erro "Sem equipamento configurado" aparece no envio.
        if (s.Status == StatusSolicitacaoExame.Solicitada && (s.TipoExame?.EnviarParaWorklist ?? false))
        {
            var candidatos = await _estacaoWorklist.ListarCandidatosAsync(s, cancellationToken);

            if (equipamentoId is { } escolhido)
            {
                if (candidatos.All(c => c.Id != escolhido))
                    throw new ValidacaoException("autorizacao.equipamento_invalido",
                        "O equipamento selecionado não atende esta unidade/modalidade.");
                s.EquipamentoId = escolhido;
            }
            else if (candidatos.Count > 1)
            {
                var nomes = string.Join(", ", candidatos.Select(c => c.Nome));
                throw new ConflitoException("autorizacao.equipamento_obrigatorio",
                    $"Esta unidade tem mais de um equipamento para a modalidade do exame ({nomes}). " +
                    "Selecione em qual o exame será realizado.");
            }
            else if (candidatos.Count == 1)
            {
                // Registra a estação mesmo quando só há uma: a decisão fica explícita no exame,
                // e um equipamento novo cadastrado depois não muda o destino deste pedido.
                s.EquipamentoId = candidatos[0].Id;
            }
        }

        var agora = DateTime.UtcNow;
        reg.ChaveConfirmacao = chave;
        reg.AutorizadoEm = agora;
        reg.AutorizadoPor = _usuarioAtual.UsuarioId;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Presença física vence qualquer estado anterior: sem resposta → confirma presencial;
        // cancelou pelo WhatsApp mas compareceu → "revive" (Confirmada, canal presencial).
        if (reg.StatusConfirmacao != StatusConfirmacaoAgendamento.Confirmada)
        {
            reg.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
            reg.ConfirmadoEm = agora;
            reg.ConfirmadoCanal = "presencial";
            reg.ConfirmacaoCanceladaEm = null;
            reg.MotivoCancelamentoPaciente = null;
        }

        // Só AGORA enfileira o envio ao PACS (se o tipo envia à worklist e ainda não foi enviado).
        var impedimento = s.Status == StatusSolicitacaoExame.Solicitada ? ImpedimentoEnvioPacs(s, reg) : null;

        if (s.Status == StatusSolicitacaoExame.Solicitada)
        {
            if (impedimento is null)
            {
                s.ProximaTentativaEm = agora;
                s.ErroIntegracaoPacs = null;
            }
            else
            {
                // Autorizar sem poder enviar NÃO passa em silêncio: a recepção liberava o paciente
                // achando que o exame estava na worklist do aparelho. Carimba o motivo no exame (a
                // tela mostra) e abre um erro rastreável em Sistema → Erros.
                s.ProximaTentativaEm = null;
                s.ErroIntegracaoPacs = impedimento;
            }
            s.AtualizadoEm = agora;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (impedimento is not null)
            await ReportarImpedimentoAsync(s, reg, impedimento, cancellationToken);
    }

    /// <summary>
    /// Motivo pelo qual um exame autorizado NÃO chegará à worklist — null quando o caminho está
    /// livre. Cobre os dois furos já vistos em produção, ambos silenciosos: exame importado sem
    /// TipoExame (código SIGTAP do SISREG não bate com nenhum cadastrado) e tipo com o envio à
    /// worklist desligado. Nos dois casos o worker nem enxerga a linha — ele filtra por
    /// <c>TipoExame.EnviarParaWorklist</c>, que é INNER JOIN e descarta quem não tem tipo.
    /// </summary>
    private static string? ImpedimentoEnvioPacs(ExameImagem s, Solicitacao reg)
    {
        if (s.TipoExameId is null)
            return $"Exame sem tipo mapeado — o procedimento SIGTAP {reg.ProcedimentoSigtapCodigo ?? "(não informado)"} " +
                   $"(\"{reg.ProcedimentoTexto}\") não está vinculado a nenhum tipo de exame. " +
                   "Vincule em Exames de Imagem → Mapeamento SIGTAP para que o exame vá à worklist.";

        if (!(s.TipoExame?.EnviarParaWorklist ?? false))
            return $"O tipo de exame \"{s.TipoExame?.Nome}\" está com o envio à worklist desligado. " +
                   "Ligue em Exames de Imagem → Tipos de Exame para que o exame chegue ao equipamento.";

        return null;
    }

    private async Task ReportarImpedimentoAsync(
        ExameImagem s, Solicitacao reg, string motivo, CancellationToken ct)
    {
        _logger.LogError(
            "Exame {Accession} autorizado mas NÃO será enviado ao PACS: {Motivo}", s.AccessionNumber, motivo);

        try
        {
            await _registroErros.RegistrarAsync(new Erros.Dtos.RegistrarErroDados(
                Metodo: "POST",
                Caminho: $"/solicitacoes-exame/{s.Id}/autorizar",
                QueryString: null,
                StatusCode: 409,
                TipoExcecao: "EnvioWorklistImpedido",
                Mensagem: $"Exame {s.AccessionNumber} autorizado sem poder ir à worklist. {motivo}",
                StackTrace: null,
                Interna: $"SIGTAP={reg.ProcedimentoSigtapCodigo}; procedimento={reg.ProcedimentoTexto}",
                TraceId: null,
                UserAgent: null), ct);
        }
        catch (Exception ex)
        {
            // Best-effort: falha ao registrar não pode derrubar a autorização (paciente no balcão).
            _logger.LogWarning(ex, "Falha ao registrar o impedimento de envio de {Accession}.", s.AccessionNumber);
        }
    }

    public async Task<IReadOnlyList<EquipamentoExameDto>> ListarEquipamentosDisponiveisAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.AsNoTracking()
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);

        var candidatos = await _estacaoWorklist.ListarCandidatosAsync(s, cancellationToken);
        return [.. candidatos.Select(c => new EquipamentoExameDto(c.Id, c.Nome, c.AeTitle, c.Id == s.EquipamentoId))];
    }

    public async Task<Guid> CadastrarAsync(CadastrarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        await ValidarReferenciasAsync(request.PacienteId, request.TipoExameId, request.UnidadeId, request.UnidadeSolicitanteId, cancellationToken);

        var accession = await _geradorIds.ProximoAccessionAsync(cancellationToken);
        var studyUid = _geradorIds.NovoStudyInstanceUid();
        var agora = DateTime.UtcNow;

        // Espinha de regulação (categoria Imagem) + satélite de execução.
        var solicitacao = new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            PacienteId = request.PacienteId,
            Categoria = CategoriaSolicitacao.Imagem,
            UnidadeExecutanteId = request.UnidadeId,
            UnidadeSolicitanteId = request.UnidadeSolicitanteId,
            // Solicitante = só o nome (texto); colunas de conselho/usuário ficam nos defaults.
            SolicitanteNome = request.SolicitanteNome.Trim(),
            CodigoSolicitacao = NormalizaOpcional(request.CodigoSolicitacao),
            ChaveConfirmacao = NormalizaOpcional(request.ChaveConfirmacao),
            Justificativa = NormalizaOpcional(request.Justificativa),
            Status = StatusSolicitacao.Solicitada,
            Prioridade = request.Prioridade,
            Observacoes = NormalizaOpcional(request.Observacoes),
            DataSolicitacao = request.DataSolicitacao,
            DataAgendada = request.DataAgendada,
            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };
        var exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            Solicitacao = solicitacao,
            AccessionNumber = accession,
            StudyInstanceUID = studyUid,
            TipoExameId = request.TipoExameId,
            Status = StatusSolicitacaoExame.Solicitada,
            // NADA vai ao PACS automaticamente: o envio só é enfileirado quando a recepção AUTORIZA.
            ProximaTentativaEm = null,
            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.Solicitacoes.Add(solicitacao);
        _db.ExamesImagem.Add(exame);
        // Confirmação por WhatsApp — só enfileira (o worker envia). Sem data futura, no-op.
        await _comunicacoes.Value.EnfileirarAsync(
            solicitacao, FinalidadeComunicacao.ConfirmacaoAgendamento, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Id público do exame = ExameImagem.Id (preservado).
        return exame.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);
        var reg = s.Solicitacao!;

        if (s.Status != StatusSolicitacaoExame.Solicitada)
        {
            throw new ConflitoException(
                "solicitacaoExame.imutavel",
                "Solicitação só pode ser editada enquanto está no status 'Solicitada'.");
        }

        await ValidarReferenciasAsync(reg.PacienteId, request.TipoExameId, request.UnidadeId, request.UnidadeSolicitanteId, cancellationToken);

        var agora = DateTime.UtcNow;
        s.TipoExameId = request.TipoExameId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        reg.UnidadeExecutanteId = request.UnidadeId;
        reg.UnidadeSolicitanteId = request.UnidadeSolicitanteId;
        reg.SolicitanteNome = request.SolicitanteNome.Trim();
        reg.CodigoSolicitacao = NormalizaOpcional(request.CodigoSolicitacao);
        reg.ChaveConfirmacao = NormalizaOpcional(request.ChaveConfirmacao);
        reg.Justificativa = NormalizaOpcional(request.Justificativa);
        reg.Prioridade = request.Prioridade;
        reg.Observacoes = NormalizaOpcional(request.Observacoes);
        reg.DataSolicitacao = request.DataSolicitacao;
        reg.DataAgendada = request.DataAgendada;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Cobre "criou sem data, agendou depois": enfileira confirmação (idempotente).
        await _comunicacoes.Value.EnfileirarAsync(
            reg, FinalidadeComunicacao.ConfirmacaoAgendamento, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancelarSolicitacaoExameRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);

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

        // Remove o item da worklist no dcm4chee (best-effort — não derruba o cancelamento local).
        // Se falhar, o campo continua preenchido e o worker refaz a remoção depois: o exame
        // cancelado entra na fila de limpeza justamente por ter WorklistItemUid.
        if (!string.IsNullOrEmpty(s.WorklistItemUid))
        {
            try
            {
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
                s.WorklistItemUid = null; // espelho do que está no PACS
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Falha ao remover MWL de {Accession} no cancelamento — segue cancelado localmente; worker retenta.",
                    s.AccessionNumber);
            }
        }

        var agora = DateTime.UtcNow;
        s.Status = StatusSolicitacaoExame.Cancelada;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Espinha reflete o cancelamento (é decisão de regulação).
        var reg = s.Solicitacao!;
        reg.Status = StatusSolicitacao.Cancelada;
        reg.CanceladoEm = agora;
        reg.CanceladoPorUsuarioId = _usuarioAtual.UsuarioId;
        reg.MotivoCancelamento = motivo;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReenviarWorklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);

        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida))
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_reenviavel",
                $"Só é possível reenviar worklist em 'Solicitada', 'Enviada' ou 'Recebida'. Atual: {s.Status}.");
        }

        // Gate de autorização (na espinha): Solicitada que nunca foi ao PACS só entra na fila
        // depois que a recepção autorizar. (Enviada/Recebida já estão no PACS — reenvio = manutenção.)
        if (s.Status == StatusSolicitacaoExame.Solicitada && s.Solicitacao!.AutorizadoEm is null)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_autorizada",
                "Este exame ainda não foi autorizado pela recepção. Autorize com a chave de confirmação antes de enviar ao PACS.");
        }

        s.ProximaTentativaEm = DateTime.UtcNow;
        s.ErroIntegracaoPacs = null;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, bool force, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);

        // Exame iniciado/realizado/laudado tem imagens — o pedido não se exclui por aqui.
        if (s.Status is StatusSolicitacaoExame.EmExecucao or StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_excluivel",
                $"Solicitação no status '{s.Status}' (exame iniciado/realizado) não pode ser excluída.");
        }

        // Anti-lixo: remove o item da worklist no dcm4chee e confirma; só então apaga localmente.
        if (force)
        {
            try
            {
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
                s.WorklistItemUid = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Campo fica preenchido de propósito: exame excluído com item pendente continua
                // elegível para a fila de limpeza do worker, que retenta até o PACS aceitar.
                _logger.LogWarning(ex,
                    "Exclusão forçada de {Accession} — falha ao remover MWL no dcm4chee; worker retenta.",
                    s.AccessionNumber);
            }
        }
        else
        {
            try
            {
                await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
                s.WorklistItemUid = null;
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
        // Soft-delete espelhado nas duas tabelas (execução + regulação).
        s.ExcluidoEm = agora;
        s.ExcluidoPor = _usuarioAtual.UsuarioId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;
        s.ProximaTentativaEm = null; // tira do worker

        var reg = s.Solicitacao!;
        reg.ExcluidoEm = agora;
        reg.ExcluidoPor = _usuarioAtual.UsuarioId;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoLaudadaAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        var uid = (studyInstanceUID ?? string.Empty).Trim();
        if (uid.Length == 0) return;

        var s = await _db.ExamesImagem.FirstOrDefaultAsync(
            x => x.StudyInstanceUID == uid && x.ExcluidoEm == null, cancellationToken);
        // Study próprio da máquina (sem worklist): resolve pela associação explícita.
        if (s is null)
        {
            var exameId = await _db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null)
                .Select(a => (Guid?)a.ExameImagemId)
                .FirstOrDefaultAsync(cancellationToken);
            if (exameId is { } eid)
                s = await _db.ExamesImagem.FirstOrDefaultAsync(
                    x => x.Id == eid && x.ExcluidoEm == null, cancellationToken);
        }
        if (s is null) return; // study sem exame amarrado — ok.

        if (s.Status is StatusSolicitacaoExame.Cancelada or StatusSolicitacaoExame.Laudada) return;

        s.Status = StatusSolicitacaoExame.Laudada;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarComoRealizadaAsync(Guid id, DateTime realizadoEm, DateTime? dataEstudo, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken);
        if (s is null) return;
        if (s.Status is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada) return;

        var agora = DateTime.UtcNow;
        s.Status = StatusSolicitacaoExame.Realizada;
        s.RealizadoEm = realizadoEm; // hora de detecção pelo servidor (auditoria)
        // Data REAL do exame vinda do DICOM (fonte da verdade). Só grava quando o PACS trouxe a tag.
        if (dataEstudo is not null) s.DataEstudo = dataEstudo;
        s.AtualizadoEm = agora;

        // Espinha reflete a realização.
        var reg = s.Solicitacao!;
        reg.Status = StatusSolicitacao.Realizada;
        reg.AtualizadoEm = agora;

        // Exame chegou → enfileira o aviso "Exame liberado" (idempotente; o worker só envia quando
        // a chave EnviarExameLiberado estiver ligada — template aguardando a Meta).
        await _comunicacoes.Value.EnfileirarAsync(
            reg, FinalidadeComunicacao.ExameLiberado, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Notifica (whatsapp/push no futuro; log no MVP).
        await _notificador.NotificarRealizadoAsync(s, cancellationToken);
    }

    public async Task ProcessarTentativaEnvioAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        // solicitacaoId aqui é o id PÚBLICO do exame (= ExameImagem.Id).
        var s = await _db.ExamesImagem
            .Include(x => x.TipoExame).ThenInclude(t => t!.ProcedimentoSigtap)
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, cancellationToken);

        if (s is null) return;

        // Só os estados de envio interessam ao worker (Recebida já é terminal).
        if (s.Status is not (StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada))
        {
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
                        s.ProximaTentativaEm = null; // terminal — worklist disponível para a máquina
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

    public async Task ProcessarLimpezaWorklistAsync(Guid exameId, CancellationToken cancellationToken = default)
    {
        // Sem o filtro de ExcluidoEm de propósito: exame soft-deleted também precisa sair da
        // worklist do equipamento (a exclusão tenta remover na hora, mas pode ter falhado).
        var s = await _db.ExamesImagem.Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == exameId, cancellationToken);

        if (s is null || s.WorklistItemUid is null) return;
        if (!DeveSairDaWorklist(s)) return;

        var agora = DateTime.UtcNow;
        try
        {
            await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);

            // Confirmado (404 do dcm4chee também conta como removido) — o espelho zera.
            s.WorklistItemUid = null;
            s.ProximaTentativaEm = null;
            s.AtualizadoEm = agora;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Worklist de {Accession} removida do PACS (exame {Status}).", s.AccessionNumber, s.Status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PACS fora/lento: tenta de novo daqui a pouco. Backoff fixo e curto — diferente do
            // envio, aqui não há paciente esperando, e o item some assim que o PACS responder.
            s.ProximaTentativaEm = agora.AddMinutes(5);
            s.AtualizadoEm = agora;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex,
                "Falha ao remover MWL de {Accession} — nova tentativa em 5 min.", s.AccessionNumber);
        }
    }

    /// <summary>
    /// Exame que TEM item de worklist mas não deveria: já executado (Realizada/Laudada),
    /// cancelado, ou excluído. Agendado-e-não-realizado (faltoso) fica na lista — sai só por
    /// decisão humana (cancelamento/exclusão).
    /// </summary>
    internal static bool DeveSairDaWorklist(ExameImagem e) =>
        e.ExcluidoEm != null
        || e.Status is StatusSolicitacaoExame.Realizada
                    or StatusSolicitacaoExame.Laudada
                    or StatusSolicitacaoExame.Cancelada;

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

    private async Task<ExameImagem?> CarregarCompletoAsync(
        System.Linq.Expressions.Expression<Func<ExameImagem, bool>> filtro,
        CancellationToken cancellationToken)
    {
        return await _db.ExamesImagem.AsNoTracking()
            .Include(e => e.TipoExame)
            .Include(e => e.Equipamento)
            .Include(e => e.Solicitacao!).ThenInclude(so => so.UnidadeExecutante)
            .Include(e => e.Solicitacao!).ThenInclude(so => so.UnidadeSolicitante)
            .Where(e => e.ExcluidoEm == null && e.Solicitacao!.ExcluidoEm == null)
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

    public async Task AlterarEquipamentoDestinoAsync(
        Guid id, Guid equipamentoId, CancellationToken cancellationToken = default)
    {
        var s = await _db.ExamesImagem
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);

        // 1) Elegibilidade (regra de negócio, não do PACS): só faz sentido trocar a sala ENQUANTO o
        //    exame não foi executado. Com imagem já adquirida (em execução/realizado/laudado) ou
        //    terminal (cancelado), a troca é proibida — não decide "há item a apagar" (isso é o PACS).
        if (s.Status is StatusSolicitacaoExame.EmExecucao or StatusSolicitacaoExame.Realizada
                or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada
            || s.RealizadoEm is not null)
        {
            throw new ConflitoException(
                "solicitacaoExame.equipamento_nao_alteravel",
                $"Não é possível trocar o equipamento de um exame no status '{s.Status}' (já em execução/realizado/cancelado).");
        }

        if (!(s.TipoExame?.EnviarParaWorklist ?? false))
        {
            throw new ConflitoException(
                "solicitacaoExame.sem_worklist",
                "Este tipo de exame não é enviado à worklist; não há equipamento de destino a trocar.");
        }

        // Não fura o gate de autorização: um exame Solicitada que nunca foi autorizado pela recepção
        // não pode ir ao PACS por esta via (mesma régua do reenvio). Enviada/Recebida já estão lá.
        if (s.Status == StatusSolicitacaoExame.Solicitada && s.Solicitacao!.AutorizadoEm is null)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_autorizada",
                "Este exame ainda não foi autorizado pela recepção. Autorize com a chave antes de trocar o equipamento de destino.");
        }

        // 2) Valida o destino contra os candidatos (unidade executante + modalidade) — mesma régua da
        //    autorização. Rejeita destino inválido e no-op (já é a estação atual).
        var candidatos = await _estacaoWorklist.ListarCandidatosAsync(s, cancellationToken);
        if (candidatos.All(c => c.Id != equipamentoId))
            throw new ValidacaoException("solicitacaoExame.equipamento_invalido",
                "O equipamento selecionado não atende esta unidade/modalidade.");
        if (s.EquipamentoId == equipamentoId)
            throw new ConflitoException("solicitacaoExame.equipamento_inalterado",
                "O exame já está destinado a este equipamento.");

        // 3) FONTE DA VERDADE É O PACS, não o Status/WorklistItemUid (espelho, que o worker atualiza em
        //    varredura e pode estar dessincronizado). SEMPRE consulta o dcm4chee: se houver item na sala
        //    atual, remove e CONFIRMA a remoção antes de recriar — nunca cria com o antigo ainda vivo,
        //    nunca omite a exclusão por confiar no status. PACS indisponível aqui aborta sem tocar nada.
        if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
        {
            await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
            s.WorklistItemUid = null;

            if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
                throw new ConflitoException("solicitacaoExame.remocao_nao_confirmada",
                    "Removi o item da worklist, mas o PACS ainda o lista. Aguarde um instante e tente de novo.");
        }

        // 4) Grava o novo destino (agora que a sala antiga está comprovadamente limpa).
        var agora = DateTime.UtcNow;
        s.EquipamentoId = equipamentoId;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        // 5) Cria no destino correto e VERIFICA a presença. Se a criação falhar depois do delete, não
        //    deixa órfão nem estado travado: zera o espelho, marca o erro e reenfileira — o
        //    EnviadorWorklistService recria pela via resiliente (reversível, sem meio-caminho no PACS).
        try
        {
            s.WorklistItemUid = await _mwlClient.CriarOuAtualizarMwlItemAsync(s, cancellationToken);
            s.ErroIntegracaoPacs = null;

            if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
            {
                s.Status = StatusSolicitacaoExame.Recebida; // confirmado na worklist consultável
                s.ProximaTentativaEm = null;
            }
            else
            {
                s.Status = StatusSolicitacaoExame.Enviada; // criado; worker fecha Enviada→Recebida
                s.ProximaTentativaEm = agora;
            }
        }
        catch (ConflitoException ex)
        {
            _logger.LogWarning(ex,
                "Troca de equipamento de {Accession}: item antigo removido, mas a criação no novo destino falhou; worker recria.",
                s.AccessionNumber);
            s.WorklistItemUid = null;
            s.Status = StatusSolicitacaoExame.Solicitada;
            s.ErroIntegracaoPacs = ex.Message;
            s.ProximaTentativaEm = agora;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Troca a UNIDADE EXECUTANTE de um exame (ticket #92). Espelha a régua PACS-first da troca de
    /// equipamento: se houver item na worklist do dcm4chee, remove e CONFIRMA a remoção antes de
    /// efetuar a troca — a existência real no PACS manda, não o status local. Ao trocar, o exame
    /// VOLTA AO ESTADO ZERO na nova unidade — perde o equipamento escolhido e a autorização da
    /// recepção — e só reentra na worklist pelo fluxo normal (a recepção da nova unidade readmite/
    /// autoriza via <see cref="AutorizarAsync"/>). Proibido depois que a imagem já voltou do PACS
    /// (em execução/realizado/laudado/cancelado ou <c>RealizadoEm</c> preenchido). Registra na
    /// trilha de auditoria, que também alimenta a linha do tempo do detalhe.
    /// </summary>
    public async Task AlterarUnidadeExecutanteAsync(
        Guid id, Guid novaUnidadeId, string motivo, CancellationToken cancellationToken = default)
    {
        var justificativa = (motivo ?? string.Empty).Trim();
        if (justificativa.Length == 0)
            throw new ValidacaoException("solicitacaoExame.motivo_obrigatorio",
                "Informe o motivo da alteração da unidade executante.");

        var s = await _db.ExamesImagem
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), id);
        var reg = s.Solicitacao!;

        // 1) Elegibilidade (regra do ticket): só bloqueia quando a imagem JÁ voltou do PACS. Nos
        //    status anteriores (Solicitada/Enviada/Recebida) a troca é permitida.
        if (s.Status is StatusSolicitacaoExame.EmExecucao or StatusSolicitacaoExame.Realizada
                or StatusSolicitacaoExame.Laudada or StatusSolicitacaoExame.Cancelada
            || s.RealizadoEm is not null)
        {
            throw new ConflitoException(
                "solicitacaoExame.unidade_nao_alteravel",
                $"Não é possível alterar a unidade executante de um exame no status '{s.Status}' (imagem já recebida do PACS).");
        }

        // 2) Valida a nova unidade e rejeita no-op.
        if (!await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == novaUnidadeId, cancellationToken))
            throw new NaoEncontradoException(nameof(Unidade), novaUnidadeId);
        if (reg.UnidadeExecutanteId == novaUnidadeId)
            throw new ConflitoException("solicitacaoExame.unidade_inalterada",
                "O exame já está nesta unidade executante.");

        var unidadeAntigaNome = await _db.Unidades.AsNoTracking()
            .Where(u => u.Id == reg.UnidadeExecutanteId).Select(u => u.Nome)
            .FirstOrDefaultAsync(cancellationToken);
        var unidadeNovaNome = await _db.Unidades.AsNoTracking()
            .Where(u => u.Id == novaUnidadeId).Select(u => u.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        // 3) FONTE DA VERDADE É O PACS: se houver item na worklist do dcm4chee, remove e CONFIRMA a
        //    remoção ANTES de trocar. PACS indisponível/recusa aqui aborta sem tocar em nada.
        if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
        {
            await _mwlClient.ExcluirMwlItemAsync(s, cancellationToken);
            s.WorklistItemUid = null;

            if (await _mwlClient.MwlItemExisteAsync(s, cancellationToken))
                throw new ConflitoException("solicitacaoExame.remocao_nao_confirmada",
                    "Removi o item da worklist, mas o PACS ainda o lista. Aguarde um instante e tente de novo.");
        }

        // 4) Troca + "estado zero" na nova unidade: sem equipamento, sem autorização, fora da
        //    worklist e SEM tentativa agendada. Só reentra na worklist pelo processo normal — a
        //    recepção da nova unidade readmite/autoriza. A partir daqui a solicitação já aparece na
        //    lista da nova unidade (a listagem filtra por UnidadeExecutanteId).
        var agora = DateTime.UtcNow;
        reg.UnidadeExecutanteId = novaUnidadeId;
        reg.AutorizadoEm = null;
        reg.AutorizadoPor = null;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        s.EquipamentoId = null;
        s.WorklistItemUid = null;
        s.Status = StatusSolicitacaoExame.Solicitada;
        s.ProximaTentativaEm = null;
        s.ErroIntegracaoPacs = null;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);

        // 5) Trilha de auditoria (também alimenta a linha do tempo do detalhe). O motivo entra no
        //    valor novo para ficar visível na tela de Auditoria e no histórico. Só audita depois de
        //    a troca persistir.
        await _auditoria.RegistrarAsync(
            "SolicitacaoExame", id.ToString(), "AlteracaoUnidadeExecutante",
            unidadeAntigaNome,
            $"{unidadeNovaNome} — Motivo: {justificativa}",
            cancellationToken);
    }
}
