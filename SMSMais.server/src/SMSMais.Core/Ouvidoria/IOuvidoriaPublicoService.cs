using SMSMais.Core.Ouvidoria.Dtos;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Canal público da ouvidoria (sem login): registrar e acompanhar por protocolo + código de acesso
/// (plano §3.2). Protocolo/código que não batem → 404, sem distinguir qual dos dois errou.
/// </summary>
public interface IOuvidoriaPublicoService
{
    /// <summary>Assuntos ativos (id, nome, pai) para o formulário público.</summary>
    Task<IReadOnlyList<AssuntoPublicoDto>> ListarAssuntosAsync(CancellationToken ct = default);

    /// <summary>Unidades ativas e não externas (id, nome).</summary>
    Task<IReadOnlyList<UnidadePublicaDto>> ListarUnidadesAsync(CancellationToken ct = default);

    /// <summary>Registro pelo site: canal <c>SitePublico</c>, origem <c>Cidadao</c>, autor "Cidadão".</summary>
    Task<ManifestacaoCriadaDto> RegistrarAsync(RegistrarManifestacaoPublicaRequest request, CancellationToken ct = default);

    Task<AcompanhamentoDto> AcompanharAsync(string protocolo, string codigo, CancellationToken ct = default);
    Task ComplementarAsync(string protocolo, string codigo, string texto, CancellationToken ct = default);
    Task RecorrerAsync(string protocolo, string codigo, string texto, CancellationToken ct = default);
}
