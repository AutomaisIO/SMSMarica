using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using SMSMais.Core.Institucional;
using SMSMais.Core.Midias;
using SMSMais.Core.Regulacao.Indicadores;
using SMSMais.Core.Regulacao.Indicadores.Dtos;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Indicadores;

/// <summary>
/// O cálculo dos indicadores é SQL cru sobre o espelho — só o Postgres diz se ele roda. Aqui roda
/// cada sistema sobre a base de teste (quase vazia): o que se verifica é que nenhuma consulta quebra
/// e que as seções saem todas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class IndicadoresRegulacaoSqlTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(FonteIndicadorRegulacao.Sisreg)]
    [InlineData(FonteIndicadorRegulacao.Ser)]
    [InlineData(FonteIndicadorRegulacao.Sernit)]
    [InlineData(FonteIndicadorRegulacao.EsusSg)]
    public async Task Calculo_roda_no_Postgres(FonteIndicadorRegulacao fonte)
    {
        await using var db = fixture.CriarDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = new IndicadoresRegulacaoService(
            db, cache, Substitute.For<IInstituicaoService>(), Substitute.For<IMidiasService>());

        var r = await servico.ObterAsync(fonte, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 1));

        Assert.Equal(12, r.Meses.Count);
        Assert.NotEmpty(r.Secoes);
    }
}
