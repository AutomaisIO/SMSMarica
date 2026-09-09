using System.Text.Json.Nodes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// O dcm4chee, na ausência de IssuerOfPatientID, fabrica um a partir do NOME
/// (<c>DCM4CHEE.{PatientName,hash}.{PatientBirthDate,hash}</c>) — e aí o nome passa a fazer parte
/// da identidade: editar um sobrenome criava um SEGUNDO paciente para o mesmo CPF e o registro
/// seguinte batia em 409. Estes testes travam a régua que tira o nome dessa conta.
/// Ver <c>docs/pendencias/identidade-do-paciente-no-pacs.md</c>.
/// </summary>
public class IssuerOfPatientIdTests
{
    private const string TagIssuer = "00100021";
    private const string TagPatientId = "00100020";

    [Theory]
    [InlineData("12389715710", "CPF")]   // CPF puro
    [InlineData("00000000000", "CPF")]   // 11 dígitos: a régua é de FORMATO, não de validade do DV
    [InlineData("0123456789", null)]     // 10 dígitos
    [InlineData("123456789012", null)]   // 12 dígitos
    [InlineData("1238971571a", null)]    // 11 caracteres, mas nem todos dígitos
    [InlineData("", null)]
    public void IssuerSaiSoParaPatientIdEmFormatoDeCpf(string patientId, string? esperado)
        => Assert.Equal(esperado, ConstrutorMwlItem.IssuerDoPatientId(patientId));

    [Fact]
    public void PacienteComCpf_ItemEPacienteLevamOIssuer()
    {
        var (exame, paciente) = Montar(cpf: "12389715710");

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");
        var registro = ConstrutorMwlItem.Paciente(exame, paciente);

        Assert.Equal("12389715710", Valor(item, TagPatientId));
        Assert.Equal("CPF", Valor(item, TagIssuer));
        Assert.Equal("CPF", Valor(registro, TagIssuer));
    }

    [Fact]
    public void PacienteSemCpf_NaoLevaIssuer()
    {
        // Sem CPF o PatientID é o Guid do hub. Mandar issuer nosso aqui seria pior que não mandar:
        // a coerção antiga continua valendo para esse formato, então o registro deixaria de casar
        // com a imagem que o equipamento devolve.
        var (exame, paciente) = Montar(cpf: null);

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");
        var registro = ConstrutorMwlItem.Paciente(exame, paciente);

        Assert.Null(Valor(item, TagIssuer));
        Assert.Null(Valor(registro, TagIssuer));
        Assert.Equal(exame.Solicitacao!.PacienteId.ToString(), Valor(item, TagPatientId));
    }

    [Fact]
    public void CpfComPontuacao_ContinuaVirandoCpfComIssuer()
    {
        var (exame, paciente) = Montar(cpf: "123.897.157-10");

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");

        Assert.Equal("12389715710", Valor(item, TagPatientId));
        Assert.Equal("CPF", Valor(item, TagIssuer));
    }

    private static (ExameImagem, PacienteResumo) Montar(string? cpf)
    {
        var pacienteId = Guid.CreateVersion7();
        var exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = "260908005",
            StudyInstanceUID = "2.25.1",
            TipoExame = new TipoExame
            {
                Id = Guid.CreateVersion7(),
                Nome = "RX TORAX",
                ModalidadeDicom = ModalidadeDicom.DX,
            },
            Solicitacao = new Solicitacao
            {
                Id = Guid.CreateVersion7(),
                PacienteId = pacienteId,
                Prioridade = PrioridadeSolicitacao.Eletiva,
            },
        };
        var paciente = new PacienteResumo(
            pacienteId, "ANGELICA DE ASSIS MELO DE ALENCAR", cpf, null,
            new DateOnly(1987, 5, 31), Sexo.Feminino);
        return (exame, paciente);
    }

    private static string? Valor(JsonObject ds, string tag) =>
        ds[tag]?["Value"]?[0]?.GetValue<string>();
}
