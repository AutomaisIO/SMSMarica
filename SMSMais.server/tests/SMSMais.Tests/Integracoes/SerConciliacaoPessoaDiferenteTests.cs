using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Integracoes.Pep.Fhir;
using SMSMais.Core.Ser.Pacientes;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// A conciliação do SER passa pelo upsert canônico em modo fonte parcial — e é lá que mora a recusa
/// de fundir duas pessoas. Hub simulado: o que se prova aqui é a DECISÃO (escreve ou não, liga o
/// pedido ou não, deixa rastro ou não), não o SQL.
/// </summary>
public class SerConciliacaoPessoaDiferenteTests
{
    private const string Cpf = "52998224725";
    private static readonly Guid FichaId = Guid.NewGuid();

    private readonly IHubFhirEscritor _hub = Substitute.For<IHubFhirEscritor>();
    private readonly IAuditoriaService _auditoria = Substitute.For<IAuditoriaService>();

    private SerConciliacaoPacienteService Servico() =>
        new(_hub, _auditoria, NullLogger<SerConciliacaoPacienteService>.Instance);

    /// <summary>A ficha que a chave acha no hub.</summary>
    private void NoHub(string nome, string nascimento)
    {
        var ficha = new Patient
        {
            Id = FichaId.ToString(),
            Meta = new Meta { VersionId = "3" },
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }],
            BirthDate = nascimento,
            Identifier = [new Identifier(UpsertCanonicoPep.SysCpf, Cpf)],
        };
        _hub.BuscarPorIdentifierAsync("Patient", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Bundle { Entry = [new Bundle.EntryComponent { Resource = ficha }] });
        _hub.AtualizarAsync("Patient", Arg.Any<string>(), Arg.Any<Resource>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Resource>(2));
    }

    private static SerSolicitacao PedidoDaMae(DateOnly nascimento) => new()
    {
        Id = Guid.NewGuid(),
        IdSer = "9000001",
        Tipo = TipoRecursoSer.Consulta,
        Situacao = SituacaoSer.EmFila,
        Cpf = Cpf,
        Cns = "700000000000001",
        PacienteNome = "JULIANA COSTA MOURA",
        DataNascimento = nascimento,
        NomeMae = "MARLENE DA COSTA",
        CriadoEm = DateTime.UtcNow,
        SincronizadoEm = DateTime.UtcNow,
    };

    [Fact]
    // Caso de 10/08/2026: o pedido da mãe achou pelo CPF a ficha da filha e gravou nela o CNS da mãe.
    public async System.Threading.Tasks.Task Pedido_de_outra_pessoa_nao_escreve_nem_liga_e_deixa_rastro_na_ficha()
    {
        NoHub("ALICE MOURA ROCHA", "2017-06-12");

        var r = await Servico().ConciliarAsync(PedidoDaMae(new DateOnly(1994, 11, 23)), CancellationToken.None);

        r.Resultado.Should().Be(ResultadoConciliacaoSer.PessoaDiferente);
        r.PacienteId.Should().BeNull("ligar o pedido à ficha é a mistura que a recusa impede");
        await _hub.DidNotReceive().AtualizarAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Resource>(), Arg.Any<CancellationToken>());
        await _hub.DidNotReceive().CriarAsync(Arg.Any<Resource>(), Arg.Any<CancellationToken>());
        await _auditoria.Received(1).RegistrarAsync(
            Arg.Is("Paciente"), Arg.Is(FichaId.ToString()),
            Arg.Is<string>(a => a.Contains("recusada")),
            Arg.Is<string?>(x => x == null),
            Arg.Is<string>(v => v.Contains("9000001") && v.Contains("ALICE") && v.Contains("JULIANA")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    // Mesma pessoa com o nascimento digitado errado num lado: segue completando, como antes.
    public async System.Threading.Tasks.Task Mesma_pessoa_com_nascimento_divergente_continua_completando()
    {
        NoHub("JULIANA COSTA MOURA", "1994-11-03");

        var r = await Servico().ConciliarAsync(PedidoDaMae(new DateOnly(1994, 11, 23)), CancellationToken.None);

        r.Resultado.Should().Be(ResultadoConciliacaoSer.Enriquecido);
        r.PacienteId.Should().Be(FichaId);
        await _hub.Received(1).AtualizarAsync(
            "Patient", FichaId.ToString(), Arg.Any<Resource>(), Arg.Any<CancellationToken>());
    }
}
