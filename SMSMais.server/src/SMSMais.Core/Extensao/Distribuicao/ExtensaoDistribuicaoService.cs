using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Institucional;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Extensao.Distribuicao;

public sealed class ExtensaoDistribuicaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IInstituicaoService instituicao,
    IMemoryCache cache) : IExtensaoDistribuicaoService
{
    // O código que vem no instalador vale por uma hora (baixar e executar); o do "Configurar",
    // dez minutos (a pessoa está ali, com o navegador aberto).
    private static readonly TimeSpan ValidadeDoInstalador = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan ValidadeDoConfigurar = TimeSpan.FromMinutes(10);
    private const int IntervaloDeConsultaSegundos = 3;
    private static readonly TimeSpan GuardaDasAtivacoes = TimeSpan.FromDays(7);

    private const int LimiteDaExtensao = 20 * 1024 * 1024;
    private const int LimiteDoAtualizador = 30 * 1024 * 1024;

    /// <summary>
    /// O que marca, no fim do arquivo, o instalador entregue pelo painel:
    /// <c>\n#SMSMAIS-INSTALADOR:{"api":"…","codigo":"…"}</c>. O atualizador lê, troca o código pelo
    /// token e se instala sem esse rabicho.
    /// </summary>
    internal const string MarcaDoInstalador = "\n#SMSMAIS-INSTALADOR:";

    private sealed record Resumo(Guid Id, string Versao, string Sha256, bool Promovido);

    // ------------------------------------------------------------------ versão em vigor
    private static string ChaveDoCache(ExtensaoArtefato artefato) => $"extensao:pacotes:{(int)artefato}";

    /// <summary>
    /// As versões entregáveis de um artefato. Fica 30 s em memória: cada computador pergunta a
    /// cada 10 minutos, e publicar/promover/retirar derruba o cache na hora.
    /// </summary>
    private async Task<List<Resumo>> EntregaveisAsync(ExtensaoArtefato artefato, CancellationToken ct)
    {
        if (cache.TryGetValue(ChaveDoCache(artefato), out List<Resumo>? emCache) && emCache is not null)
            return emCache;
        var lista = await db.ExtensaoPacotes.AsNoTracking()
            .Where(p => p.Artefato == artefato && p.RetiradoEm == null)
            .Select(p => new Resumo(p.Id, p.Versao, p.Sha256, p.PromovidoEm != null))
            .ToListAsync(ct);
        cache.Set(ChaveDoCache(artefato), lista, TimeSpan.FromSeconds(30));
        return lista;
    }

    /// <summary>
    /// A versão que um canal recebe: <c>Prod</c> só enxerga o que foi promovido; <c>Teste</c>
    /// enxerga tudo — e por isso acompanha a produção quando ela passa na frente.
    /// </summary>
    private async Task<Resumo?> EmVigorAsync(ExtensaoArtefato artefato, ExtensaoCanal canal, CancellationToken ct)
    {
        var lista = await EntregaveisAsync(artefato, ct);
        return lista
            .Where(p => canal == ExtensaoCanal.Teste || p.Promovido)
            .OrderByDescending(p => p.Versao, Comparer<string>.Create(VersaoPublicada.Comparar))
            .FirstOrDefault();
    }

    private void DerrubarCache(ExtensaoArtefato artefato) => cache.Remove(ChaveDoCache(artefato));

    // ------------------------------------------------------------------ painel: qualquer logado
    public async Task<SituacaoDistribuicaoDto> ObterSituacaoAsync(CancellationToken ct = default)
    {
        var atualizador = await EmVigorAsync(ExtensaoArtefato.Atualizador, ExtensaoCanal.Prod, ct);
        var extensao = await EmVigorAsync(ExtensaoArtefato.Extensao, ExtensaoCanal.Prod, ct);
        return new SituacaoDistribuicaoDto(atualizador?.Versao, extensao?.Versao);
    }

    public async Task<ArquivoPublicado> GerarInstaladorAsync(string apiBase, CancellationToken ct = default)
    {
        // O código nasce autorizado por quem baixou — precisa haver uma pessoa por trás.
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new UnauthorizedAccessException("O instalador só é entregue a um usuário logado no painel.");
        var emVigor = await EmVigorAsync(ExtensaoArtefato.Atualizador, ExtensaoCanal.Prod, ct)
            ?? throw new ConflitoException("extensao.sem_instalador", "O instalador ainda não foi publicado nesta plataforma.");
        var executavel = await db.ExtensaoPacotes.AsNoTracking()
            .Where(p => p.Id == emVigor.Id)
            .Select(p => p.Conteudo)
            .FirstAsync(ct);

        var agora = DateTime.UtcNow;
        var codigo = ExtensaoSegredos.GerarSegredo();
        db.ExtensaoAtivacoes.Add(new ExtensaoAtivacao
        {
            Id = Guid.CreateVersion7(),
            CodigoHash = ExtensaoSegredos.Hash(codigo),
            CodigoPublico = await CodigoPublicoLivreAsync(ct),
            PeloInstalador = true,
            CriadoEm = agora,
            ExpiraEm = agora + ValidadeDoInstalador,
            AutorizadoEm = agora,
            AutorizadoPor = usuarioId,
            UnidadeId = usuarioAtual.UnidadeAtivaId,
        });
        await db.SaveChangesAsync(ct);

        var rabicho = Encoding.UTF8.GetBytes(
            MarcaDoInstalador + JsonSerializer.Serialize(new { api = apiBase.TrimEnd('/'), codigo }));
        return new ArquivoPublicado(
            [.. executavel, .. rabicho], "application/octet-stream", "SMSMais-Atualizador.exe", emVigor.Versao);
    }

    public async Task<AtivacaoPendenteDto> ObterAtivacaoAsync(string codigoPublico, CancellationToken ct = default)
    {
        var ativacao = await BuscarAtivacaoAsync(codigoPublico, rastrear: false, ct);
        return ParaDto(ativacao, DateTime.UtcNow);
    }

    public async Task<AtivacaoPendenteDto> AutorizarAtivacaoAsync(string codigoPublico, CancellationToken ct = default)
    {
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new UnauthorizedAccessException("Só um usuário logado no painel autoriza um computador.");
        var ativacao = await BuscarAtivacaoAsync(codigoPublico, rastrear: true, ct);
        var agora = DateTime.UtcNow;
        switch (Situacao(ativacao, agora))
        {
            case SituacaoAtivacao.Vencida:
                throw new ConflitoException(
                    "extensao.ativacao_vencida", "Este pedido venceu. No computador, use o Configurar de novo.");
            case SituacaoAtivacao.Pendente:
                ativacao.AutorizadoEm = agora;
                ativacao.AutorizadoPor = usuarioId;
                ativacao.UnidadeId = usuarioAtual.UnidadeAtivaId;
                await db.SaveChangesAsync(ct);
                break;
            // Autorizada ou Usada: autorizar de novo não muda nada (o clique repetido não é erro).
        }
        return ParaDto(ativacao, agora);
    }

    private async Task<ExtensaoAtivacao> BuscarAtivacaoAsync(string codigoPublico, bool rastrear, CancellationToken ct)
    {
        var codigo = (codigoPublico ?? string.Empty).Trim().ToUpperInvariant();
        var consulta = rastrear ? db.ExtensaoAtivacoes : db.ExtensaoAtivacoes.AsNoTracking();
        return await consulta.FirstOrDefaultAsync(a => a.CodigoPublico == codigo, ct)
            ?? throw new NaoEncontradoException("Pedido de autorização", codigo);
    }

    private static SituacaoAtivacao Situacao(ExtensaoAtivacao a, DateTime agora) =>
        a.UsadoEm is not null ? SituacaoAtivacao.Usada
        : a.ExpiraEm <= agora ? SituacaoAtivacao.Vencida
        : a.AutorizadoEm is not null ? SituacaoAtivacao.Autorizada
        : SituacaoAtivacao.Pendente;

    private static AtivacaoPendenteDto ParaDto(ExtensaoAtivacao a, DateTime agora) =>
        new(a.CodigoPublico, a.Computador, a.CriadoEm, a.ExpiraEm, Situacao(a, agora));

    private async Task<string> CodigoPublicoLivreAsync(CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var codigo = ExtensaoSegredos.GerarCodigoPublico();
            if (!await db.ExtensaoAtivacoes.AnyAsync(a => a.CodigoPublico == codigo, ct))
                return codigo;
        }
        throw new InvalidOperationException("Não foi possível gerar um código de autorização livre.");
    }

    // ------------------------------------------------------------------ computador: sem login
    public async Task<AtivacaoIniciadaDto> IniciarAtivacaoAsync(IniciarAtivacaoRequest request, CancellationToken ct = default)
    {
        var painel = (await instituicao.ObterAsync(ct)).UrlPainel;
        if (string.IsNullOrWhiteSpace(painel))
        {
            throw new ConflitoException(
                "extensao.painel_sem_endereco",
                "A instituição ainda não tem o endereço do painel configurado; sem ele não há página de autorização.");
        }

        var agora = DateTime.UtcNow;
        // Os pedidos vencidos ficam uma semana (para quem for investigar) e somem aqui mesmo.
        await db.ExtensaoAtivacoes.Where(a => a.ExpiraEm < agora - GuardaDasAtivacoes).ExecuteDeleteAsync(ct);

        var codigo = ExtensaoSegredos.GerarSegredo();
        var publico = await CodigoPublicoLivreAsync(ct);
        db.ExtensaoAtivacoes.Add(new ExtensaoAtivacao
        {
            Id = Guid.CreateVersion7(),
            CodigoHash = ExtensaoSegredos.Hash(codigo),
            CodigoPublico = publico,
            Computador = Limpar(request.Computador, 100),
            VersaoAtualizador = Limpar(request.VersaoAtualizador, 32),
            CriadoEm = agora,
            ExpiraEm = agora + ValidadeDoConfigurar,
        });
        await db.SaveChangesAsync(ct);

        return new AtivacaoIniciadaDto(
            codigo,
            $"{painel.TrimEnd('/')}/app/extensao/autorizar/{publico}",
            (int)ValidadeDoConfigurar.TotalSeconds,
            IntervaloDeConsultaSegundos);
    }

    public async Task<TrocaDeCodigo> TrocarCodigoAsync(TrocarCodigoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return new TrocaDeCodigo(EstadoDaTroca.Encerrada);

        var hash = ExtensaoSegredos.Hash(request.Codigo.Trim());
        var ativacao = await db.ExtensaoAtivacoes.AsNoTracking().FirstOrDefaultAsync(a => a.CodigoHash == hash, ct);
        var agora = DateTime.UtcNow;
        if (ativacao is null || ativacao.UsadoEm is not null || ativacao.ExpiraEm <= agora)
            return new TrocaDeCodigo(EstadoDaTroca.Encerrada);
        if (ativacao.AutorizadoEm is null)
            return new TrocaDeCodigo(EstadoDaTroca.Aguardando);

        // Uso único de verdade: duas trocas simultâneas do mesmo código, só uma marca a linha.
        var dispositivoId = Guid.CreateVersion7();
        var marcadas = await db.ExtensaoAtivacoes
            .Where(a => a.Id == ativacao.Id && a.UsadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UsadoEm, agora).SetProperty(a => a.DispositivoId, dispositivoId), ct);
        if (marcadas == 0)
            return new TrocaDeCodigo(EstadoDaTroca.Encerrada);

        var token = ExtensaoSegredos.GerarSegredo();
        db.ExtensaoDispositivos.Add(new ExtensaoDispositivo
        {
            Id = dispositivoId,
            Computador = Limpar(request.Computador, 100) ?? ativacao.Computador ?? "(sem nome)",
            TokenHash = ExtensaoSegredos.Hash(token),
            Canal = ExtensaoCanal.Prod,
            AutorizadoEm = agora,
            AutorizadoPor = ativacao.AutorizadoPor,
            UnidadeId = ativacao.UnidadeId,
            VersaoAtualizador = Limpar(request.VersaoAtualizador, 32) ?? ativacao.VersaoAtualizador,
        });
        await db.SaveChangesAsync(ct);
        return new TrocaDeCodigo(EstadoDaTroca.Autorizado, token);
    }

    // ------------------------------------------------------------------ computador: com token
    public async Task<DispositivoAutenticado?> AutenticarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return null;
        var hash = ExtensaoSegredos.Hash(token.Trim());
        return await db.ExtensaoDispositivos.AsNoTracking()
            .Where(d => d.TokenHash == hash && d.RevogadoEm == null)
            .Select(d => new DispositivoAutenticado(d.Id, d.Canal))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VersaoDaExtensaoDto?> ConsultarExtensaoAsync(
        DispositivoAutenticado dispositivo, InventarioDoComputador inventario, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var instalada = Limpar(inventario.Instalada, 32);
        var atualizador = Limpar(inventario.Atualizador, 32);
        var chrome = Limpar(inventario.Chrome, 30);
        await db.ExtensaoDispositivos
            .Where(d => d.Id == dispositivo.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.UltimoContatoEm, agora)
                    .SetProperty(d => d.VersaoExtensao, instalada)
                    .SetProperty(d => d.VersaoAtualizador, d => atualizador ?? d.VersaoAtualizador)
                    .SetProperty(d => d.SituacaoChrome, chrome),
                ct);

        var emVigor = await EmVigorAsync(ExtensaoArtefato.Extensao, dispositivo.Canal, ct);
        return emVigor is null ? null : new VersaoDaExtensaoDto(emVigor.Versao, NomeDoCanal(dispositivo.Canal));
    }

    public async Task<VersaoDoAtualizadorDto?> ConsultarAtualizadorAsync(
        DispositivoAutenticado dispositivo, CancellationToken ct = default)
    {
        var emVigor = await EmVigorAsync(ExtensaoArtefato.Atualizador, dispositivo.Canal, ct);
        return emVigor is null ? null : new VersaoDoAtualizadorDto(emVigor.Versao, emVigor.Sha256);
    }

    public async Task<ArquivoPublicado?> BaixarAsync(
        DispositivoAutenticado dispositivo, ExtensaoArtefato artefato, CancellationToken ct = default)
    {
        var emVigor = await EmVigorAsync(artefato, dispositivo.Canal, ct);
        if (emVigor is null) return null;
        var conteudo = await db.ExtensaoPacotes.AsNoTracking()
            .Where(p => p.Id == emVigor.Id)
            .Select(p => p.Conteudo)
            .FirstOrDefaultAsync(ct);
        if (conteudo is null) return null;
        return artefato == ExtensaoArtefato.Extensao
            ? new ArquivoPublicado(conteudo, "application/zip", $"extensao-{emVigor.Versao}.zip", emVigor.Versao)
            : new ArquivoPublicado(conteudo, "application/octet-stream", "smsmais-atualizador.exe", emVigor.Versao);
    }

    private static string NomeDoCanal(ExtensaoCanal canal) => canal == ExtensaoCanal.Teste ? "teste" : "prod";

    // ------------------------------------------------------------------ administração
    // O filtro entra ANTES da projeção: o EF não compõe consulta por cima de um record montado
    // pelo construtor.
    private IQueryable<DispositivoDto> Dispositivos(Guid? id = null) =>
        db.ExtensaoDispositivos.AsNoTracking().Where(d => id == null || d.Id == id).Select(d => new DispositivoDto(
            d.Id,
            d.Computador,
            d.Canal,
            d.AutorizadoEm,
            db.Usuarios.Where(u => u.Id == d.AutorizadoPor).Select(u => u.NomeCompleto).FirstOrDefault(),
            d.UltimoContatoEm,
            d.VersaoExtensao,
            d.VersaoAtualizador,
            d.SituacaoChrome,
            d.RevogadoEm));

    public async Task<IReadOnlyList<DispositivoDto>> ListarDispositivosAsync(CancellationToken ct = default)
    {
        var lista = await Dispositivos().ToListAsync(ct);
        // Os revogados vão para o fim; entre os ativos, quem falou por último aparece primeiro.
        return lista
            .OrderBy(d => d.RevogadoEm is not null)
            .ThenByDescending(d => d.UltimoContatoEm ?? d.AutorizadoEm)
            .ToList();
    }

    private async Task<DispositivoDto> DispositivoAsync(Guid id, CancellationToken ct) =>
        await Dispositivos(id).FirstOrDefaultAsync(ct)
        ?? throw new NaoEncontradoException("Computador", id);

    public async Task<DispositivoDto> DefinirCanalAsync(Guid id, ExtensaoCanal canal, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(canal))
            throw new ValidacaoException("canal", "Canal desconhecido.");
        var alteradas = await db.ExtensaoDispositivos
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Canal, canal), ct);
        if (alteradas == 0) throw new NaoEncontradoException("Computador", id);
        return await DispositivoAsync(id, ct);
    }

    public async Task<DispositivoDto> RevogarAsync(Guid id, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;
        await db.ExtensaoDispositivos
            .Where(d => d.Id == id && d.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.RevogadoEm, agora).SetProperty(d => d.RevogadoPor, quem), ct);
        return await DispositivoAsync(id, ct); // 404 se não existe; revogar de novo não muda nada
    }

    public async Task<IReadOnlyList<PacoteDto>> ListarPacotesAsync(CancellationToken ct = default)
    {
        var linhas = await db.ExtensaoPacotes.AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Artefato,
                p.Versao,
                p.Tamanho,
                p.Sha256,
                p.Notas,
                p.PublicadoEm,
                PublicadoPorNome = db.Usuarios.Where(u => u.Id == p.PublicadoPor).Select(u => u.NomeCompleto).FirstOrDefault(),
                p.PublicadoPelaApi,
                p.PromovidoEm,
                p.PromovidoPelaApi,
                p.RetiradoEm,
            })
            .ToListAsync(ct);

        var resultado = new List<PacoteDto>();
        foreach (var grupo in linhas.GroupBy(p => p.Artefato).OrderBy(g => g.Key))
        {
            var ordenadas = grupo.OrderByDescending(p => p.Versao, Comparer<string>.Create(VersaoPublicada.Comparar)).ToList();
            var emTeste = ordenadas.FirstOrDefault(p => p.RetiradoEm is null)?.Id;
            var emProd = ordenadas.FirstOrDefault(p => p.RetiradoEm is null && p.PromovidoEm is not null)?.Id;
            resultado.AddRange(ordenadas.Select(p => new PacoteDto(
                p.Id, p.Artefato, p.Versao, p.Tamanho, p.Sha256, p.Notas, p.PublicadoEm, p.PublicadoPorNome,
                p.PublicadoPelaApi, p.PromovidoEm, p.PromovidoPelaApi, p.RetiradoEm,
                AtualEmTeste: p.Id == emTeste, AtualEmProd: p.Id == emProd)));
        }
        return resultado;
    }

    private async Task<PacoteDto> PacoteAsync(Guid id, CancellationToken ct) =>
        (await ListarPacotesAsync(ct)).FirstOrDefault(p => p.Id == id)
        ?? throw new NaoEncontradoException("Versão publicada", id);

    public async Task<PacoteDto> PublicarAsync(
        ExtensaoArtefato artefato, string? versao, string? notas, byte[] conteudo,
        bool pelaApi = false, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(artefato))
            throw new ValidacaoException("artefato", "Informe o que está sendo publicado: a extensão ou o atualizador.");
        if (conteudo is null || conteudo.Length == 0)
            throw new ValidacaoException("arquivo", "Arquivo não enviado.");

        var informada = string.IsNullOrWhiteSpace(versao) ? null : versao.Trim();
        string versaoFinal;
        if (artefato == ExtensaoArtefato.Extensao)
        {
            if (conteudo.Length > LimiteDaExtensao)
                throw new ValidacaoException("arquivo", "O pacote da extensão passa de 20 MB.");
            // A versão da extensão é a do manifest: é ela que o Chrome e o atualizador vão ler.
            versaoFinal = PacoteDaExtensao.LerVersao(conteudo);
            if (informada is not null && informada != versaoFinal)
                throw new ValidacaoException("versao", $"A versão informada ({informada}) não é a do manifest do pacote ({versaoFinal}).");
        }
        else
        {
            if (conteudo.Length > LimiteDoAtualizador)
                throw new ValidacaoException("arquivo", "O executável do atualizador passa de 30 MB.");
            if (conteudo.Length < 2 || conteudo[0] != (byte)'M' || conteudo[1] != (byte)'Z')
                throw new ValidacaoException("arquivo", "O arquivo não é um executável do Windows.");
            if (TemRabichoDeInstalador(conteudo))
            {
                throw new ValidacaoException(
                    "arquivo",
                    "Este arquivo é um instalador baixado do painel (traz um código de ativação). Publique o executável gerado pela compilação.");
            }
            if (!VersaoPublicada.Valida(informada))
                throw new ValidacaoException("versao", "Informe a versão do atualizador no formato 1.2.3 (a mesma do executável).");
            versaoFinal = informada!;
        }

        // Compara com TUDO o que já foi publicado, inclusive o que foi retirado: computador não
        // rebaixa, então uma versão menor ou igual nunca seria instalada por quem já recebeu a outra.
        var existentes = await db.ExtensaoPacotes.AsNoTracking()
            .Where(p => p.Artefato == artefato)
            .Select(p => p.Versao)
            .ToListAsync(ct);
        if (existentes.Contains(versaoFinal))
            throw new ConflitoException("extensao.versao_repetida", $"A versão {versaoFinal} já foi publicada.");
        var maior = existentes.OrderByDescending(v => v, Comparer<string>.Create(VersaoPublicada.Comparar)).FirstOrDefault();
        if (maior is not null && !VersaoPublicada.MaisNova(versaoFinal, maior))
        {
            throw new ValidacaoException(
                "versao", $"A versão {versaoFinal} não é maior que a última publicada ({maior}). Os computadores não rebaixam.");
        }

        var pacote = new ExtensaoPacote
        {
            Id = Guid.CreateVersion7(),
            Artefato = artefato,
            Versao = versaoFinal,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo)),
            Tamanho = conteudo.Length,
            Conteudo = conteudo,
            Notas = Limpar(notas, 1000),
            PublicadoEm = DateTime.UtcNow,
            PublicadoPor = pelaApi ? null : usuarioAtual.UsuarioId,
            PublicadoPelaApi = pelaApi,
        };
        db.ExtensaoPacotes.Add(pacote);
        await db.SaveChangesAsync(ct);
        DerrubarCache(artefato);
        return await PacoteAsync(pacote.Id, ct);
    }

    public async Task<PacoteDto> PromoverAsync(Guid id, bool pelaApi = false, CancellationToken ct = default)
    {
        var pacote = await PacoteAsync(id, ct);
        if (pacote.RetiradoEm is not null)
            throw new ConflitoException("extensao.versao_retirada", "Esta versão foi retirada; não pode ser promovida.");
        if (pacote.PromovidoEm is not null)
            return pacote;

        var emProd = await EmVigorAsync(pacote.Artefato, ExtensaoCanal.Prod, ct);
        if (emProd is not null && !VersaoPublicada.MaisNova(pacote.Versao, emProd.Versao))
        {
            throw new ConflitoException(
                "extensao.producao_mais_nova",
                $"A produção já está na versão {emProd.Versao}, mais nova que esta. Os computadores não rebaixam.");
        }

        var agora = DateTime.UtcNow;
        var quem = pelaApi ? null : usuarioAtual.UsuarioId;
        await db.ExtensaoPacotes
            .Where(p => p.Id == id && p.PromovidoEm == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.PromovidoEm, agora)
                    .SetProperty(p => p.PromovidoPor, quem)
                    .SetProperty(p => p.PromovidoPelaApi, pelaApi),
                ct);
        DerrubarCache(pacote.Artefato);
        return await PacoteAsync(id, ct);
    }

    public async Task<PacoteDto> RetirarAsync(Guid id, CancellationToken ct = default)
    {
        var pacote = await PacoteAsync(id, ct);
        if (pacote.RetiradoEm is not null)
            return pacote;
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;
        await db.ExtensaoPacotes
            .Where(p => p.Id == id && p.RetiradoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RetiradoEm, agora).SetProperty(p => p.RetiradoPor, quem), ct);
        DerrubarCache(pacote.Artefato);
        return await PacoteAsync(id, ct);
    }

    // ------------------------------------------------------------------ API de publicação
    private const string PrefixoDaChave = "pub_";

    private IQueryable<ExtensaoChavePublicacao> ChaveAtiva() =>
        db.ExtensaoChavesPublicacao.Where(c => c.RevogadaEm == null);

    public async Task<ChavePublicacaoDto> ObterChavePublicacaoAsync(CancellationToken ct = default)
    {
        var chave = await ChaveAtiva().AsNoTracking()
            .OrderByDescending(c => c.CriadaEm)
            .Select(c => new
            {
                c.Prefixo,
                c.CriadaEm,
                CriadaPorNome = db.Usuarios.Where(u => u.Id == c.CriadaPor).Select(u => u.NomeCompleto).FirstOrDefault(),
                c.UltimoUsoEm,
            })
            .FirstOrDefaultAsync(ct);
        return chave is null
            ? new ChavePublicacaoDto(false, null, null, null, null)
            : new ChavePublicacaoDto(true, chave.Prefixo, chave.CriadaEm, chave.CriadaPorNome, chave.UltimoUsoEm);
    }

    public async Task<ChaveGeradaDto> GerarChavePublicacaoAsync(CancellationToken ct = default)
    {
        var quem = usuarioAtual.UsuarioId
            ?? throw new UnauthorizedAccessException("Só um usuário logado no painel gera a chave de publicação.");
        var agora = DateTime.UtcNow;
        // Uma ativa por vez: a nova revoga a anterior.
        await ChaveAtiva().ExecuteUpdateAsync(
            s => s.SetProperty(c => c.RevogadaEm, agora).SetProperty(c => c.RevogadaPor, quem), ct);

        var chave = PrefixoDaChave + ExtensaoSegredos.GerarSegredo();
        db.ExtensaoChavesPublicacao.Add(new ExtensaoChavePublicacao
        {
            Id = Guid.CreateVersion7(),
            ChaveHash = ExtensaoSegredos.Hash(chave),
            Prefixo = chave[..(PrefixoDaChave.Length + 6)],
            CriadaEm = agora,
            CriadaPor = quem,
        });
        await db.SaveChangesAsync(ct);
        return new ChaveGeradaDto(chave, await ObterChavePublicacaoAsync(ct));
    }

    public async Task<ChavePublicacaoDto> RevogarChavePublicacaoAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;
        await ChaveAtiva().ExecuteUpdateAsync(
            s => s.SetProperty(c => c.RevogadaEm, agora).SetProperty(c => c.RevogadaPor, quem), ct);
        return await ObterChavePublicacaoAsync(ct);
    }

    public async Task<bool> ChavePublicacaoValeAsync(string? chave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > 200)
            return false;
        var hash = ExtensaoSegredos.Hash(chave.Trim());
        var agora = DateTime.UtcNow;
        // Conferir e registrar o uso num comando só: 1 linha alterada = a chave existe e está ativa.
        var usadas = await ChaveAtiva()
            .Where(c => c.ChaveHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UltimoUsoEm, agora), ct);
        return usadas == 1;
    }

    // ------------------------------------------------------------------------- miúdos
    private static bool TemRabichoDeInstalador(byte[] conteudo)
    {
        var marca = Encoding.UTF8.GetBytes(MarcaDoInstalador);
        var janela = conteudo.AsSpan(Math.Max(0, conteudo.Length - 4096));
        return janela.IndexOf(marca) >= 0;
    }

    /// <summary>Texto vindo de fora (do computador): sem caracteres de controle e com teto de tamanho.</summary>
    private static string? Limpar(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = new string(texto.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpo.Length == 0) return null;
        return limpo.Length <= maximo ? limpo : limpo[..maximo];
    }
}
