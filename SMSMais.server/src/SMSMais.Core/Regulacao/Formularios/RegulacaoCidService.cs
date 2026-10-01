using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Ser;
using SMSMais.Core.Sernit;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.Formularios;

public sealed record CidRegulacaoDto(string Codigo, string Descricao, string Texto);

public sealed record CidRegulacaoSugestoesDto(
    IReadOnlyList<CidRegulacaoDto> Itens,
    /// <summary>O sistema cortou a lista no teto dele (500) — há mais CID que casam o termo.</summary>
    bool Truncado,
    /// <summary>De qual sistema veio a lista — o do destino, ou o que o procedimento tem.</summary>
    SistemaRegulacao Sistema);

public interface IRegulacaoCidService
{
    /// <summary>
    /// Os CID que o sistema de destino aceita como Hipótese para AQUELE procedimento.
    ///
    /// <para>Do espelho quando o recurso já tem lista copiada; senão, a consulta ao vivo no
    /// SER/SERNIT — só o fetch de sugestões, nada é gravado.</para>
    /// </summary>
    Task<CidRegulacaoSugestoesDto> BuscarAsync(
        Guid procedimentoId, SistemaRegulacao? sistema, string termo, CancellationToken ct);
}

/// <summary>
/// A caixa de CID da Hipótese no fluxo Externo (ADR-0052).
///
/// <para><b>A lista é do RECURSO, não do CID-10</b> (docs/ser-criar-solicitacao.md §2.1.3): o
/// oncológico aceita 136 códigos, a cardiologia os 14.226. Por isso a pergunta é feita pelo
/// procedimento canônico + sistema, e daqui se chega ao recurso e ao ramo daquele sistema — uma
/// lista única ofereceria código que o destino recusa em silêncio na hora de gravar.</para>
///
/// <para>Destino ESUS SG (ou ainda não escolhido) usa o SER, e na falta dele o SERNIT: o ESUS
/// não tem bloco dinâmico mapeado, e a Hipótese que aparece no formulário é a deles.</para>
/// </summary>
public sealed class RegulacaoCidService(
    SmsMaisDbContext db,
    ISerCatalogoService serCatalogo,
    ISerNovaSolicitacaoService serAoVivo,
    ISernitCatalogoService sernitCatalogo,
    ISernitNovaSolicitacaoService sernitAoVivo) : IRegulacaoCidService
{
    public async Task<CidRegulacaoSugestoesDto> BuscarAsync(
        Guid procedimentoId, SistemaRegulacao? sistema, string termo, CancellationToken ct)
    {
        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.ProcedimentoId == procedimentoId && o.Ativo
                && (o.Sistema == SistemaRegulacao.Ser || o.Sistema == SistemaRegulacao.Sernit))
            .Select(o => new { o.Sistema, o.ChaveExterna, o.Ramo })
            .ToListAsync(ct);

        var origem =
            origens.FirstOrDefault(o => o.Sistema == sistema)
            ?? origens.FirstOrDefault(o => o.Sistema == SistemaRegulacao.Ser)
            ?? origens.FirstOrDefault(o => o.Sistema == SistemaRegulacao.Sernit)
            ?? throw new ValidacaoException(
                "procedimento",
                "Este procedimento não tem recurso no SER nem no SERNIT — não há lista de CID para a hipótese.");

        // Chave externa: SER "{tipo}|{valor}|{AE|NAO_AE}", SERNIT "{tipo}|{valor}".
        var partes = origem.ChaveExterna.Split('|');
        if (partes.Length < 2 || !int.TryParse(partes[0], out var tipo))
        {
            throw new ValidacaoException(
                "procedimento",
                $"A origem {origem.Sistema} deste procedimento está com a chave fora do formato ({origem.ChaveExterna}).");
        }

        var recurso = partes[1];
        var busca = termo ?? string.Empty;

        if (origem.Sistema == SistemaRegulacao.Ser)
        {
            var tipoSer = (TipoRecursoSer)tipo;
            var ae = origem.Ramo == "AE";
            var r = await serCatalogo.BuscarCidsAsync(tipoSer, recurso, ae, busca, ct)
                    ?? await serAoVivo.SugerirCidsAsync(tipoSer.ToString(), recurso, ae, busca, ct);
            return new CidRegulacaoSugestoesDto(
                [.. r.Itens.Select(c => new CidRegulacaoDto(c.Codigo, c.Descricao, c.Texto))],
                r.Truncado, SistemaRegulacao.Ser);
        }

        var tipoSernit = (TipoRecursoSernit)tipo;
        var s = await sernitCatalogo.BuscarCidsAsync(tipoSernit, recurso, busca, ct)
                ?? await sernitAoVivo.SugerirCidsAsync(tipoSernit.ToString(), recurso, busca, ct);
        return new CidRegulacaoSugestoesDto(
            [.. s.Itens.Select(c => new CidRegulacaoDto(c.Codigo, c.Descricao, c.Texto))],
            s.Truncado, SistemaRegulacao.Sernit);
    }
}
