using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using SMSMais.Core.RoboAtendimento;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Data;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// Habilitação de comandos no dispatcher. O ponto sob teste é a exceção do CONJUNTO BASE: 22% das
/// mensagens não casam com assunto nenhum, e sem essa exceção o robô ficava sem ferramenta
/// justamente aí — pedia CPF e data de nascimento, não conseguia consultar nada e afirmava ao
/// cidadão que a identidade não conferia. A regra geral (habilitação por assunto) tem de continuar
/// valendo para todo o resto.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RoboComandoDispatcherBaseTests(PostgresFixture fixture)
{
    private sealed class ComandoFalso(ComandoRobo comando) : IRoboComando
    {
        public ComandoRobo Comando => comando;
        public bool Idempotente => false;
        public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:{comando}";
        public Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct) =>
            Task.FromResult(new RoboComandoResultado(true, "executou"));
    }

    private static async Task<Guid> SemearConversaAsync(SmsMaisDbContext db)
    {
        var c = new Conversa
        {
            Id = Guid.NewGuid(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = $"5521{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
            Status = StatusConversa.Aberta,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.Conversas.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static RoboComandoDispatcher Criar(SmsMaisDbContext db, params ComandoRobo[] comandos) =>
        new(db, [.. comandos.Select(c => new ComandoFalso(c))],
            NullLogger<RoboComandoDispatcher>.Instance);

    [Fact]
    public async Task Comando_base_executa_mesmo_SEM_assunto_identificado()
    {
        await using var db = fixture.CriarDbContext();
        var conversaId = await SemearConversaAsync(db);
        var dispatcher = Criar(db, ComandoRobo.ConsultarCadastro);

        var r = await dispatcher.ExecutarAsync(
            conversaId, null, assuntoId: null, ComandoRobo.ConsultarCadastro, default, default);

        Assert.True(r.Sucesso);
        Assert.Equal("executou", r.Mensagem);
    }

    [Fact]
    public async Task Comando_fora_do_base_continua_exigindo_assunto_que_o_habilite()
    {
        await using var db = fixture.CriarDbContext();
        var conversaId = await SemearConversaAsync(db);
        var dispatcher = Criar(db, ComandoRobo.ConfirmarPresenca);

        var r = await dispatcher.ExecutarAsync(
            conversaId, null, assuntoId: null, ComandoRobo.ConfirmarPresenca, default, default);

        Assert.False(r.Sucesso);
        Assert.Contains("não habilitado", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Conjunto_base_so_tem_comando_seguro()
    {
        // Nada que ALTERE dado pode ser sempre-disponível: escrita continua presa ao assunto.
        var escrita = ComandoRoboCatalogo.Itens.Where(i => i.Escrita).Select(i => i.Comando).ToHashSet();
        Assert.DoesNotContain(ComandoRoboCatalogo.Base, c => escrita.Contains(c));
        Assert.Contains(ComandoRobo.ConsultarCadastro, ComandoRoboCatalogo.Base);
    }
}
