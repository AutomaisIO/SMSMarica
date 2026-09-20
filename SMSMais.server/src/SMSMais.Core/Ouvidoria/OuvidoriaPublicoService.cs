using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

public sealed class OuvidoriaPublicoService(
    SmsMaisDbContext db,
    IOuvidoriaManifestacaoService manifestacoes,
    IOuvidoriaCatalogoService catalogo) : IOuvidoriaPublicoService
{
    public async Task<IReadOnlyList<AssuntoPublicoDto>> ListarAssuntosAsync(CancellationToken ct = default)
    {
        await catalogo.GarantirCatalogoBaseAsync(ct);
        return await db.OuvidoriaAssuntos.AsNoTracking()
            .Where(a => a.Ativo)
            .OrderBy(a => a.PaiId == null ? 0 : 1)
            .ThenBy(a => a.Ordem)
            .ThenBy(a => a.Nome)
            .Select(a => new AssuntoPublicoDto(a.Id, a.Nome, a.PaiId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UnidadePublicaDto>> ListarUnidadesAsync(CancellationToken ct = default)
        => await db.Unidades.AsNoTracking()
            .Where(u => u.Ativo && !u.Externa)
            .OrderBy(u => u.Nome)
            .Select(u => new UnidadePublicaDto(u.Id, u.Nome))
            .ToListAsync(ct);

    public Task<ManifestacaoCriadaDto> RegistrarAsync(RegistrarManifestacaoPublicaRequest request, CancellationToken ct = default)
        => manifestacoes.RegistrarAsync(new RegistrarManifestacaoRequest(
            request.Tipo,
            request.Identificacao,
            OuvidoriaCanal.SitePublico,
            OuvidoriaOrigem.Cidadao,
            request.Teor,
            Resumo: null,
            request.AssuntoId,
            SubassuntoId: null,
            request.UnidadeId,
            request.DataFato,
            request.LocalFato,
            request.Manifestante,
            request.Referido,
            request.EnvolvidoDescricao,
            ProtocoloExterno: null,
            SistemaExterno: null,
            RegulacaoSolicitacaoId: null,
            Anexos: []), ct);

    public async Task<AcompanhamentoDto> AcompanharAsync(string protocolo, string codigo, CancellationToken ct = default)
    {
        var m = await LocalizarAsync(protocolo, codigo, ct);

        var eventos = await db.OuvidoriaEventos.AsNoTracking()
            .Where(e => e.ManifestacaoId == m.Id && e.VisivelAoCidadao)
            .OrderBy(e => e.CriadoEm)
            .Select(e => new { e.Tipo, e.Texto, e.CriadoEm })
            .ToListAsync(ct);

        var temRecurso = eventos.Any(e => e.Tipo == OuvidoriaTipoEvento.Recurso);

        return new AcompanhamentoDto(
            m.Protocolo,
            m.Tipo,
            m.Status,
            m.RegistradaEm,
            m.PrazoRespostaEm,
            m.ProrrogadoEm != null,
            m.RespostaConclusiva,
            m.Resolutividade,
            PodeComplementar: m.Status == OuvidoriaStatus.AguardandoComplementacao,
            PodeRecorrer: m.Status == OuvidoriaStatus.Respondida && !temRecurso,
            [.. eventos.Select(e => new EventoPublicoDto(e.Tipo, e.Texto, e.CriadoEm))]);
    }

    public async Task ComplementarAsync(string protocolo, string codigo, string texto, CancellationToken ct = default)
    {
        var m = await LocalizarAsync(protocolo, codigo, ct);
        await manifestacoes.ComplementarAsync(m.Id, new TextoComAnexosRequest(texto, []), ct);
    }

    public async Task RecorrerAsync(string protocolo, string codigo, string texto, CancellationToken ct = default)
    {
        var m = await LocalizarAsync(protocolo, codigo, ct);
        await manifestacoes.RegistrarRecursoAsync(m.Id, new TextoRequest(texto), ct);
    }

    /// <summary>Protocolo + código em tempo constante. Qualquer falha é o mesmo 404 — não se diz o que errou.</summary>
    private async Task<OuvidoriaManifestacao> LocalizarAsync(string protocolo, string codigo, CancellationToken ct)
    {
        var normalizado = OuvidoriaProtocolo.Normalizar(protocolo);
        var m = normalizado.Length == 0
            ? null
            : await db.OuvidoriaManifestacoes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Protocolo == normalizado && x.ExcluidoEm == null, ct);

        if (m is null || !OuvidoriaProtocolo.Confere(codigo, m.CodigoAcessoHash))
        {
            throw new NaoEncontradoException("Manifestacao", normalizado);
        }
        return m;
    }
}
