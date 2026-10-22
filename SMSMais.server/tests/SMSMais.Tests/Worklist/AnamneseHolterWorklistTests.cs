using System.Text.Json.Nodes;
using SMSMais.Core.Anamneses;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// Anamnese do Holter (branch ANAMNESE-CDT-HOLTER): altura, peso e fumante saem da anamnese e vão
/// no item de worklist em (0010,1020), (0010,1030) e (0010,21A0) — o que o CardioNet pede e hoje
/// recebe vazio. Sem anamnese o item não muda um byte.
/// </summary>
public class AnamneseHolterWorklistTests
{
    [Fact]
    public void Sem_dados_clinicos_o_item_sai_igual()
    {
        var (exame, paciente) = Montar();

        var antes = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT");
        var depois = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT", clinicos: null);

        Assert.Equal(antes.ToJsonString(), depois.ToJsonString());
        Assert.False(antes.ContainsKey("00101020"));
    }

    [Fact]
    public void Altura_em_metros_peso_em_kg_e_fumante_no_item()
    {
        var (exame, paciente) = Montar();
        var clinicos = AnamneseHolter.ParaWorklist("""{"alturaCm":172,"pesoKg":80.5,"fumante":"sim"}""");

        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT", clinicos: clinicos);

        Assert.Equal("DS", item["00101020"]!["vr"]!.GetValue<string>());
        Assert.Equal("1.72", Valor(item["00101020"]));
        Assert.Equal("80.5", Valor(item["00101030"]));
        Assert.Equal("CS", item["001021A0"]!["vr"]!.GetValue<string>());
        Assert.Equal("YES", Valor(item["001021A0"]));
    }

    [Theory]
    [InlineData("nao", "NO")]
    [InlineData("parou", "NO")]
    public void Nao_fuma_hoje_sai_NO(string resposta, string esperado)
    {
        var clinicos = AnamneseHolter.ParaWorklist($$"""{"alturaCm":160,"pesoKg":60,"fumante":"{{resposta}}"}""");

        Assert.Equal(esperado, clinicos!.SmokingStatus);
    }

    [Theory]
    [InlineData("""{"alturaCm":null,"pesoKg":60,"fumante":"sim"}""")]
    [InlineData("""{"alturaCm":17,"pesoKg":60,"fumante":"sim"}""")]
    [InlineData("""{"alturaCm":170,"pesoKg":0,"fumante":"sim"}""")]
    [InlineData("""{"alturaCm":170,"pesoKg":60,"fumante":"talvez"}""")]
    [InlineData("nao e json")]
    public void Conteudo_incompleto_ou_fora_da_faixa_e_recusado(string json)
    {
        Assert.Throws<ValidacaoException>(() => AnamneseHolter.Validar(json));
        Assert.Null(AnamneseHolter.ParaWorklist(json));
    }

    [Fact]
    public void Questionario_sai_da_modalidade()
    {
        Assert.Equal("holter", AnamneseHolter.QuestionarioDa(ModalidadeDicom.ECG));
        Assert.Equal("mamografia", AnamneseHolter.QuestionarioDa(ModalidadeDicom.MG));
        Assert.Equal("mamografia", AnamneseHolter.QuestionarioDa(ModalidadeDicom.OT));
        Assert.Equal("mamografia", AnamneseHolter.QuestionarioDa(null));
    }

    [Theory]
    [InlineData("0010,1020")]
    [InlineData("0010,1030")]
    [InlineData("0010,21A0")]
    public void Campo_opcional_nao_sobrescreve_os_dados_da_anamnese(string tag)
    {
        Assert.NotNull(TagDicomWorklist.Problema(tag));
    }

    private static (ExameImagem, PacienteResumo) Montar()
    {
        var pacienteId = Guid.CreateVersion7();
        const string nome = "HOLTER 24 HORAS";
        var exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = "2610053375",
            StudyInstanceUID = "2.25.1",
            TipoExame = new TipoExame
            {
                Id = Guid.CreateVersion7(),
                Nome = nome,
                CodigoSisreg = "1703204",
                ModalidadeDicom = ModalidadeDicom.ECG,
                RequestedProcedureDescription = nome,
                ScheduledProcedureStepDescription = nome,
            },
            Solicitacao = new Solicitacao
            {
                Id = Guid.CreateVersion7(),
                PacienteId = pacienteId,
                Prioridade = PrioridadeSolicitacao.Eletiva,
                ProcedimentoCodigoSisreg = "1703204",
                ProcedimentoTexto = nome,
            },
        };
        var paciente = new PacienteResumo(
            pacienteId, "PACIENTE TESTE", "18405018719", null, new DateOnly(1998, 1, 5), Sexo.Feminino);
        return (exame, paciente);
    }

    private static string Valor(JsonNode? atributo) => atributo!["Value"]![0]!.GetValue<string>();
}
