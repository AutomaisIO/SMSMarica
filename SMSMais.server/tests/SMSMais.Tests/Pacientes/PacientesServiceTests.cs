using Hl7.Fhir.Model;
using NSubstitute;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;

// `Hl7.Fhir.Model.Task` (o recurso FHIR) colide com o Task do runtime neste arquivo.
using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// Busca de paciente por CNS contra o hub.
///
/// <para>O hub lê <c>identifier</c> <b>sem <c>|</c></b> como CPF (<c>SepararIdentifier</c>), então
/// mandar o CNS pelado virava <c>WHERE cpf = '&lt;15 dígitos&gt;'</c> — que não casa nunca. O efeito
/// era um "não existe" silencioso para paciente que existe, e como
/// <see cref="PacientesService.ObterPorCnsAsync"/> é a ÚNICA verificação de "já temos este
/// paciente" do importador do SISREG, toda linha ia ao CADSUS. Medido no CDT em 08/09/2026:
/// 5.786 CNS mandados ao SER numa varredura só, com a triagem "ligada".</para>
///
/// <para>Nada cobria isto — por isso passou. O teste que importa é o segundo: ele replica a régua
/// do hub e falha com o código antigo.</para>
/// </summary>
public class PacientesServiceTests
{
    private const string Cns = "700123456789012";
    private const string Nome = "MARIA DE TESTE";
    private static readonly string Qualificado = $"{PatientMergeFhir.SystemCns}|{Cns}";

    private static PacientesService Criar(IPacienteFhirClient hub) =>
        new(hub,
            Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            Substitute.For<SMSMais.Core.Geo.IGeocodificadorService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PacientesService>.Instance);

    private static Bundle Vazio() => new() { Type = Bundle.BundleType.Searchset, Entry = [] };

    private static Bundle ComPaciente() => new()
    {
        Type = Bundle.BundleType.Searchset,
        Entry =
        [
            new Bundle.EntryComponent
            {
                Resource = new Patient
                {
                    Id = Guid.CreateVersion7().ToString(),
                    Active = true,
                    Name = [new HumanName { Use = HumanName.NameUse.Official, Text = Nome }],
                    Identifier = [new Identifier(PatientMergeFhir.SystemCns, Cns)],
                },
            },
        ],
    };

    [Fact]
    public async Task Pergunta_ao_hub_com_o_system_do_CNS_nunca_com_o_numero_pelado()
    {
        var hub = Substitute.For<IPacienteFhirClient>();
        hub.BuscarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Vazio());

        await Criar(hub).ObterPorCnsAsync(Cns);

        await hub.Received(1).BuscarAsync(
            Qualificado, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await hub.DidNotReceive().BuscarAsync(
            Cns, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acha_o_paciente_que_o_hub_so_devolve_na_forma_qualificada()
    {
        // Réplica da régua do hub: só a forma `system|valor` chega ao ramo que procura em
        // `cns_todos`; o CNS pelado cai no filtro de CPF e volta vazio.
        var hub = Substitute.For<IPacienteFhirClient>();
        hub.BuscarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string?>(0) == Qualificado ? ComPaciente() : Vazio());

        var achado = await Criar(hub).ObterPorCnsAsync(Cns);

        Assert.NotNull(achado);
        Assert.Equal(Nome, achado!.NomeCompleto);
    }

    [Fact]
    public async Task CNS_invalido_nao_chega_a_perguntar_ao_hub()
    {
        var hub = Substitute.For<IPacienteFhirClient>();

        Assert.Null(await Criar(hub).ObterPorCnsAsync("123"));

        await hub.DidNotReceiveWithAnyArgs().BuscarAsync(default, default, default, default);
    }
}
