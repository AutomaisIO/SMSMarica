using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Catalogo;

/// <summary>
/// Cache de vetores de consulta. Não toca no banco — é só o freio de gasto entre a barra de
/// busca e o provedor de embeddings.
/// </summary>
public class CacheVetorConsultaTests
{
    [Fact]
    public async Task Mesmo_termo_normalizado_nao_chama_o_provedor_duas_vezes()
    {
        var cache = new CacheVetorConsulta();
        var fake = new EmbeddingsFake();

        var a = await cache.ObterOuEmbedarAsync("CARDIOLOGIA", fake, CancellationToken.None);
        var b = await cache.ObterOuEmbedarAsync("CARDIOLOGIA", fake, CancellationToken.None);

        fake.ChamadasUnitarias.Should().Be(1, "a segunda busca do mesmo termo sai do cache");
        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public async Task Termos_diferentes_nao_compartilham_entrada()
    {
        var cache = new CacheVetorConsulta();
        var fake = new EmbeddingsFake();

        await cache.ObterOuEmbedarAsync("CARDIOLOGIA", fake, CancellationToken.None);
        await cache.ObterOuEmbedarAsync("DERMATOLOGIA", fake, CancellationToken.None);

        fake.ChamadasUnitarias.Should().Be(2);
        cache.Tamanho.Should().Be(2);
    }

    [Fact]
    public async Task Cache_tem_teto_e_nao_cresce_sem_limite()
    {
        var cache = new CacheVetorConsulta();
        var fake = new EmbeddingsFake();

        // 600 termos distintos contra um teto de 500: o excedente é despejado.
        for (var i = 0; i < 600; i++)
        {
            await cache.ObterOuEmbedarAsync($"TERMO {i}", fake, CancellationToken.None);
        }

        cache.Tamanho.Should().BeLessThanOrEqualTo(500);
    }
}
