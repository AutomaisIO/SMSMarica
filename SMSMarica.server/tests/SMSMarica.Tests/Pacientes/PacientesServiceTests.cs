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

    private static CadastrarPacienteRequest NovoRequest(
        string nome = "Maria da Silva",
        string cpf = "12345678900",
        string? cns = "123456789012345") =>
        new(
            NomeCompleto: nome,
            Cpf: cpf,
            DataNascimento: new DateOnly(1990, 1, 1),
            Cns: cns,
            Rg: null);

    [Fact]
    public async Task Cadastrar_ComDadosValidos_RetornaIdEPersiste()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(NovoRequest());

        id.Should().NotBeEmpty();

        await using var verificacao = _postgres.CriarDbContext();
        var paciente = await verificacao.Pacientes.Include(p => p.Usuario).FirstAsync(p => p.Id == id);
        paciente.Usuario.NomeCompleto.Should().Be("Maria da Silva");
        paciente.Usuario.Cpf.Should().Be("12345678900");
        paciente.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Cadastrar_ComCpfDuplicado_LancaConflito()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        await service.CadastrarAsync(NovoRequest("Primeiro", "11122233344", null));

        var act = async () => await service.CadastrarAsync(NovoRequest("Segundo", "11122233344", null));

        var ex = await act.Should().ThrowAsync<ConflitoException>();
        ex.Which.Codigo.Should().Be("paciente.cpf_duplicado");
    }

    [Fact]
    public async Task Cadastrar_ComCpfDeDesativado_LancaConflitoParaReativacao()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(NovoRequest("Inativo", "44455566677", null));
        await service.DesativarAsync(id);

        var act = async () => await service.CadastrarAsync(NovoRequest("Tentando recriar", "44455566677", null));
        var ex = await act.Should().ThrowAsync<ConflitoException>();
        ex.Which.Codigo.Should().Be("paciente.cpf_desativado");
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

        var id = await service.CadastrarAsync(NovoRequest("Para desativar", "55566677788", null));

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

        var id = await service.CadastrarAsync(NovoRequest("Já inativo", "99988877766", null));
        await service.DesativarAsync(id);

        var act = async () => await service.DesativarAsync(id);
        var ex = await act.Should().ThrowAsync<ConflitoException>();
        ex.Which.Codigo.Should().Be("paciente.ja_inativo");
    }

    [Fact]
    public async Task Reativar_DesativadoVoltaAtivo()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(NovoRequest("Para reativar", "33344455566", null));
        await service.DesativarAsync(id);

        await service.ReativarAsync(id);

        await using var verificacao = _postgres.CriarDbContext();
        var paciente = await verificacao.Pacientes.FirstAsync(p => p.Id == id);
        paciente.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Buscar_SemTermo_RetornaVazio()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        await service.CadastrarAsync(NovoRequest("Bernardo dos Santos Leite", "00011122233", null));

        var resultado = await service.BuscarAsync(null);
        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Buscar_PorTokensSeparados_RetornaPaciente()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(NovoRequest("Bernardo dos Santos Leite Almeida", "10011122244", null));

        var porParteFinal = await service.BuscarAsync("almeida santos");
        porParteFinal.Should().ContainSingle(p => p.Id == id);

        var porSubstrings = await service.BuscarAsync("ardo eite");
        porSubstrings.Should().ContainSingle(p => p.Id == id);
    }

    [Fact]
    public async Task Buscar_PorCpfFormatado_NormalizaEEncontra()
    {
        await using var db = _postgres.CriarDbContext();
        var service = new PacientesService(db);

        var id = await service.CadastrarAsync(NovoRequest("João da Costa", "20011122255", null));

        var resultado = await service.BuscarAsync("200.111.222-55");
        resultado.Should().ContainSingle(p => p.Id == id);
    }
}
