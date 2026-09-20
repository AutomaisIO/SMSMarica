using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Implementação da máquina de estados da manifestação (plano §2). As permissões do usuário atual
/// são lidas pelo mesmo mecanismo de <c>ConversaService</c>/<c>RegulacaoEscopo</c>
/// (<see cref="IIdentidadeService.ObterPermissoesResolvidasAsync"/>); o pertencimento a ponto de
/// resposta vem de <c>ouvidoria_ponto_resposta_membro</c>.
/// </summary>
public sealed class OuvidoriaManifestacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade,
    IAuditoriaService auditoria,
    IPacientesService pacientes,
    IOuvidoriaCatalogoService catalogo,
    OuvidoriaNotificador notificador,
    ILogger<OuvidoriaManifestacaoService> logger) : IOuvidoriaManifestacaoService
{
    private const string AutorCidadao = "Cidadão";
    private const string AutorSistema = "Sistema";

    /// <summary>Status finais: nada mais acontece (exceto anotação).</summary>
    private static readonly OuvidoriaStatus[] Finais =
        [OuvidoriaStatus.Concluida, OuvidoriaStatus.Arquivada, OuvidoriaStatus.EncaminhadaOutroOrgao];

    /// <summary>Em aberto para o relógio do cidadão (nem respondida nem final).</summary>
    private static readonly OuvidoriaStatus[] Abertos =
    [
        OuvidoriaStatus.Registrada, OuvidoriaStatus.EmTriagem, OuvidoriaStatus.Encaminhada,
        OuvidoriaStatus.AguardandoComplementacao, OuvidoriaStatus.RespondidaPelaArea,
        OuvidoriaStatus.EmValidacao, OuvidoriaStatus.EmRecurso,
    ];

    /// <summary>O que o ponto de resposta enxerga da manifestação encaminhada a ele (§2.9).</summary>
    private static readonly OuvidoriaStatus[] StatusVisiveisAoPonto =
    [
        OuvidoriaStatus.Encaminhada, OuvidoriaStatus.RespondidaPelaArea, OuvidoriaStatus.EmValidacao,
        OuvidoriaStatus.Respondida, OuvidoriaStatus.Concluida,
    ];

    private ContextoAcesso? _contexto;
    private string? _autorNome;

    // ================= Consulta =================

    public async Task<PaginaDto<ManifestacaoListaDto>> ListarAsync(ManifestacaoFiltro filtro, CancellationToken ct = default)
    {
        var ctx = await ContextoAsync(ct);
        var pagina = Math.Max(1, filtro.Pagina);
        // Tamanho ausente/zero na query (binding do record) cai no padrão do contrato (50), não em 1.
        var tamanho = filtro.Tamanho <= 0 ? 50 : Math.Min(filtro.Tamanho, 200);

        if (!ctx.VeAlgo) return new PaginaDto<ManifestacaoListaDto>([], 0, pagina, tamanho);

        var hoje = OuvidoriaPrazos.Hoje();
        var query = AplicarEscopo(Base(), ctx);

        if (filtro.Status is { Length: > 0 }) query = query.Where(m => filtro.Status.Contains(m.Status));
        if (filtro.Tipo is { } tipo) query = query.Where(m => m.Tipo == tipo);
        if (filtro.UnidadeId is { } unidadeId) query = query.Where(m => m.UnidadeId == unidadeId);
        if (filtro.PontoRespostaId is { } pontoId) query = query.Where(m => m.PontoRespostaId == pontoId);
        if (filtro.Prioridade is { } prioridade) query = query.Where(m => m.Prioridade == prioridade);
        if (filtro.Atrasadas == true) query = query.Where(m => Abertos.Contains(m.Status) && m.PrazoRespostaEm < hoje);
        if (filtro.AguardandoValidacao == true)
            query = query.Where(m => m.Status == OuvidoriaStatus.RespondidaPelaArea || m.Status == OuvidoriaStatus.EmValidacao);
        if (filtro.De is { } de) query = query.Where(m => m.RegistradaEm >= InicioUtc(de));
        if (filtro.Ate is { } ate) query = query.Where(m => m.RegistradaEm < InicioUtc(ate.AddDays(1)));

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = filtro.Busca.Trim();
            var protocolo = OuvidoriaProtocolo.Normalizar(termo);
            var digitos = SoDigitos(termo);
            var padrao = $"%{termo}%";
            var temSigilo = ctx.TemSigilo;
            if (ctx.TemOuvidoria)
            {
                // Nome/CPF só em manifestação cuja identidade este usuário pode ver — senão a busca
                // vazaria a existência de uma sigilosa pelo nome.
                query = query.Where(m =>
                    m.Protocolo.Contains(protocolo)
                    || (m.Resumo != null && EF.Functions.ILike(m.Resumo, padrao))
                    || (m.Identificacao == OuvidoriaIdentificacao.Identificada
                        && (temSigilo || m.Tipo != OuvidoriaTipo.Denuncia)
                        && ((m.ManifestanteNome != null && EF.Functions.ILike(m.ManifestanteNome, padrao))
                            || (digitos.Length == 11 && m.ManifestanteCpf == digitos))));
            }
            else
            {
                query = query.Where(m => m.Protocolo.Contains(protocolo)
                    || (m.Resumo != null && EF.Functions.ILike(m.Resumo, padrao)));
            }
        }

        var total = await query.CountAsync(ct);
        var linhas = await query
            .OrderByDescending(m => m.UltimaAtividadeEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(m => new
            {
                m.Id, m.Protocolo, m.Tipo, m.Status, m.Prioridade, m.Identificacao, m.Canal, m.Resumo,
                AssuntoNome = m.Assunto != null ? m.Assunto.Nome : null,
                UnidadeNome = m.Unidade != null ? m.Unidade.Nome : null,
                PontoRespostaNome = m.PontoResposta != null ? m.PontoResposta.Nome : null,
                m.ManifestanteNome, m.RegistradaEm, m.PrazoRespostaEm, m.PrazoAreaEm, m.DiasAtraso,
                m.UltimaAtividadeEm, m.ResponsavelId, m.PontoRespostaId,
            })
            .ToListAsync(ct);

        var nomes = await NomesUsuariosAsync(linhas.Where(l => l.ResponsavelId.HasValue).Select(l => l.ResponsavelId!.Value), ct);

        var itens = linhas.Select(l =>
        {
            var modoPonto = ModoPonto(ctx, l.Tipo, l.PontoRespostaId);
            var restrita = modoPonto || IdentidadeRestrita(ctx, l.Tipo, l.Identificacao);
            var aberta = Abertos.Contains(l.Status);
            var diasAtraso = aberta ? OuvidoriaPrazos.DiasAtraso(l.PrazoRespostaEm, hoje) : l.DiasAtraso ?? 0;
            return new ManifestacaoListaDto(
                l.Id, l.Protocolo, l.Tipo, l.Status, l.Prioridade, l.Identificacao, l.Canal, l.Resumo,
                l.AssuntoNome, l.UnidadeNome, l.PontoRespostaNome,
                restrita ? null : l.ManifestanteNome,
                l.RegistradaEm, l.PrazoRespostaEm, l.PrazoAreaEm,
                diasAtraso, aberta && diasAtraso > 0,
                l.Status == OuvidoriaStatus.Encaminhada && l.PrazoAreaEm is { } pa && pa < hoje,
                l.UltimaAtividadeEm,
                l.ResponsavelId is { } r ? nomes.GetValueOrDefault(r) : null);
        }).ToList();

        return new PaginaDto<ManifestacaoListaDto>(itens, total, pagina, tamanho);
    }

    public async Task<OuvidoriaResumoDto> ObterResumoAsync(CancellationToken ct = default)
    {
        var ctx = await ContextoAsync(ct);
        if (!ctx.VeAlgo) return new OuvidoriaResumoDto(0, 0, 0, 0, 0, 0, 0, 0, 0);

        var hoje = OuvidoriaPrazos.Hoje();
        var query = AplicarEscopo(Base(), ctx);

        var porStatus = await query
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Qtd, ct);

        var atrasadas = await query.CountAsync(m => Abertos.Contains(m.Status) && m.PrazoRespostaEm < hoje, ct);
        var areaAtrasadas = await query.CountAsync(m => m.Status == OuvidoriaStatus.Encaminhada && m.PrazoAreaEm != null && m.PrazoAreaEm < hoje, ct);

        var pontos = ctx.Pontos;
        var meuPonto = pontos.Length == 0
            ? 0
            : await Base().CountAsync(m => m.Status == OuvidoriaStatus.Encaminhada
                && m.PontoRespostaId != null && pontos.Contains(m.PontoRespostaId.Value), ct);

        return new OuvidoriaResumoDto(
            porStatus.GetValueOrDefault(OuvidoriaStatus.Registrada),
            porStatus.GetValueOrDefault(OuvidoriaStatus.EmTriagem),
            porStatus.GetValueOrDefault(OuvidoriaStatus.Encaminhada),
            porStatus.GetValueOrDefault(OuvidoriaStatus.AguardandoComplementacao),
            porStatus.GetValueOrDefault(OuvidoriaStatus.RespondidaPelaArea) + porStatus.GetValueOrDefault(OuvidoriaStatus.EmValidacao),
            atrasadas,
            areaAtrasadas,
            porStatus.GetValueOrDefault(OuvidoriaStatus.EmRecurso),
            meuPonto);
    }

    public async Task<ManifestacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var ctx = await ContextoAsync(ct);
        if (!ctx.VeAlgo) throw new NaoEncontradoException(nameof(OuvidoriaManifestacao), id);

        var m = await AplicarEscopo(Base(), ctx)
            .Include(x => x.Assunto)
            .Include(x => x.Unidade)
            .Include(x => x.PontoResposta)
            .Include(x => x.Marcadores)
            .Include(x => x.Eventos.OrderBy(e => e.CriadoEm))
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaManifestacao), id);

        var hoje = OuvidoriaPrazos.Hoje();
        var modoPonto = ModoPonto(ctx, m.Tipo, m.PontoRespostaId);
        var restrita = modoPonto || IdentidadeRestrita(ctx, m.Tipo, m.Identificacao);

        // Eventos: o ponto vê os do próprio ponto + os visíveis ao cidadão; a ouvidoria vê tudo.
        var pontos = ctx.Pontos;
        var eventos = modoPonto
            ? m.Eventos.Where(e => e.VisivelAoCidadao || (e.PontoRespostaId is { } p && pontos.Contains(p))).ToList()
            : m.Eventos.ToList();
        var eventoIds = eventos.Select(e => e.Id).ToHashSet();
        var anexos = modoPonto
            ? m.Anexos.Where(a => a.VisivelAoCidadao || (a.EventoId is { } e && eventoIds.Contains(e))).ToList()
            : m.Anexos.ToList();

        var pontoIds = eventos.Where(e => e.PontoRespostaId.HasValue).Select(e => e.PontoRespostaId!.Value).Distinct().ToList();
        var nomesPontos = pontoIds.Count == 0
            ? []
            : await db.OuvidoriaPontosResposta.AsNoTracking()
                .Where(p => pontoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
        var nomes = await NomesUsuariosAsync(m.ResponsavelId is { } r ? [r] : [], ct);

        var eventosDto = eventos.Select(e => new EventoDto(
            e.Id, e.Tipo, e.StatusAnterior, e.StatusNovo, e.AutorNome,
            e.PontoRespostaId is { } p ? nomesPontos.GetValueOrDefault(p) : null,
            e.Texto, e.VisivelAoCidadao, e.CriadoEm,
            [.. anexos.Where(a => a.EventoId == e.Id).Select(MapAnexo)])).ToList();

        var duplicatas = modoPonto || string.IsNullOrEmpty(m.ManifestanteCpf)
            ? []
            : await PossiveisDuplicatasAsync(m, ct);

        var aberta = Abertos.Contains(m.Status);
        var diasAtraso = aberta ? OuvidoriaPrazos.DiasAtraso(m.PrazoRespostaEm, hoje) : m.DiasAtraso ?? 0;
        var temRecurso = m.Eventos.Any(e => e.Tipo == OuvidoriaTipoEvento.Recurso);
        var membroDoPonto = m.PontoRespostaId is { } pr && pontos.Contains(pr);

        var manifestante = restrita || m.Identificacao == OuvidoriaIdentificacao.Anonima
            ? null
            : new ManifestanteDto(m.ManifestanteNome, m.ManifestanteCpf, m.ManifestanteTelefone, m.ManifestanteEmail, m.ManifestantePatientId);
        var referido = modoPonto || (m.ReferidoPatientId is null && m.ReferidoNome is null && m.ReferidoCpf is null && m.ReferidoCns is null)
            ? null
            : new ReferidoDto(m.ReferidoPatientId, m.ReferidoNome, m.ReferidoCpf, m.ReferidoCns);
        var teor = modoPonto && m.Tipo == OuvidoriaTipo.Denuncia ? m.TeorPseudonimizado ?? m.Teor : m.Teor;

        return new ManifestacaoDetalheDto(
            m.Id, m.Protocolo, m.Tipo, m.Status, m.Prioridade, m.Identificacao, m.Canal, m.Resumo,
            m.Assunto?.Nome, m.Unidade?.Nome, m.PontoResposta?.Nome,
            restrita ? null : m.ManifestanteNome,
            m.RegistradaEm, m.PrazoRespostaEm, m.PrazoAreaEm,
            diasAtraso, aberta && diasAtraso > 0,
            m.Status == OuvidoriaStatus.Encaminhada && m.PrazoAreaEm is { } pa && pa < hoje,
            m.UltimaAtividadeEm,
            m.ResponsavelId is { } resp ? nomes.GetValueOrDefault(resp) : null,
            teor,
            modoPonto ? null : m.TeorPseudonimizado,
            manifestante,
            restrita && m.Identificacao != OuvidoriaIdentificacao.Anonima,
            referido,
            m.EnvolvidoPractitionerId, m.EnvolvidoDescricao,
            m.AssuntoId, m.SubassuntoId, m.UnidadeId, m.PontoRespostaId, m.RegulacaoSolicitacaoId,
            m.ProtocoloExterno, m.SistemaExterno, m.DataFato, m.LocalFato, m.Origem,
            m.ProrrogadoEm, m.ProrrogacaoJustificativa, m.ComplementacaoUsada,
            m.EncaminhadaEm, m.RespondidaEm, m.ConcluidaEm,
            m.Resolutividade, m.SituacaoFinal, m.MotivoNaoAtendimento, m.MotivoArquivamento,
            m.RespostaConclusiva, m.HabilitadaEm, m.ResponsavelId,
            [.. m.Marcadores.Select(mm => mm.MarcadorId)],
            duplicatas,
            eventosDto,
            [.. anexos.Select(MapAnexo)],
            AcoesPermitidas(m, ctx, modoPonto, membroDoPonto, temRecurso));
    }

    public async Task<OuvidoriaPainelDto> ObterPainelAsync(DateOnly? de, DateOnly? ate, Guid? unidadeId, CancellationToken ct = default)
    {
        var hoje = OuvidoriaPrazos.Hoje();
        var fim = ate ?? hoje;
        var inicio = de ?? fim.AddDays(-30);
        if (inicio > fim) (inicio, fim) = (fim, inicio);

        var query = Base().Where(m => m.RegistradaEm >= InicioUtc(inicio) && m.RegistradaEm < InicioUtc(fim.AddDays(1)));
        if (unidadeId is { } u) query = query.Where(m => m.UnidadeId == u);

        var linhas = await query
            .Select(m => new
            {
                m.Id, m.Tipo, m.Status, m.Canal, m.AssuntoId, m.UnidadeId, m.RespondidaEm, m.DiasAteResposta,
                m.DiasAtraso, m.Resolutividade,
            })
            .ToListAsync(ct);

        var assuntoIds = linhas.Where(l => l.AssuntoId.HasValue).Select(l => l.AssuntoId!.Value).Distinct().ToList();
        var nomesAssuntos = assuntoIds.Count == 0
            ? []
            : await db.OuvidoriaAssuntos.AsNoTracking().Where(a => assuntoIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Nome, ct);
        var unidadeIds = linhas.Where(l => l.UnidadeId.HasValue).Select(l => l.UnidadeId!.Value).Distinct().ToList();
        var nomesUnidades = unidadeIds.Count == 0
            ? []
            : await db.Unidades.AsNoTracking().Where(x => unidadeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Nome, ct);

        // Tempo médio da área: primeiro Encaminhamento → primeira RespostaArea posterior, por manifestação.
        double? tempoMedioArea = null;
        if (linhas.Count > 0)
        {
            var ids = linhas.Select(l => l.Id).ToList();
            var eventos = await db.OuvidoriaEventos.AsNoTracking()
                .Where(e => ids.Contains(e.ManifestacaoId)
                    && (e.Tipo == OuvidoriaTipoEvento.Encaminhamento || e.Tipo == OuvidoriaTipoEvento.RespostaArea))
                .Select(e => new { e.ManifestacaoId, e.Tipo, e.CriadoEm })
                .ToListAsync(ct);
            var duracoes = eventos.GroupBy(e => e.ManifestacaoId)
                .Select(g =>
                {
                    var enc = g.Where(e => e.Tipo == OuvidoriaTipoEvento.Encaminhamento).MinBy(e => e.CriadoEm);
                    if (enc is null) return (double?)null;
                    var resp = g.Where(e => e.Tipo == OuvidoriaTipoEvento.RespostaArea && e.CriadoEm >= enc.CriadoEm).MinBy(e => e.CriadoEm);
                    return resp is null ? null : (resp.CriadoEm - enc.CriadoEm).TotalDays;
                })
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();
            if (duracoes.Count > 0) tempoMedioArea = Math.Round(duracoes.Average(), 1);
        }

        var respondidas = linhas.Where(l => l.RespondidaEm != null).ToList();
        var comTempo = respondidas.Where(l => l.DiasAteResposta.HasValue).Select(l => l.DiasAteResposta!.Value).ToList();

        return new OuvidoriaPainelDto(
            linhas.Count,
            Contar(linhas.Select(l => l.Tipo.ToString())),
            Contar(linhas.Select(l => l.Status.ToString())),
            Contar(linhas.Select(l => l.Canal.ToString())),
            Contar(linhas.Select(l => l.AssuntoId is { } a ? (a.ToString(), nomesAssuntos.GetValueOrDefault(a) ?? "(assunto)") : ("sem", "Sem assunto"))),
            Contar(linhas.Select(l => l.UnidadeId is { } un ? (un.ToString(), nomesUnidades.GetValueOrDefault(un) ?? "(unidade)") : ("sem", "Sem unidade"))),
            respondidas.Count,
            respondidas.Count(l => (l.DiasAtraso ?? 0) == 0),
            respondidas.Count(l => (l.DiasAtraso ?? 0) > 0),
            comTempo.Count == 0 ? null : Math.Round(comTempo.Average(), 1),
            tempoMedioArea,
            linhas.Count(l => Abertos.Contains(l.Status)),
            linhas.Count(l => l.Resolutividade == OuvidoriaResolutividade.Resolvida),
            linhas.Count(l => l.Resolutividade == OuvidoriaResolutividade.NaoResolvida),
            [
                new ContagemDto("ate30", "Até 30 dias", comTempo.Count(d => OuvidoriaPrazos.FaixaPrazo(d) == "ate30")),
                new ContagemDto("31a60", "31 a 60 dias", comTempo.Count(d => OuvidoriaPrazos.FaixaPrazo(d) == "31a60")),
                new ContagemDto("mais60", "Mais de 60 dias", comTempo.Count(d => OuvidoriaPrazos.FaixaPrazo(d) == "mais60")),
            ]);
    }

    // ================= Registro =================

    public async Task<ManifestacaoCriadaDto> RegistrarAsync(RegistrarManifestacaoRequest request, CancellationToken ct = default)
    {
        var manifestante = request.Identificacao == OuvidoriaIdentificacao.Anonima ? null : request.Manifestante;
        ValidarIdentificacao(request.Tipo, request.Identificacao, manifestante);

        await catalogo.GarantirCatalogoBaseAsync(ct);
        var config = await ConfigAsync(ct);
        await ValidarReferenciasAsync(request.AssuntoId, request.SubassuntoId, request.UnidadeId, request.RegulacaoSolicitacaoId, ct);

        var agora = DateTime.UtcNow;
        var hoje = OuvidoriaPrazos.Hoje();
        var seq = await ProximaSequenciaAsync(ct);
        var codigo = request.Identificacao == OuvidoriaIdentificacao.Anonima ? null : OuvidoriaProtocolo.GerarCodigoAcesso();
        var cpf = Limpar(SoDigitos(manifestante?.Cpf));
        var patientId = manifestante?.PatientId ?? await ResolverPacienteAsync(cpf, ct);
        var autor = await AutorNomeAsync(ct);

        var m = new OuvidoriaManifestacao
        {
            Id = Guid.CreateVersion7(),
            Protocolo = OuvidoriaProtocolo.Formatar(hoje.Year, seq),
            CodigoAcessoHash = codigo is null ? null : OuvidoriaProtocolo.Hash(codigo),
            Tipo = request.Tipo,
            Identificacao = request.Identificacao,
            Canal = request.Canal,
            Origem = request.Origem,
            Prioridade = OuvidoriaPrioridade.Normal,
            Status = OuvidoriaStatus.Registrada,
            AssuntoId = request.AssuntoId,
            SubassuntoId = request.SubassuntoId,
            Resumo = Limpar(request.Resumo),
            Teor = request.Teor.Trim(),
            UnidadeId = request.UnidadeId,
            RegulacaoSolicitacaoId = request.RegulacaoSolicitacaoId,
            ProtocoloExterno = Limpar(request.ProtocoloExterno),
            SistemaExterno = Limpar(request.SistemaExterno),
            DataFato = request.DataFato,
            LocalFato = Limpar(request.LocalFato),
            ManifestanteNome = Limpar(manifestante?.Nome),
            ManifestanteCpf = cpf,
            ManifestanteTelefone = Limpar(SoDigitos(manifestante?.Telefone)),
            ManifestanteEmail = Limpar(manifestante?.Email)?.ToLowerInvariant(),
            ManifestantePatientId = patientId,
            ReferidoPatientId = request.Referido?.PatientId,
            ReferidoNome = Limpar(request.Referido?.Nome),
            ReferidoCpf = Limpar(SoDigitos(request.Referido?.Cpf)),
            ReferidoCns = Limpar(SoDigitos(request.Referido?.Cns)),
            EnvolvidoDescricao = Limpar(request.EnvolvidoDescricao),
            RegistradaEm = agora,
            PrazoRespostaEm = OuvidoriaPrazos.PrazoCidadao(hoje, config.PrazoCidadaoDias),
            UltimaAtividadeEm = agora,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };

        var evento = NovoEvento(m, OuvidoriaTipoEvento.Registro, null, OuvidoriaStatus.Registrada,
            $"Manifestação registrada pelo canal {RotuloCanal(m.Canal)}.", visivel: true, autor);
        AdicionarAnexos(m, evento, request.Anexos, visivel: true, agora);

        db.OuvidoriaManifestacoes.Add(m);
        await db.SaveChangesAsync(ct);

        await notificador.RegistroAsync(m, codigo, config, ct);
        return new ManifestacaoCriadaDto(m.Id, m.Protocolo, codigo, m.PrazoRespostaEm);
    }

    // ================= Triagem / encaminhamento =================

    public async Task TriarAsync(Guid id, TriarRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        Exigir(m, [OuvidoriaStatus.Registrada, OuvidoriaStatus.EmTriagem], OuvidoriaStatus.EmTriagem);
        var config = await ConfigAsync(ct);
        var autor = await AutorNomeAsync(ct);
        var mudancas = new List<string>();

        if (request.Tipo is { } novoTipo && novoTipo != m.Tipo)
        {
            var manifestante = new ManifestanteDto(m.ManifestanteNome, m.ManifestanteCpf, m.ManifestanteTelefone, m.ManifestanteEmail, m.ManifestantePatientId);
            ValidarIdentificacao(novoTipo, m.Identificacao, manifestante);
            var anterior = m.Tipo;
            m.Tipo = novoTipo;
            // Denúncia que vira outro tipo perde a habilitação (o juízo de admissibilidade era da denúncia).
            if (anterior == OuvidoriaTipo.Denuncia) { m.HabilitadaEm = null; m.HabilitadaPor = null; }
            NovoEvento(m, OuvidoriaTipoEvento.Reclassificacao, null, null,
                $"Reclassificada de {anterior} para {novoTipo}.", visivel: anterior == OuvidoriaTipo.Denuncia, autor);
            mudancas.Add($"tipo {anterior} → {novoTipo}");
        }

        await ValidarReferenciasAsync(request.AssuntoId, request.SubassuntoId, request.UnidadeId, request.RegulacaoSolicitacaoId, ct);

        if (request.AssuntoId is { } assunto && assunto != m.AssuntoId) { m.AssuntoId = assunto; m.SubassuntoId = request.SubassuntoId; mudancas.Add("assunto"); }
        if (request.SubassuntoId is { } sub && sub != m.SubassuntoId) { m.SubassuntoId = sub; mudancas.Add("subassunto"); }
        if (request.UnidadeId is { } unidade && unidade != m.UnidadeId) { m.UnidadeId = unidade; mudancas.Add("unidade"); }
        if (request.Resumo is not null) { m.Resumo = Limpar(request.Resumo); mudancas.Add("resumo"); }
        if (request.ResponsavelId is { } resp && resp != m.ResponsavelId)
        {
            if (!await db.Usuarios.AnyAsync(u => u.Id == resp && u.ExcluidoEm == null, ct))
                throw new NaoEncontradoException("Usuario", resp);
            m.ResponsavelId = resp;
            mudancas.Add("responsável");
        }
        if (request.RegulacaoSolicitacaoId is { } reg && reg != m.RegulacaoSolicitacaoId) { m.RegulacaoSolicitacaoId = reg; mudancas.Add("vínculo com regulação"); }
        if (request.Prioridade is { } prioridade && prioridade != m.Prioridade)
        {
            m.Prioridade = prioridade;
            // Ainda não encaminhada: o prazo da área é só previsão e acompanha a prioridade.
            if (m.EncaminhadaEm is null)
                m.PrazoAreaEm = OuvidoriaPrazos.PrazoArea(OuvidoriaPrazos.Hoje(), prioridade, config, null);
            mudancas.Add($"prioridade {prioridade}");
        }
        if (request.MarcadorIds is not null)
        {
            var desejados = request.MarcadorIds.Distinct().ToHashSet();
            var validos = await db.OuvidoriaMarcadores.Where(x => desejados.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
            if (validos.Count != desejados.Count) throw new ValidacaoException("marcadorIds", "Um ou mais marcadores não existem.");
            foreach (var atual in m.Marcadores.ToList())
            {
                if (!desejados.Remove(atual.MarcadorId)) db.OuvidoriaManifestacaoMarcadores.Remove(atual);
            }
            foreach (var novo in desejados)
            {
                m.Marcadores.Add(new OuvidoriaManifestacaoMarcador { ManifestacaoId = m.Id, MarcadorId = novo });
            }
            mudancas.Add("marcadores");
        }

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.EmTriagem;
        NovoEvento(m, OuvidoriaTipoEvento.Triagem, statusAnterior, OuvidoriaStatus.EmTriagem,
            mudancas.Count == 0 ? "Triagem sem alterações." : $"Triagem: {string.Join(", ", mudancas)}.", visivel: false, autor);
        await db.SaveChangesAsync(ct);
    }

    public async Task EncaminharAsync(Guid id, EncaminharRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        Exigir(m,
            [OuvidoriaStatus.Registrada, OuvidoriaStatus.EmTriagem, OuvidoriaStatus.RespondidaPelaArea, OuvidoriaStatus.EmValidacao, OuvidoriaStatus.EmRecurso],
            OuvidoriaStatus.Encaminhada);

        var ponto = await db.OuvidoriaPontosResposta.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PontoRespostaId && p.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaPontoResposta), request.PontoRespostaId);
        if (!ponto.Ativo) throw new ConflitoException("ouvidoria.ponto_inativo", $"O ponto de resposta \"{ponto.Nome}\" está inativo.");

        if (!string.IsNullOrWhiteSpace(request.TeorPseudonimizado)) m.TeorPseudonimizado = request.TeorPseudonimizado.Trim();

        if (m.Tipo == OuvidoriaTipo.Denuncia)
        {
            if (m.HabilitadaEm is null)
                throw new ConflitoException("ouvidoria.denuncia_nao_habilitada", "A denúncia precisa passar pelo juízo de admissibilidade (habilitar) antes de ser encaminhada.");
            if (ponto.Tipo != OuvidoriaTipoPontoResposta.Apuracao)
                throw new ConflitoException("ouvidoria.denuncia_ponto_invalido", "Denúncia só pode ser encaminhada a um ponto de resposta do tipo Apuração.");
            if (string.IsNullOrWhiteSpace(m.TeorPseudonimizado))
                throw new ValidacaoException("teorPseudonimizado", "Preencha o teor pseudonimizado antes de encaminhar a denúncia — a unidade apuratória não vê o teor original.");
        }

        var config = await ConfigAsync(ct);
        var agora = DateTime.UtcNow;
        var hoje = OuvidoriaPrazos.Hoje();
        var autor = await AutorNomeAsync(ct);

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.Encaminhada;
        m.PontoRespostaId = ponto.Id;
        m.EncaminhadaEm = agora;
        m.PrazoAreaEm = request.PrazoDias is { } dias && dias > 0
            ? hoje.AddDays(dias)
            : OuvidoriaPrazos.PrazoArea(hoje, m.Prioridade, config, ponto.PrazoDias);

        NovoEvento(m, OuvidoriaTipoEvento.Encaminhamento, statusAnterior, OuvidoriaStatus.Encaminhada,
            $"Encaminhada à unidade/área responsável em {hoje:dd/MM/yyyy}.", visivel: true, autor, ponto.Id);
        if (!string.IsNullOrWhiteSpace(request.Texto))
        {
            // Orientação à área: interna, presa ao ponto para que ele a veja.
            NovoEvento(m, OuvidoriaTipoEvento.Anotacao, null, null, request.Texto.Trim(), visivel: false, autor, ponto.Id);
        }

        await db.SaveChangesAsync(ct);
        await notificador.EncaminhamentoAsync(m, config, ct);
    }

    // ================= Complementação =================

    public async Task PedirComplementacaoAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (m.Identificacao == OuvidoriaIdentificacao.Anonima)
            throw new ConflitoException("ouvidoria.anonima_sem_contato", "Manifestação anônima não tem contato para pedir complementação.");
        if (m.ComplementacaoUsada)
            throw new ConflitoException("ouvidoria.complementacao_unica", "A complementação só pode ser pedida uma vez (PN CGU 116, art. 27).");
        Exigir(m, Abertos.Where(s => s != OuvidoriaStatus.AguardandoComplementacao).ToArray(), OuvidoriaStatus.AguardandoComplementacao);

        var config = await ConfigAsync(ct);
        var agora = DateTime.UtcNow;
        var autor = await AutorNomeAsync(ct);

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.AguardandoComplementacao;
        m.ComplementacaoUsada = true;
        m.ComplementacaoSolicitadaEm = agora;
        m.SuspensaEm = agora;

        NovoEvento(m, OuvidoriaTipoEvento.PedidoComplementacao, statusAnterior, OuvidoriaStatus.AguardandoComplementacao,
            request.Texto.Trim(), visivel: true, autor);
        await db.SaveChangesAsync(ct);
        await notificador.PedidoComplementacaoAsync(m, config, request.Texto, ct);
    }

    public async Task ComplementarAsync(Guid id, TextoComAnexosRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        Exigir(m, [OuvidoriaStatus.AguardandoComplementacao], OuvidoriaStatus.EmTriagem);

        var agora = DateTime.UtcNow;
        var hoje = OuvidoriaPrazos.Hoje();
        var autor = await AutorNomeAsync(ct);

        // Retoma o relógio: os dias parados não contam contra a ouvidoria.
        if (m.SuspensaEm is { } suspensa)
        {
            var dias = Math.Max(0, hoje.DayNumber - OuvidoriaPrazos.DataDe(suspensa).DayNumber);
            m.DiasSuspensos += dias;
            m.PrazoRespostaEm = m.PrazoRespostaEm.AddDays(dias);
            m.SuspensaEm = null;
        }

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.EmTriagem;
        var evento = NovoEvento(m, OuvidoriaTipoEvento.Complementacao, statusAnterior, OuvidoriaStatus.EmTriagem,
            request.Texto.Trim(), visivel: true, autor);
        AdicionarAnexos(m, evento, request.Anexos, visivel: true, agora);
        await db.SaveChangesAsync(ct);
    }

    // ================= Área =================

    public async Task ResponderAreaAsync(Guid id, TextoComAnexosRequest request, CancellationToken ct = default)
    {
        var ctx = await ContextoAsync(ct);
        var m = await CarregarAsync(id, ct);

        // Quem não é técnico da ouvidoria só responde pelo ponto de que é membro — e a manifestação
        // precisa estar encaminhada a ele. Fora disso, 404 (não vaza existência).
        if (!ctx.PodeEditar && !(m.PontoRespostaId is { } p && ctx.Pontos.Contains(p)))
            throw new NaoEncontradoException(nameof(OuvidoriaManifestacao), id);

        Exigir(m, [OuvidoriaStatus.Encaminhada], OuvidoriaStatus.RespondidaPelaArea);

        var agora = DateTime.UtcNow;
        var autor = await AutorNomeAsync(ct);
        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.RespondidaPelaArea;
        var evento = NovoEvento(m, OuvidoriaTipoEvento.RespostaArea, statusAnterior, OuvidoriaStatus.RespondidaPelaArea,
            request.Texto.Trim(), visivel: false, autor, m.PontoRespostaId);
        AdicionarAnexos(m, evento, request.Anexos, visivel: false, agora);
        await db.SaveChangesAsync(ct);
    }

    public async Task DevolverParaReanaliseAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        Exigir(m, [OuvidoriaStatus.RespondidaPelaArea, OuvidoriaStatus.EmValidacao], OuvidoriaStatus.Encaminhada);

        var config = await ConfigAsync(ct);
        var hoje = OuvidoriaPrazos.Hoje();
        var autor = await AutorNomeAsync(ct);

        // Metade do prazo original da área, mínimo 2 dias.
        var original = m.PrazoAreaEm is { } prazo && m.EncaminhadaEm is { } enc
            ? Math.Max(1, prazo.DayNumber - OuvidoriaPrazos.DataDe(enc).DayNumber)
            : config.PrazoAreaDias;
        var novoPrazo = Math.Max(2, original / 2);

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.Encaminhada;
        m.EncaminhadaEm = DateTime.UtcNow;
        m.PrazoAreaEm = hoje.AddDays(novoPrazo);
        NovoEvento(m, OuvidoriaTipoEvento.DevolucaoParaReanalise, statusAnterior, OuvidoriaStatus.Encaminhada,
            request.Texto.Trim(), visivel: false, autor, m.PontoRespostaId);
        await db.SaveChangesAsync(ct);
    }

    // ================= Resposta ao cidadão =================

    public async Task ResponderCidadaoAsync(Guid id, ResponderCidadaoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        var permitidos = new[]
        {
            OuvidoriaStatus.EmTriagem, OuvidoriaStatus.Encaminhada, OuvidoriaStatus.RespondidaPelaArea,
            OuvidoriaStatus.EmValidacao, OuvidoriaStatus.EmRecurso,
        };
        var autor = await AutorNomeAsync(ct);

        if (!request.Conclusiva)
        {
            if (!permitidos.Contains(m.Status)) throw TransicaoInvalida(m.Status, m.Status);
            NovoEvento(m, OuvidoriaTipoEvento.RespostaIntermediaria, null, null, request.Texto.Trim(), visivel: true, autor);
            await db.SaveChangesAsync(ct);
            return;
        }

        Exigir(m, permitidos, OuvidoriaStatus.Respondida);
        ValidarConclusao(m.Tipo, request);

        var config = await ConfigAsync(ct);
        var agora = DateTime.UtcNow;
        var hoje = OuvidoriaPrazos.Hoje();

        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.Respondida;
        m.Resolutividade = request.Resolutividade;
        m.SituacaoFinal = request.SituacaoFinal;
        m.MotivoNaoAtendimento = request.SituacaoFinal == OuvidoriaSituacaoFinal.NaoAtendida ? request.MotivoNaoAtendimento : null;
        m.RespostaConclusiva = request.Texto.Trim();
        m.RespondidaEm = agora;
        m.DiasAteResposta = Math.Max(0, hoje.DayNumber - OuvidoriaPrazos.DataDe(m.RegistradaEm).DayNumber - m.DiasSuspensos);
        m.DiasAtraso = OuvidoriaPrazos.DiasAtraso(m.PrazoRespostaEm, hoje);

        NovoEvento(m, OuvidoriaTipoEvento.RespostaConclusiva, statusAnterior, OuvidoriaStatus.Respondida,
            m.RespostaConclusiva, visivel: true, autor);

        // Anônima não tem acompanhamento nem recurso: a resposta fica registrada e já conclui.
        if (m.Identificacao == OuvidoriaIdentificacao.Anonima)
        {
            m.Status = OuvidoriaStatus.Concluida;
            m.ConcluidaEm = agora;
            NovoEvento(m, OuvidoriaTipoEvento.Conclusao, OuvidoriaStatus.Respondida, OuvidoriaStatus.Concluida,
                "Concluída: manifestação anônima, sem canal de retorno.", visivel: true, autor);
        }

        await db.SaveChangesAsync(ct);
        await notificador.RespostaConclusivaAsync(m, config, ct);
    }

    // ================= Prazo, cobrança, escalonamento =================

    public async Task ProrrogarAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (m.Status == OuvidoriaStatus.EncaminhadaOutroOrgao)
            throw new ConflitoException("ouvidoria.prorrogacao_vedada_externo", "Manifestação encaminhada a outro órgão não pode ser prorrogada.");
        if (m.ProrrogadoEm is not null)
            throw new ConflitoException("ouvidoria.prorrogacao_unica", "O prazo só pode ser prorrogado uma vez (Lei 13.460, art. 16 §1º).");
        if (!Abertos.Contains(m.Status)) throw TransicaoInvalida(m.Status, m.Status);
        if (string.IsNullOrWhiteSpace(request.Texto) || request.Texto.Trim().Length < 20)
            throw new ValidacaoException("texto", "A justificativa da prorrogação precisa de pelo menos 20 caracteres (justificativa expressa, art. 16 §1º).");

        var config = await ConfigAsync(ct);
        var autor = await AutorNomeAsync(ct);
        m.ProrrogadoEm = DateTime.UtcNow;
        m.ProrrogacaoJustificativa = request.Texto.Trim();
        m.PrazoRespostaEm = m.PrazoRespostaEm.AddDays(config.ProrrogacaoDias);

        NovoEvento(m, OuvidoriaTipoEvento.Prorrogacao, null, null,
            $"Prazo prorrogado por {config.ProrrogacaoDias} dias (Lei 13.460/2017, art. 16). Novo prazo: {m.PrazoRespostaEm:dd/MM/yyyy}. Justificativa: {m.ProrrogacaoJustificativa}",
            visivel: true, autor);
        await db.SaveChangesAsync(ct);
        await notificador.ProrrogacaoAsync(m, config, ct);
    }

    public async Task CobrarAsync(Guid id, TextoRequest? request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (m.Status != OuvidoriaStatus.Encaminhada) throw TransicaoInvalida(m.Status, m.Status);

        var autor = await AutorNomeAsync(ct);
        var texto = string.IsNullOrWhiteSpace(request?.Texto)
            ? $"Cobrança de resposta à área (prazo {m.PrazoAreaEm:dd/MM/yyyy})."
            : request!.Texto.Trim();
        NovoEvento(m, OuvidoriaTipoEvento.Cobranca, null, null, texto, visivel: false, autor, m.PontoRespostaId);
        await db.SaveChangesAsync(ct);
    }

    public async Task EscalonarAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (Finais.Contains(m.Status)) throw TransicaoInvalida(m.Status, m.Status);
        var autor = await AutorNomeAsync(ct);
        NovoEvento(m, OuvidoriaTipoEvento.Escalonamento, null, null, request.Texto.Trim(), visivel: false, autor, m.PontoRespostaId);
        await db.SaveChangesAsync(ct);
    }

    // ================= Recurso, conclusão, arquivamento, externo =================

    public async Task RegistrarRecursoAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (await db.OuvidoriaEventos.AnyAsync(e => e.ManifestacaoId == id && e.Tipo == OuvidoriaTipoEvento.Recurso, ct))
            throw new ConflitoException("ouvidoria.recurso_unico", "Já houve recurso nesta manifestação; cabe um só.");
        Exigir(m, [OuvidoriaStatus.Respondida], OuvidoriaStatus.EmRecurso);

        var autor = await AutorNomeAsync(ct);
        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.EmRecurso;
        NovoEvento(m, OuvidoriaTipoEvento.Recurso, statusAnterior, OuvidoriaStatus.EmRecurso, request.Texto.Trim(), visivel: true, autor);
        await db.SaveChangesAsync(ct);
    }

    public async Task ConcluirAsync(Guid id, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        Exigir(m, [OuvidoriaStatus.Respondida], OuvidoriaStatus.Concluida);
        var autor = await AutorNomeAsync(ct);
        Concluir(m, autor, "Concluída.");
        await db.SaveChangesAsync(ct);
    }

    public async Task ArquivarAsync(Guid id, ArquivarRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (Finais.Contains(m.Status)) throw TransicaoInvalida(m.Status, OuvidoriaStatus.Arquivada);
        if (request.Motivo == OuvidoriaMotivoArquivamento.Duplicidade && string.IsNullOrWhiteSpace(request.Texto))
            throw new ValidacaoException("texto", "Arquivamento por duplicidade exige o protocolo da manifestação original.");

        var config = await ConfigAsync(ct);
        var autor = await AutorNomeAsync(ct);
        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.Arquivada;
        m.MotivoArquivamento = request.Motivo;
        m.SuspensaEm = null;

        NovoEvento(m, OuvidoriaTipoEvento.Arquivamento, statusAnterior, OuvidoriaStatus.Arquivada,
            $"Arquivada: {RotuloMotivoArquivamento(request.Motivo)}.", visivel: true, autor);
        if (!string.IsNullOrWhiteSpace(request.Texto))
        {
            NovoEvento(m, OuvidoriaTipoEvento.Anotacao, null, null, request.Texto.Trim(), visivel: false, autor);
        }
        await db.SaveChangesAsync(ct);
        await notificador.ArquivamentoAsync(m, config, ct);
    }

    public async Task EncaminharExternoAsync(Guid id, EncaminharExternoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (Finais.Contains(m.Status)) throw TransicaoInvalida(m.Status, OuvidoriaStatus.EncaminhadaOutroOrgao);

        var autor = await AutorNomeAsync(ct);
        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.EncaminhadaOutroOrgao;
        m.SistemaExterno = request.SistemaExterno.Trim();
        m.ProtocoloExterno = Limpar(request.ProtocoloExterno);
        m.SuspensaEm = null;

        var protocolo = m.ProtocoloExterno is null ? string.Empty : $" (protocolo {m.ProtocoloExterno})";
        NovoEvento(m, OuvidoriaTipoEvento.EncaminhamentoExterno, statusAnterior, OuvidoriaStatus.EncaminhadaOutroOrgao,
            $"Encaminhada a {m.SistemaExterno}{protocolo}: o assunto não é de competência desta ouvidoria.", visivel: true, autor);
        NovoEvento(m, OuvidoriaTipoEvento.Anotacao, null, null, request.Texto.Trim(), visivel: false, autor);
        await db.SaveChangesAsync(ct);
    }

    // ================= Denúncia / sigilo / anotação =================

    public async Task HabilitarDenunciaAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        if (m.Tipo != OuvidoriaTipo.Denuncia)
            throw new ConflitoException("ouvidoria.nao_denuncia", "Só denúncia passa por habilitação.");
        if (m.HabilitadaEm is not null)
            throw new ConflitoException("ouvidoria.denuncia_ja_habilitada", "Esta denúncia já foi habilitada.");
        if (Finais.Contains(m.Status)) throw TransicaoInvalida(m.Status, m.Status);

        var autor = await AutorNomeAsync(ct);
        m.HabilitadaEm = DateTime.UtcNow;
        m.HabilitadaPor = usuarioAtual.UsuarioId;
        NovoEvento(m, OuvidoriaTipoEvento.Habilitacao, null, null, request.Texto.Trim(), visivel: false, autor);
        await db.SaveChangesAsync(ct);
    }

    public async Task AtualizarTeorPseudonimizadoAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        var autor = await AutorNomeAsync(ct);
        m.TeorPseudonimizado = request.Texto.Trim();
        NovoEvento(m, OuvidoriaTipoEvento.Anotacao, null, null, "Teor pseudonimizado atualizado.", visivel: false, autor);
        await db.SaveChangesAsync(ct);
    }

    public async Task AnotarAsync(Guid id, TextoRequest request, CancellationToken ct = default)
    {
        var m = await CarregarAsync(id, ct);
        var autor = await AutorNomeAsync(ct);
        NovoEvento(m, OuvidoriaTipoEvento.Anotacao, null, null, request.Texto.Trim(), visivel: false, autor);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ManifestanteDto> RevelarIdentidadeAsync(Guid id, string justificativa, CancellationToken ct = default)
    {
        var ctx = await ContextoAsync(ct);
        if (!ctx.TemSigilo)
            throw new UnauthorizedAccessException("Revelar identidade exige o módulo Ouvidoria — Sigilo.");
        if (string.IsNullOrWhiteSpace(justificativa) || justificativa.Trim().Length < 15)
            throw new ValidacaoException("texto", "Informe a justificativa do acesso (mínimo 15 caracteres) — ela fica registrada.");

        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new UnauthorizedAccessException("Revelar identidade exige usuário autenticado.");
        var m = await CarregarAsync(id, ct);
        if (m.Identificacao == OuvidoriaIdentificacao.Anonima)
            throw new ConflitoException("ouvidoria.anonima_sem_identidade", "Manifestação anônima não tem identidade a revelar.");

        var agora = DateTime.UtcNow;
        var autor = await AutorNomeAsync(ct);
        db.OuvidoriaAcessosIdentidade.Add(new OuvidoriaAcessoIdentidade
        {
            Id = Guid.CreateVersion7(),
            ManifestacaoId = m.Id,
            UsuarioId = usuarioId,
            Justificativa = justificativa.Trim(),
            Ip = usuarioAtual.Ip,
            CriadoEm = agora,
        });
        NovoEvento(m, OuvidoriaTipoEvento.AcessoIdentidade, null, null, "Identidade do manifestante consultada.", visivel: false, autor);
        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("OuvidoriaManifestacao", m.Id.ToString(), "RevelarIdentidade", null, justificativa.Trim(), ct);

        return new ManifestanteDto(m.ManifestanteNome, m.ManifestanteCpf, m.ManifestanteTelefone, m.ManifestanteEmail, m.ManifestantePatientId);
    }

    // ================= Rotinas =================

    public async Task<int> ArquivarSemComplementacaoAsync(CancellationToken ct = default)
    {
        var config = await ConfigAsync(ct);
        var limite = DateTime.UtcNow.AddDays(-config.ComplementacaoDias);
        // Rastreada de propósito (Base() é AsNoTracking): a rotina altera status/motivo e salva.
        var vencidas = await db.OuvidoriaManifestacoes
            .Where(m => m.ExcluidoEm == null && m.Status == OuvidoriaStatus.AguardandoComplementacao
                && m.ComplementacaoSolicitadaEm != null && m.ComplementacaoSolicitadaEm < limite)
            .ToListAsync(ct);

        foreach (var m in vencidas)
        {
            var statusAnterior = m.Status;
            m.Status = OuvidoriaStatus.Arquivada;
            m.MotivoArquivamento = OuvidoriaMotivoArquivamento.SemComplementacao;
            m.SuspensaEm = null;
            NovoEvento(m, OuvidoriaTipoEvento.Arquivamento, statusAnterior, OuvidoriaStatus.Arquivada,
                $"Arquivada: as informações complementares não chegaram em {config.ComplementacaoDias} dias.", visivel: true, AutorSistema);
        }
        if (vencidas.Count == 0) return 0;

        await db.SaveChangesAsync(ct);
        foreach (var m in vencidas)
        {
            await notificador.ArquivamentoAsync(m, config, ct);
        }
        return vencidas.Count;
    }

    public async Task<int> ConcluirRespondidasSemRecursoAsync(CancellationToken ct = default)
    {
        var config = await ConfigAsync(ct);
        var limite = DateTime.UtcNow.AddDays(-config.ArquivamentoAutomaticoDias);
        // Rastreada de propósito (Base() é AsNoTracking): a rotina altera status e salva.
        var vencidas = await db.OuvidoriaManifestacoes
            .Where(m => m.ExcluidoEm == null && m.Status == OuvidoriaStatus.Respondida && m.RespondidaEm != null && m.RespondidaEm < limite)
            .ToListAsync(ct);

        foreach (var m in vencidas)
        {
            Concluir(m, AutorSistema, $"Concluída automaticamente: {config.ArquivamentoAutomaticoDias} dias após a resposta, sem recurso.");
        }
        if (vencidas.Count == 0) return 0;

        await db.SaveChangesAsync(ct);
        return vencidas.Count;
    }

    // ================= Internos: acesso =================

    /// <summary>O que o usuário atual pode ver/fazer. Sem usuário (rotina) = sistema, vê tudo.</summary>
    private sealed record ContextoAcesso(
        Guid? UsuarioId,
        bool EhSistema,
        bool TemOuvidoria,
        bool PodeEditar,
        bool PodeExcluir,
        bool TemSigilo,
        bool TemSigiloInclusao,
        bool TemSigiloEdicao,
        bool TemGestaoEdicao,
        Guid[] Pontos)
    {
        public bool VeAlgo => EhSistema || TemOuvidoria || Pontos.Length > 0;
    }

    private async Task<ContextoAcesso> ContextoAsync(CancellationToken ct)
    {
        if (_contexto is not null) return _contexto;

        var usuarioId = usuarioAtual.UsuarioId;
        if (usuarioId is null)
        {
            return _contexto = new ContextoAcesso(null, true, true, true, true, true, true, true, true, []);
        }

        var ouvidoria = AcoesPermissao.Nenhuma;
        var sigilo = AcoesPermissao.Nenhuma;
        var gestao = AcoesPermissao.Nenhuma;
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId.Value, ct);
            foreach (var p in perms.Resolvidas)
            {
                switch (p.Modulo)
                {
                    case ModuloPermissao.Ouvidoria: ouvidoria |= p.Acoes; break;
                    case ModuloPermissao.OuvidoriaSigilo: sigilo |= p.Acoes; break;
                    case ModuloPermissao.OuvidoriaGestao: gestao |= p.Acoes; break;
                }
            }
        }
        catch (NaoEncontradoException)
        {
            // Usuário sumiu entre o token e a consulta: fail-closed.
        }

        var pontos = (await catalogo.PontosDoUsuarioAsync(usuarioId.Value, ct)).ToArray();

        return _contexto = new ContextoAcesso(
            usuarioId,
            EhSistema: false,
            TemOuvidoria: ouvidoria.HasFlag(AcoesPermissao.Consulta),
            PodeEditar: ouvidoria.HasFlag(AcoesPermissao.Edicao),
            PodeExcluir: ouvidoria.HasFlag(AcoesPermissao.Exclusao),
            TemSigilo: sigilo.HasFlag(AcoesPermissao.Consulta),
            TemSigiloInclusao: sigilo.HasFlag(AcoesPermissao.Inclusao),
            TemSigiloEdicao: sigilo.HasFlag(AcoesPermissao.Edicao),
            TemGestaoEdicao: gestao.HasFlag(AcoesPermissao.Edicao),
            pontos);
    }

    private IQueryable<OuvidoriaManifestacao> Base()
        => db.OuvidoriaManifestacoes.AsNoTracking().Where(m => m.ExcluidoEm == null);

    /// <summary>
    /// Escopo (§2.9): sistema e ouvidoria-com-sigilo veem tudo; ouvidoria sem sigilo não vê denúncia
    /// (salvo as encaminhadas a um ponto seu); membro de ponto vê só o encaminhado aos seus pontos.
    /// </summary>
    private static IQueryable<OuvidoriaManifestacao> AplicarEscopo(IQueryable<OuvidoriaManifestacao> query, ContextoAcesso ctx)
    {
        if (ctx.EhSistema || (ctx.TemOuvidoria && ctx.TemSigilo)) return query;

        var pontos = ctx.Pontos;
        if (ctx.TemOuvidoria)
        {
            return query.Where(m => m.Tipo != OuvidoriaTipo.Denuncia
                || (m.PontoRespostaId != null && pontos.Contains(m.PontoRespostaId.Value) && StatusVisiveisAoPonto.Contains(m.Status)));
        }

        return query.Where(m => m.PontoRespostaId != null && pontos.Contains(m.PontoRespostaId.Value) && StatusVisiveisAoPonto.Contains(m.Status));
    }

    /// <summary>Enxerga como ponto de resposta: não é da ouvidoria, ou é denúncia que só vê por ser do ponto apurador.</summary>
    private static bool ModoPonto(ContextoAcesso ctx, OuvidoriaTipo tipo, Guid? pontoRespostaId)
    {
        if (ctx.EhSistema) return false;
        if (!ctx.TemOuvidoria) return true;
        return tipo == OuvidoriaTipo.Denuncia && !ctx.TemSigilo && pontoRespostaId is { } p && ctx.Pontos.Contains(p);
    }

    /// <summary>Sigilosa é sempre restrita (revela-se pelo endpoint, com justificativa); denúncia identificada só sem sigilo.</summary>
    private static bool IdentidadeRestrita(ContextoAcesso ctx, OuvidoriaTipo tipo, OuvidoriaIdentificacao identificacao)
    {
        if (ctx.EhSistema) return false;
        if (identificacao == OuvidoriaIdentificacao.Sigilosa) return true;
        return tipo == OuvidoriaTipo.Denuncia && !ctx.TemSigilo;
    }

    private static IReadOnlyList<string> AcoesPermitidas(OuvidoriaManifestacao m, ContextoAcesso ctx, bool modoPonto, bool membroDoPonto, bool temRecurso)
    {
        var acoes = new List<string>();
        var s = m.Status;
        var aberta = Abertos.Contains(s);
        var final = Finais.Contains(s);
        var edita = ctx.PodeEditar && !modoPonto;

        if (edita && s is OuvidoriaStatus.Registrada or OuvidoriaStatus.EmTriagem) acoes.Add("triar");
        if (edita && s is OuvidoriaStatus.Registrada or OuvidoriaStatus.EmTriagem or OuvidoriaStatus.RespondidaPelaArea or OuvidoriaStatus.EmValidacao or OuvidoriaStatus.EmRecurso) acoes.Add("encaminhar");
        if (edita && aberta && s != OuvidoriaStatus.AguardandoComplementacao && !m.ComplementacaoUsada && m.Identificacao != OuvidoriaIdentificacao.Anonima) acoes.Add("pedirComplementacao");
        if (edita && s == OuvidoriaStatus.AguardandoComplementacao) acoes.Add("complementar");
        if (s == OuvidoriaStatus.Encaminhada && (edita || membroDoPonto)) acoes.Add("responderArea");
        if (edita && s is OuvidoriaStatus.RespondidaPelaArea or OuvidoriaStatus.EmValidacao) acoes.Add("devolverArea");
        if (edita && s is OuvidoriaStatus.EmTriagem or OuvidoriaStatus.Encaminhada or OuvidoriaStatus.RespondidaPelaArea or OuvidoriaStatus.EmValidacao or OuvidoriaStatus.EmRecurso) acoes.Add("responderCidadao");
        if (edita && aberta && m.ProrrogadoEm is null) acoes.Add("prorrogar");
        if (edita && s == OuvidoriaStatus.Encaminhada) acoes.Add("cobrar");
        if (ctx.TemGestaoEdicao && !modoPonto && !final) acoes.Add("escalonar");
        if (edita && s == OuvidoriaStatus.Respondida && !temRecurso) acoes.Add("recurso");
        if (edita && s == OuvidoriaStatus.Respondida) acoes.Add("concluir");
        if (ctx.PodeExcluir && !modoPonto && !final) acoes.Add("arquivar");
        if (edita && !final) acoes.Add("encaminharExterno");
        if (ctx.TemSigiloInclusao && !modoPonto && m.Tipo == OuvidoriaTipo.Denuncia && m.HabilitadaEm is null && !final) acoes.Add("habilitar");
        if (ctx.TemSigiloEdicao && !modoPonto && m.Tipo == OuvidoriaTipo.Denuncia && !final) acoes.Add("editarTeorPseudonimizado");
        if (edita) acoes.Add("anotar");
        if (ctx.TemSigilo && !modoPonto && m.Identificacao != OuvidoriaIdentificacao.Anonima
            && (m.Identificacao == OuvidoriaIdentificacao.Sigilosa || m.Tipo == OuvidoriaTipo.Denuncia)) acoes.Add("revelarIdentidade");

        return acoes;
    }

    // ================= Internos: transições =================

    private async Task<OuvidoriaManifestacao> CarregarAsync(Guid id, CancellationToken ct)
        => await db.OuvidoriaManifestacoes
            .Include(m => m.Marcadores)
            .FirstOrDefaultAsync(m => m.Id == id && m.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaManifestacao), id);

    private static void Exigir(OuvidoriaManifestacao m, OuvidoriaStatus[] permitidos, OuvidoriaStatus destino)
    {
        if (!permitidos.Contains(m.Status)) throw TransicaoInvalida(m.Status, destino);
    }

    private static ConflitoException TransicaoInvalida(OuvidoriaStatus de, OuvidoriaStatus para)
        => new("ouvidoria.transicao_invalida", de == para
            ? $"Ação não permitida no status {de}."
            : $"Transição inválida de {de} para {para}.");

    /// <summary>Acrescenta um evento (append-only) e carimba a atividade/atualização da manifestação.</summary>
    private OuvidoriaEvento NovoEvento(OuvidoriaManifestacao m, OuvidoriaTipoEvento tipo, OuvidoriaStatus? de, OuvidoriaStatus? para,
        string? texto, bool visivel, string autorNome, Guid? pontoRespostaId = null)
    {
        var agora = DateTime.UtcNow;
        var evento = new OuvidoriaEvento
        {
            Id = Guid.CreateVersion7(),
            ManifestacaoId = m.Id,
            Tipo = tipo,
            StatusAnterior = de,
            StatusNovo = para,
            AutorId = autorNome == AutorSistema ? null : usuarioAtual.UsuarioId,
            AutorNome = autorNome,
            PontoRespostaId = pontoRespostaId,
            Texto = texto,
            VisivelAoCidadao = visivel,
            CriadoEm = agora,
        };
        m.Eventos.Add(evento);
        // Add explícito: a manifestação já está rastreada (Unchanged) e o Id do evento vem preenchido;
        // descoberto só pela navegação, o EF o marcaria como Modified (UPDATE de linha inexistente →
        // DbUpdateConcurrencyException em toda transição). No registro a manifestação ainda não está
        // rastreada e o Add dela arrastaria o evento como Added de qualquer jeito.
        db.OuvidoriaEventos.Add(evento);
        m.UltimaAtividadeEm = agora;
        if (tipo != OuvidoriaTipoEvento.Registro)
        {
            m.AtualizadoEm = agora;
            m.AtualizadoPor = autorNome == AutorSistema ? null : usuarioAtual.UsuarioId;
        }
        return evento;
    }

    private void AdicionarAnexos(OuvidoriaManifestacao m, OuvidoriaEvento evento, IReadOnlyList<AnexoRef>? anexos, bool visivel, DateTime agora)
    {
        if (anexos is null) return;
        foreach (var a in anexos.GroupBy(x => x.MidiaId).Select(g => g.First()))
        {
            var anexo = new OuvidoriaAnexo
            {
                Id = Guid.CreateVersion7(),
                ManifestacaoId = m.Id,
                EventoId = evento.Id,
                MidiaId = a.MidiaId,
                NomeArquivo = a.NomeArquivo.Trim(),
                VisivelAoCidadao = visivel,
                CriadoEm = agora,
            };
            m.Anexos.Add(anexo);
            db.OuvidoriaAnexos.Add(anexo); // mesmo motivo do NovoEvento: Id preenchido + pai já rastreado
        }
    }

    private void Concluir(OuvidoriaManifestacao m, string autor, string texto)
    {
        var statusAnterior = m.Status;
        m.Status = OuvidoriaStatus.Concluida;
        m.ConcluidaEm = DateTime.UtcNow;
        NovoEvento(m, OuvidoriaTipoEvento.Conclusao, statusAnterior, OuvidoriaStatus.Concluida, texto, visivel: true, autor);
    }

    // ================= Internos: validação =================

    /// <summary>§2.1.1: solicitação/informação exigem identificação (CPF, ou nome + telefone); anônima só em denúncia.</summary>
    private static void ValidarIdentificacao(OuvidoriaTipo tipo, OuvidoriaIdentificacao identificacao, ManifestanteDto? manifestante)
    {
        if (identificacao == OuvidoriaIdentificacao.Anonima && tipo != OuvidoriaTipo.Denuncia)
            throw new ValidacaoException("identificacao", "Manifestação anônima só é admitida em denúncia.");

        if (tipo is OuvidoriaTipo.Solicitacao or OuvidoriaTipo.Informacao)
        {
            if (identificacao != OuvidoriaIdentificacao.Identificada)
                throw new ValidacaoException("identificacao", "Solicitação e pedido de informação exigem manifestante identificado.");

            var cpf = SoDigitos(manifestante?.Cpf);
            var temCpf = cpf.Length == 11;
            var temNomeTelefone = !string.IsNullOrWhiteSpace(manifestante?.Nome) && SoDigitos(manifestante?.Telefone).Length >= 10;
            if (!temCpf && !temNomeTelefone)
                throw new ValidacaoException("identificacao", "Informe o CPF do manifestante, ou nome e telefone, para solicitação e pedido de informação.");
        }
    }

    /// <summary>§2.6: situação final coerente com o tipo; não atendida exige motivo.</summary>
    private static void ValidarConclusao(OuvidoriaTipo tipo, ResponderCidadaoRequest request)
    {
        if (request.Resolutividade is null)
            throw new ValidacaoException("resolutividade", "Resposta conclusiva exige a resolutividade.");
        if (request.SituacaoFinal is not { } situacao)
            throw new ValidacaoException("situacaoFinal", "Resposta conclusiva exige a situação final.");

        var validas = tipo switch
        {
            OuvidoriaTipo.Solicitacao => new[] { OuvidoriaSituacaoFinal.Atendida, OuvidoriaSituacaoFinal.NaoAtendida, OuvidoriaSituacaoFinal.NaoLocalizado, OuvidoriaSituacaoFinal.Faleceu },
            OuvidoriaTipo.Reclamacao or OuvidoriaTipo.Denuncia => [OuvidoriaSituacaoFinal.Procede, OuvidoriaSituacaoFinal.NaoProcede, OuvidoriaSituacaoFinal.Inconclusiva],
            _ => [OuvidoriaSituacaoFinal.Atendida],
        };
        if (!validas.Contains(situacao))
            throw new ValidacaoException("situacaoFinal", $"Situação final {situacao} não se aplica a {tipo}.");
        if (situacao == OuvidoriaSituacaoFinal.NaoAtendida && request.MotivoNaoAtendimento is null)
            throw new ValidacaoException("motivoNaoAtendimento", "Solicitação não atendida exige o motivo.");
    }

    private async Task ValidarReferenciasAsync(Guid? assuntoId, Guid? subassuntoId, Guid? unidadeId, Guid? regulacaoId, CancellationToken ct)
    {
        if (assuntoId is { } a && !await db.OuvidoriaAssuntos.AnyAsync(x => x.Id == a, ct))
            throw new NaoEncontradoException(nameof(OuvidoriaAssunto), a);
        if (subassuntoId is { } s)
        {
            var sub = await db.OuvidoriaAssuntos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s, ct)
                ?? throw new NaoEncontradoException(nameof(OuvidoriaAssunto), s);
            if (assuntoId is { } pai && sub.PaiId != pai)
                throw new ValidacaoException("subassuntoId", "O subassunto não pertence ao assunto informado.");
        }
        if (unidadeId is { } u && !await db.Unidades.AnyAsync(x => x.Id == u, ct))
            throw new NaoEncontradoException("Unidade", u);
        if (regulacaoId is { } r && !await db.RegulacaoSolicitacoes.AnyAsync(x => x.Id == r, ct))
            throw new NaoEncontradoException("RegulacaoSolicitacao", r);
    }

    // ================= Internos: apoio =================

    private Task<OuvidoriaConfiguracao> ConfigAsync(CancellationToken ct) => OuvidoriaCatalogoBase.GarantirConfiguracaoAsync(db, ct);

    private async Task<long> ProximaSequenciaAsync(CancellationToken ct)
    {
        var valores = await db.Database
            .SqlQueryRaw<long>("SELECT nextval('smsmarica.ouvidoria_protocolo_seq') AS \"Value\"")
            .ToListAsync(ct);
        return valores[0];
    }

    private async Task<string> AutorNomeAsync(CancellationToken ct)
    {
        if (_autorNome is not null) return _autorNome;
        var id = usuarioAtual.UsuarioId;
        if (id is null) return _autorNome = AutorCidadao;
        var nome = await db.Usuarios.AsNoTracking().Where(u => u.Id == id).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct);
        return _autorNome = string.IsNullOrWhiteSpace(nome) ? "Ouvidoria" : nome;
    }

    private async Task<Guid?> ResolverPacienteAsync(string? cpf, CancellationToken ct)
    {
        if (cpf is null || cpf.Length != 11) return null;
        try
        {
            var paciente = await pacientes.ObterPorCpfAsync(cpf, ct);
            return paciente?.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: o vínculo com o Patient é conveniência, não requisito do registro.
            logger.LogWarning(ex, "Ouvidoria: não foi possível resolver o paciente pelo CPF do manifestante.");
            return null;
        }
    }

    private async Task<IReadOnlyList<string>> PossiveisDuplicatasAsync(OuvidoriaManifestacao m, CancellationToken ct)
    {
        var desde = DateTime.UtcNow.AddDays(-90);
        return await Base()
            .Where(x => x.Id != m.Id
                && x.ManifestanteCpf == m.ManifestanteCpf
                && x.AssuntoId == m.AssuntoId
                && x.UnidadeId == m.UnidadeId
                && x.RegistradaEm >= desde
                && x.Status != OuvidoriaStatus.Arquivada)
            .OrderByDescending(x => x.RegistradaEm)
            .Select(x => x.Protocolo)
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, string>> NomesUsuariosAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return await db.Usuarios.AsNoTracking().Where(u => lista.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);
    }

    private static IReadOnlyList<ContagemDto> Contar(IEnumerable<string> chaves)
        => Contar(chaves.Select(c => (c, c)));

    private static IReadOnlyList<ContagemDto> Contar(IEnumerable<(string Chave, string Rotulo)> itens)
        => [.. itens.GroupBy(i => i.Chave)
            .Select(g => new ContagemDto(g.Key, g.First().Rotulo, g.Count()))
            .OrderByDescending(c => c.Quantidade)
            .ThenBy(c => c.Rotulo)];

    private static AnexoDto MapAnexo(OuvidoriaAnexo a) => new(a.Id, a.MidiaId, a.NomeArquivo, a.VisivelAoCidadao, a.CriadoEm);

    private static DateTime InicioUtc(DateOnly dia) => FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(TimeOnly.MinValue));

    private static string SoDigitos(string? s) => s is null ? string.Empty : new string(s.Where(char.IsDigit).ToArray());

    private static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string RotuloCanal(OuvidoriaCanal canal) => canal switch
    {
        OuvidoriaCanal.Painel => "painel da ouvidoria",
        OuvidoriaCanal.SitePublico => "site",
        OuvidoriaCanal.AppCidadao => "aplicativo do cidadão",
        OuvidoriaCanal.WhatsApp => "WhatsApp",
        OuvidoriaCanal.Presencial => "atendimento presencial",
        OuvidoriaCanal.Telefone => "telefone",
        OuvidoriaCanal.Email => "e-mail",
        OuvidoriaCanal.Carta => "carta",
        OuvidoriaCanal.Urna => "urna",
        OuvidoriaCanal.BuscaAtiva => "busca ativa",
        OuvidoriaCanal.Disque136 => "Disque 136",
        OuvidoriaCanal.FalaBr => "Fala.BR",
        OuvidoriaCanal.OuvidoriaGeral => "Ouvidoria Geral",
        _ => "outro canal",
    };

    private static string RotuloMotivoArquivamento(OuvidoriaMotivoArquivamento motivo) => motivo switch
    {
        OuvidoriaMotivoArquivamento.Duplicidade => "duplicidade",
        OuvidoriaMotivoArquivamento.TextoIncompreensivel => "texto incompreensível",
        OuvidoriaMotivoArquivamento.FaltaUrbanidade => "falta de urbanidade",
        OuvidoriaMotivoArquivamento.Impropria => "manifestação imprópria",
        OuvidoriaMotivoArquivamento.CopiaConhecimento => "cópia para conhecimento",
        OuvidoriaMotivoArquivamento.PerdaObjeto => "perda do objeto",
        OuvidoriaMotivoArquivamento.SemComplementacao => "sem complementação",
        OuvidoriaMotivoArquivamento.SemElementosMinimos => "sem elementos mínimos",
        _ => "outro motivo",
    };
}
