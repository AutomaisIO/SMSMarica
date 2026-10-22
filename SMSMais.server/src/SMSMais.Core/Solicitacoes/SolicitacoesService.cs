using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Solicitacoes;

/// <summary>
/// Serviço do ciclo de vida de EXAMES DE IMAGEM (ADR-0021). Opera sobre o satélite
/// <see cref="ExameImagem"/> (cujo <c>Id</c> é o id PÚBLICO do exame, preservado do modelo antigo)
/// e acessa a regulação pela navegação <see cref="ExameImagem.Solicitacao"/>. O status de EXECUÇÃO
/// (worklist/PACS) vive em <c>ExameImagem.Status</c>; a espinha <c>Solicitacao.Status</c> é o resumo
/// de regulação, sincronizado nas transições Cancelada/Realizada.
/// </summary>
public sealed class SolicitacoesService(
    SmsMaisDbContext db,
    IGeradorIdentificadores geradorIds,
    IDcm4cheeMwlClient mwlClient,
    IResolvedorEstacaoWorklist estacaoWorklist,
    IEscopoExameUnidade escopoExame,
    INotificadorExame notificador,
    IUsuarioAtualAccessor usuarioAtual,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
    Pacientes.IPacientesService pacientes,
    Telefones.IDispensaContatoService dispensasContato,
    Lazy<Laudos.Assinatura.ILaudoAssinaturaService> assinaturas,
    // Lazy: quebra o ciclo Solicitacoes → Comunicacao → LoginLink → Solicitacoes.
    Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> comunicacoes,
    Erros.IRegistroErroService registroErros,
    Auditoria.IAuditoriaService auditoria,
    Common.Cid.ICidCatalogoService cidCatalogo,
    ILogger<SolicitacoesService> logger,
    // Opcional: só precisa do banco, e os testes que montam o serviço à mão seguem compilando.
    Equipamentos.IRegrasDoExameService? regrasDoExame = null)
    : ISolicitacoesService
{
    private readonly Equipamentos.IRegrasDoExameService _regrasDoExame =
        regrasDoExame ?? new Equipamentos.RegrasDoExameService(db);
    private readonly SmsMaisDbContext _db = db;
    private readonly IGeradorIdentificadores _geradorIds = geradorIds;
    private readonly IDcm4cheeMwlClient _mwlClient = mwlClient;
    private readonly IResolvedorEstacaoWorklist _estacaoWorklist = estacaoWorklist;
    private readonly IEscopoExameUnidade _escopoExame = escopoExame;
    private readonly Pacientes.IPacientesService _pacientes = pacientes;
    private readonly INotificadorExame _notificador = notificador;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;
    private readonly Pacientes.Fhir.IPacienteResolver _pacienteResolver = pacienteResolver;
    private readonly Telefones.IDispensaContatoService _dispensasContato = dispensasContato;
    // Lazy: ponto de entrada do subsistema de Laudos. Sem isso a construção do
    // SolicitacoesService puxa Assinatura → PdfRenderer → Laudos, que reentra
    // aqui por várias arestas (direta e via ExameAssociacao) — dependência circular.
    private readonly Lazy<Laudos.Assinatura.ILaudoAssinaturaService> _assinaturas = assinaturas;
    private readonly Lazy<Notificacoes.Comunicacao.IComunicacaoPacienteService> _comunicacoes = comunicacoes;
    private readonly Erros.IRegistroErroService _registroErros = registroErros;
    private readonly Auditoria.IAuditoriaService _auditoria = auditoria;
    private readonly Common.Cid.ICidCatalogoService _cidCatalogo = cidCatalogo;
    private readonly ILogger<SolicitacoesService> _logger = logger;

    // Resolve nome/CPF/CNS do paciente (hub FHIR) e embute nos DTOs.
    private async Task<IReadOnlyList<SolicitacaoListItemDto>> EnriquecerAsync(
        List<SolicitacaoListItemDto> dtos, CancellationToken ct)
    {
        var nomes = await _pacienteResolver.ResolverManyAsync(dtos.Select(d => d.PacienteId), ct);
        return [.. dtos.Select(d => nomes.TryGetValue(d.PacienteId, out var r)
            ? d with { PacienteNome = r.Nome, PacienteCpf = r.Cpf } : d)];
    }

    private async Task<SolicitacaoDto> EnriquecerAsync(SolicitacaoDto dto, CancellationToken ct)
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

        // Diagnóstico inicial (CID-10 do SISREG) por extenso — a médica abre a ficha para saber o
        // tipo de laudo (ticket #155). Só no detalhe; a listagem não precisa. Best-effort: código
        // fora do catálogo fica só com o código.
        if (!string.IsNullOrWhiteSpace(enriquecido.CidCodigo)
            && await _cidCatalogo.DescricaoAsync(enriquecido.CidCodigo, ct) is { } cidDesc)
            enriquecido = enriquecido with { CidDescricao = cidDesc };

        // Todos os procedimentos do pedido (ticket #149). Itens só existem quando o SISREG mandou
        // 2+ procedimentos com o mesmo nº; sem eles, fica o item único que o mapper montou da espinha.
        var itens = await _db.SolicitacaoProcedimentos.AsNoTracking()
            .Where(p => p.SolicitacaoId == dto.SolicitacaoId && p.ExcluidoEm == null)
            .OrderBy(p => p.Ordem)
            .ToListAsync(ct);
        if (itens.Count > 0)
            enriquecido = enriquecido with { Procedimentos = [.. itens.Select(SolicitacoesMapper.ParaDto)] };

        // Regra do aparelho (ADR-0075): liberação ao paciente e anamnese. Só exame de imagem — o id
        // público do exame é o da linha.
        if (await _db.ExamesImagem.AsNoTracking()
                .Where(e => e.Id == dto.Id && e.ExcluidoEm == null)
                .Select(e => new { e.Status, e.LiberadoPacienteEm })
                .FirstOrDefaultAsync(ct) is { } doExame)
        {
            var regra = await _regrasDoExame.ResolverAsync(dto.Id, ct);
            var realizado = doExame.Status is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada;
            enriquecido = enriquecido with
            {
                LiberadoPacienteEm = doExame.LiberadoPacienteEm,
                RetidoAteLaudo = realizado && doExame.LiberadoPacienteEm is null
                    && !await ExameTemLaudoAssinadoAsync(dto.Id, ct),
                AparelhoRegraNome = regra.EquipamentoNome,
                LiberacaoPaciente = regra.Liberacao,
                UsaAnamnese = regra.UsaAnamnese,
            };
        }

        // Em pedido de imagem, cada procedimento tem o seu exame (ADR-0074): o detalhe mostra o
        // accession e a situação de cada um, e o selo "vinculado a" com os outros exames.
        var exames = await _db.ExamesImagem.AsNoTracking()
            .Where(e => e.SolicitacaoId == dto.SolicitacaoId && e.ExcluidoEm == null)
            .OrderBy(e => e.Ordem)
            .Select(e => new { e.Id, e.Ordem, e.AccessionNumber, e.Status, e.SolicitacaoProcedimentoId })
            .ToListAsync(ct);
        if (exames.Count > 0 && enriquecido.Procedimentos is { } procedimentos)
        {
            var principal = exames.FirstOrDefault(e => e.Ordem == 0);
            var itemPorOrdem = itens.ToDictionary(i => i.Ordem, i => i.Id);
            enriquecido = enriquecido with
            {
                Procedimentos = [.. procedimentos.Select(p =>
                {
                    var exame = p.Ordem == 1
                        ? principal
                        : itemPorOrdem.TryGetValue(p.Ordem, out var itemId)
                            ? exames.FirstOrDefault(e => e.SolicitacaoProcedimentoId == itemId)
                            : null;
                    return exame is null
                        ? p
                        : p with { ExameId = exame.Id, AccessionNumber = exame.AccessionNumber, StatusExame = exame.Status };
                })],
                AccessionsVinculados = exames.Count > 1
                    ? [.. exames.Where(e => e.Id != dto.Id).Select(e => e.AccessionNumber)]
                    : null,
            };
        }

        return enriquecido;
    }

    /// <summary>Existe um laudo (última versão finalizada) ASSINADO para o estudo deste exame?</summary>
    private async Task<bool> ExameTemLaudoAssinadoAsync(Guid exameId, CancellationToken ct) =>
        await LaudoOficialDoExameAsync(exameId, ct) is not null;

    public async Task<byte[]?> ObterLaudoOficialPdfAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        if (!await _db.ExamesImagem.AsNoTracking().AnyAsync(e => e.Id == solicitacaoExameId && e.ExcluidoEm == null, cancellationToken))
            throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        var laudoId = await LaudoOficialDoExameAsync(solicitacaoExameId, cancellationToken);
        if (laudoId is null) return null;

        return await _db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.Concluida && a.PdfAssinado != null)
            .Select(a => a.PdfAssinado)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Laudo OFICIAL do exame: a versão atual (maior versão finalizada) de um dos estudos do
    /// exame que tenha assinatura concluída — mesma régua do botão "Ver laudo" da listagem.
    /// Study = o do próprio exame + os das associações ativas. Null = não há laudo liberado.
    /// </summary>
    private async Task<Guid?> LaudoOficialDoExameAsync(Guid exameId, CancellationToken ct)
    {
        var studyProprio = await _db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == exameId).Select(e => e.StudyInstanceUID).FirstOrDefaultAsync(ct);
        var studyAssoc = await _db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExameImagemId == exameId && a.ExcluidoEm == null)
            .Select(a => a.StudyInstanceUID).ToListAsync(ct);
        var studies = studyAssoc.Append(studyProprio ?? string.Empty)
            .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToArray();
        if (studies.Length == 0) return null;

        var laudos = await _db.Laudos.AsNoTracking()
            .Where(l => !l.Excluido && l.Status == StatusLaudo.Finalizado && studies.Contains(l.StudyInstanceUID))
            .Select(l => new { l.Id, l.StudyInstanceUID, l.Versao })
            .ToListAsync(ct);
        if (laudos.Count == 0) return null;

        var atuais = laudos
            .GroupBy(l => l.StudyInstanceUID)
            .Select(g => g.OrderByDescending(x => x.Versao).First().Id)
            .ToArray();
        var assinados = await _assinaturas.Value.QuaisAssinadosAsync(atuais, ct);
        return atuais.Where(assinados.Contains).Select(id => (Guid?)id).FirstOrDefault();
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
    private async Task<IReadOnlyList<SolicitacaoListItemDto>> EnriquecerLaudosAsync(
        List<SolicitacaoListItemDto> dtos, CancellationToken ct)
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
        SolicitacaoListItemDto d, Dictionary<Guid, List<string>> assocPorSolicitacao)
    {
        if (assocPorSolicitacao.TryGetValue(d.Id, out var associados))
            foreach (var study in associados) yield return study;
        if (!string.IsNullOrEmpty(d.StudyInstanceUID)) yield return d.StudyInstanceUID;
    }

    public async Task<PaginaSolicitacoesDto> ListarAsync(
        FiltroSolicitacoesDto filtro,
        CancellationToken cancellationToken = default)
    {
        var tamanho = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        // A lista parte da ESPINHA: toda solicitação regulada (exame de imagem, consulta,
        // laboratório…). O exame de imagem entra por LEFT JOIN — quando existe, precisa estar vivo;
        // pedido de imagem sem exame não aparece (não teria o que executar).
        // Pedido com mais de um código interno (ADR-0074) tem um exame por código: a página conta
        // PEDIDOS, e cada exame vivo vira uma linha (ver LinhasDoPedido).
        IQueryable<Solicitacao> query = _db.Solicitacoes.AsNoTracking()
            .Include(s => s.UnidadeExecutante)
            .Include(s => s.ExameImagem!).ThenInclude(e => e.TipoExame)
            .Include(s => s.ExamesImagem).ThenInclude(e => e.TipoExame)
            .Include(s => s.ExamesImagem).ThenInclude(e => e.SolicitacaoProcedimento)
            .Where(s => s.ExcluidoEm == null
                && (s.ExameImagem != null
                    ? s.ExameImagem.ExcluidoEm == null
                    : s.Categoria != CategoriaSolicitacao.Imagem));

        if (filtro.Categoria is { } categoria) query = query.Where(s => s.Categoria == categoria);
        if (filtro.Status is { } status)
        {
            // Com exame, vale o status de execução (de qualquer exame do pedido); sem exame, o da
            // espinha na mesma régua.
            var statusEspinha = SolicitacoesMapper.StatusDaEspinha(status);
            query = query.Where(s => s.ExameImagem != null
                ? s.ExamesImagem.Any(e => e.ExcluidoEm == null && e.Status == status)
                : statusEspinha != null && s.Status == statusEspinha);
        }
        if (filtro.PacienteId.HasValue) query = query.Where(s => s.PacienteId == filtro.PacienteId);
        if (filtro.UnidadeId.HasValue) query = query.Where(s => s.UnidadeExecutanteId == filtro.UnidadeId);
        if (filtro.TipoExameId.HasValue)
            query = query.Where(s => s.ExamesImagem.Any(e => e.ExcluidoEm == null && e.TipoExameId == filtro.TipoExameId));
        // Só retornos (vaga do SISREG) — ticket #135. Filtro de servidor, como os demais.
        if (filtro.SomenteRetornos) query = query.Where(s => s.TipoVaga == TipoVaga.Retorno);
        if (!string.IsNullOrWhiteSpace(filtro.AccessionNumber))
        {
            var a = filtro.AccessionNumber.Trim();
            query = query.Where(s => s.ExamesImagem.Any(e => e.AccessionNumber == a));
        }
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            // Busca livre: nº do pedido (accession/código) e procedimento/especialidade por ILIKE
            // local + nome/CPF/CNS resolvidos no hub FHIR (ids). Hub fora do ar → só match local
            // (nunca 500).
            var termo = filtro.Busca.Trim();
            var padrao = $"%{termo}%";
            // Teto de pacientes resolvidos segue o tamanho da página pedido (item do ticket #91:
            // "o teto deve obedecer ao dropdown da tela").
            var idsPaciente = (await _pacienteResolver.BuscarIdsPorTermoAsync(termo, tamanho, cancellationToken)).ToArray();
            query = query.Where(s =>
                s.ExamesImagem.Any(e => EF.Functions.ILike(e.AccessionNumber, padrao))
                || (s.CodigoSolicitacao != null && EF.Functions.ILike(s.CodigoSolicitacao, padrao))
                || (s.EspecialidadeTexto != null && EF.Functions.ILike(s.EspecialidadeTexto, padrao))
                || (s.ProcedimentoTexto != null && EF.Functions.ILike(s.ProcedimentoTexto, padrao))
                || idsPaciente.Contains(s.PacienteId));
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
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada >= ini);
        }
        if (!buscaPontual && filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada <= fim);
        }

        // Recorte do painel de início ("ver todos" de uma raia). Os predicados são os MESMOS de
        // PainelInicioService — se divergirem, o total do painel deixa de bater com a lista que
        // ele abre, que é o jeito mais rápido de perder a confiança do operador.
        if (filtro.Painel == RecortePainel.Cancelados)
        {
            query = query.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada
                && (s.Status == StatusSolicitacao.Solicitada || s.Status == StatusSolicitacao.Agendada));
        }
        else if (filtro.Painel == RecortePainel.Aguardando)
        {
            var agora = DateTime.UtcNow;
            var limiteJanela = agora.AddDays(PainelInicio.JanelasPainel.AguardandoDias);
            query = query.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && (s.Status == StatusSolicitacao.Solicitada || s.Status == StatusSolicitacao.Agendada)
                && s.DataAgendada != null
                && s.DataAgendada >= agora
                && s.DataAgendada <= limiteJanela);
        }

        // Multitenancy por unidade: executora OU solicitante entre as vinculadas ao usuário. A
        // cascata de resolução vive em EscopoUnidade (ADR-0033); sem vínculo nenhum ⇒ não vê nada
        // (fail-closed, ADR-0037). A referência (a ativa resolvida, ou null na visão do conjunto)
        // marca a direção (recebida/enviada) de cada linha.
        var escopo = await EscopoUnidade.ResolverAsync(_db, _usuarioAtual, cancellationToken);
        query = SolicitacaoNoEscopo.Filtrar(query, escopo);
        var unidadeReferencia = escopo.Referencia;

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
                ? query.Where(s => s.UnidadeSolicitanteId == refVisao)
                : query.Where(s => s.UnidadeExecutanteId == refVisao);
        }

        var total = await query.CountAsync(cancellationToken);

        // Urgentes sempre no topo, independente da data (Prioridade: Urgente=3 > Prioritaria=2 > Eletiva=1).
        var lista = await query
            .OrderByDescending(s => s.Prioridade)
            .ThenByDescending(s => s.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.SelectMany(s => LinhasDoPedido(s, filtro, unidadeReferencia))], cancellationToken);
        var comLaudos = await EnriquecerLaudosAsync([.. dtos], cancellationToken);
        var comAnamnese = await EnriquecerAnamneseAsync([.. comLaudos], cancellationToken);
        var itens = await EnriquecerComunicacoesAsync([.. comAnamnese], cancellationToken);
        return new PaginaSolicitacoesDto(itens, total, pagina, tamanho);
    }

    /// <summary>
    /// As linhas de um pedido na lista (ADR-0074). Um exame vivo só (quase sempre) = a linha de
    /// sempre. Dois ou mais = uma linha por exame, cada uma com o seu procedimento e o selo
    /// "vinculado a" com os accessions das outras. Os filtros que são do EXAME (situação, tipo,
    /// accession exato) escolhem quais exames do pedido aparecem.
    /// </summary>
    internal static IEnumerable<SolicitacaoListItemDto> LinhasDoPedido(
        Solicitacao s, FiltroSolicitacoesDto filtro, Guid? unidadeReferencia)
    {
        var exames = s.ExamesImagem.Where(e => e.ExcluidoEm == null).OrderBy(e => e.Ordem).ToList();
        if (exames.Count <= 1)
        {
            yield return SolicitacoesMapper.ParaListItem(s, unidadeReferencia);
            yield break;
        }

        var accession = filtro.AccessionNumber?.Trim();
        var visiveis = exames.Where(e =>
                (filtro.Status is not { } st || e.Status == st)
                && (filtro.TipoExameId is not { } tipo || e.TipoExameId == tipo)
                && (string.IsNullOrEmpty(accession) || e.AccessionNumber == accession))
            .ToList();
        if (visiveis.Count == 0) visiveis = [exames[0]];

        foreach (var exame in visiveis)
            yield return SolicitacoesMapper.ParaListItem(s, exame, unidadeReferencia) with
            {
                AccessionsVinculados = [.. exames.Where(e => e.Id != exame.Id).Select(e => e.AccessionNumber)],
            };
    }

    // Marca as linhas que já têm anamnese preenchida (muda a cor do botão Anamnese na lista).
    private async Task<IReadOnlyList<SolicitacaoListItemDto>> EnriquecerAnamneseAsync(
        List<SolicitacaoListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        // Anamnese FK = ExameImagemId (id preservado = DTO.Id). É do PEDIDO e mora no exame
        // principal (ADR-0074): a linha do exame -1 acende junto com a do principal.
        var ids = dtos.Select(d => d.Id).ToArray();
        var exameDaAnamnese = await ExamesDoPedido.ExameDaAnamneseAsync(_db, ids, ct);
        var principais = exameDaAnamnese.Values.Distinct().ToArray();
        var principaisComAnamnese = (await _db.Anamneses.AsNoTracking()
            .Where(a => principais.Contains(a.ExameImagemId) && a.ExcluidoEm == null)
            .Select(a => a.ExameImagemId)
            .ToListAsync(ct)).ToHashSet();
        var comAnamnese = ids.Where(id => principaisComAnamnese.Contains(exameDaAnamnese[id])).ToHashSet();
        // O protocolo do SISCAN vem no mesmo enriquecimento: é o mesmo conjunto de ids, e a fila
        // precisa mostrar quais já foram enviadas sem abrir uma a uma. A liberação ao paciente
        // (ADR-0075) também: é por ela que a linha mostra o cadeado de "retido até o laudo".
        var dadosExame = await _db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.SiscanProtocolo, e.LiberadoPacienteEm })
            .ToDictionaryAsync(e => e.Id, ct);
        var regras = await _regrasDoExame.ResolverAsync(ids, ct);

        return [.. dtos.Select(d => d with
        {
            TemAnamnese = comAnamnese.Contains(d.Id),
            SiscanProtocolo = dadosExame.GetValueOrDefault(d.Id)?.SiscanProtocolo,
            RetidoAteLaudo = dadosExame.TryGetValue(d.Id, out var x)
                && d.Status is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada
                && x.LiberadoPacienteEm is null && !d.LaudoAssinado,
            UsaAnamnese = !regras.TryGetValue(d.Id, out var regra) || regra.UsaAnamnese,
        })];
    }

    // Checks de comunicação na lista (✓/✓✓/✓✓azul/⚠): resume a confirmação, ExameLiberado e
    // LaudoPronto de cada solicitação da página. As comunicações são ancoradas na ESPINHA, que o
    // DTO já carrega.
    private async Task<IReadOnlyList<SolicitacaoListItemDto>> EnriquecerComunicacoesAsync(
        List<SolicitacaoListItemDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        var solicIds = dtos.Select(d => d.SolicitacaoId).ToArray();

        var comunicacoes = await _db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId != null && solicIds.Contains(c.SolicitacaoId.Value))
            .Select(c => new { c.SolicitacaoId, c.Finalidade, c.Status, c.VisualizadoEm, c.MotivoFalha })
            .ToListAsync(ct);
        if (comunicacoes.Count == 0) return dtos;

        var mapa = comunicacoes.ToDictionary(
            c => (c.SolicitacaoId!.Value, c.Finalidade),
            c => new ComunicacaoChipDto(c.Status.ToString(), c.VisualizadoEm != null, c.MotivoFalha));

        return [.. dtos.Select(d => d with
        {
            ChipConfirmacao = mapa.GetValueOrDefault((d.SolicitacaoId, FinalidadeComunicacao.ConfirmacaoAgendamento)),
            ChipExameLiberado = mapa.GetValueOrDefault((d.SolicitacaoId, FinalidadeComunicacao.ExameLiberado)),
            ChipLaudoPronto = mapa.GetValueOrDefault((d.SolicitacaoId, FinalidadeComunicacao.LaudoPronto)),
        })];
    }

    public async Task<SolicitacaoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await CarregarCompletoAsync(x => x.Id == id, cancellationToken) is { } exame)
            return await EnriquecerAsync(SolicitacoesMapper.ParaDto(exame), cancellationToken);

        // Sem exame de imagem, o id público é o da própria espinha (consulta, laboratório…).
        var reg = await _db.Solicitacoes.AsNoTracking()
            .Include(x => x.UnidadeExecutante)
            .Include(x => x.UnidadeSolicitante)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null && x.ExameImagem == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Solicitacao), id);
        return await EnriquecerAsync(SolicitacoesMapper.ParaDto(reg, null), cancellationToken);
    }

    /// <summary>
    /// Carrega, para alteração, a solicitação de um id PÚBLICO: o exame de imagem (com a espinha)
    /// quando o id é de um exame, senão a espinha sem exame. Lança quando não existe.
    /// </summary>
    private async Task<(ExameImagem? Exame, Solicitacao Reg)> CarregarParaAlterarAsync(
        Guid id, CancellationToken ct)
    {
        var exame = await _db.ExamesImagem
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, ct);
        if (exame is not null) return (exame, exame.Solicitacao!);

        var reg = await _db.Solicitacoes
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null && x.ExameImagem == null, ct)
            ?? throw new NaoEncontradoException(nameof(Solicitacao), id);
        return (null, reg);
    }

    public async Task<SolicitacaoDto?> ObterPorAccessionAsync(string accession, CancellationToken cancellationToken = default)
    {
        var a = (accession ?? string.Empty).Trim();
        if (a.Length == 0) return null;
        var s = await CarregarCompletoAsync(x => x.AccessionNumber == a, cancellationToken);
        return s is null ? null : await EnriquecerAsync(SolicitacoesMapper.ParaDto(s), cancellationToken);
    }

    public async Task<SolicitacaoDto?> ObterPorStudyAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
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

        return s is null ? null : await EnriquecerAsync(SolicitacoesMapper.ParaDto(s), cancellationToken);
    }

    public async Task<DefinirCpfPacienteResultadoDto> DefinirCpfDoPacienteAsync(
        Guid id, string cpf, CancellationToken cancellationToken = default)
    {
        var digitos = CpfBr.SoDigitos(cpf);
        if (!CpfBr.EhValido(digitos))
        {
            throw new ValidacaoException(
                "solicitacao.cpf_invalido",
                "CPF inválido — confira os dígitos. Um CPF mal digitado que por acaso exista "
                + "vincularia este exame ao PACIENTE ERRADO.");
        }

        var (_, reg) = await CarregarParaAlterarAsync(id, cancellationToken);

        var atual = await _pacienteResolver.ResolverAsync(reg.PacienteId, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", reg.PacienteId);

        if (!string.IsNullOrWhiteSpace(atual.Cpf))
        {
            // Idempotente: reenviar o mesmo CPF (duplo clique, retry de rede) não é erro.
            if (atual.Cpf == digitos)
                return new DefinirCpfPacienteResultadoDto(atual.Id, atual.Nome, digitos, Repontado: false);

            throw new ConflitoException(
                "solicitacao.paciente_ja_tem_cpf",
                $"O paciente desta solicitação já tem CPF cadastrado. Se não for a mesma pessoa, "
                + "corrija o vínculo do exame em vez de trocar o CPF do cadastro.");
        }

        // O CPF já é de outro cadastro? Então a pessoa já existia e este registro sem CPF é uma
        // sombra dela criada pela importação. Repontamos a solicitação — nada é fundido nem apagado.
        var existente = await _pacientes.ObterPorCpfAsync(digitos, cancellationToken);
        if (existente is not null && existente.Id != reg.PacienteId)
        {
            var sombraId = reg.PacienteId;

            // O CNS TEM de ir junto: senão a próxima varredura reencontra a sombra por CNS e
            // reponta de novo, para sempre. O telefone entra por append (contato só acumula).
            await _pacientes.AbsorverIdentificadoresAsync(
                existente.Id, atual.Cns, atual.Celular, cancellationToken);

            reg.PacienteId = existente.Id;
            reg.AtualizadoEm = DateTime.UtcNow;
            reg.AtualizadoPor = _usuarioAtual.UsuarioId;
            await _db.SaveChangesAsync(cancellationToken);

            await _auditoria.RegistrarAsync(
                "Solicitacao", reg.Id.ToString(), "RepontarPacientePorCpf",
                $"{atual.Nome} ({sombraId})", $"{existente.NomeCompleto} ({existente.Id})", cancellationToken);

            _logger.LogInformation(
                "SOLICITACAO_REPONTADA_POR_CPF: solicitação {Solicitacao} saiu do cadastro-sombra {Sombra} "
                + "para o paciente {Destino} ao informar o CPF.", reg.Id, sombraId, existente.Id);

            return new DefinirCpfPacienteResultadoDto(
                existente.Id, existente.NomeCompleto, digitos, Repontado: true);
        }

        await _pacientes.DefinirCpfAsync(reg.PacienteId, digitos, cancellationToken);
        return new DefinirCpfPacienteResultadoDto(atual.Id, atual.Nome, digitos, Repontado: false);
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

        var (s, reg) = await CarregarParaAlterarAsync(id, cancellationToken);

        // Crítica da chave: se a chave do SISREG já foi lida e guardada (comando único de
        // revelação), o que a recepção digitou tem de bater com ela. Sem chave guardada NÃO se
        // vai ao SISREG aqui — o balcão não gasta requisição; segue como sempre foi.
        if (!string.IsNullOrWhiteSpace(reg.ChaveConfirmacaoSisreg)
            && !string.Equals(reg.ChaveConfirmacaoSisreg.Trim(), chave, StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "autorizacao.chave_divergente",
                "A chave informada não confere com a chave de confirmação do SISREG desta solicitação. "
                + "Confira o comprovante do paciente.");
        }

        // As travas de CPF, contato verificado e sala existem por causa do exame de imagem (o CPF é
        // o PatientID do DICOM; o contato leva o resultado e o laudo; a sala é o destino da
        // worklist). Na consulta e nas demais, autorizar é registrar a chegada com a chave.
        if (s is not null)
            await ValidarAutorizacaoDoExameAsync(s, reg, equipamentoId, cancellationToken);

        // O pedido é UM SÓ (ADR-0074): receber um exame recebe todos. As linhas do mesmo nº chegam
        // com a mesma data, hora e unidade — é a mesma visita. Os irmãos herdam a sala escolhida
        // quando ela também os atende; o exame clicado já foi validado acima.
        var exames = s is null
            ? []
            : await _db.ExamesImagem
                .Include(x => x.TipoExame)
                .Where(x => x.SolicitacaoId == reg.Id && x.ExcluidoEm == null)
                .OrderBy(x => x.Ordem)
                .ToListAsync(cancellationToken);
        foreach (var irmao in exames.Where(x => x.Id != s!.Id))
            await DefinirEstacaoDoIrmaoAsync(irmao, reg, s!.EquipamentoId ?? equipamentoId, cancellationToken);

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

        // Só AGORA enfileira o envio ao PACS (se o tipo envia à worklist e ainda não foi enviado) —
        // de TODOS os exames do pedido, cada um com o seu código interno e o seu item de worklist.
        var impedidos = new List<(ExameImagem Exame, string Motivo)>();
        foreach (var exame in exames.Where(x => x.Status == StatusSolicitacaoExame.Solicitada))
        {
            // Holter: a worklist espera a anamnese (altura, peso, fumante vão no item). Não é erro —
            // é o próximo passo, e quem o dá é a técnica ao salvar a anamnese (AnamneseSalvaAsync).
            if (await AguardaAnamneseHolterAsync(exame, cancellationToken))
            {
                exame.ProximaTentativaEm = null;
                exame.ErroIntegracaoPacs = AguardandoAnamneseHolter;
                exame.AtualizadoEm = agora;
                continue;
            }

            var impedimento = await ImpedimentoEnvioPacsAsync(exame, reg, cancellationToken);
            if (impedimento is null)
            {
                exame.ProximaTentativaEm = agora;
                exame.ErroIntegracaoPacs = null;
            }
            else
            {
                // Autorizar sem poder enviar NÃO passa em silêncio: a recepção liberava o paciente
                // achando que o exame estava na worklist do aparelho. Carimba o motivo no exame (a
                // tela mostra) e abre um erro rastreável em Sistema → Erros.
                exame.ProximaTentativaEm = null;
                exame.ErroIntegracaoPacs = impedimento;
                impedidos.Add((exame, impedimento));
            }
            exame.AtualizadoEm = agora;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var (exame, motivo) in impedidos)
            await ReportarImpedimentoAsync(exame, reg, motivo, cancellationToken);
    }

    /// <summary>
    /// Sala de um exame irmão do que a recepção autorizou (ADR-0074). Não pergunta nada: usa a sala
    /// escolhida se ela atende este exame, ou a única que atende; com duas ou mais e nenhuma
    /// escolhida válida, deixa para o envio deduzir — o mesmo caminho de qualquer exame sem sala.
    /// </summary>
    private async Task DefinirEstacaoDoIrmaoAsync(
        ExameImagem irmao, Solicitacao reg, Guid? escolhido, CancellationToken cancellationToken)
    {
        if (irmao.Status != StatusSolicitacaoExame.Solicitada || irmao.EquipamentoId is not null) return;
        var escopo = irmao.TipoExameId is { } tipo
            ? await _escopoExame.ObterAsync(tipo, reg.UnidadeExecutanteId, cancellationToken)
            : null;
        if (!(escopo?.EnviarParaWorklist ?? false)) return;

        var candidatos = await _estacaoWorklist.ListarCandidatosAsync(irmao, cancellationToken);
        if (escolhido is { } id && candidatos.Any(c => c.Id == id))
            irmao.EquipamentoId = id;
        else if (candidatos.Count == 1)
            irmao.EquipamentoId = candidatos[0].Id;
    }

    /// <summary>Travas da autorização que só valem para o exame de imagem (ver AutorizarAsync).</summary>
    private async Task ValidarAutorizacaoDoExameAsync(
        ExameImagem s, Solicitacao reg, Guid? equipamentoId, CancellationToken cancellationToken)
    {
        // Gate: paciente precisa ter um número VERIFICADO (marcador no telecom do Patient FHIR)
        // OU uma dispensa registrada. A dispensa existe porque o gate rígido travava o balcão:
        // quem não tem celular, ou não consegue confirmar o código, ficava sem caminho de saída.
        var paciente = await _pacienteResolver.ResolverAsync(reg.PacienteId, cancellationToken);

        // Gate do CPF. O paciente pode ter entrado SEM CPF: a importação do SISREG deixa passar
        // o agendamento ancorado só no CNS em vez de virar pendência que ninguém resolve. Aqui é
        // onde o débito é cobrado — e não é só regra de negócio: o PatientID do DICOM É o CPF,
        // então sem ele o exame não teria como ir para a worklist do PACS.
        // Backstop do gate da tela (a recepção não abre a solicitação sem informar o CPF): esta
        // é a única barreira que vale para chamada direta de API.
        if (string.IsNullOrWhiteSpace(paciente?.Cpf))
        {
            throw new ValidacaoException(
                "autorizacao.paciente_sem_cpf",
                "O paciente ainda não tem CPF no cadastro. Informe o CPF na solicitação para liberar "
                + "o exame — sem ele o pedido não pode ser enviado ao equipamento.");
        }

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
        var escopoDaUnidade = s.TipoExameId is { } tipoParaEscopo
            ? await _escopoExame.ObterAsync(tipoParaEscopo, reg.UnidadeExecutanteId, cancellationToken)
            : null;

        if (s.Status == StatusSolicitacaoExame.Solicitada && (escopoDaUnidade?.EnviarParaWorklist ?? false))
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
    }

    /// <summary>Texto que a tela mostra no exame de Holter parado à espera da anamnese.</summary>
    private const string AguardandoAnamneseHolter =
        "Aguardando a anamnese do Holter (altura, peso e fumante). O exame vai à worklist do aparelho "
        + "quando a técnica salvar a anamnese.";

    /// <summary>
    /// O exame é Holter, o aparelho usa anamnese (ADR-0075) e ela ainda não foi salva. Aparelho com
    /// a anamnese desligada não segura nada — senão desligar o botão prenderia o exame para sempre.
    /// </summary>
    private async Task<bool> AguardaAnamneseHolterAsync(ExameImagem exame, CancellationToken ct)
    {
        var modalidade = exame.TipoExame?.ModalidadeDicom
            ?? await _db.TiposExame.AsNoTracking()
                .Where(t => t.Id == exame.TipoExameId)
                .Select(t => (ModalidadeDicom?)t.ModalidadeDicom)
                .FirstOrDefaultAsync(ct);
        if (!Anamneses.AnamneseHolter.EhHolter(modalidade)) return false;
        if (!(await _regrasDoExame.ResolverAsync(exame.Id, ct)).UsaAnamnese) return false;

        var exameDaAnamnese = await ExamesDoPedido.ExameDaAnamneseAsync(_db, exame.Id, ct);
        return !await _db.Anamneses.AsNoTracking().AnyAsync(a =>
            a.ExameImagemId == exameDaAnamnese && a.ExcluidoEm == null && a.Tipo == Anamneses.AnamneseHolter.Tipo, ct);
    }

    /// <summary>Altura, peso e fumante da anamnese do Holter para o item de worklist; null fora do Holter.</summary>
    private async Task<DadosClinicosWorklist?> DadosClinicosAsync(ExameImagem s, CancellationToken ct)
    {
        if (!Anamneses.AnamneseHolter.EhHolter(s.TipoExame?.ModalidadeDicom)) return null;

        var exameDaAnamnese = await ExamesDoPedido.ExameDaAnamneseAsync(_db, s.Id, ct);
        var conteudo = await _db.Anamneses.AsNoTracking()
            .Where(a => a.ExameImagemId == exameDaAnamnese && a.ExcluidoEm == null && a.Tipo == Anamneses.AnamneseHolter.Tipo)
            .Select(a => a.ConteudoJson)
            .FirstOrDefaultAsync(ct);
        return conteudo is null ? null : Anamneses.AnamneseHolter.ParaWorklist(conteudo);
    }

    public async Task<string?> AnamneseSalvaAsync(Guid exameId, CancellationToken cancellationToken = default)
    {
        var alvo = await _db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == exameId && e.ExcluidoEm == null)
            .Select(e => new { e.SolicitacaoId })
            .FirstOrDefaultAsync(cancellationToken);
        if (alvo is null) return null;

        // A anamnese é do pedido (ADR-0074): vale para todos os exames de Holter dele.
        var exames = await _db.ExamesImagem
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao)
            .Where(x => x.SolicitacaoId == alvo.SolicitacaoId && x.ExcluidoEm == null)
            .OrderBy(x => x.Ordem)
            .ToListAsync(cancellationToken);

        var agora = DateTime.UtcNow;
        string? aviso = null;
        var impedidos = new List<(ExameImagem Exame, string Motivo)>();
        foreach (var exame in exames)
        {
            if (!Anamneses.AnamneseHolter.EhHolter(exame.TipoExame?.ModalidadeDicom)) continue;
            if (!(await _regrasDoExame.ResolverAsync(exame.Id, cancellationToken)).UsaAnamnese) continue;

            // Antes da recepção, nada a fazer: a autorização já encontra a anamnese e enfileira.
            if (exame.Solicitacao!.AutorizadoEm is null)
            {
                aviso = "Anamnese salva. O exame vai à worklist quando a recepção confirmar a presença.";
                continue;
            }

            switch (exame.Status)
            {
                case StatusSolicitacaoExame.Solicitada:
                    if (await ImpedimentoEnvioPacsAsync(exame, exame.Solicitacao, cancellationToken) is { } motivo)
                    {
                        exame.ProximaTentativaEm = null;
                        exame.ErroIntegracaoPacs = motivo;
                        impedidos.Add((exame, motivo));
                        aviso = motivo;
                        break;
                    }
                    exame.ProximaTentativaEm = agora;
                    exame.TentativasEnvio = 0;
                    exame.ErroIntegracaoPacs = null;
                    aviso = "Anamnese salva. O exame está indo para a worklist do aparelho.";
                    break;

                case StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida:
                    // Correção: volta a Solicitada e o worker refaz o POST /mwlitems — upsert pela
                    // mesma chave (StudyInstanceUID + SPS ID), então o item da estação é SOBRESCRITO,
                    // não duplicado. O WorklistItemUid fica: é a mesma chave.
                    exame.Status = StatusSolicitacaoExame.Solicitada;
                    exame.ProximaTentativaEm = agora;
                    exame.TentativasEnvio = 0;
                    exame.ErroIntegracaoPacs = null;
                    aviso = "Anamnese corrigida. O paciente está sendo atualizado na worklist do aparelho.";
                    break;

                default:
                    // Em execução, realizado, laudado ou cancelado: o item já saiu (ou vai sair) da
                    // estação. A correção fica na anamnese, mas não chega mais ao aparelho.
                    var situacao = exame.Status switch
                    {
                        StatusSolicitacaoExame.EmExecucao => "em execução",
                        StatusSolicitacaoExame.Realizada => "realizado",
                        StatusSolicitacaoExame.Laudada => "laudado",
                        StatusSolicitacaoExame.Cancelada => "cancelado",
                        _ => "fora da worklist",
                    };
                    aviso = $"Anamnese salva. O exame já está {situacao} — a correção não chega mais à "
                        + "worklist; se o aparelho já gravou, corrija os dados nele.";
                    continue;
            }
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = _usuarioAtual.UsuarioId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        foreach (var (exame, motivo) in impedidos)
            await ReportarImpedimentoAsync(exame, exame.Solicitacao!, motivo, cancellationToken);
        return aviso;
    }

    /// <summary>
    /// Motivo pelo qual um exame autorizado NÃO chegará à worklist — null quando o caminho está
    /// livre. Cobre os furos já vistos em produção, todos silenciosos: exame importado sem
    /// TipoExame (código SIGTAP do SISREG não bate com nenhum cadastrado), exame fora do escopo da
    /// unidade e escopo com o envio desligado. Em todos, o worker nem enxerga a linha.
    ///
    /// <para>A pergunta é sempre "esta UNIDADE manda este exame?" — nunca mais "o município
    /// manda?". O texto diz qual unidade, porque o mesmo tipo pode estar ligado numa e desligado
    /// noutra de propósito (quem tem aparelho × quem não tem).</para>
    /// </summary>
    private async Task<string?> ImpedimentoEnvioPacsAsync(
        ExameImagem s, Solicitacao reg, CancellationToken ct)
    {
        if (s.TipoExameId is null)
            return $"Exame sem tipo mapeado — o procedimento SIGTAP {reg.ProcedimentoSigtapCodigo ?? "(não informado)"} " +
                   $"(\"{reg.ProcedimentoTexto}\") não está vinculado a nenhum tipo de exame. " +
                   "Vincule em Exames de Imagem → Mapeamento SIGTAP para que o exame vá à worklist.";

        var nomeTipo = s.TipoExame?.Nome ?? "(sem nome)";
        var escopo = await _escopoExame.ObterAsync(s.TipoExameId.Value, reg.UnidadeExecutanteId, ct);

        if (escopo is null)
        {
            var unidade = await _db.Unidades.AsNoTracking()
                .Where(u => u.Id == reg.UnidadeExecutanteId)
                .Select(u => u.Nome)
                .FirstOrDefaultAsync(ct) ?? "(unidade não identificada)";

            return $"O exame \"{nomeTipo}\" não está no escopo de {unidade}. " +
                   "Adicione em Unidades → a unidade → Exames de imagem para que ele chegue ao equipamento.";
        }

        if (!escopo.EnviarParaWorklist)
            return $"O exame \"{nomeTipo}\" está com o envio à worklist desligado nesta unidade. " +
                   "Ligue em Unidades → a unidade → Exames de imagem para que ele chegue ao equipamento.";

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
                Caminho: $"/solicitacoes/{s.Id}/autorizar",
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
            // Com data já é agendamento; sem data, Solicitada ("ainda sem data firme").
            Status = request.DataAgendada is not null ? StatusSolicitacao.Agendada : StatusSolicitacao.Solicitada,
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
            // Pedido manual tem um procedimento só: o exame é o principal (ADR-0074).
            SolicitacaoPrincipalId = solicitacao.Id,
            Ordem = 0,
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
        // Status acompanha a data enquanto a espinha está aberta (nunca mexe em Realizada/Cancelada).
        if (reg.Status is StatusSolicitacao.Solicitada or StatusSolicitacao.Agendada)
            reg.Status = reg.DataAgendada is not null ? StatusSolicitacao.Agendada : StatusSolicitacao.Solicitada;
        reg.AtualizadoEm = agora;
        reg.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Cobre "criou sem data, agendou depois": enfileira confirmação (idempotente).
        await _comunicacoes.Value.EnfileirarAsync(
            reg, FinalidadeComunicacao.ConfirmacaoAgendamento, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancelarSolicitacaoRequest request, CancellationToken cancellationToken = default)
    {
        var (s, reg) = await CarregarParaAlterarAsync(id, cancellationToken);

        // Com exame, só antes de executar; sem exame, enquanto a regulação não fechou.
        var cancelavel = s is not null
            ? s.Status is StatusSolicitacaoExame.Solicitada or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida
            : reg.Status is StatusSolicitacao.Solicitada or StatusSolicitacao.Agendada;
        if (!cancelavel)
        {
            var status = s?.Status ?? SolicitacoesMapper.StatusComum(reg.Status);
            throw new ConflitoException(
                "solicitacao.nao_cancelavel",
                $"Solicitação no status '{status}' não pode ser cancelada.");
        }

        // O pedido é um só (ADR-0074): com um exame irmão já feito, o pedido não se cancela pela
        // tela de outro — cancelar aqui fecharia como "cancelado" um pedido em que o paciente foi
        // atendido.
        if (s is not null
            && await _db.ExamesImagem.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.SolicitacaoId == reg.Id && x.ExcluidoEm == null
                    && (x.Status == StatusSolicitacaoExame.EmExecucao
                        || x.Status == StatusSolicitacaoExame.Realizada
                        || x.Status == StatusSolicitacaoExame.Laudada),
                cancellationToken) is { } feito)
        {
            throw new ConflitoException(
                "solicitacao.nao_cancelavel",
                $"O exame {feito.AccessionNumber} deste mesmo pedido já foi executado ('{feito.Status}'): "
                + "o pedido não pode ser cancelado.");
        }

        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
        {
            throw new ValidacaoException("solicitacao.motivo_obrigatorio", "Motivo do cancelamento é obrigatório.");
        }

        var agora = DateTime.UtcNow;
        // O pedido é um só (ADR-0074): cancelar pela tela de um exame cancela todos os exames dele
        // que ainda não foram executados — como o SISREG faz.
        var exames = s is null
            ? []
            : await _db.ExamesImagem
                .Where(x => x.SolicitacaoId == reg.Id && x.ExcluidoEm == null
                    && (x.Status == StatusSolicitacaoExame.Solicitada
                        || x.Status == StatusSolicitacaoExame.Enviada
                        || x.Status == StatusSolicitacaoExame.Recebida))
                .ToListAsync(cancellationToken);
        foreach (var exame in exames)
        {
            // Remove o item da worklist no dcm4chee (best-effort — não derruba o cancelamento local).
            // Se falhar, o campo continua preenchido e o worker refaz a remoção depois: o exame
            // cancelado entra na fila de limpeza justamente por ter WorklistItemUid.
            if (!string.IsNullOrEmpty(exame.WorklistItemUid))
            {
                try
                {
                    await _mwlClient.ExcluirMwlItemAsync(exame, cancellationToken);
                    exame.WorklistItemUid = null; // espelho do que está no PACS
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "Falha ao remover MWL de {Accession} no cancelamento — segue cancelado localmente; worker retenta.",
                        exame.AccessionNumber);
                }
            }

            exame.Status = StatusSolicitacaoExame.Cancelada;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = _usuarioAtual.UsuarioId;
        }

        // Espinha reflete o cancelamento (é decisão de regulação).
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
        // TipoExame entra no Include porque ImpedimentoEnvioPacs o lê: sem carregar, a navegação vem
        // nula e TODO reenvio seria recusado como "tipo com envio desligado".
        var s = await _db.ExamesImagem
            .Include(x => x.Solicitacao)
            .Include(x => x.TipoExame)
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

        // Enfileirar o que o worker NUNCA vai processar é pior que recusar: ele filtra por
        // TipoExame.EnviarParaWorklist (INNER JOIN), então o exame ficaria na fila para sempre — e,
        // como o reenvio limpa ErroIntegracaoPacs, o motivo sumia da tela junto. Foi o que aconteceu
        // com o 260908001 em 08/09/2026: a autorização carimbou o impedimento e abriu o ERRO-KF775R,
        // alguém clicou em Reenviar 15 min depois, o aviso sumiu e o exame ficou parado com
        // tentativas_envio=0. Quem clicou não tinha como saber. Agora recusa dizendo o porquê.
        if (await ImpedimentoEnvioPacsAsync(s, s.Solicitacao!, cancellationToken) is { } impedimento)
        {
            throw new ConflitoException("solicitacaoExame.envio_impedido", impedimento);
        }

        if (await AguardaAnamneseHolterAsync(s, cancellationToken))
            throw new ConflitoException("solicitacaoExame.aguardando_anamnese", AguardandoAnamneseHolter);

        // Enviada/Recebida já têm item no PACS: reenviar é REFAZER o item com o cadastro de agora
        // (ex.: o aparelho passou a receber o médico solicitante). O worker só pega Solicitada e
        // Enviada — até 16/10/2026 o reenvio de um Recebida só marcava a fila, o worker nunca o
        // pegava e o botão não fazia nada, calado. Voltando a Solicitada (a autorização continua
        // valendo), o worker recria o item — o POST /mwlitems é upsert por StudyUID + SPS ID — e
        // confirma de novo até Recebida.
        if (s.Status is StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida)
            s.Status = StatusSolicitacaoExame.Solicitada;

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

        // Excluir apaga o PEDIDO, e o pedido é um só (ADR-0074): leva todos os exames dele.
        var exames = await _db.ExamesImagem
            .Where(x => x.SolicitacaoId == s.SolicitacaoId && x.ExcluidoEm == null)
            .ToListAsync(cancellationToken);

        // Exame iniciado/realizado/laudado tem imagens — o pedido não se exclui por aqui, nem pelo
        // irmão que ainda não foi feito.
        if (exames.FirstOrDefault(x => x.Status is StatusSolicitacaoExame.EmExecucao
                or StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada) is { } executado)
        {
            throw new ConflitoException(
                "solicitacaoExame.nao_excluivel",
                $"Solicitação no status '{executado.Status}' (exame {executado.AccessionNumber} iniciado/realizado) "
                + "não pode ser excluída.");
        }

        // Anti-lixo: remove o item da worklist no dcm4chee e confirma; só então apaga localmente.
        foreach (var exame in exames)
        {
            if (force)
            {
                try
                {
                    await _mwlClient.ExcluirMwlItemAsync(exame, cancellationToken);
                    exame.WorklistItemUid = null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Campo fica preenchido de propósito: exame excluído com item pendente continua
                    // elegível para a fila de limpeza do worker, que retenta até o PACS aceitar.
                    _logger.LogWarning(ex,
                        "Exclusão forçada de {Accession} — falha ao remover MWL no dcm4chee; worker retenta.",
                        exame.AccessionNumber);
                }
            }
            else
            {
                try
                {
                    await _mwlClient.ExcluirMwlItemAsync(exame, cancellationToken);
                    exame.WorklistItemUid = null;
                }
                catch (ConflitoException)
                {
                    throw new ConflitoException(
                        "solicitacaoExame.exclusao_pacs_falhou",
                        $"Não foi possível remover o item da worklist do exame {exame.AccessionNumber} no dcm4chee. "
                        + "Verifique o PACS e tente de novo, ou use a exclusão forçada (limpa apenas a base local).");
                }
            }
        }

        var agora = DateTime.UtcNow;
        // Soft-delete espelhado nas duas tabelas (execução + regulação).
        foreach (var exame in exames)
        {
            exame.ExcluidoEm = agora;
            exame.ExcluidoPor = _usuarioAtual.UsuarioId;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = _usuarioAtual.UsuarioId;
            exame.ProximaTentativaEm = null; // tira do worker
        }

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

        // Liberação ao paciente (ADR-0075): quem decide é o aparelho. "Assim que chegar" libera agora e
        // avisa ("Exame liberado"); "só com o laudo assinado" retém — imagem e laudo saem juntos quando
        // o laudo for aprovado, numa mensagem só ("Laudo pronto"). Decisão nova a cada realização: o
        // estudo que chega depois de uma correção de identidade não herda a liberação do anterior.
        var regra = await _regrasDoExame.ResolverAsync(s.Id, cancellationToken);
        s.LiberadoPacienteEm = regra.LiberaAoChegar ? agora : null;
        s.LiberadoPacientePor = null;

        // Exame liberado → enfileira o aviso (idempotente; o worker só envia com a chave
        // EnviarExameLiberado ligada).
        if (regra.LiberaAoChegar)
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
            // As duas unidades: o equipamento pode pedir o nome de uma delas na worklist (adendo ADR-0074).
            .Include(x => x.Solicitacao).ThenInclude(p => p!.UnidadeExecutante)
            .Include(x => x.Solicitacao).ThenInclude(p => p!.UnidadeSolicitante)
            // O código do procedimento (0008,0100) do exame extra é o do item do pedido (ADR-0074).
            .Include(x => x.SolicitacaoProcedimento)
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
                    // Última trava do Holter: qualquer caminho que enfileire (escopo religado,
                    // correção de identidade, satélite) para aqui até a anamnese existir.
                    if (await AguardaAnamneseHolterAsync(s, cancellationToken))
                    {
                        s.ProximaTentativaEm = null;
                        s.ErroIntegracaoPacs = AguardandoAnamneseHolter;
                        s.AtualizadoEm = agora;
                        await _db.SaveChangesAsync(cancellationToken);
                        break;
                    }

                    // 1) Cria o MWL item (resolve paciente no FHIR + registra no dcm4chee + POST /mwlitems).
                    var sps = await _mwlClient.CriarOuAtualizarMwlItemAsync(
                        s, await DadosClinicosAsync(s, cancellationToken), cancellationToken);
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
        catch (DbUpdateConcurrencyException)
        {
            await SoltarExameMudadoPorForaAsync(s, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PACS fora/lento: tenta de novo daqui a pouco. Backoff fixo e curto — diferente do
            // envio, aqui não há paciente esperando, e o item some assim que o PACS responder.
            s.ProximaTentativaEm = agora.AddMinutes(5);
            s.AtualizadoEm = agora;
            try { await _db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { await SoltarExameMudadoPorForaAsync(s, cancellationToken); return; }
            _logger.LogWarning(ex,
                "Falha ao remover MWL de {Accession} — nova tentativa em 5 min.", s.AccessionNumber);
        }
    }

    /// <summary>
    /// Outra rotina mexeu no exame enquanto a limpeza ia ao PACS (trava <c>xmin</c>). Não é falha:
    /// a próxima passada repete — o PACS responde 404, que conta como removido, e o espelho zera.
    /// Gravar de novo o estado desatualizado só repetia o erro, e o registro preso no DbContext (é o
    /// da passada inteira) derrubava os exames seguintes. Medido em 05/10/2026: a passada seguinte
    /// resolveu em 15 s.
    /// </summary>
    private async Task SoltarExameMudadoPorForaAsync(ExameImagem s, CancellationToken cancellationToken)
    {
        await _db.Entry(s).ReloadAsync(cancellationToken);
        _logger.LogInformation(
            "Limpeza da worklist de {Accession}: o exame mudou durante a remoção no PACS — a próxima passada confirma.",
            s.AccessionNumber);
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
            // Exame extra (ADR-0074): o procedimento dele vem do item do pedido.
            .Include(e => e.SolicitacaoProcedimento)
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
            .Include(x => x.Solicitacao).ThenInclude(p => p!.UnidadeExecutante)
            .Include(x => x.Solicitacao).ThenInclude(p => p!.UnidadeSolicitante)
            .Include(x => x.SolicitacaoProcedimento)
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

        var escopoTroca = s.TipoExameId is { } tipoTroca
            ? await _escopoExame.ObterAsync(tipoTroca, s.Solicitacao!.UnidadeExecutanteId, cancellationToken)
            : null;

        if (!(escopoTroca?.EnviarParaWorklist ?? false))
        {
            throw new ConflitoException(
                "solicitacaoExame.sem_worklist",
                "Este exame não é enviado à worklist nesta unidade; não há equipamento de destino a trocar.");
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
            s.WorklistItemUid = await _mwlClient.CriarOuAtualizarMwlItemAsync(
                s, await DadosClinicosAsync(s, cancellationToken), cancellationToken);
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

        // A unidade é do PEDIDO, e o pedido é um só (ADR-0074): os exames irmãos vão juntos.
        var exames = await _db.ExamesImagem
            .Where(x => x.SolicitacaoId == reg.Id && x.ExcluidoEm == null)
            .OrderBy(x => x.Ordem)
            .ToListAsync(cancellationToken);

        // 1) Elegibilidade (regra do ticket): só bloqueia quando a imagem JÁ voltou do PACS. Nos
        //    status anteriores (Solicitada/Enviada/Recebida) a troca é permitida — para todos.
        if (exames.FirstOrDefault(x => x.Status is StatusSolicitacaoExame.EmExecucao
                or StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada
                or StatusSolicitacaoExame.Cancelada
            || x.RealizadoEm is not null) is { } bloqueado)
        {
            throw new ConflitoException(
                "solicitacaoExame.unidade_nao_alteravel",
                $"Não é possível alterar a unidade executante de um exame no status '{bloqueado.Status}' "
                + $"(exame {bloqueado.AccessionNumber}: imagem já recebida do PACS).");
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
        foreach (var exame in exames)
        {
            if (!await _mwlClient.MwlItemExisteAsync(exame, cancellationToken)) continue;
            await _mwlClient.ExcluirMwlItemAsync(exame, cancellationToken);
            exame.WorklistItemUid = null;

            if (await _mwlClient.MwlItemExisteAsync(exame, cancellationToken))
                throw new ConflitoException("solicitacaoExame.remocao_nao_confirmada",
                    $"Removi o item da worklist do exame {exame.AccessionNumber}, mas o PACS ainda o lista. "
                    + "Aguarde um instante e tente de novo.");
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

        foreach (var exame in exames)
        {
            exame.EquipamentoId = null;
            exame.WorklistItemUid = null;
            exame.Status = StatusSolicitacaoExame.Solicitada;
            exame.ProximaTentativaEm = null;
            exame.ErroIntegracaoPacs = null;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = _usuarioAtual.UsuarioId;
        }

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
