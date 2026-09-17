using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Regulacao.Notificacoes;
using SMSMais.Core.Ser;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Ser;

/// <summary>
/// Limpeza automática das notificações de Alta: só Alta, só pendente, só depois de 5 dias. Precisa
/// de Postgres real porque é <c>ExecuteUpdate</c>, que não existe em memória.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerNotificacaoLimpezaAltaTests(PostgresFixture fixture)
{
    private static Guid Gatilho(SmsMaisDbContext db, SituacaoSer situacao, DateTime criadoEm, DateTime? processadoEm = null)
    {
        var idSer = "T" + Random.Shared.Next(100000, 999999);
        var s = new SerSolicitacao
        {
            Id = Guid.CreateVersion7(),
            IdSer = idSer,
            Tipo = TipoRecursoSer.Consulta,
            Recurso = "CARDIOLOGIA",
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = situacao,
            SincronizadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.SerSolicitacoes.Add(s);
        var g = new SerGatilho
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            IdSer = idSer,
            Tipo = TipoGatilhoSer.MudancaSituacao,
            ChaveEvento = situacao.ToString(),
            SituacaoAnterior = SituacaoSer.EmFila,
            SituacaoAtual = situacao,
            CriadoEm = criadoEm,
            ProcessadoEm = processadoEm,
            ProcessadoPor = processadoEm is null ? null : "PESSOA",
        };
        db.SerGatilhos.Add(g);
        return g.Id;
    }

    [Fact]
    public async Task Limpa_so_alta_pendente_com_mais_de_5_dias()
    {
        await using var db = fixture.CriarDbContext();
        var agora = DateTime.UtcNow;

        var altaVelha = Gatilho(db, SituacaoSer.Alta, agora.AddDays(-6));
        var altaNova = Gatilho(db, SituacaoSer.Alta, agora.AddDays(-4));
        var canceladaVelha = Gatilho(db, SituacaoSer.Cancelada, agora.AddDays(-30));
        var altaJaVista = Gatilho(db, SituacaoSer.Alta, agora.AddDays(-10), processadoEm: agora.AddDays(-9));
        await db.SaveChangesAsync();

        var limpas = await new SerNotificacaoService(db, new UsuarioAtualAccessorFake())
            .LimparAltasAntigasAsync(CancellationToken.None);
        Assert.True(limpas >= 1);

        await using var leitura = fixture.CriarDbContext();
        var linhas = await leitura.SerGatilhos.AsNoTracking()
            .Where(g => new[] { altaVelha, altaNova, canceladaVelha, altaJaVista }.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id);

        Assert.NotNull(linhas[altaVelha].ProcessadoEm);
        Assert.Equal(LimpezaAltaNotificacao.ProcessadoPor, linhas[altaVelha].ProcessadoPor);
        Assert.Null(linhas[altaNova].ProcessadoEm);
        Assert.Null(linhas[canceladaVelha].ProcessadoEm);
        // Marcação de pessoa não é sobrescrita.
        Assert.Equal("PESSOA", linhas[altaJaVista].ProcessadoPor);
    }
}
