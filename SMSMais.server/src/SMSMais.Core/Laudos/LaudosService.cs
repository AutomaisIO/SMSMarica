using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Associacoes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Core.Laudos.BiRads;
using SMSMais.Core.Laudos.Dtos;
using SMSMais.Core.Medicos;
using SMSMais.Core.Medicos.Assinatura;
using SMSMais.Core.Medicos.Dtos;
using SMSMais.Core.Medicos.Fhir;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos;

public sealed class LaudosService(
    SmsMaisDbContext db,
    IHtmlSanitizer sanitizer,
    Lazy<ISolicitacoesExameService> solicitacoes,
    IPacienteFhirClient pacienteFhir,
    IPractitionerFhirClient practitionerFhir,
    IPacienteResolver pacienteResolver,
    IAssinaturaMedicoService assinaturaMedico,
    IExameAssociacaoService associacao,
    IConsultaStudyClient consultaStudy,
    Configuracao.ILaudoConfiguracaoService configuracao,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<LaudosService> logger,
    Microsoft.Extensions.Options.IOptions<Assinatura.Nuvem.IntegraIcpOptions> nuvemOptions) : ILaudosService
{
    private readonly bool _nuvemHabilitada = nuvemOptions.Value.Habilitado;
    private readonly SmsMaisDbContext _db = db;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;
    // Lazy: quebra a dependência circular SolicitacoesExame → Assinatura → PdfRenderer
    // → Laudos → SolicitacoesExame na construção do grafo de DI (resolução só no uso).
    private readonly Lazy<ISolicitacoesExameService> _solicitacoes = solicitacoes;
    private readonly IPacienteFhirClient _pacienteFhir = pacienteFhir;
    private readonly IPractitionerFhirClient _practitionerFhir = practitionerFhir;
    private readonly IPacienteResolver _pacienteResolver = pacienteResolver;
    private readonly IAssinaturaMedicoService _assinaturaMedico = assinaturaMedico;
    private readonly IExameAssociacaoService _associacao = associacao;
    private readonly IConsultaStudyClient _consultaStudy = consultaStudy;
    private readonly Configuracao.ILaudoConfiguracaoService _configuracao = configuracao;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;
    private readonly ILogger<LaudosService> _logger = logger;

    private async Task<IReadOnlyList<LaudoListItemDto>> EnriquecerAsync(
        List<LaudoListItemDto> dtos, CancellationToken ct)
    {
        var ids = dtos.Where(d => d.PacienteId.HasValue).Select(d => d.PacienteId!.Value);
        var nomes = await _pacienteResolver.ResolverManyAsync(ids, ct);
        return [.. dtos.Select(d => d.PacienteId.HasValue && nomes.TryGetValue(d.PacienteId.Value, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }

    private async Task<LaudoDto> EnriquecerAsync(LaudoDto dto, CancellationToken ct)
    {
        if (!dto.PacienteId.HasValue) return dto;
        var r = await _pacienteResolver.ResolverAsync(dto.PacienteId.Value, ct);
        return r is null ? dto : dto with { PacienteNome = r.Nome, PacienteCpf = r.Cpf };
    }

    public async Task<PaginaLaudosDto> ListarAsync(
        FiltroLaudosDto filtro,
        CancellationToken cancellationToken = default)
    {
        var tamanho = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;

        IQueryable<Laudo> query = _db.Laudos.AsNoTracking().Where(l => !l.Excluido);

        if (!string.IsNullOrWhiteSpace(filtro.StudyInstanceUID))
        {
            var uid = filtro.StudyInstanceUID.Trim();
            query = query.Where(l => l.StudyInstanceUID == uid);
        }
        if (filtro.PacienteId.HasValue)
            query = query.Where(l => l.PacienteId == filtro.PacienteId);
        if (filtro.MedicoId.HasValue)
            query = query.Where(l => l.MedicoId == filtro.MedicoId);
        if (filtro.Status.HasValue)
            query = query.Where(l => l.Status == filtro.Status);
        if (CalculadoraBiRads.Normalizar(filtro.BiRads) is { } bir)
            query = query.Where(l => l.BiRads == bir);
        if (filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CriadoEm >= ini);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(l => l.CriadoEm <= fim);
        }
        if (filtro.Vinculado == true)
            query = query.Where(l => l.PacienteId != null);
        else if (filtro.Vinculado == false)
            query = query.Where(l => l.PacienteId == null);
        if (filtro.Assinado == true)
            query = query.Where(l => _db.LaudoAssinaturas.Any(a => a.LaudoId == l.Id && a.Status == StatusAssinatura.Concluida));
        else if (filtro.Assinado == false)
            query = query.Where(l => !_db.LaudoAssinaturas.Any(a => a.LaudoId == l.Id && a.Status == StatusAssinatura.Concluida));

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            // Busca livre: paciente (nome/CPF/CNS via hub → ids) OU nome DICOM do estudo OU nº do
            // pedido (accession/nosso número) e nº SISREG (código da solicitação) — estes casam
            // pelos studies dos exames correspondentes (study próprio do exame + associações).
            var termo = filtro.Termo.Trim();
            var padrao = $"%{termo}%";
            var idsPaciente = (await _pacienteResolver.BuscarIdsPorTermoAsync(termo, tamanho, cancellationToken)).ToArray();

            var examesCasando = _db.ExamesImagem.AsNoTracking().Where(e => e.ExcluidoEm == null
                && (EF.Functions.ILike(e.AccessionNumber, padrao)
                    || (e.Solicitacao!.CodigoSolicitacao != null
                        && EF.Functions.ILike(e.Solicitacao!.CodigoSolicitacao, padrao))));
            var studiesProprios = examesCasando.Select(e => e.StudyInstanceUID);
            var idsExamesCasando = examesCasando.Select(e => e.Id);
            var studiesAssociados = _db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.ExcluidoEm == null && idsExamesCasando.Contains(a.ExameImagemId))
                .Select(a => a.StudyInstanceUID);

            query = query.Where(l =>
                (l.PacienteId != null && idsPaciente.Contains(l.PacienteId.Value))
                || (l.PacienteNomeDicom != null && EF.Functions.ILike(l.PacienteNomeDicom, padrao))
                || studiesProprios.Contains(l.StudyInstanceUID)
                || studiesAssociados.Contains(l.StudyInstanceUID));
        }

        query = await AplicarEscopoUnidadeAsync(query, cancellationToken);

        var total = await query.CountAsync(cancellationToken);
        var lista = await query
            .OrderByDescending(l => l.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.Select(LaudosMapper.ParaListItem)], cancellationToken);
        var assinados = await ResolverAssinadosAsync([.. dtos.Select(d => d.Id)], cancellationToken);
        var comAssinatura = dtos
            .Select(d => assinados.TryGetValue(d.Id, out var semCertificado)
                ? d with { Assinado = true, AssinaturaSemCertificado = semCertificado }
                : d)
            .ToList();
        var comComunicacao = await EnriquecerComunicacoesAsync(comAssinatura, cancellationToken);
        var itens = await EnriquecerPedidoAsync([.. comComunicacao], cancellationToken);
        return new PaginaLaudosDto(itens, total, pagina, tamanho);
    }

    /// <summary>
    /// Anexa a cada linha o contexto do PEDIDO — nº SMS, nº SISREG, procedimento, modalidade e
    /// unidade executante —, o que dá à lista de laudos a mesma leitura da de Solicitações e da de
    /// Exames.
    ///
    /// <para>O laudo se liga ao exame só pelo <c>StudyInstanceUID</c>, sem FK, então a resolução
    /// usa os dois caminhos de sempre: associação explícita (que prevalece) e worklist consumada.
    /// Laudo cujo study não casa com solicitação nenhuma fica com os campos nulos — é o órfão, que
    /// a lista continua mostrando de propósito.</para>
    /// </summary>
    private async Task<IReadOnlyList<LaudoListItemDto>> EnriquecerPedidoAsync(
        List<LaudoListItemDto> dtos, CancellationToken ct)
    {
        var studies = dtos.Select(d => d.StudyInstanceUID)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (studies.Length == 0) return dtos;

        var contexto = new Dictionary<string, PedidoDoLaudo>(StringComparer.Ordinal);

        var associadas = await _db.ExameAssociacoes.AsNoTracking()
            .Where(a => studies.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .Select(a => new PedidoDoLaudo(
                a.StudyInstanceUID,
                a.ExameImagem!.AccessionNumber,
                a.ExameImagem!.Solicitacao!.CodigoSolicitacao,
                a.ExameImagem!.TipoExame!.Nome,
                (ModalidadeDicom?)a.ExameImagem!.TipoExame!.ModalidadeDicom,
                a.ExameImagem!.Solicitacao!.UnidadeExecutante!.Nome))
            .ToListAsync(ct);
        foreach (var a in associadas) contexto[a.StudyInstanceUID] = a;

        var faltam = studies.Where(u => !contexto.ContainsKey(u)).ToArray();
        if (faltam.Length > 0)
        {
            var diretas = await _db.ExamesImagem.AsNoTracking()
                .Where(e => faltam.Contains(e.StudyInstanceUID) && e.ExcluidoEm == null)
                .Select(e => new PedidoDoLaudo(
                    e.StudyInstanceUID,
                    e.AccessionNumber,
                    e.Solicitacao!.CodigoSolicitacao,
                    e.TipoExame!.Nome,
                    (ModalidadeDicom?)e.TipoExame!.ModalidadeDicom,
                    e.Solicitacao!.UnidadeExecutante!.Nome))
                .ToListAsync(ct);
            foreach (var d in diretas) contexto.TryAdd(d.StudyInstanceUID, d);
        }

        if (contexto.Count == 0) return dtos;

        return [.. dtos.Select(d => contexto.TryGetValue(d.StudyInstanceUID, out var p)
            ? d with
            {
                AccessionNumber = p.AccessionNumber,
                CodigoSolicitacao = p.CodigoSolicitacao,
                TipoExameNome = p.TipoExameNome,
                Modalidade = p.Modalidade,
                UnidadeExecutanteNome = p.UnidadeExecutanteNome,
            }
            : d)];
    }

    /// <summary>Projeção do pedido por study — os dois caminhos devolvem esta mesma forma.</summary>
    private sealed record PedidoDoLaudo(
        string StudyInstanceUID,
        string? AccessionNumber,
        string? CodigoSolicitacao,
        string? TipoExameNome,
        ModalidadeDicom? Modalidade,
        string? UnidadeExecutanteNome);

    // Checks do aviso "laudo pronto" (zap) na lista: resolve a solicitação de cada study
    // (direto pela worklist consumada ou via associação explícita) e anexa o estado da
    // comunicação às linhas ASSINADAS — o aviso só é enfileirado depois da assinatura.
    private async Task<IReadOnlyList<LaudoListItemDto>> EnriquecerComunicacoesAsync(
        List<LaudoListItemDto> dtos, CancellationToken ct)
    {
        var studies = dtos.Where(d => d.Assinado).Select(d => d.StudyInstanceUID).Distinct().ToArray();
        if (studies.Length == 0) return dtos;

        // Mapa study → id da ESPINHA (as comunicações são ancoradas nela).
        var diretas = await _db.ExamesImagem.AsNoTracking()
            .Where(s => studies.Contains(s.StudyInstanceUID) && s.ExcluidoEm == null)
            .Select(s => new { s.StudyInstanceUID, SolicitacaoId = s.SolicitacaoId })
            .ToListAsync(ct);
        var associadas = await _db.ExameAssociacoes.AsNoTracking()
            .Where(a => studies.Contains(a.StudyInstanceUID) && a.ExcluidoEm == null)
            .Select(a => new { a.StudyInstanceUID, SolicitacaoId = a.ExameImagem!.SolicitacaoId })
            .ToListAsync(ct);

        var solicitacaoPorStudy = new Dictionary<string, Guid>();
        foreach (var v in diretas.Concat(associadas))
            solicitacaoPorStudy.TryAdd(v.StudyInstanceUID, v.SolicitacaoId);
        if (solicitacaoPorStudy.Count == 0) return dtos;

        var solicitacaoIds = solicitacaoPorStudy.Values.Distinct().ToArray();
        var chips = (await _db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId != null
                        && solicitacaoIds.Contains(c.SolicitacaoId.Value)
                        && c.Finalidade == FinalidadeComunicacao.LaudoPronto)
            .Select(c => new { c.SolicitacaoId, c.Status, c.VisualizadoEm, c.MotivoFalha })
            .ToListAsync(ct))
            .ToDictionary(
                c => c.SolicitacaoId!.Value,
                c => new SolicitacoesExame.Dtos.ComunicacaoChipDto(
                    c.Status.ToString(), c.VisualizadoEm != null, c.MotivoFalha));
        if (chips.Count == 0) return dtos;

        return [.. dtos.Select(d =>
            d.Assinado
            && solicitacaoPorStudy.TryGetValue(d.StudyInstanceUID, out var sid)
            && chips.TryGetValue(sid, out var chip)
                ? d with { ChipLaudoPronto = chip }
                : d)];
    }

    // Multitenancy por unidade (mesmo escopo de exames/consultas): restringe a listagem aos
    // laudos cujo study resolve a uma solicitação de unidade vinculada ao usuário — direto pela
    // worklist consumada (ExameImagem) ou via associação explícita. A cascata de resolução vive em
    // EscopoUnidade (ADR-0033); aqui só se aplica o filtro, porque o caminho do laudo até a unidade
    // é indireto (via study). Sem vínculo nenhum ⇒ não vê nada (fail-closed, ADR-0037).
    private async Task<IQueryable<Laudo>> AplicarEscopoUnidadeAsync(
        IQueryable<Laudo> query, CancellationToken ct)
    {
        var escopo = await EscopoUnidade.ResolverAsync(_db, _usuarioAtual, ct);
        if (escopo.VeTudo) return query;
        if (escopo.SemAcesso) return query.Where(_ => false);
        return FiltrarPorUnidades(query, escopo.Unidades);
    }

    // O laudo não tem FK de unidade: a tradução study → solicitação usa os dois caminhos que o
    // serviço já usa nas comunicações, exigindo EXECUTORA ou SOLICITANTE dentro do escopo. Laudo
    // ÓRFÃO (study sem vínculo com solicitação nenhuma) permanece visível: sem solicitação não há
    // unidade dona, e escondê-lo tiraria a linha da tela de gestão de órfãos.
    private IQueryable<Laudo> FiltrarPorUnidades(IQueryable<Laudo> query, Guid[] unidades)
    {
        return query.Where(l =>
            _db.ExamesImagem.Any(e => e.StudyInstanceUID == l.StudyInstanceUID
                && e.ExcluidoEm == null && e.Solicitacao!.ExcluidoEm == null
                && (unidades.Contains(e.Solicitacao!.UnidadeExecutanteId)
                    || (e.Solicitacao!.UnidadeSolicitanteId != null
                        && unidades.Contains(e.Solicitacao!.UnidadeSolicitanteId.Value))))
            || _db.ExameAssociacoes.Any(a => a.StudyInstanceUID == l.StudyInstanceUID
                && a.ExcluidoEm == null
                && a.ExameImagem!.ExcluidoEm == null && a.ExameImagem!.Solicitacao!.ExcluidoEm == null
                && (unidades.Contains(a.ExameImagem!.Solicitacao!.UnidadeExecutanteId)
                    || (a.ExameImagem!.Solicitacao!.UnidadeSolicitanteId != null
                        && unidades.Contains(a.ExameImagem!.Solicitacao!.UnidadeSolicitanteId.Value))))
            || (!_db.ExamesImagem.Any(e => e.StudyInstanceUID == l.StudyInstanceUID && e.ExcluidoEm == null)
                && !_db.ExameAssociacoes.Any(a => a.StudyInstanceUID == l.StudyInstanceUID && a.ExcluidoEm == null)));
    }

    /// <summary>
    /// Laudos oficializados (assinatura Concluida) entre <paramref name="ids"/> → true quando
    /// o documento oficial saiu só com carimbo, sem ICP-Brasil (ADR-0061).
    /// </summary>
    private async Task<Dictionary<Guid, bool>> ResolverAssinadosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var assinados = await _db.LaudoAssinaturas.AsNoTracking()
            .Where(a => ids.Contains(a.LaudoId) && a.Status == StatusAssinatura.Concluida)
            .Select(a => new { a.LaudoId, a.Formato })
            .ToListAsync(ct);
        return assinados
            .GroupBy(a => a.LaudoId)
            .ToDictionary(g => g.Key, g => Assinatura.LaudoAssinaturaService.EhCarimboSemCertificado(g.First().Formato));
    }

    public async Task<LaudoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await CarregarCompletoAsync(id, asNoTracking: true, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);
        var dto = await EnriquecerAsync(LaudosMapper.ParaDto(l), cancellationToken);
        var oficial = await _db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == id && a.Status == StatusAssinatura.Concluida)
            .Select(a => new { a.Formato })
            .FirstOrDefaultAsync(cancellationToken);
        var assinado = oficial is not null;

        var (temRubrica, podeAssinar, motivo, modo) =
            await ResolverElegibilidadeAssinaturaAsync(l, assinado, cancellationToken);
        return dto with
        {
            Assinado = assinado,
            AssinaturaSemCertificado = assinado && Assinatura.LaudoAssinaturaService.EhCarimboSemCertificado(oficial!.Formato),
            MedicoTemRubrica = temRubrica,
            PodeAssinar = podeAssinar,
            MotivoBloqueioAssinatura = motivo,
            ModoAssinatura = modo,
        };
    }

    /// <summary>
    /// Elegibilidade da assinatura digital para o usuário logado: ele é o autor +
    /// laudo finalizado + ainda não assinado + o autor tem rubrica cadastrada.
    /// Resolvido no servidor (e não no front) porque a rubrica vive sob o módulo
    /// Medicos — permissão que o próprio médico não possui; o front nunca saberia
    /// consultá-la sozinho.
    /// </summary>
    private async Task<(bool TemRubrica, bool PodeAssinar, string? Motivo, ModoAssinaturaMedico Modo)> ResolverElegibilidadeAssinaturaAsync(
        Laudo l, bool assinado, CancellationToken ct)
    {
        var temRubrica = await _assinaturaMedico.ObterAsync(l.MedicoId, ct) is not null;
        var modo = (await _assinaturaMedico.ObterModoAsync(l.MedicoId, ct)).Modo;
        // Modo Nuvem sem canal IntegraICP configurado nesta instância: não adianta liberar o botão.
        var nuvemIndisponivel = modo == ModoAssinaturaMedico.Nuvem && !_nuvemHabilitada;

        var ehAutor = false;
        if (_usuarioAtual.UsuarioId is { } usuarioId)
        {
            var medicoLogado = await ResolverMedicoOuNullAsync(usuarioId, ct);
            ehAutor = medicoLogado is not null && medicoLogado.Id == l.MedicoId;
        }

        // Assinar SEMPRE exige associação (paciente confiável) — regra dura, não-configurável.
        var temAssociacao = await _associacao.ResolverVinculoAsync(l.StudyInstanceUID, ct) is not null;

        var finalizado = l.Status == StatusLaudo.Finalizado;
        var podeAssinar = ehAutor && finalizado && !assinado && temRubrica && temAssociacao && !nuvemIndisponivel;

        string? motivo = null;
        if (ehAutor && finalizado && !assinado)
        {
            if (!temAssociacao)
                motivo = "Associe o exame a um pedido antes de assinar.";
            else if (!temRubrica)
                motivo = "Rubrica não cadastrada — solicite ao administrador o cadastro da sua assinatura (imagem).";
            else if (nuvemIndisponivel)
                motivo = "Assinatura em nuvem não configurada nesta instância — peça ao administrador.";
        }

        return (temRubrica, podeAssinar, motivo, modo);
    }

    public async Task<LaudoDto?> ObterPorStudyAsync(
        string studyInstanceUID,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);
        var l = await _db.Laudos.AsNoTracking()
            .Include(x => x.LaudoTemplate)
            .Where(x => x.StudyInstanceUID == uid && !x.Excluido)
            .OrderByDescending(x => x.Versao)
            .FirstOrDefaultAsync(cancellationToken);
        return l is null ? null : await EnriquecerAsync(LaudosMapper.ParaDto(l), cancellationToken);
    }

    public async Task<IReadOnlyList<LaudoHistoricoItemDto>> ListarHistoricoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alvo = await _db.Laudos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.StudyInstanceUID)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        var historico = await _db.Laudos.AsNoTracking()
            .Where(x => x.StudyInstanceUID == alvo && !x.Excluido)
            .OrderByDescending(x => x.Versao)
            .ToListAsync(cancellationToken);

        return [.. historico.Select(LaudosMapper.ParaHistoricoItem)];
    }

    public async Task<IReadOnlyList<LaudoPorStudyDto>> ListarPorStudyUidsAsync(
        IReadOnlyList<string> studyInstanceUIDs,
        CancellationToken cancellationToken = default)
    {
        if (studyInstanceUIDs is null || studyInstanceUIDs.Count == 0)
            return [];

        var uids = studyInstanceUIDs
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct()
            .ToArray();

        if (uids.Length == 0) return [];

        var bruto = await _db.Laudos.AsNoTracking()
            .Where(l => uids.Contains(l.StudyInstanceUID) && !l.Excluido)
            .Select(l => new { l.StudyInstanceUID, l.Versao, l.Id, l.Status })
            .ToListAsync(cancellationToken);

        var maisRecentes = bruto
            .GroupBy(x => x.StudyInstanceUID)
            .Select(g => g.OrderByDescending(x => x.Versao).First())
            .ToList();

        // Resolve quais (do conjunto exibido) já estão assinados — habilita o gate de
        // associar/desassociar no front (livre até assinar; assinado trava).
        var assinados = await ResolverAssinadosAsync([.. maisRecentes.Select(x => x.Id)], cancellationToken);

        return [.. maisRecentes
            .Select(x => new LaudoPorStudyDto(
                x.StudyInstanceUID, x.Id, x.Versao, x.Status, assinados.ContainsKey(x.Id)))];
    }

    public async Task<Guid> CadastrarAsync(
        Guid usuarioId,
        CadastrarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(request.StudyInstanceUID);
        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        var config = await _configuracao.ObterAsync(cancellationToken);

        // Vínculo = associação explícita (tabela) OU casamento implícito por StudyInstanceUID
        // (exame de worklist). Por padrão é OBRIGATÓRIO (sem ele não há paciente confiável — o
        // patientId DICOM é texto livre). A configuração pode liberar iniciar sem associação;
        // ASSINAR, porém, sempre exige (ver LaudoAssinaturaService).
        var vinculo = await _associacao.ResolverVinculoAsync(uid, cancellationToken);
        if (vinculo is null && !config.PermitirLaudarSemAssociacao)
            throw new ValidacaoException("laudo.sem_associacao",
                "Associe o exame a um pedido antes de iniciar o laudo.");

        // Anamnese: por padrão exige a anamnese da solicitação preenchida (só faz sentido
        // quando há vínculo/solicitação). A configuração pode liberar iniciar sem anamnese.
        if (vinculo is not null && !config.PermitirLaudarSemAnamnese)
        {
            var temAnamnese = await _db.Anamneses.AsNoTracking()
                .AnyAsync(a => a.ExameImagemId == vinculo.SolicitacaoExameId, cancellationToken);
            if (!temAnamnese)
                throw new ValidacaoException("laudo.sem_anamnese",
                    "Preencha a anamnese da solicitação antes de iniciar o laudo.");
        }

        if (request.LaudoTemplateId.HasValue)
        {
            var existe = await _db.LaudoTemplates.AsNoTracking().AnyAsync(t => t.Id == request.LaudoTemplateId, cancellationToken);
            if (!existe)
                throw new NaoEncontradoException(nameof(LaudoTemplate), request.LaudoTemplateId);
        }

        var proximaVersao = (await _db.Laudos
            .Where(x => x.StudyInstanceUID == uid && !x.Excluido)
            .Select(x => (int?)x.Versao)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        // Sem vínculo → guarda o nome do DICOM (0010,0010) do estudo como rótulo
        // TEMPORÁRIO, só para o laudo órfão exibir "de quem parece ser" (cinza, na
        // listagem). Best-effort: PACS fora do ar não impede criar o laudo; limpo ao
        // associar. Com vínculo, o paciente confiável já resolve o nome — não captura.
        string? nomeDicomTemporario = null;
        if (vinculo is null)
        {
            // Cap de latência: o rótulo é só exibição — um PACS LENTO (não fora do ar) não
            // pode segurar a criação do laudo até o timeout de 10s do HttpClient. Timeout
            // próprio (curto). O cancelamento REAL do request (token externo) ainda propaga;
            // o estouro do nosso cap é engolido e o laudo nasce sem rótulo temporário.
            using var capturaCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            capturaCts.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                nomeDicomTemporario = await _consultaStudy.ObterNomePacienteAsync(uid, capturaCts.Token);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Falha/timeout ao ler PatientName do DICOM para {Uid} — laudo segue sem rótulo temporário.", uid);
            }
        }

        var agora = DateTime.UtcNow;
        var laudo = new Laudo
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            Versao = proximaVersao,
            PacienteId = vinculo?.PacienteId,
            PacienteNomeDicom = nomeDicomTemporario,
            MedicoId = medico.Id,
            LaudoTemplateId = request.LaudoTemplateId,
            Titulo = NormalizarTitulo(request.Titulo),
            ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson,
            ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty),
            Status = StatusLaudo.Rascunho,
            CriadoEm = agora,
        };
        AplicarChecklist(laudo, request.Checklist);

        _db.Laudos.Add(laudo);
        await _db.SaveChangesAsync(cancellationToken);
        return laudo.Id;
    }

    public async Task AtualizarAsync(
        Guid id,
        Guid usuarioId,
        AtualizarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status == StatusLaudo.Finalizado)
            throw new ConflitoException(
                "laudo.finalizado_imutavel",
                "Laudo finalizado não pode ser editado. Use 'Nova versão' para gerar um rascunho derivado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("laudo.nao_e_autor", "Apenas o médico autor do rascunho pode editá-lo.");

        // Só atualiza o paciente quando vier valor — NUNCA zera um PacienteId já
        // setado (o gate de CadastrarAsync preenche-o; o front pode mandar null
        // numa corrida de carregamento e apagaria o vínculo correto).
        if (request.PacienteId.HasValue)
        {
            if (request.PacienteId != laudo.PacienteId)
                await GarantirPacienteExisteAsync(request.PacienteId.Value, cancellationToken);
            laudo.PacienteId = request.PacienteId;
        }

        laudo.Titulo = NormalizarTitulo(request.Titulo);
        laudo.ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson;
        laudo.ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty);
        AplicarChecklist(laudo, request.Checklist);
        laudo.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizarAsync(
        Guid id,
        Guid usuarioId,
        FinalizarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos
            .FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status == StatusLaudo.Finalizado)
            throw new ConflitoException("laudo.ja_finalizado", "Laudo já está finalizado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("laudo.nao_e_autor", "Apenas o médico autor pode finalizar o laudo.");

        var html = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty);
        if (string.IsNullOrWhiteSpace(html))
            throw new ValidacaoException("laudo.conteudo_vazio", "Conteúdo do laudo não pode estar vazio.");

        laudo.Titulo = NormalizarTitulo(request.Titulo);
        laudo.ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson;
        laudo.ConteudoHtml = html;
        AplicarChecklist(laudo, request.Checklist);

        // Snapshot dos dados do médico (Practitioner do hub) no momento da finalização.
        laudo.MedicoNomeSnapshot = medico.NomeCompleto;
        laudo.MedicoCrmSnapshot = medico.Registro;
        laudo.MedicoUfCrmSnapshot = medico.UfConselho;
        laudo.MedicoRqeSnapshot = medico.Rqe;

        laudo.Status = StatusLaudo.Finalizado;
        laudo.FinalizadoEm = DateTime.UtcNow;
        laudo.AtualizadoEm = laudo.FinalizadoEm;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _solicitacoes.Value.MarcarComoLaudadaAsync(laudo.StudyInstanceUID, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao marcar solicitação como Laudada para StudyInstanceUID {Uid}.", laudo.StudyInstanceUID);
        }
    }

    public async Task<Guid> CriarNovaVersaoAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var anterior = await _db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (anterior.Status != StatusLaudo.Finalizado)
            throw new ConflitoException(
                "laudo.nova_versao_so_finalizado",
                "Só é possível criar nova versão a partir de um laudo finalizado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);

        var proximaVersao = (await _db.Laudos
            .Where(x => x.StudyInstanceUID == anterior.StudyInstanceUID && !x.Excluido)
            .Select(x => (int?)x.Versao)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var agora = DateTime.UtcNow;
        var novo = new Laudo
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = anterior.StudyInstanceUID,
            Versao = proximaVersao,
            LaudoAnteriorId = anterior.Id,
            PacienteId = anterior.PacienteId,
            PacienteNomeDicom = anterior.PacienteNomeDicom,
            MedicoId = medico.Id,
            LaudoTemplateId = anterior.LaudoTemplateId,
            Titulo = anterior.Titulo,
            ConteudoJson = anterior.ConteudoJson,
            ConteudoHtml = anterior.ConteudoHtml,
            BiRads = anterior.BiRads,
            BiRadsSugerido = anterior.BiRadsSugerido,
            RespostasChecklist = anterior.RespostasChecklist,
            Status = StatusLaudo.Rascunho,
            CriadoEm = agora,
        };

        _db.Laudos.Add(novo);
        await _db.SaveChangesAsync(cancellationToken);
        return novo.Id;
    }

    public async Task ExcluirAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status != StatusLaudo.Rascunho)
            throw new ConflitoException(
                "laudo.so_rascunho_apaga",
                "Apenas rascunhos podem ser excluídos. Laudos finalizados são imutáveis (CFM).");

        laudo.Excluido = true;
        laudo.ExcluidoEm = DateTime.UtcNow;
        laudo.ExcluidoPorUsuarioId = usuarioId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Laudo?> CarregarParaPdfAsync(Guid id, CancellationToken cancellationToken = default) =>
        await CarregarCompletoAsync(id, asNoTracking: true, cancellationToken);

    // ---------------- helpers ----------------

    /// <summary>
    /// Aplica as respostas do checklist ao laudo: o servidor recalcula o BI-RADS
    /// sugerido (regra "achado mais suspeito") e grava o final escolhido pela
    /// profissional (override; default = sugerido). Null = laudo de texto livre,
    /// não mexe nos campos.
    /// </summary>
    private static void AplicarChecklist(Laudo laudo, ChecklistLaudoInput? checklist)
    {
        if (checklist is null) return;

        var sugerido = CalculadoraBiRads.Sugerir(checklist.Contribuicoes ?? []);
        var final = CalculadoraBiRads.Normalizar(checklist.BiRadsFinal) ?? sugerido;

        laudo.BiRadsSugerido = sugerido;
        laudo.BiRads = final;
        laudo.RespostasChecklist = string.IsNullOrWhiteSpace(checklist.RespostasJson)
            ? null
            : checklist.RespostasJson;
    }

    private Task<Laudo?> CarregarCompletoAsync(Guid id, bool asNoTracking, CancellationToken ct)
    {
        IQueryable<Laudo> q = _db.Laudos.Include(x => x.LaudoTemplate);
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, ct);
    }

    /// <summary>
    /// Resolve o médico-laudador no hub FHIR, lançando se o usuário não for médico.
    /// MVP: o id do usuário logado é tratado como id do Practitioner (link
    /// Usuário↔Practitioner é fatia futura); fallback por CPF.
    /// </summary>
    private async Task<MedicoDto> ResolverMedicoAsync(Guid usuarioId, CancellationToken ct) =>
        await ResolverMedicoOuNullAsync(usuarioId, ct)
        ?? throw new ConflitoException(
            "laudo.usuario_sem_papel_medico",
            "Apenas médicos podem criar/editar Laudos.");

    /// <summary>
    /// Como <see cref="ResolverMedicoAsync"/>, mas devolve <c>null</c> em vez de
    /// lançar — usado na leitura (derivar elegibilidade de assinatura sem quebrar
    /// quando o usuário logado não é médico).
    /// </summary>
    private async Task<MedicoDto?> ResolverMedicoOuNullAsync(Guid usuarioId, CancellationToken ct)
    {
        // 1) Compat: médicos cujo login tem o MESMO id do Practitioner (Usuario.Id == Practitioner.Id).
        var p = await _practitionerFhir.ObterAsync(usuarioId, ct);

        // 2) Caso geral: o login é um Usuario à parte (id próprio) — casa pelo CPF com o
        //    Practitioner do hub (ex.: médico importado que ganhou login por CPF).
        if (p is null)
        {
            var cpf = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Cpf)
                .FirstOrDefaultAsync(ct);
            var cpfDigits = SoDigitos(cpf);
            if (cpfDigits.Length == 11)
            {
                var bundle = await _practitionerFhir.BuscarAsync(identifier: cpfDigits, ct: ct);
                p = bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Practitioner>().FirstOrDefault();
            }
        }

        return p is null ? null : MedicoFhirMapper.ParaDto(p);
    }

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string(v.Where(char.IsDigit).ToArray());

    private async Task GarantirPacienteExisteAsync(Guid pacienteId, CancellationToken ct)
    {
        var p = await _pacienteFhir.ObterAsync(pacienteId, ct);
        if (p is null)
            throw new NaoEncontradoException("Paciente", pacienteId);
    }

    private static string NormalizarUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            throw new ValidacaoException("laudo.studyUid_obrigatorio", "StudyInstanceUID é obrigatório.");
        return uid.Trim();
    }

    private static string NormalizarTitulo(string? titulo)
    {
        var t = (titulo ?? string.Empty).Trim();
        return string.IsNullOrEmpty(t) ? "Laudo" : t;
    }
}
