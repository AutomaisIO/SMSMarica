using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

public sealed class OuvidoriaCatalogoService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IOuvidoriaCatalogoService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task GarantirCatalogoBaseAsync(CancellationToken ct = default)
    {
        await OuvidoriaCatalogoBase.GarantirAssuntosAsync(_db, ct);
        await OuvidoriaCatalogoBase.GarantirConfiguracaoAsync(_db, ct);
    }

    // ================= Pontos de resposta =================

    public async Task<IReadOnlyList<PontoRespostaDto>> ListarPontosRespostaAsync(CancellationToken ct = default)
    {
        var pontos = await _db.OuvidoriaPontosResposta.AsNoTracking()
            .Where(p => p.ExcluidoEm == null)
            .Include(p => p.Membros)
            .Include(p => p.Unidade)
            .OrderBy(p => p.Nome)
            .ToListAsync(ct);

        return await ProjetarPontosAsync(pontos, ct);
    }

    public async Task<PontoRespostaDto> ObterPontoRespostaAsync(Guid id, CancellationToken ct = default)
    {
        var ponto = await _db.OuvidoriaPontosResposta.AsNoTracking()
            .Include(p => p.Membros)
            .Include(p => p.Unidade)
            .FirstOrDefaultAsync(p => p.Id == id && p.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaPontoResposta), id);

        return (await ProjetarPontosAsync([ponto], ct))[0];
    }

    public async Task<PontoRespostaDto> CriarPontoRespostaAsync(SalvarPontoRespostaRequest request, CancellationToken ct = default)
    {
        await ValidarPontoAsync(null, request, ct);

        var agora = DateTime.UtcNow;
        var ponto = new OuvidoriaPontoResposta
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Tipo = request.Tipo,
            UnidadeId = request.Tipo == OuvidoriaTipoPontoResposta.Unidade ? request.UnidadeId : null,
            PrazoDias = request.PrazoDias,
            Ativo = request.Ativo,
            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        foreach (var m in MembrosDistintos(request.Membros))
        {
            ponto.Membros.Add(new OuvidoriaPontoRespostaMembro
            {
                Id = Guid.CreateVersion7(),
                PontoRespostaId = ponto.Id,
                UsuarioId = m.UsuarioId,
                Titular = m.Titular,
                CriadoEm = agora,
            });
        }

        _db.OuvidoriaPontosResposta.Add(ponto);
        await _db.SaveChangesAsync(ct);
        return await ObterPontoRespostaAsync(ponto.Id, ct);
    }

    public async Task AtualizarPontoRespostaAsync(Guid id, SalvarPontoRespostaRequest request, CancellationToken ct = default)
    {
        var ponto = await _db.OuvidoriaPontosResposta
            .Include(p => p.Membros)
            .FirstOrDefaultAsync(p => p.Id == id && p.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaPontoResposta), id);

        await ValidarPontoAsync(id, request, ct);

        var agora = DateTime.UtcNow;
        ponto.Nome = request.Nome.Trim();
        ponto.Tipo = request.Tipo;
        ponto.UnidadeId = request.Tipo == OuvidoriaTipoPontoResposta.Unidade ? request.UnidadeId : null;
        ponto.PrazoDias = request.PrazoDias;
        ponto.Ativo = request.Ativo;
        ponto.AtualizadoEm = agora;
        ponto.AtualizadoPor = _usuarioAtual.UsuarioId;

        // Membros substituídos por inteiro: quem não veio sai, quem veio entra/atualiza titularidade.
        var desejados = MembrosDistintos(request.Membros).ToDictionary(m => m.UsuarioId, m => m.Titular);
        foreach (var existente in ponto.Membros.ToList())
        {
            if (desejados.TryGetValue(existente.UsuarioId, out var titular))
            {
                existente.Titular = titular;
                desejados.Remove(existente.UsuarioId);
            }
            else
            {
                _db.OuvidoriaPontoRespostaMembros.Remove(existente);
            }
        }
        foreach (var (usuarioId, titular) in desejados)
        {
            ponto.Membros.Add(new OuvidoriaPontoRespostaMembro
            {
                Id = Guid.CreateVersion7(),
                PontoRespostaId = ponto.Id,
                UsuarioId = usuarioId,
                Titular = titular,
                CriadoEm = agora,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> PontosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
        => await _db.OuvidoriaPontoRespostaMembros.AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId
                && m.PontoResposta!.Ativo && m.PontoResposta.ExcluidoEm == null)
            .Select(m => m.PontoRespostaId)
            .Distinct()
            .ToListAsync(ct);

    private async Task ValidarPontoAsync(Guid? id, SalvarPontoRespostaRequest request, CancellationToken ct)
    {
        if (request.Tipo == OuvidoriaTipoPontoResposta.Unidade)
        {
            if (request.UnidadeId is null)
            {
                throw new ValidacaoException("unidadeId", "Ponto de resposta do tipo Unidade exige a unidade de saúde.");
            }

            var unidadeExiste = await _db.Unidades.AnyAsync(u => u.Id == request.UnidadeId, ct);
            if (!unidadeExiste)
            {
                throw new NaoEncontradoException("Unidade", request.UnidadeId);
            }

            var jaTemPonto = await _db.OuvidoriaPontosResposta
                .AnyAsync(p => p.UnidadeId == request.UnidadeId && p.ExcluidoEm == null && p.Id != id, ct);
            if (jaTemPonto)
            {
                throw new ConflitoException("ouvidoria.ponto_unidade_duplicado", "Esta unidade já tem um ponto de resposta.");
            }
        }

        var usuarios = MembrosDistintos(request.Membros).Select(m => m.UsuarioId).ToList();
        if (usuarios.Count > 0)
        {
            var existentes = await _db.Usuarios.Where(u => usuarios.Contains(u.Id) && u.ExcluidoEm == null).CountAsync(ct);
            if (existentes != usuarios.Count)
            {
                throw new ValidacaoException("membros", "Um ou mais usuários informados não existem.");
            }
        }
    }

    private async Task<IReadOnlyList<PontoRespostaDto>> ProjetarPontosAsync(IReadOnlyList<OuvidoriaPontoResposta> pontos, CancellationToken ct)
    {
        if (pontos.Count == 0) return [];

        var ids = pontos.Select(p => p.Id).ToList();
        var pendentes = await _db.OuvidoriaManifestacoes.AsNoTracking()
            .Where(m => m.ExcluidoEm == null && m.PontoRespostaId != null && ids.Contains(m.PontoRespostaId.Value)
                && m.Status == OuvidoriaStatus.Encaminhada)
            .GroupBy(m => m.PontoRespostaId!.Value)
            .Select(g => new { PontoId = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.PontoId, x => x.Qtd, ct);

        var usuarioIds = pontos.SelectMany(p => p.Membros).Select(m => m.UsuarioId).Distinct().ToList();
        var nomes = usuarioIds.Count == 0
            ? []
            : await _db.Usuarios.AsNoTracking()
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

        return [.. pontos.Select(p => new PontoRespostaDto(
            p.Id, p.Nome, p.Tipo, p.UnidadeId, p.Unidade?.Nome, p.PrazoDias, p.Ativo,
            [.. p.Membros
                .OrderByDescending(m => m.Titular)
                .ThenBy(m => nomes.GetValueOrDefault(m.UsuarioId))
                .Select(m => new PontoRespostaMembroDto(m.UsuarioId, nomes.GetValueOrDefault(m.UsuarioId) ?? "(usuário)", m.Titular))],
            pendentes.GetValueOrDefault(p.Id)))];
    }

    private static IEnumerable<SalvarMembroRequest> MembrosDistintos(IReadOnlyList<SalvarMembroRequest>? membros)
        => membros is null ? [] : membros.GroupBy(m => m.UsuarioId).Select(g => g.OrderByDescending(m => m.Titular).First());

    // ================= Assuntos =================

    public async Task<IReadOnlyList<AssuntoDto>> ListarAssuntosAsync(CancellationToken ct = default)
    {
        await GarantirCatalogoBaseAsync(ct);
        return await _db.OuvidoriaAssuntos.AsNoTracking()
            .OrderBy(a => a.PaiId == null ? 0 : 1)
            .ThenBy(a => a.Ordem)
            .ThenBy(a => a.Nome)
            .Select(a => new AssuntoDto(a.Id, a.PaiId, a.Nome, a.CodigoOuvidorSus, a.Ordem, a.Ativo))
            .ToListAsync(ct);
    }

    public async Task<AssuntoDto> CriarAssuntoAsync(SalvarAssuntoRequest request, CancellationToken ct = default)
    {
        await ValidarAssuntoAsync(null, request, ct);
        var assunto = new OuvidoriaAssunto
        {
            Id = Guid.CreateVersion7(),
            PaiId = request.PaiId,
            Nome = request.Nome.Trim(),
            CodigoOuvidorSus = Limpar(request.CodigoOuvidorSus),
            Ordem = request.Ordem,
            Ativo = request.Ativo,
        };
        _db.OuvidoriaAssuntos.Add(assunto);
        await _db.SaveChangesAsync(ct);
        return new AssuntoDto(assunto.Id, assunto.PaiId, assunto.Nome, assunto.CodigoOuvidorSus, assunto.Ordem, assunto.Ativo);
    }

    public async Task AtualizarAssuntoAsync(Guid id, SalvarAssuntoRequest request, CancellationToken ct = default)
    {
        var assunto = await _db.OuvidoriaAssuntos.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaAssunto), id);

        if (request.PaiId == id)
        {
            throw new ValidacaoException("paiId", "Um assunto não pode ser pai de si mesmo.");
        }
        await ValidarAssuntoAsync(id, request, ct);

        assunto.PaiId = request.PaiId;
        assunto.Nome = request.Nome.Trim();
        assunto.CodigoOuvidorSus = Limpar(request.CodigoOuvidorSus);
        assunto.Ordem = request.Ordem;
        assunto.Ativo = request.Ativo;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidarAssuntoAsync(Guid? id, SalvarAssuntoRequest request, CancellationToken ct)
    {
        if (request.PaiId is { } paiId)
        {
            var pai = await _db.OuvidoriaAssuntos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == paiId, ct)
                ?? throw new NaoEncontradoException(nameof(OuvidoriaAssunto), paiId);
            // Dois níveis só: subassunto não tem filho.
            if (pai.PaiId is not null)
            {
                throw new ValidacaoException("paiId", "O catálogo tem dois níveis: um subassunto não pode ter filhos.");
            }
        }

        var nome = request.Nome.Trim();
        var duplicado = await _db.OuvidoriaAssuntos
            .AnyAsync(a => a.PaiId == request.PaiId && a.Nome == nome && a.Id != id, ct);
        if (duplicado)
        {
            throw new ConflitoException("ouvidoria.assunto_duplicado", $"Já existe o assunto \"{nome}\" neste nível.");
        }
    }

    // ================= Marcadores =================

    public async Task<IReadOnlyList<MarcadorDto>> ListarMarcadoresAsync(CancellationToken ct = default)
        => await _db.OuvidoriaMarcadores.AsNoTracking()
            .OrderBy(m => m.Nome)
            .Select(m => new MarcadorDto(m.Id, m.Nome, m.Ativo))
            .ToListAsync(ct);

    public async Task<MarcadorDto> CriarMarcadorAsync(SalvarMarcadorRequest request, CancellationToken ct = default)
    {
        var nome = request.Nome.Trim();
        if (await _db.OuvidoriaMarcadores.AnyAsync(m => m.Nome == nome, ct))
        {
            throw new ConflitoException("ouvidoria.marcador_duplicado", $"Já existe o marcador \"{nome}\".");
        }

        var marcador = new OuvidoriaMarcador { Id = Guid.CreateVersion7(), Nome = nome, Ativo = request.Ativo };
        _db.OuvidoriaMarcadores.Add(marcador);
        await _db.SaveChangesAsync(ct);
        return new MarcadorDto(marcador.Id, marcador.Nome, marcador.Ativo);
    }

    public async Task AtualizarMarcadorAsync(Guid id, SalvarMarcadorRequest request, CancellationToken ct = default)
    {
        var marcador = await _db.OuvidoriaMarcadores.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NaoEncontradoException(nameof(OuvidoriaMarcador), id);

        var nome = request.Nome.Trim();
        if (await _db.OuvidoriaMarcadores.AnyAsync(m => m.Nome == nome && m.Id != id, ct))
        {
            throw new ConflitoException("ouvidoria.marcador_duplicado", $"Já existe o marcador \"{nome}\".");
        }

        marcador.Nome = nome;
        marcador.Ativo = request.Ativo;
        await _db.SaveChangesAsync(ct);
    }

    // ================= Configuração =================

    public async Task<OuvidoriaConfiguracaoDto> ObterConfiguracaoAsync(CancellationToken ct = default)
    {
        await GarantirCatalogoBaseAsync(ct);
        var c = await OuvidoriaCatalogoBase.GarantirConfiguracaoAsync(_db, ct);
        return new OuvidoriaConfiguracaoDto(
            c.PrazoCidadaoDias, c.ProrrogacaoDias, c.PrazoAreaDias, c.PrazoAreaAltaDias, c.PrazoAreaUrgenteDiasUteis,
            c.ComplementacaoDias, c.ArquivamentoAutomaticoDias, c.NotificarPorWhatsApp, c.TextoRecibo);
    }

    public async Task SalvarConfiguracaoAsync(OuvidoriaConfiguracaoDto request, CancellationToken ct = default)
    {
        var c = await OuvidoriaCatalogoBase.GarantirConfiguracaoAsync(_db, ct);
        c.PrazoCidadaoDias = request.PrazoCidadaoDias;
        c.ProrrogacaoDias = request.ProrrogacaoDias;
        c.PrazoAreaDias = request.PrazoAreaDias;
        c.PrazoAreaAltaDias = request.PrazoAreaAltaDias;
        c.PrazoAreaUrgenteDiasUteis = request.PrazoAreaUrgenteDiasUteis;
        c.ComplementacaoDias = request.ComplementacaoDias;
        c.ArquivamentoAutomaticoDias = request.ArquivamentoAutomaticoDias;
        c.NotificarPorWhatsApp = request.NotificarPorWhatsApp;
        c.TextoRecibo = Limpar(request.TextoRecibo);
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(ct);
    }

    private static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
