using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Acompanhantes;
using SMSMais.Core.Acompanhantes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Dtos;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;
using static SMSMais.Tests.Tratamentos.TransporteFabrica;

namespace SMSMais.Tests.Tratamentos;

/// <summary>
/// Acompanhante entra na lista do paciente pelo par CPF + nascimento, e o nome nunca é digitado:
/// vem da base (se o CPF já é de paciente e o nascimento bate) ou do proxy de CPF. Ninguém sai da
/// lista enquanto estiver escolhido para viagem futura.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AcompanhantesServiceTests(PostgresFixture fixture)
{
    private const string CpfMae = "52998224725";
    private static readonly DateOnly NascimentoMae = new(1960, 5, 20);

    private sealed record Dubles(
        IPacientesService Pacientes, IPacienteResolver Resolver, IConsultaCpfService ConsultaCpf, IMemoryCache Cache);

    private static Dubles NovosDubles() => new(
        Substitute.For<IPacientesService>(),
        Substitute.For<IPacienteResolver>(),
        Substitute.For<IConsultaCpfService>(),
        new MemoryCache(new MemoryCacheOptions()));

    private static AcompanhantesService Servico(SmsMaisDbContext db, Dubles d, Guid? usuarioId = null) =>
        new(db, d.Pacientes, d.Resolver, d.ConsultaCpf, d.Cache, new UsuarioAtualAccessorFake(usuarioId),
            NullLogger<AcompanhantesService>.Instance);

    private static void ProxyConfirma(Dubles d, string cpf, DateOnly nascimento, string nome) =>
        d.ConsultaCpf.ConsultarCpfAsync(cpf, nascimento, Arg.Any<CancellationToken>())
            .Returns(new HubCpfRespostaDto(cpf, nome, nascimento.ToString("dd/MM/yyyy"), "REGULAR"));

    [Fact]
    public async Task Cpf_com_digito_errado_e_recusado_sem_consultar_ninguem()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db, d).ConsultarAsync(
            Guid.NewGuid(), new ConsultarAcompanhanteRequest("52998224726", NascimentoMae)));

        await d.ConsultaCpf.DidNotReceiveWithAnyArgs().ConsultarCpfAsync(default!, default, default);
    }

    [Fact]
    public async Task O_proprio_paciente_nao_e_acompanhante_dele()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        var pacienteId = Guid.NewGuid();
        d.Resolver.ResolverAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(new PacienteResumo(pacienteId, "Paciente", "529.982.247-25", null, null, Sexo.Feminino));

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db, d).AdicionarAsync(
            pacienteId, new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, null, null), OrigemCadastroAcompanhante.Painel));
    }

    [Fact]
    public async Task Cpf_ja_na_base_com_o_nascimento_certo_usa_o_nome_de_la_e_nao_paga_consulta()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        var maeNaBase = Guid.NewGuid();
        d.Pacientes.ObterPorCpfAsync(CpfMae, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(maeNaBase, "MARIA DA BASE", CpfMae, true));
        d.Resolver.ResolverAsync(maeNaBase, Arg.Any<CancellationToken>())
            .Returns(new PacienteResumo(maeNaBase, "MARIA DA BASE", CpfMae, null, NascimentoMae, Sexo.Feminino));

        var pacienteId = Guid.NewGuid();
        var salvo = await Servico(db, d, Guid.NewGuid()).AdicionarAsync(
            pacienteId, new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, ParentescoAcompanhante.Mae, "(21) 99999-0000"),
            OrigemCadastroAcompanhante.Painel);

        Assert.Equal("MARIA DA BASE", salvo.Nome);
        Assert.True(salvo.TambemEPaciente);
        await d.ConsultaCpf.DidNotReceiveWithAnyArgs().ConsultarCpfAsync(default!, default, default);
        var linha = await db.Acompanhantes.AsNoTracking().SingleAsync(a => a.Id == salvo.Id);
        Assert.Equal(FonteNomeAcompanhante.Base, linha.FonteNome);
        Assert.Equal("21999990000", linha.Telefone);
    }

    [Fact]
    public async Task Par_que_nao_confere_no_proxy_e_recusado_sem_dizer_qual_dado_esta_errado()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        d.ConsultaCpf.ConsultarCpfAsync(CpfMae, NascimentoMae, Arg.Any<CancellationToken>())
            .Throws(new ValidacaoException("proxy.cpf.nao_encontrado", "Nenhum motor confirmou."));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db, d).ConsultarAsync(
            Guid.NewGuid(), new ConsultarAcompanhanteRequest(CpfMae, NascimentoMae)));
        Assert.True(ex.Erros.ContainsKey("acompanhante.nao_confere"));
    }

    [Fact]
    public async Task Consultar_e_depois_adicionar_paga_uma_consulta_so_e_nao_repete_a_pessoa()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        ProxyConfirma(d, CpfMae, NascimentoMae, "MARIA DA RECEITA");
        var pacienteId = Guid.NewGuid();

        var consulta = await Servico(db, d).ConsultarAsync(pacienteId, new ConsultarAcompanhanteRequest(CpfMae, NascimentoMae));
        Assert.Equal("MARIA DA RECEITA", consulta.Nome);
        Assert.False(consulta.JaCadastrado);

        var salvo = await Servico(db, d).AdicionarAsync(pacienteId,
            new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, null, null), OrigemCadastroAcompanhante.App);
        Assert.Equal(OrigemCadastroAcompanhante.App, salvo.Origem);
        await d.ConsultaCpf.ReceivedWithAnyArgs(1).ConsultarCpfAsync(default!, default, default);

        await Assert.ThrowsAsync<ConflitoException>(() => Servico(db, d).AdicionarAsync(pacienteId,
            new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, null, null), OrigemCadastroAcompanhante.Painel));
    }

    [Fact]
    public async Task Pelo_app_so_tira_quem_cadastrou_pelo_app()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        ProxyConfirma(d, CpfMae, NascimentoMae, "MARIA");
        var pacienteId = Guid.NewGuid();
        var daEquipe = await Servico(db, d).AdicionarAsync(pacienteId,
            new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, null, null), OrigemCadastroAcompanhante.Painel);

        await Assert.ThrowsAsync<ConflitoException>(() =>
            Servico(db, d).RemoverAsync(pacienteId, daEquipe.Id, OrigemCadastroAcompanhante.App));

        await Servico(db, d).RemoverAsync(pacienteId, daEquipe.Id, OrigemCadastroAcompanhante.Painel);
        Assert.Empty(await Servico(db, d).ListarDoPacienteAsync(pacienteId));
    }

    [Fact]
    public async Task Quem_esta_escolhido_para_viagem_futura_nao_sai_da_lista()
    {
        await using var db = fixture.CriarDbContext();
        var d = NovosDubles();
        ProxyConfirma(d, CpfMae, NascimentoMae, "MARIA");
        var pacienteId = Guid.NewGuid();
        var atendimento = await TransporteFabrica.Servico(db).CadastrarAsync(
            Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db), pacienteId: pacienteId));
        var sessaoId = (await db.Sessoes.AsNoTracking().FirstAsync(s => s.TratamentoId == atendimento)).Id;
        var mae = await Servico(db, d).AdicionarAsync(pacienteId,
            new AdicionarAcompanhanteRequest(CpfMae, NascimentoMae, null, null), OrigemCadastroAcompanhante.Painel);
        await TransporteFabrica.Servico(db).DefinirAcompanhantesDaSessaoAsync(atendimento, sessaoId,
            new DefinirAcompanhantesSessaoRequest([mae.Id]));

        await Assert.ThrowsAsync<ConflitoException>(() =>
            Servico(db, d).RemoverAsync(pacienteId, mae.Id, OrigemCadastroAcompanhante.Painel));
    }
}
