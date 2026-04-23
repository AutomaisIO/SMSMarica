using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Pacientes;

[Collection(nameof(PostgresCollection))]
public sealed class PacientesServiceTests(PostgresFixture postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Cadastrar_ComDadosValidos_RetornaIdEPersiste()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(new CadastrarPacienteRequest(
            NomeCompleto: "Maria da Silva",
            Cpf: "12345678900",
            Cns: "123456789012345",
            Latitude: -22.9192,
            Longitude: -42.8186));

        id.Should().NotBeEmpty();

        await using var verificacao = _postgres.CriarDbContext();
        var paciente = await verificacao.Pacientes.FirstAsync(p => p.Id == id);
        paciente.NomeCompleto.Should().Be("Maria da Silva");
        paciente.Cpf.Should().Be("12345678900");
        paciente.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Cadastrar_ComCpfDuplicado_LancaConflito()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        await service.CadastrarAsync(new CadastrarPacienteRequest(
            "Primeiro", "11122233344", null, -22.9, -42.8));

        var act = async () => await service.CadastrarAsync(new CadastrarPacienteRequest(
            "Segundo", "11122233344", null, -22.9, -42.8));

        var ex = await act.Should().ThrowAsync<ConflitoException>();
        ex.Which.Codigo.Should().Be("paciente.cpf_duplicado");
    }

    [Fact]
    public async Task ObterPorId_NaoExiste_LancaNaoEncontrado()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var act = async () => await service.ObterPorIdAsync(Guid.CreateVersion7());

        await act.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task Desativar_PacienteAtivo_MarcaInativo()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(new CadastrarPacienteRequest(
            "Para desativar", "55566677788", null, -22.9, -42.8));

        await service.DesativarAsync(id);

        await using var verificacao = _postgres.CriarDbContext();
        var paciente = await verificacao.Pacientes.FirstAsync(p => p.Id == id);
        paciente.Ativo.Should().BeFalse();
        paciente.AtualizadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Desativar_PacienteJaInativo_LancaConflito()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(new CadastrarPacienteRequest(
            "Já inativo", "99988877766", null, -22.9, -42.8));
        await service.DesativarAsync(id);

        var act = async () => await service.DesativarAsync(id);
        var ex = await act.Should().ThrowAsync<ConflitoException>();
        ex.Which.Codigo.Should().Be("paciente.ja_inativo");
    }
}
