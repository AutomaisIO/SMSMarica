using System.Text.Json;
using NSubstitute;
using SMSMais.Core.Pacientes;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// O robô verifica contato de quem AINDA NÃO tem número provado — e nunca troca um já provado
/// (ADR-0057). Trocar exigiria só os 4 dígitos do CPF, virando a porta dos fundos da regra que
/// manda ir ao posto: quem tivesse a guia de papel apontaria laudo e login para o próprio celular.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class VerificarCadastroComandoTests(PostgresFixture fixture)
{
    private const string Cpf = "04528822733";
    private readonly string _telefone = $"5521{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private static JsonElement Args(string cpf) =>
        JsonDocument.Parse($$"""{"cpf":"{{cpf}}"}""").RootElement;

    private (VerificarCadastroComando Comando, ITelefoneValidacaoService Telefones) Criar(
        SmsMaisDbContext db, Guid pacienteId, string? telefoneVerificado)
    {
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(new SMSMais.Core.Pacientes.Dtos.PacienteDto(
                pacienteId, "MARIA DA SILVA", Cpf, null, 0, 0, true, DateTime.UtcNow,
                null, new DateOnly(1980, 3, 15), default, default, default, default, null, null, "Brasileira",
                null, null, null, null,
                null, null, null, null, null,
                null, null, default, default, [], [], [], [], null,
                null, null,
                TelefoneVerificado: telefoneVerificado));
        var telefones = Substitute.For<ITelefoneValidacaoService>();
        return (new VerificarCadastroComando(db, pacientes, telefones), telefones);
    }

    [Fact]
    public async Task Sem_numero_verificado_o_robo_carimba()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (comando, telefones) = Criar(db, pacienteId, telefoneVerificado: null);

        var r = await comando.ExecutarAsync(
            new RoboComandoContexto(Guid.NewGuid(), pacienteId, null, _telefone, Args("0452")), default);

        Assert.True(r.Sucesso);
        await telefones.Received(1).MarcarValidadoAsync(
            Cpf, _telefone, "robo-cadastral", null, Arg.Any<CancellationToken>(),
            Arg.Any<SMSMais.Data.Entities.Enums.VinculoContatoVerificado>());
    }

    [Fact]
    public async Task Com_OUTRO_numero_verificado_nao_troca_e_manda_ao_posto()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (comando, telefones) = Criar(db, pacienteId, telefoneVerificado: "5521999991234");

        var r = await comando.ExecutarAsync(
            new RoboComandoContexto(Guid.NewGuid(), pacienteId, null, _telefone, Args("0452")), default);

        Assert.False(r.Sucesso);
        Assert.Contains("posto de saúde", r.Mensagem);
        await telefones.DidNotReceiveWithAnyArgs().MarcarValidadoAsync(
            default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task Com_o_MESMO_numero_verificado_segue_normal()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var (comando, _) = Criar(db, pacienteId, telefoneVerificado: _telefone);

        var r = await comando.ExecutarAsync(
            new RoboComandoContexto(Guid.NewGuid(), pacienteId, null, _telefone, Args("0452")), default);

        Assert.True(r.Sucesso);
    }
}
