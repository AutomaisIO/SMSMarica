using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A chave-mestra do sincronismo automático com o SISREG — o interruptor que os dois agendadores
/// (varredura das unidades e lote de mapeamento) consultam antes de disparar qualquer coisa.
///
/// <para>O que está sendo protegido aqui são dois erros que não dão sintoma na hora:</para>
///
/// <para><b>1. "Sem configuração" não pode significar "desligado".</b> A varredura não depende da
/// linha de <c>sisreg_configuracao</c> para funcionar (a credencial do scraping mora em
/// <c>integracao_credencial</c>). Se a ausência da linha fosse lida como off, uma instância que
/// nunca abriu a tela da API-SISREG pararia de importar sem que nada aparecesse na tela.</para>
///
/// <para><b>2. Desligar tem que sobreviver ao INSERT.</b> Num <c>bool</c>, o sentinela do EF é
/// <c>false</c>: se a coluna tivesse default de banco <c>true</c>, gravar <c>false</c> numa linha
/// recém-criada guardaria <c>true</c> — o operador clicaria em "Desligar", a tela confirmaria, e o
/// sincronismo continuaria rodando. É por isso que a coluna NÃO tem default e a migration preenche
/// a linha existente na mão.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SincronismoAutomaticoSisregTests(PostgresFixture fixture)
{
    /// <summary>
    /// A configuração é singleton: cada teste começa da estaca zero para que a ordem de execução
    /// não decida o resultado.
    /// </summary>
    private static async Task LimparAsync(SmsMaisDbContext db) =>
        await db.SisregConfiguracoes.ExecuteDeleteAsync();

    private static SisregConfiguracao Configuracao(bool sincronismoAtivo) => new()
    {
        Id = Guid.CreateVersion7(),
        Uf = "RJ",
        Municipio = "3302700",
        CentraisReguladoras = string.Empty,
        Ativo = true,
        SincronismoAutomaticoAtivo = sincronismoAtivo,
        CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Sem_linha_de_configuracao_o_sincronismo_continua_ligado()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);

        Assert.True(await SincronismoAutomaticoSisreg.LigadoAsync(db, CancellationToken.None));
    }

    [Fact]
    public async Task Ligado_na_configuracao_libera_o_disparo()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        db.SisregConfiguracoes.Add(Configuracao(sincronismoAtivo: true));
        await db.SaveChangesAsync();

        Assert.True(await SincronismoAutomaticoSisreg.LigadoAsync(db, CancellationToken.None));
    }

    [Fact]
    public async Task Desligado_na_configuracao_bloqueia_o_disparo()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        db.SisregConfiguracoes.Add(Configuracao(sincronismoAtivo: false));
        await db.SaveChangesAsync();

        Assert.False(await SincronismoAutomaticoSisreg.LigadoAsync(db, CancellationToken.None));
    }

    /// <summary>
    /// O caso do sentinela: <c>false</c> gravado num INSERT tem de chegar ao banco como <c>false</c>.
    /// Reler numa conexão nova (não do change tracker) é o ponto do teste — em memória o valor
    /// estaria certo mesmo com a coluna errada.
    /// </summary>
    [Fact]
    public async Task Desligar_sobrevive_ao_insert_de_uma_configuracao_nova()
    {
        await using (var escrita = fixture.CriarDbContext())
        {
            await LimparAsync(escrita);
            escrita.SisregConfiguracoes.Add(Configuracao(sincronismoAtivo: false));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        Assert.False(await SincronismoAutomaticoSisreg.LigadoAsync(leitura, CancellationToken.None));
    }

    /// <summary>
    /// Religar é o caminho de volta e precisa valer tanto quanto o de ida: um interruptor que
    /// desliga e não religa é pior do que não existir.
    /// </summary>
    [Fact]
    public async Task Religar_volta_a_liberar_o_disparo()
    {
        await using (var escrita = fixture.CriarDbContext())
        {
            await LimparAsync(escrita);
            escrita.SisregConfiguracoes.Add(Configuracao(sincronismoAtivo: false));
            await escrita.SaveChangesAsync();

            var config = await escrita.SisregConfiguracoes.SingleAsync();
            config.SincronismoAutomaticoAtivo = true;
            await escrita.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        Assert.True(await SincronismoAutomaticoSisreg.LigadoAsync(leitura, CancellationToken.None));
    }
}
