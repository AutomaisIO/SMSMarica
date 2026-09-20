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

    /// <summary>
    /// A regra continua sendo "escrita fica presa ao assunto" — com UMA exceção nomeada.
    ///
    /// <para><b>IniciarCancelamento</b> entrou no Base em 20/09/2026 porque o que ele escreve é
    /// INTENÇÃO ("esta pessoa disse que não vem"), não cancelamento: a vaga só cai quando um
    /// humano trata na aba Cancelamento. Estando preso a um único assunto, ele faltava justamente
    /// quando a conversa chegava por outro caminho — e aí o robô prometia "vou registrar" sem
    /// registrar nada. Varredura de 01→20/09: 74 vagas futuras presas assim.</para>
    ///
    /// <para>A lista de exceções é explícita de propósito. Se alguém quiser acrescentar outra
    /// escrita ao Base, este teste quebra e obriga a justificar aqui — que é o ponto.</para>
    /// </summary>
    [Fact]
    public void Conjunto_base_so_tem_escrita_explicitamente_excecionada()
    {
        // Escrita que pode ser sempre-disponível, uma a uma, com motivo escrito acima.
        var excecoes = new HashSet<ComandoRobo> { ComandoRobo.IniciarCancelamento };

        var escrita = ComandoRoboCatalogo.Itens.Where(i => i.Escrita).Select(i => i.Comando).ToHashSet();
        var indevidas = ComandoRoboCatalogo.Base.Where(c => escrita.Contains(c) && !excecoes.Contains(c)).ToList();

        Assert.True(indevidas.Count == 0,
            $"Comando de ESCRITA no conjunto Base sem exceção declarada: {string.Join(", ", indevidas)}. "
            + "Escrita sempre-disponível precisa de motivo escrito no teste e no catálogo.");
        Assert.Contains(ComandoRobo.ConsultarCadastro, ComandoRoboCatalogo.Base);
    }

    /// <summary>
    /// O robô precisa conseguir registrar "não vou" mesmo sem assunto identificado — é o caso que
    /// deixou 74 vagas presas. Guarda o comportamento, não só a configuração.
    /// </summary>
    [Fact]
    public async Task Pedido_de_cancelamento_e_registrado_mesmo_SEM_assunto_identificado()
    {
        await using var db = fixture.CriarDbContext();
        var conversaId = await SemearConversaAsync(db);
        var dispatcher = Criar(db, ComandoRobo.IniciarCancelamento);

        var r = await dispatcher.ExecutarAsync(
            conversaId, null, assuntoId: null, ComandoRobo.IniciarCancelamento, default, default);

        Assert.True(r.Sucesso);
    }
}
