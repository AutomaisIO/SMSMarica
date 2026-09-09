using System.Text.Json.Nodes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// O limite de descrição do item de worklist é do APARELHO, não da rede. Até 09/09/2026 valia 16
/// para todos — um remendo do mamógrafo Fuji FDR-3000AWS (que falha com erro 31027 em descrição
/// longa) cobrando imposto dos outros 7 equipamentos e truncando 189 dos 205 tipos de exame no
/// meio da palavra ("RADIOGRAFIA DE T"). Estes testes travam o padrão em 64 (teto do VR LO) e o
/// corte por equipamento.
/// </summary>
public class DescricaoMaxPorEquipamentoTests
{
    private const string TagRequestedProcDesc = "00321060";
    private const string NomeLongo = "RADIOGRAFIA DE TORAX (PA)";   // 25 caracteres

    [Fact]
    public void PadraoNaoTrunca_NomeDeExameSaiInteiro()
    {
        var (exame, paciente) = Montar();

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");

        Assert.Equal(NomeLongo, Valor(item, TagRequestedProcDesc));
        Assert.Equal(NomeLongo, SpsDescription(item));
    }

    [Fact]
    public void PadraoDaEntidade_E64_OTetoDoDicom()
        => Assert.Equal(64, Equipamento.DescricaoMaxPadrao);

    [Fact]
    public void AparelhoComLimiteMenor_CortaSoNaquelaEstacao()
    {
        var (exame, paciente) = Montar();

        var fuji = ConstrutorMwlItem.Item(exame, paciente, "FDR-MAMO", descricaoMax: 16);
        var rx = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");

        Assert.Equal("RADIOGRAFIA DE T", Valor(fuji, TagRequestedProcDesc));
        Assert.Equal(NomeLongo, Valor(rx, TagRequestedProcDesc));
    }

    [Fact]
    public void NomeMaiorQueOTeto_ContinuaCortadoEm64()
    {
        // O VR LO do DICOM não passa de 64: acima disso o corte não é escolha nossa.
        var gigante = new string('A', 120);
        var (exame, paciente) = Montar(gigante);

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");

        Assert.Equal(64, Valor(item, TagRequestedProcDesc)!.Length);
    }

    [Fact]
    public void NomeCurto_NaoGanhaEspacoNemMuda()
    {
        var (exame, paciente) = Montar("RX MAO");

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", descricaoMax: 16);

        Assert.Equal("RX MAO", Valor(item, TagRequestedProcDesc));
    }

    private static (ExameImagem, PacienteResumo) Montar(string nomeExame = NomeLongo)
    {
        var pacienteId = Guid.CreateVersion7();
        var exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = "260909001",
            StudyInstanceUID = "2.25.1",
            TipoExame = new TipoExame
            {
                Id = Guid.CreateVersion7(),
                Nome = nomeExame,
                ModalidadeDicom = ModalidadeDicom.DX,
                RequestedProcedureDescription = nomeExame,
                ScheduledProcedureStepDescription = nomeExame,
            },
            Solicitacao = new Solicitacao
            {
                Id = Guid.CreateVersion7(),
                PacienteId = pacienteId,
                Prioridade = PrioridadeSolicitacao.Eletiva,
            },
        };
        var paciente = new PacienteResumo(
            pacienteId, "ALAFY SANTOS NUNES", "18405018719", null,
            new DateOnly(1998, 1, 5), Sexo.Masculino);
        return (exame, paciente);
    }

    private static string? Valor(JsonObject ds, string tag) =>
        ds[tag]?["Value"]?[0]?.GetValue<string>();

    private static string? SpsDescription(JsonObject item) =>
        item["00400100"]?["Value"]?[0]?["00400007"]?["Value"]?[0]?.GetValue<string>();
}
