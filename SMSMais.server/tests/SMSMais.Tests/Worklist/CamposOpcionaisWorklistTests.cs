using System.Text.Json.Nodes;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Equipamentos.Validators;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// Campos opcionais do item de worklist, por equipamento (ADR-0074, adendo de 16/10/2026): médico
/// solicitante, unidade ("clínica", da executante ou da solicitante) e o código do procedimento numa
/// tag de texto. Nasceu com o CardioNet da cardiologia do CDT, que pede (0008,0090). Tudo desligado
/// por padrão — o item dos outros aparelhos não pode mudar um byte.
/// </summary>
public class CamposOpcionaisWorklistTests
{
    [Fact]
    public void Sem_campos_o_item_sai_byte_a_byte_igual()
    {
        var (exame, paciente) = Montar();

        var antes = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT");
        var depois = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT", 64,
            DestinoCodigoProcedimento.Nenhum, null, CamposOpcionaisWorklist.De(null, null, OrigemUnidadeWorklist.Executante, null));

        Assert.Equal(antes.ToJsonString(), depois.ToJsonString());
        Assert.False(antes.ContainsKey("00080090"));
    }

    [Fact]
    public void Medico_solicitante_vai_como_PN_na_tag_escolhida()
    {
        var (exame, paciente) = Montar();

        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT",
            campos: new CamposOpcionaisWorklist(MedicoSolicitanteTag: "00080090"));

        Assert.Equal("PN", item["00080090"]!["vr"]!.GetValue<string>());
        Assert.Equal("ARAUJO^JOSE CARLOS", item["00080090"]!["Value"]![0]!["Alphabetic"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(OrigemUnidadeWorklist.Executante, "CDT - DR ALBERTO LUIS M. BORGES")]
    [InlineData(OrigemUnidadeWorklist.Solicitante, "USF Sao Jose I")]
    public void Unidade_sai_da_origem_escolhida_sem_acento(OrigemUnidadeWorklist origem, string esperado)
    {
        var (exame, paciente) = Montar();

        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT",
            campos: new CamposOpcionaisWorklist(UnidadeTag: "00080080", UnidadeOrigem: origem));

        Assert.Equal("LO", item["00080080"]!["vr"]!.GetValue<string>());
        Assert.Equal(esperado, Valor(item["00080080"]));
    }

    [Fact]
    public void Tag_da_etapa_agendada_vai_dentro_da_0040_0100()
    {
        var (exame, paciente) = Montar();

        // (0040,0011) Scheduled Procedure Step Location é atributo da SPS, VR SH (16).
        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT",
            campos: new CamposOpcionaisWorklist(UnidadeTag: "00400011"));

        Assert.False(item.ContainsKey("00400011"));
        Assert.Equal("CDT - DR ALBERTO", Valor(Sps(item)["00400011"]));
    }

    [Fact]
    public void Codigo_numa_tag_de_texto_dispensa_as_sequencias()
    {
        var (exame, paciente) = Montar();
        var ambos = DestinoCodigoProcedimento.ProcedimentoSolicitado | DestinoCodigoProcedimento.ProtocoloAgendado;

        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT", 64, ambos, null,
            new CamposOpcionaisWorklist(CodigoProcedimentoTag: "00402400"));

        Assert.Equal("1703204", Valor(item["00402400"]));
        Assert.False(item.ContainsKey("00321064"));
        Assert.False(Sps(item).ContainsKey("00400008"));
    }

    [Fact]
    public void Sem_valor_a_tag_nao_entra()
    {
        var (exame, paciente) = Montar();
        exame.Solicitacao!.SolicitanteNome = "  ";
        exame.Solicitacao.UnidadeSolicitante = null;

        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT",
            campos: new CamposOpcionaisWorklist("00080090", "00321033", OrigemUnidadeWorklist.Solicitante));

        Assert.False(item.ContainsKey("00080090"));
        Assert.False(item.ContainsKey("00321033"));
    }

    [Fact]
    public void Tag_gravada_que_colide_com_campo_do_sistema_e_ignorada_no_item()
    {
        var (exame, paciente) = Montar();
        var antes = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT");

        // Cadastro feito por fora da validação (banco, versão antiga): o PatientName não é tocado.
        var item = ConstrutorMwlItem.Item(exame, paciente, "CARDIO-CDT",
            campos: new CamposOpcionaisWorklist(MedicoSolicitanteTag: "00100010"));

        Assert.Equal(antes.ToJsonString(), item.ToJsonString());
    }

    [Theory]
    [InlineData("0008,0090", null)]
    [InlineData("(0008,0090)", null)]
    [InlineData("00080090", null)]
    [InlineData("0032,1033", null)]
    [InlineData("0040,0011", null)]
    [InlineData("8,90", "formato")]
    [InlineData("0010,0010", "já é preenchida")]
    [InlineData("0040,0001", "já é preenchida")]
    [InlineData("0008,0020", "tipo DA")]
    [InlineData("0009,0010", "não existe")]
    [InlineData("0011,1234", "não existe")]
    public void Regua_da_tag(string tag, string? trechoDoProblema)
    {
        var problema = TagDicomWorklist.Problema(tag);

        if (trechoDoProblema is null) Assert.Null(problema);
        else Assert.Contains(trechoDoProblema, problema);
    }

    [Fact]
    public void Cadastro_recusa_dois_campos_na_mesma_tag()
    {
        // Unidade ligada SEM tag pega o padrão da origem — e ele colide com o médico posto ali à mão.
        var request = new CadastrarEquipamentoRequest(
            "CardioNet", Guid.CreateVersion7(), ModalidadeDicom.ECG, "CARDIO-CDT",
            EnviarMedicoSolicitante: true, MedicoSolicitanteTag: "0008,0080",
            EnviarUnidade: true, UnidadeOrigem: OrigemUnidadeWorklist.Executante);

        var resultado = new CadastrarEquipamentoValidator().Validate(request);

        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("mesma tag (0008,0080)"));
    }

    [Fact]
    public void Cadastro_recusa_tag_reservada_so_quando_o_campo_esta_ligado()
    {
        var desligado = new CadastrarEquipamentoRequest(
            "CardioNet", Guid.CreateVersion7(), ModalidadeDicom.ECG, "CARDIO-CDT", MedicoSolicitanteTag: "0010,0010");
        var ligado = desligado with { EnviarMedicoSolicitante = true };

        Assert.True(new CadastrarEquipamentoValidator().Validate(desligado).IsValid);
        Assert.False(new CadastrarEquipamentoValidator().Validate(ligado).IsValid);
    }

    [Fact]
    public void Codigo_no_padrao_0008_0100_e_o_mesmo_que_nao_escolher_tag()
    {
        var request = new CadastrarEquipamentoRequest(
            "RX", Guid.CreateVersion7(), ModalidadeDicom.DX, "RX-CDT",
            CodigoNoProcedimentoSolicitado: true, CodigoProcedimentoTag: "0008,0100");

        Assert.True(new CadastrarEquipamentoValidator().Validate(request).IsValid);
        Assert.Null(RegrasCamposWorklist.ProblemaTagCodigo("0008,0100"));
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
                SolicitanteNome = "José Carlos Araújo",
                UnidadeExecutante = new Unidade { Id = Guid.CreateVersion7(), Nome = "CDT - DR ALBERTO LUIS M. BORGES" },
                UnidadeSolicitante = new Unidade { Id = Guid.CreateVersion7(), Nome = "USF São José I" },
            },
        };
        var paciente = new PacienteResumo(
            pacienteId, "PACIENTE TESTE", "18405018719", null, new DateOnly(1998, 1, 5), Sexo.Feminino);
        return (exame, paciente);
    }

    private static JsonObject Sps(JsonObject item) => (JsonObject)item["00400100"]!["Value"]![0]!;

    private static string Valor(JsonNode? atributo) => atributo!["Value"]![0]!.GetValue<string>();
}
