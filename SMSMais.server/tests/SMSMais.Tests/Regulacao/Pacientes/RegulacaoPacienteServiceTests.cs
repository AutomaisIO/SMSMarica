using NSubstitute;
using NSubstitute.ExceptionExtensions;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Cadastro;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Regulacao.Pacientes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Regulacao.Pacientes;

/// <summary>
/// Resolução do paciente no wizard de solicitação (plano 10).
///
/// <para>Tudo aqui é dublê: o que está sob teste é a <b>ordem das decisões</b>, não a persistência.
/// E a ordem é o que evita o defeito mais caro deste módulo — criar um segundo cadastro para
/// alguém que já existe, o que depois não se desfaz.</para>
/// </summary>
public class RegulacaoPacienteServiceTests
{
    private static readonly Guid IdExistente = Guid.NewGuid();

    /// <summary>CPF com DV válido, para o caminho feliz.</summary>
    private const string CpfValido = "52998224725";
    private const string CnsValido = "700000000000000";

    private static (RegulacaoPacienteService Servico, IPacientesService Pacientes, ICadastroPacienteService Cadastro) Montar()
    {
        var pacientes = Substitute.For<IPacientesService>();
        var cadastro = Substitute.For<ICadastroPacienteService>();
        cadastro.FonteAtualAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FonteCadastroPaciente.Ser));
        return (new RegulacaoPacienteService(pacientes, cadastro), pacientes, cadastro);
    }

    /// <summary>
    /// `PacienteDto` é um record posicional com 37 campos obrigatórios (é o retrato completo do
    /// paciente FHIR). Aqui só importam cinco — os outros vão no default e a fábrica existe para
    /// o teste não virar uma parede de argumentos.
    /// </summary>
    private static PacienteDto Dto(Guid id, string nome, string? cpf, string? cns) =>
        new(
            Id: id,
            NomeCompleto: nome,
            Cpf: cpf ?? string.Empty,
            Cns: cns,
            Latitude: 0,
            Longitude: 0,
            Ativo: true,
            CadastradoEm: DateTime.UtcNow,
            Rg: null,
            DataNascimento: new DateOnly(1980, 1, 1),
            Sexo: Sexo.NaoInformado,
            EstadoCivil: EstadoCivil.NaoInformado,
            RacaCor: RacaCor.NaoInformado,
            Escolaridade: Escolaridade.NaoInformado,
            Ocupacao: null,
            Naturalidade: null,
            Nacionalidade: "Brasileira",
            NomeDaMae: null,
            NomeDoPai: null,
            ResponsavelLegal: null,
            Endereco: null,
            TelefonePrincipal: null,
            TelefoneCelular: null,
            TelefoneResidencial: null,
            Email: null,
            ContatoEmergencia: null,
            AlturaCm: null,
            PesoKg: null,
            TipoSanguineo: TipoSanguineo.NaoInformado,
            FatorRh: FatorRh.NaoInformado,
            Alergias: [],
            MedicamentosContinuos: [],
            Comorbidades: [],
            Deficiencias: [],
            PlanoSaude: null,
            Observacoes: null,
            FotoBase64: null);

    [Fact]
    public async Task Existente_por_cns_reusa_e_nao_cria_nem_altera_nome()
    {
        var (servico, pacientes, _) = Montar();
        pacientes.ObterPorCnsAsync(CnsValido, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(IdExistente, "NOME LOCAL", CpfValido, true));
        pacientes.ObterPorIdAsync(IdExistente, Arg.Any<CancellationToken>())
            .Returns(Dto(IdExistente, "NOME LOCAL", CpfValido, CnsValido));

        var r = await servico.ConfirmarCadsusAsync(
            new PacienteCadsusDto(CpfValido, CnsValido, "NOME DIFERENTE NO CADSUS",
                new DateOnly(1980, 1, 1), "Masculino", "MAE", "Ser"),
            CancellationToken.None);

        r.Id.Should().Be(IdExistente);
        r.Nome.Should().Be("NOME LOCAL", "o dado local pode ter sido corrigido por quem atendeu a pessoa");
        await pacientes.DidNotReceive().CadastrarAsync(
            Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existente_por_cpf_com_cns_diferente_reusa()
    {
        var (servico, pacientes, _) = Montar();
        pacientes.ObterPorCnsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        pacientes.ObterPorCpfAsync(CpfValido, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(IdExistente, "NOME LOCAL", CpfValido, true));
        pacientes.ObterPorIdAsync(IdExistente, Arg.Any<CancellationToken>())
            .Returns(Dto(IdExistente, "NOME LOCAL", CpfValido, "700000000000999"));

        var r = await servico.ConfirmarCadsusAsync(
            new PacienteCadsusDto(CpfValido, CnsValido, "OUTRO NOME", new DateOnly(1980, 1, 1),
                null, null, "Ser"),
            CancellationToken.None);

        r.Id.Should().Be(IdExistente);
        await pacientes.DidNotReceive().CadastrarAsync(
            Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Miss_total_cria_o_cadastro()
    {
        var (servico, pacientes, _) = Montar();
        var novoId = Guid.NewGuid();
        pacientes.ObterPorCnsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        pacientes.ObterPorCpfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        pacientes.CadastrarAsync(Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>())
            .Returns(novoId);
        pacientes.ObterPorIdAsync(novoId, Arg.Any<CancellationToken>())
            .Returns(Dto(novoId, "NOVO", CpfValido, CnsValido));

        var r = await servico.ConfirmarCadsusAsync(
            new PacienteCadsusDto(CpfValido, CnsValido, "NOVO", new DateOnly(1990, 5, 3),
                "Feminino", "MAE", "Ser"),
            CancellationToken.None);

        r.Id.Should().Be(novoId);
        await pacientes.Received(1).CadastrarAsync(
            Arg.Is<CadastrarPacienteRequest>(x => x.Cpf == CpfValido && x.Cns == CnsValido),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cpf_sem_dv_valido_nao_ancora_o_cadastro()
    {
        var (servico, pacientes, _) = Montar();
        var novoId = Guid.NewGuid();
        pacientes.ObterPorCnsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);
        pacientes.CadastrarAsync(Arg.Any<CadastrarPacienteRequest>(), Arg.Any<CancellationToken>())
            .Returns(novoId);
        pacientes.ObterPorIdAsync(novoId, Arg.Any<CancellationToken>())
            .Returns(Dto(novoId, "NOVO", null, CnsValido));

        await servico.ConfirmarCadsusAsync(
            new PacienteCadsusDto("11111111111", CnsValido, "NOVO", new DateOnly(1990, 5, 3),
                null, null, "Ser"),
            CancellationToken.None);

        // ADR-0041: entra ancorado no CNS e marcado; gravar CPF que não fecha o DV cria
        // duplicata que depois não se desfaz.
        await pacientes.Received(1).CadastrarAsync(
            Arg.Is<CadastrarPacienteRequest>(x => x.Cpf == null && x.Cns == CnsValido),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cadsus_nao_encontrado_continua_nao_encontrado()
    {
        var (servico, _, cadastro) = Montar();
        cadastro.ConsultarPorCpfAsync(CpfValido, Arg.Any<CancellationToken>())
            .Throws(new NaoEncontradoException("Paciente no CADSUS", CpfValido));

        var acao = () => servico.ConsultarCadsusAsync(CpfValido, CancellationToken.None);

        await acao.Should().ThrowAsync<NaoEncontradoException>(
            "não achar é resposta, não falha — a tela oferece cadastrar à mão");
    }

    [Fact]
    public async Task Cadsus_indisponivel_cita_a_porta()
    {
        var (servico, _, cadastro) = Montar();
        cadastro.ConsultarPorCpfAsync(CpfValido, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("connection reset"));

        var acao = () => servico.ConsultarCadsusAsync(CpfValido, CancellationToken.None);

        // "O SISREG caiu" e "o SER caiu" levam o operador a ações diferentes.
        (await acao.Should().ThrowAsync<ValidacaoException>())
            .And.Message.Should().Contain("Ser");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123456789")]
    public async Task Documento_com_tamanho_errado_e_recusado(string documento)
    {
        var (servico, _, _) = Montar();

        var acao = () => servico.ConsultarCadsusAsync(documento, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Cpf_de_outro_cadastro_conflita_e_diz_qual()
    {
        var (servico, pacientes, _) = Montar();
        var outro = Guid.NewGuid();
        pacientes.ObterPorCpfAsync(CpfValido, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(outro, "JOAO DA SILVA", CpfValido, true));

        var acao = () => servico.InformarCpfAsync(IdExistente, CpfValido, CancellationToken.None);

        var e = await acao.Should().ThrowAsync<ConflitoException>();
        e.And.Codigo.Should().Be("paciente.cpf_de_outro_cadastro");
        // O id vai na mensagem para a tela oferecer "usar o cadastro existente" — sem saída, o
        // operador criaria um terceiro registro.
        e.And.Message.Should().Contain(outro.ToString());
        await pacientes.DidNotReceive().DefinirCpfAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Busca_por_cns_de_15_digitos_usa_o_atalho()
    {
        var (servico, pacientes, _) = Montar();
        pacientes.ObterPorCnsAsync(CnsValido, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(IdExistente, "ACHADO", CpfValido, true));
        pacientes.ObterPorIdAsync(IdExistente, Arg.Any<CancellationToken>())
            .Returns(Dto(IdExistente, "ACHADO", CpfValido, CnsValido));

        var r = await servico.BuscarLocalAsync(CnsValido, CancellationToken.None);

        r.Should().ContainSingle().Which.Id.Should().Be(IdExistente);
        await pacientes.DidNotReceive().BuscarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Termo_curto_nao_vai_ao_banco()
    {
        var (servico, pacientes, _) = Montar();

        (await servico.BuscarLocalAsync("ab", CancellationToken.None)).Should().BeEmpty();

        await pacientes.DidNotReceive().BuscarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
