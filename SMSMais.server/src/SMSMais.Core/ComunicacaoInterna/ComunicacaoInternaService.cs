using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Armazenamento;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.ComunicacaoInterna.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.ComunicacaoInterna;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.ComunicacaoInterna;

/// <summary>
/// Implementação da Comunicação interna (ADR-0079). Participação é DERIVADA na hora: conversa direta
/// = as duas pessoas; conversa de unidade = quem tem qualquer das duas como unidade PADRÃO
/// (<c>usuario_unidade.principal</c>, ADR-0081). Os demais vínculos (escopo de dados) não contam aqui:
/// no chat cada pessoa é de uma unidade só. Nada é editado nem apagado.
/// </summary>
public sealed class ComunicacaoInternaService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IPresencaInterna presenca,
    IArmazenamentoArquivos armazenamento,
    IComunicacaoInternaNotificador notificador,
    ILogger<ComunicacaoInternaService> logger) : IComunicacaoInternaService
{
    /// <summary>Teto de um anexo (arquivo ou áudio). A Api aplica o mesmo no tamanho da requisição.</summary>
    public const long TamanhoMaximoAnexoBytes = 25L * 1024 * 1024;

    private const int TamanhoMaximoTexto = 8000;
    private const int LimiteLista = 200;
    private const int LimiteBusca = 50;

    /// <summary>
    /// Tipos que o painel desenha na própria página (miniatura, player, PDF). Qualquer outro é servido
    /// como octet-stream: um HTML ou SVG anexado e aberto como blob rodaria script na origem do painel.
    /// </summary>
    private static readonly HashSet<string> MimesInline =
        ["image/png", "image/jpeg", "image/gif", "image/webp", "application/pdf"];

    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    private Guid Eu => usuarioAtual.UsuarioId
        ?? throw new ValidacaoException("usuario", "Usuário não identificado na requisição.");

    // ---------------------------------------------------------------- Contexto e resumo

    public async Task<ContextoComunicacaoInternaDto> ObterContextoAsync(CancellationToken ct = default)
    {
        var eu = Eu;
        var unidades = await db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == eu && v.Principal && v.Unidade!.Ativo)
            .Select(v => new UnidadeVinculoInternoDto(v.UnidadeId, v.Unidade!.Nome, v.Principal))
            .ToListAsync(ct);

        return new ContextoComunicacaoInternaDto(eu, unidades, UnidadeAtual(unidades));
    }

    public async Task<ResumoComunicacaoInternaDto> ObterResumoAsync(CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);
        var conversas = Visiveis(db.ConversasInternas.AsNoTracking(), eu, minhas).Select(c => c.Id);
        var total = await NaoLidas(eu, conversas, ignorarSilenciadas: true).CountAsync(ct);
        return new ResumoComunicacaoInternaDto(total);
    }

    // ---------------------------------------------------------------- Conversas

    public async Task<IReadOnlyList<ConversaInternaItemDto>> ListarConversasAsync(CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);

        // Conversa aberta e nunca usada só aparece para quem a abriu (não polui a lista dos outros).
        var query = Visiveis(db.ConversasInternas.AsNoTracking(), eu, minhas)
            .Where(c => c.UltimaMensagemEm != null || c.CriadoPor == eu)
            .OrderByDescending(c => c.UltimaMensagemEm ?? c.CriadoEm)
            .Take(LimiteLista);

        return await MontarItensAsync(eu, minhas, query, ct);
    }

    public async Task<ConversaInternaDetalheDto> ObterConversaAsync(Guid conversaId, CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);
        var item = await ItemAsync(eu, minhas, conversaId, ct);

        IReadOnlyList<ParticipanteInternoDto> participantes;
        DateTime? outroLidaAte = null;
        if (item.Tipo == TipoConversaInterna.Direta)
        {
            var ids = new[] { eu, item.OutroUsuarioId!.Value };
            var nomes = await db.Usuarios.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.NomeCompleto })
                .ToListAsync(ct);
            var unidades = await UnidadesDosUsuariosAsync(ids, ct);
            participantes = nomes
                .Select(u => new ParticipanteInternoDto(
                    u.Id, u.NomeCompleto, unidades.GetValueOrDefault(u.Id), presenca.EstaOnline(u.Id)))
                .ToList();

            outroLidaAte = await db.ConversaInternaLeituras.AsNoTracking()
                .Where(l => l.ConversaId == conversaId && l.UsuarioId == item.OutroUsuarioId)
                .Select(l => l.LidaAte)
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            var unidadesConversa = new[] { item.UnidadeAId!.Value, item.UnidadeBId!.Value };
            var membros = await VinculosElegiveis()
                .Where(v => unidadesConversa.Contains(v.UnidadeId))
                .Select(v => new { v.UsuarioId, v.Usuario!.NomeCompleto, UnidadeNome = v.Unidade!.Nome })
                .ToListAsync(ct);
            participantes = membros
                .GroupBy(m => m.UsuarioId)
                .Select(g => new ParticipanteInternoDto(
                    g.Key, g.First().NomeCompleto,
                    string.Join(", ", g.Select(x => x.UnidadeNome).Distinct()),
                    presenca.EstaOnline(g.Key)))
                .OrderByDescending(p => p.Online)
                .ThenBy(p => p.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
                .ToList();
        }

        return new ConversaInternaDetalheDto(item, participantes, outroLidaAte);
    }

    public async Task<ConversaInternaItemDto> AbrirConversaUnidadeAsync(
        AbrirConversaUnidadeRequest request, CancellationToken ct = default)
    {
        var eu = Eu;
        var vinculos = await db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == eu && v.Principal && v.Unidade!.Ativo)
            .Select(v => new UnidadeVinculoInternoDto(v.UnidadeId, v.Unidade!.Nome, v.Principal))
            .ToListAsync(ct);
        if (vinculos.Count == 0)
        {
            throw new ValidacaoException("unidadeOrigemId",
                "Você ainda não tem unidade padrão. Converse direto com uma pessoa ou peça ao administrador para definir a sua.");
        }

        var origem = request.UnidadeOrigemId ?? UnidadeAtual(vinculos)!.Value;
        if (vinculos.All(v => v.UnidadeId != origem))
            throw new ValidacaoException("unidadeOrigemId", "A conversa sai da sua unidade padrão.");

        if (!await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == request.UnidadeDestinoId && u.Ativo, ct))
            throw new NaoEncontradoException("Unidade", request.UnidadeDestinoId);

        var (a, b) = OrdenarPar(origem, request.UnidadeDestinoId);
        var id = await ObterOuCriarAsync(
            c => c.Tipo == TipoConversaInterna.Unidade && c.UnidadeAId == a && c.UnidadeBId == b,
            () => new ConversaInterna { Tipo = TipoConversaInterna.Unidade, UnidadeAId = a, UnidadeBId = b },
            eu, ct);

        return await ItemAsync(eu, await MinhasUnidadesAsync(eu, ct), id, ct);
    }

    public async Task<ConversaInternaItemDto> AbrirConversaDiretaAsync(
        AbrirConversaDiretaRequest request, CancellationToken ct = default)
    {
        var eu = Eu;
        if (request.UsuarioId == eu)
            throw new ValidacaoException("usuarioId", "Escolha outra pessoa — não dá para conversar consigo mesmo.");

        var comModulo = UsuariosComModulo();
        var elegivel = await db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Id == request.UsuarioId && u.ExcluidoEm == null && u.Ativo
                && comModulo.Contains(u.Id), ct);
        if (!elegivel)
            throw new ValidacaoException("usuarioId", "Essa pessoa não usa a Comunicação interna.");

        var (a, b) = OrdenarPar(eu, request.UsuarioId);
        var id = await ObterOuCriarAsync(
            c => c.Tipo == TipoConversaInterna.Direta && c.UsuarioAId == a && c.UsuarioBId == b,
            () => new ConversaInterna { Tipo = TipoConversaInterna.Direta, UsuarioAId = a, UsuarioBId = b },
            eu, ct);

        return await ItemAsync(eu, await MinhasUnidadesAsync(eu, ct), id, ct);
    }

    // ---------------------------------------------------------------- Mensagens

    public async Task<PaginaMensagensInternasDto> ListarMensagensAsync(
        Guid conversaId, DateTime? antesDe, int limite, CancellationToken ct = default)
    {
        var eu = Eu;
        await ConversaVisivelAsync(eu, conversaId, rastrear: false, ct);

        limite = Math.Clamp(limite, 1, 100);
        var query = db.MensagensInternas.AsNoTracking().Where(m => m.ConversaId == conversaId);
        if (antesDe is { } cursor)
        {
            var utc = ParaUtc(cursor);
            query = query.Where(m => m.EnviadaEm < utc);
        }

        var itens = await query
            .OrderByDescending(m => m.EnviadaEm)
            .ThenByDescending(m => m.Id)
            .Take(limite + 1)
            .Select(ProjecaoMensagem)
            .ToListAsync(ct);

        var temMais = itens.Count > limite;
        if (temMais) itens.RemoveAt(itens.Count - 1);
        itens.Reverse();
        return new PaginaMensagensInternasDto(itens, temMais);
    }

    public async Task<MensagemInternaDto> EnviarAsync(
        Guid conversaId, string? texto, AnexoEnviadoInterno? anexo, CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);
        var conversa = await ConversaVisivelAsync(eu, conversaId, rastrear: true, ct, minhas);

        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto is null && anexo is null)
            throw new ValidacaoException("texto", "Escreva uma mensagem ou anexe um arquivo.");
        if (texto is { Length: > TamanhoMaximoTexto })
            throw new ValidacaoException("texto", $"A mensagem passa de {TamanhoMaximoTexto} caracteres.");
        if (anexo is not null && anexo.Conteudo.Length == 0)
            throw new ValidacaoException("arquivo", "O arquivo está vazio.");
        if (anexo is not null && anexo.Conteudo.Length > TamanhoMaximoAnexoBytes)
            throw new ValidacaoException("arquivo", "O arquivo passa de 25 MB.");

        var agora = DateTime.UtcNow;
        var mensagem = new MensagemInterna
        {
            Id = Guid.NewGuid(),
            ConversaId = conversaId,
            AutorId = eu,
            AutorUnidadeId = UnidadeDoAutor(minhas),
            Texto = texto,
            EnviadaEm = agora,
        };

        if (anexo is not null)
        {
            var mime = NormalizarMime(anexo.Mime);
            var nome = NomeSeguro(anexo.Nome, anexo.EhAudio);
            var chave = armazenamento.MontarChaveComunicacaoInterna(conversaId, mensagem.Id, Extensao(nome, mime));
            await armazenamento.SalvarAsync(chave, anexo.Conteudo, ct);

            mensagem.AnexoChave = chave;
            mensagem.AnexoNome = nome;
            mensagem.AnexoMime = mime;
            mensagem.AnexoTamanhoBytes = anexo.Conteudo.LongLength;
            mensagem.AnexoEhAudio = anexo.EhAudio;
            mensagem.AnexoDuracaoSegundos = anexo.EhAudio ? Math.Clamp(anexo.DuracaoSegundos ?? 0, 0, 3600) : null;
        }

        db.MensagensInternas.Add(mensagem);
        conversa.UltimaMensagemEm = agora;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sem a linha, o arquivo seria órfão no armazenamento.
            if (mensagem.AnexoChave is { } chave)
            {
                try { await armazenamento.ExcluirAsync(chave, CancellationToken.None); }
                catch (Exception ex) { logger.LogWarning(ex, "Anexo órfão da comunicação interna: {Chave}", chave); }
            }
            throw;
        }

        // Quem escreve leu tudo até aqui.
        await GravarLidaAsync(conversaId, eu, agora, ct);

        var dto = await db.MensagensInternas.AsNoTracking()
            .Where(m => m.Id == mensagem.Id)
            .Select(ProjecaoMensagem)
            .FirstAsync(ct);

        var nomesUnidades = conversa.Tipo == TipoConversaInterna.Unidade
            ? await db.Unidades.AsNoTracking()
                .Where(u => u.Id == conversa.UnidadeAId || u.Id == conversa.UnidadeBId)
                .ToDictionaryAsync(u => u.Id, u => u.Nome, ct)
            : [];

        await NotificarAsync(() => notificador.MensagemAsync(
            new MensagemInternaEventoDto(
                dto,
                conversa.Tipo,
                conversa.UnidadeAId is { } ua ? nomesUnidades.GetValueOrDefault(ua) : null,
                conversa.UnidadeBId is { } ub ? nomesUnidades.GetValueOrDefault(ub) : null),
            Audiencia(conversa, eu),
            ct));

        return dto;
    }

    public async Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default)
    {
        var eu = Eu;
        var conversa = await ConversaVisivelAsync(eu, conversaId, rastrear: false, ct);
        var agora = DateTime.UtcNow;
        await GravarLidaAsync(conversaId, eu, agora, ct);

        // Direta: a outra pessoa vê o ✓✓. Unidade: só as minhas outras abas (baixar o contador).
        var audiencia = conversa.Tipo == TipoConversaInterna.Direta
            ? Audiencia(conversa, eu)
            : new AudienciaInterna([], [eu]);
        await NotificarAsync(() => notificador.LeituraAsync(new LeituraInternaEventoDto(conversaId, eu, agora), audiencia, ct));
    }

    public async Task SilenciarAsync(Guid conversaId, bool silenciada, CancellationToken ct = default)
    {
        var eu = Eu;
        await ConversaVisivelAsync(eu, conversaId, rastrear: false, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smsmarica.conversa_interna_leitura (conversa_id, usuario_id, lida_ate, silenciada)
            VALUES ({conversaId}, {eu}, NULL, {silenciada})
            ON CONFLICT (conversa_id, usuario_id) DO UPDATE SET silenciada = EXCLUDED.silenciada
            """, ct);
    }

    public async Task<ConteudoAnexoInterno> ObterAnexoAsync(Guid mensagemId, CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);
        var visiveis = Visiveis(db.ConversasInternas.AsNoTracking(), eu, minhas).Select(c => c.Id);

        var anexo = await db.MensagensInternas.AsNoTracking()
            .Where(m => m.Id == mensagemId && m.AnexoChave != null && visiveis.Contains(m.ConversaId))
            .Select(m => new { m.AnexoChave, m.AnexoNome, m.AnexoMime })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Anexo", mensagemId);

        var conteudo = await armazenamento.LerAsync(anexo.AnexoChave!, ct)
            ?? throw new NaoEncontradoException("Anexo", mensagemId);

        return new ConteudoAnexoInterno(anexo.AnexoNome ?? "arquivo", MimeParaServir(anexo.AnexoMime), conteudo);
    }

    // ---------------------------------------------------------------- Seletor de destino

    public async Task<IReadOnlyList<UnidadeInternaDto>> ListarUnidadesAsync(CancellationToken ct = default)
    {
        var eu = Eu;
        var minhas = (await MinhasUnidadesAsync(eu, ct)).ToHashSet();
        var vinculos = await VinculosElegiveis()
            .Where(v => v.Unidade!.Ativo)
            .Select(v => new { v.UnidadeId, v.Unidade!.Nome, v.UsuarioId })
            .ToListAsync(ct);

        var ordem = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);
        return vinculos
            .GroupBy(v => new { v.UnidadeId, v.Nome })
            .Select(g =>
            {
                var usuarios = g.Select(x => x.UsuarioId).Distinct().ToList();
                return new UnidadeInternaDto(
                    g.Key.UnidadeId, g.Key.Nome, usuarios.Count,
                    usuarios.Count(presenca.EstaOnline), minhas.Contains(g.Key.UnidadeId));
            })
            .OrderBy(u => u.Nome, ordem)
            .ToList();
    }

    public async Task<IReadOnlyList<UsuarioInternoDto>> ListarUsuariosDaUnidadeAsync(
        Guid unidadeId, CancellationToken ct = default)
    {
        var eu = Eu;
        if (!await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct))
            throw new NaoEncontradoException("Unidade", unidadeId);

        var pessoas = await VinculosElegiveis()
            .Where(v => v.UnidadeId == unidadeId)
            .Select(v => new { v.UsuarioId, v.Usuario!.NomeCompleto })
            .ToListAsync(ct);

        var ordem = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);
        return pessoas
            .Select(p => new UsuarioInternoDto(p.UsuarioId, p.NomeCompleto, presenca.EstaOnline(p.UsuarioId), p.UsuarioId == eu))
            .OrderByDescending(p => p.Online)
            .ThenBy(p => p.Nome, ordem)
            .ToList();
    }

    // ---------------------------------------------------------------- Busca

    public async Task<IReadOnlyList<ResultadoBuscaInternaDto>> BuscarAsync(string? termo, CancellationToken ct = default)
    {
        termo = termo?.Trim() ?? string.Empty;
        if (termo.Length < 2) return [];

        var eu = Eu;
        var minhas = await MinhasUnidadesAsync(eu, ct);
        var visiveis = Visiveis(db.ConversasInternas.AsNoTracking(), eu, minhas).Select(c => c.Id);

        // f_unaccent (IMMUTABLE) nos dois lados: atendida pelo GIN trigram da migration, como a
        // busca da Central (ver ConversaService). Acha no texto e no nome do arquivo anexado.
        var padrao = $"%{termo}%";
        var achados = await db.MensagensInternas.AsNoTracking()
            .Where(m => visiveis.Contains(m.ConversaId))
            .Where(m =>
                (m.Texto != null && EF.Functions.ILike(SmsMaisDbContext.FUnaccent(m.Texto), SmsMaisDbContext.FUnaccent(padrao)))
                || (m.AnexoNome != null && EF.Functions.ILike(SmsMaisDbContext.FUnaccent(m.AnexoNome), SmsMaisDbContext.FUnaccent(padrao))))
            .OrderByDescending(m => m.EnviadaEm)
            .Take(LimiteBusca)
            .Select(m => new { m.Id, m.ConversaId, Autor = m.Autor!.NomeCompleto, m.Texto, m.AnexoNome, m.EnviadaEm })
            .ToListAsync(ct);
        if (achados.Count == 0) return [];

        var ids = achados.Select(a => a.ConversaId).Distinct().ToList();
        var titulos = (await MontarItensAsync(
                eu, minhas, db.ConversasInternas.AsNoTracking().Where(c => ids.Contains(c.Id)), ct))
            .ToDictionary(i => i.Id, i => i.Titulo);

        return achados
            .Select(a => new ResultadoBuscaInternaDto(
                a.Id, a.ConversaId, titulos.GetValueOrDefault(a.ConversaId) ?? "Conversa", a.Autor,
                Trecho(a.Texto ?? a.AnexoNome ?? string.Empty, termo), a.EnviadaEm))
            .ToList();
    }

    // ---------------------------------------------------------------- Hub

    public async Task<(string Nome, AudienciaInterna Audiencia)?> ObterAudienciaDigitandoAsync(
        Guid usuarioId, Guid conversaId, CancellationToken ct = default)
    {
        var minhas = await MinhasUnidadesAsync(usuarioId, ct);
        var conversa = await Visiveis(db.ConversasInternas.AsNoTracking(), usuarioId, minhas)
            .FirstOrDefaultAsync(c => c.Id == conversaId, ct);
        if (conversa is null) return null;

        var nome = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct) ?? "Alguém";
        return (nome, Audiencia(conversa, usuarioId));
    }

    // ================================================================ Internos

    private sealed record UltimaMensagem(string? Texto, string? AnexoNome, bool EhAudio, string? AutorNome);

    private sealed record LinhaConversa(
        Guid Id, TipoConversaInterna Tipo,
        Guid? UnidadeAId, string? UnidadeANome, Guid? UnidadeBId, string? UnidadeBNome,
        Guid? UsuarioAId, string? UsuarioANome, Guid? UsuarioBId, string? UsuarioBNome,
        DateTime? UltimaMensagemEm, UltimaMensagem? Ultima);

    private static readonly Expression<Func<MensagemInterna, MensagemInternaDto>> ProjecaoMensagem = m =>
        new MensagemInternaDto(
            m.Id,
            m.ConversaId,
            m.AutorId,
            m.Autor!.NomeCompleto,
            m.AutorUnidade != null ? m.AutorUnidade.Nome : null,
            m.Texto,
            m.EnviadaEm,
            m.AnexoChave == null
                ? null
                : new AnexoInternoDto(
                    m.AnexoNome ?? "arquivo",
                    m.AnexoMime ?? "application/octet-stream",
                    m.AnexoTamanhoBytes ?? 0,
                    m.AnexoDuracaoSegundos,
                    m.AnexoEhAudio));

    /// <summary>A minha unidade no chat: só a padrão (lista de 0 ou 1, no formato que o recorte usa).</summary>
    private Task<List<Guid?>> MinhasUnidadesAsync(Guid eu, CancellationToken ct) =>
        db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == eu && v.Principal)
            .Select(v => (Guid?)v.UnidadeId)
            .ToListAsync(ct);

    /// <summary>
    /// O recorte de TUDO: direta = sou uma das duas pessoas; unidade = uma das duas é a minha
    /// unidade padrão. <c>AcessoGlobal</c> não entra — não há "ver tudo" nesta fase (ADR-0079 §2).
    /// </summary>
    private static IQueryable<ConversaInterna> Visiveis(IQueryable<ConversaInterna> query, Guid eu, List<Guid?> minhas) =>
        query.Where(c =>
            (c.Tipo == TipoConversaInterna.Direta && (c.UsuarioAId == eu || c.UsuarioBId == eu))
            || (c.Tipo == TipoConversaInterna.Unidade && (minhas.Contains(c.UnidadeAId) || minhas.Contains(c.UnidadeBId))));

    private async Task<ConversaInterna> ConversaVisivelAsync(
        Guid eu, Guid conversaId, bool rastrear, CancellationToken ct, List<Guid?>? minhas = null)
    {
        minhas ??= await MinhasUnidadesAsync(eu, ct);
        var fonte = rastrear ? db.ConversasInternas : db.ConversasInternas.AsNoTracking();
        return await Visiveis(fonte, eu, minhas).FirstOrDefaultAsync(c => c.Id == conversaId, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);
    }

    /// <summary>Quem tem o módulo (Consulta) — perfil OU permissão avulsa, como a resolução da Identidade.</summary>
    private IQueryable<Guid> UsuariosComModulo() =>
        db.UsuariosPerfis
            .Where(up => up.Perfil.Permissoes.Any(pp => pp.Modulo == ModuloPermissao.ComunicacaoInterna
                && (pp.Acoes & AcoesPermissao.Consulta) != AcoesPermissao.Nenhuma))
            .Select(up => up.UsuarioId)
            .Union(db.PermissoesUsuario
                .Where(p => p.Modulo == ModuloPermissao.ComunicacaoInterna
                    && (p.Acoes & AcoesPermissao.Consulta) != AcoesPermissao.Nenhuma)
                .Select(p => p.UsuarioId));

    /// <summary>
    /// Unidade padrão de quem de fato pode ler: ativo, não excluído e com o módulo. Sem esse filtro,
    /// uma mensagem direta para quem não tem o módulo cairia num buraco calado. Só a padrão: o
    /// técnico vinculado às 56 unidades para enxergar os pedidos aparece só na dele (ADR-0081).
    /// </summary>
    private IQueryable<UsuarioUnidade> VinculosElegiveis()
    {
        var comModulo = UsuariosComModulo();
        return db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.Principal && v.Usuario!.ExcluidoEm == null && v.Usuario.Ativo && comModulo.Contains(v.UsuarioId));
    }

    /// <summary>
    /// Mensagens de OUTROS depois do que eu li. Quem chegou à equipe depois de uma mensagem a vê no
    /// histórico, mas não como "não lida" — senão o primeiro dia de alguém começaria com o histórico
    /// inteiro da unidade no contador.
    /// </summary>
    private IQueryable<MensagemInterna> NaoLidas(Guid eu, IQueryable<Guid> conversas, bool ignorarSilenciadas) =>
        db.MensagensInternas.AsNoTracking()
            .Where(m => conversas.Contains(m.ConversaId) && m.AutorId != eu)
            .Where(m => !db.ConversaInternaLeituras.Any(l => l.ConversaId == m.ConversaId && l.UsuarioId == eu
                && ((ignorarSilenciadas && l.Silenciada) || (l.LidaAte != null && l.LidaAte >= m.EnviadaEm))))
            .Where(m => m.Conversa!.Tipo == TipoConversaInterna.Direta
                || db.UsuarioUnidades.Any(v => v.UsuarioId == eu && v.Principal
                    && (v.UnidadeId == m.Conversa!.UnidadeAId || v.UnidadeId == m.Conversa!.UnidadeBId)
                    && v.CriadoEm <= m.EnviadaEm));

    private async Task<ConversaInternaItemDto> ItemAsync(Guid eu, List<Guid?> minhas, Guid conversaId, CancellationToken ct)
    {
        var itens = await MontarItensAsync(
            eu, minhas, Visiveis(db.ConversasInternas.AsNoTracking(), eu, minhas).Where(c => c.Id == conversaId), ct);
        return itens.Count > 0 ? itens[0] : throw new NaoEncontradoException("Conversa", conversaId);
    }

    /// <summary>Monta os itens da lista (do ponto de vista de <paramref name="eu"/>) na ordem da consulta.</summary>
    private async Task<List<ConversaInternaItemDto>> MontarItensAsync(
        Guid eu, List<Guid?> minhas, IQueryable<ConversaInterna> query, CancellationToken ct)
    {
        var linhas = await query
            .Select(c => new LinhaConversa(
                c.Id, c.Tipo,
                c.UnidadeAId, c.UnidadeA != null ? c.UnidadeA.Nome : null,
                c.UnidadeBId, c.UnidadeB != null ? c.UnidadeB.Nome : null,
                c.UsuarioAId, c.UsuarioA != null ? c.UsuarioA.NomeCompleto : null,
                c.UsuarioBId, c.UsuarioB != null ? c.UsuarioB.NomeCompleto : null,
                c.UltimaMensagemEm,
                c.Mensagens
                    .OrderByDescending(m => m.EnviadaEm)
                    .Select(m => new UltimaMensagem(m.Texto, m.AnexoNome, m.AnexoEhAudio, m.Autor!.NomeCompleto))
                    .FirstOrDefault()))
            .ToListAsync(ct);
        if (linhas.Count == 0) return [];

        var ids = linhas.Select(l => l.Id).ToList();
        var naoLidas = await NaoLidas(eu, db.ConversasInternas.Where(c => ids.Contains(c.Id)).Select(c => c.Id), ignorarSilenciadas: false)
            .GroupBy(m => m.ConversaId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var silenciadas = (await db.ConversaInternaLeituras.AsNoTracking()
            .Where(l => l.UsuarioId == eu && l.Silenciada && ids.Contains(l.ConversaId))
            .Select(l => l.ConversaId)
            .ToListAsync(ct)).ToHashSet();

        var outros = linhas
            .Where(l => l.Tipo == TipoConversaInterna.Direta)
            .Select(l => l.UsuarioAId == eu ? l.UsuarioBId!.Value : l.UsuarioAId!.Value)
            .Distinct()
            .ToList();
        var unidadesDosOutros = outros.Count > 0 ? await UnidadesDosUsuariosAsync(outros, ct) : [];
        var minhasSet = minhas.Where(u => u.HasValue).Select(u => u!.Value).ToHashSet();

        return linhas.Select(l =>
        {
            Guid? outroId = l.Tipo == TipoConversaInterna.Direta ? (l.UsuarioAId == eu ? l.UsuarioBId : l.UsuarioAId) : null;
            var (titulo, subtitulo) = Titulo(l, eu, outroId, minhasSet, unidadesDosOutros);
            return new ConversaInternaItemDto(
                l.Id, l.Tipo, titulo, subtitulo,
                l.UnidadeAId, l.UnidadeANome, l.UnidadeBId, l.UnidadeBNome,
                outroId, outroId is { } o && presenca.EstaOnline(o),
                l.UltimaMensagemEm,
                l.Ultima is null ? null : Previa(l.Ultima),
                l.Ultima?.AutorNome,
                naoLidas.GetValueOrDefault(l.Id),
                silenciadas.Contains(l.Id));
        }).ToList();
    }

    private static (string Titulo, string? Subtitulo) Titulo(
        LinhaConversa l, Guid eu, Guid? outroId, HashSet<Guid> minhas, IReadOnlyDictionary<Guid, string> unidadesDosOutros)
    {
        if (l.Tipo == TipoConversaInterna.Direta)
        {
            var nome = (l.UsuarioAId == eu ? l.UsuarioBNome : l.UsuarioANome) ?? "Usuário";
            return (nome, outroId is { } o ? unidadesDosOutros.GetValueOrDefault(o) : null);
        }

        var a = l.UnidadeANome ?? "Unidade";
        var b = l.UnidadeBNome ?? "Unidade";
        if (l.UnidadeAId == l.UnidadeBId) return (a, "Equipe da unidade · todos");

        var souA = minhas.Contains(l.UnidadeAId!.Value);
        var souB = minhas.Contains(l.UnidadeBId!.Value);
        return (souA, souB) switch
        {
            (true, false) => (b, $"Todos · {a} ↔ {b}"),
            (false, true) => (a, $"Todos · {a} ↔ {b}"),
            _ => ($"{a} ↔ {b}", "Todos das duas unidades"),
        };
    }

    private static string? Previa(UltimaMensagem m)
    {
        if (!string.IsNullOrWhiteSpace(m.Texto))
        {
            var t = m.Texto.ReplaceLineEndings(" ");
            return t.Length > 140 ? t[..140] + "…" : t;
        }
        if (m.EhAudio) return "🎤 Áudio";
        return m.AnexoNome is null ? null : $"📎 {m.AnexoNome}";
    }

    /// <summary>Unidade padrão de cada pessoa, para o subtítulo da conversa direta.</summary>
    private async Task<Dictionary<Guid, string>> UnidadesDosUsuariosAsync(IReadOnlyCollection<Guid> usuarios, CancellationToken ct) =>
        await db.UsuarioUnidades.AsNoTracking()
            .Where(v => usuarios.Contains(v.UsuarioId) && v.Principal && v.Unidade!.Ativo)
            .Select(v => new { v.UsuarioId, v.Unidade!.Nome })
            .ToDictionaryAsync(v => v.UsuarioId, v => v.Nome, ct);

    private async Task<Guid> ObterOuCriarAsync(
        Expression<Func<ConversaInterna, bool>> chave, Func<ConversaInterna> nova, Guid eu, CancellationToken ct)
    {
        var existente = await db.ConversasInternas.AsNoTracking().Where(chave).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (existente is { } id) return id;

        var conversa = nova();
        conversa.Id = Guid.NewGuid();
        conversa.CriadoEm = DateTime.UtcNow;
        conversa.CriadoPor = eu;
        db.ConversasInternas.Add(conversa);
        try
        {
            await db.SaveChangesAsync(ct);
            return conversa.Id;
        }
        catch (DbUpdateException)
        {
            // Os dois lados abriram ao mesmo tempo: o índice único segurou — fica a que ganhou.
            db.Entry(conversa).State = EntityState.Detached;
            return await db.ConversasInternas.AsNoTracking().Where(chave).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct)
                ?? throw new ConflitoException("conversa_interna.criacao", "Não foi possível abrir a conversa. Tente de novo.");
        }
    }

    /// <summary>Upsert do "li até aqui" — nunca anda para trás (duas abas marcando ao mesmo tempo).</summary>
    private Task GravarLidaAsync(Guid conversaId, Guid usuarioId, DateTime lidaAte, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smsmarica.conversa_interna_leitura (conversa_id, usuario_id, lida_ate, silenciada)
            VALUES ({conversaId}, {usuarioId}, {lidaAte}, FALSE)
            ON CONFLICT (conversa_id, usuario_id) DO UPDATE
                SET lida_ate = GREATEST(COALESCE(conversa_interna_leitura.lida_ate, EXCLUDED.lida_ate), EXCLUDED.lida_ate)
            """, ct);

    private static AudienciaInterna Audiencia(ConversaInterna c, Guid autor) =>
        c.Tipo == TipoConversaInterna.Direta
            ? new AudienciaInterna([], [c.UsuarioAId!.Value, c.UsuarioBId!.Value])
            : new AudienciaInterna(new HashSet<Guid> { c.UnidadeAId!.Value, c.UnidadeBId!.Value }, [autor]);

    /// <summary>
    /// De onde a pessoa fala: sempre da unidade padrão (a unidade do cabeçalho não muda isso). Na
    /// conversa de unidade ela é, por construção, uma das duas — senão a conversa não seria visível.
    /// </summary>
    private static Guid? UnidadeDoAutor(List<Guid?> minhas) => minhas.FirstOrDefault();

    /// <summary>A unidade padrão (a lista já vem só com ela).</summary>
    private static Guid? UnidadeAtual(IReadOnlyList<UnidadeVinculoInternoDto> vinculos) =>
        vinculos.FirstOrDefault()?.UnidadeId;

    /// <summary>
    /// Ordena o par pela ordem TEXTUAL do Guid — a mesma do <c>uuid</c> no Postgres, que é quem
    /// confere o <c>ck_conversa_interna_par</c> (A ≤ B).
    /// </summary>
    private static (Guid A, Guid B) OrdenarPar(Guid x, Guid y) =>
        string.CompareOrdinal(x.ToString("D"), y.ToString("D")) <= 0 ? (x, y) : (y, x);

    private static DateTime ParaUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };

    private static string NormalizarMime(string? mime)
    {
        var m = (mime ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        return m.Length is > 0 and <= 150 && m.Contains('/') ? m : "application/octet-stream";
    }

    private static string MimeParaServir(string? mime)
    {
        var m = NormalizarMime(mime);
        return MimesInline.Contains(m) || m.StartsWith("audio/", StringComparison.Ordinal) ? m : "application/octet-stream";
    }

    private static string NomeSeguro(string? nome, bool ehAudio)
    {
        var n = Path.GetFileName((nome ?? string.Empty).Replace('\\', '/'));
        n = new string(n.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (n.Length > 200) n = n[^200..];
        return n.Length > 0 ? n : ehAudio ? "audio.webm" : "arquivo";
    }

    /// <summary>Extensão curta e segura para a chave (a origem é o nome do arquivo; senão o tipo).</summary>
    private static string Extensao(string nome, string mime)
    {
        var e = Path.GetExtension(nome).TrimStart('.').ToLowerInvariant();
        if (e.Length is > 0 and <= 8 && e.All(char.IsAsciiLetterOrDigit)) return e;
        return mime switch
        {
            _ when mime.Contains("webm") => "webm",
            _ when mime.Contains("ogg") => "ogg",
            _ when mime.Contains("mp4") => "m4a",
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "application/pdf" => "pdf",
            _ => "bin",
        };
    }

    /// <summary>~40 caracteres antes e ~90 depois do termo (sem diferenciar acento/maiúscula).</summary>
    private static string Trecho(string texto, string termo)
    {
        var plano = texto.ReplaceLineEndings(" ");
        var i = Comparador.IndexOf(plano, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        if (i < 0) return plano.Length > 130 ? plano[..130] + "…" : plano;
        var inicio = Math.Max(0, i - 40);
        var fim = Math.Min(plano.Length, i + termo.Length + 90);
        return (inicio > 0 ? "…" : string.Empty) + plano[inicio..fim] + (fim < plano.Length ? "…" : string.Empty);
    }

    /// <summary>Tempo real é melhor esforço: a mensagem já está gravada; o poll do front cobre a falha.</summary>
    private async Task NotificarAsync(Func<Task> envio)
    {
        try { await envio(); }
        catch (Exception ex) { logger.LogWarning(ex, "Falha ao publicar evento da comunicação interna."); }
    }
}
