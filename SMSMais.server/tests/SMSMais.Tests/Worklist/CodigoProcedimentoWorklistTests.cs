using System.Text.Json.Nodes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// Código do procedimento no item de worklist (ADR-0074): o código interno do SISREG em
/// (0008,0100), ligado POR EQUIPAMENTO. O raio-X do CDT escolhe o protocolo por ele; os outros
/// aparelhos (US, MG, o Fuji…) não podem receber nada de diferente do que já recebiam.
/// </summary>
public class CodigoProcedimentoWorklistTests
{
    private const string SeqProcedimentoSolicitado = "00321064";
    private const string SeqProtocoloAgendado = "00400008";

    [Fact]
    public void Desligado_item_sai_sem_sequencia_de_codigo()
    {
        var (exame, paciente) = Montar();

        var padrao = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT");
        var explicito = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, DestinoCodigoProcedimento.Nenhum, "99SMS");

        Assert.False(padrao.ContainsKey(SeqProcedimentoSolicitado));
        Assert.False(Sps(padrao).ContainsKey(SeqProtocoloAgendado));
        // Byte a byte o mesmo item: ligar num aparelho não pode mexer no dos outros.
        Assert.Equal(padrao.ToJsonString(), explicito.ToJsonString());
    }

    [Fact]
    public void Ligado_no_procedimento_solicitado_vai_so_no_nivel_do_item()
    {
        var (exame, paciente) = Montar();

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, DestinoCodigoProcedimento.ProcedimentoSolicitado);

        var codigo = Codigo(item[SeqProcedimentoSolicitado]);
        Assert.Equal("1304085", codigo.Valor);
        Assert.Equal("99SMS", codigo.Esquema);
        Assert.Equal("RADIOGRAFIA DE JOELHO (AP + LATERAL) DIREITO", codigo.Descricao);
        Assert.False(Sps(item).ContainsKey(SeqProtocoloAgendado));
    }

    [Fact]
    public void Ligado_nas_duas_sequencias_leva_o_mesmo_codigo_nas_duas()
    {
        var (exame, paciente) = Montar();
        var ambos = DestinoCodigoProcedimento.ProcedimentoSolicitado | DestinoCodigoProcedimento.ProtocoloAgendado;

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, ambos, "SISREG");

        Assert.Equal("1304085", Codigo(item[SeqProcedimentoSolicitado]).Valor);
        var protocolo = Codigo(Sps(item)[SeqProtocoloAgendado]);
        Assert.Equal("1304085", protocolo.Valor);
        Assert.Equal("SISREG", protocolo.Esquema);
    }

    [Fact]
    public void Exame_extra_leva_o_codigo_do_proprio_procedimento()
    {
        // Pedido com joelho D (principal) e E (extra, accession -1): cada exame leva o SEU código,
        // senão o aparelho abriria o protocolo do direito para o esquerdo.
        var (exame, paciente) = Montar();
        exame.Ordem = 1;
        exame.AccessionNumber = "261016019-1";
        exame.SolicitacaoProcedimento = new SolicitacaoProcedimento
        {
            Ordem = 2,
            ProcedimentoCodigoSisreg = "1304086",
            ProcedimentoTexto = "RADIOGRAFIA DE JOELHO (AP + LATERAL) ESQUERDO",
        };

        var item = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, DestinoCodigoProcedimento.ProcedimentoSolicitado);

        var codigo = Codigo(item[SeqProcedimentoSolicitado]);
        Assert.Equal("1304086", codigo.Valor);
        Assert.Equal("RADIOGRAFIA DE JOELHO (AP + LATERAL) ESQUERDO", codigo.Descricao);
    }

    [Fact]
    public void Pedido_sem_codigo_usa_o_do_tipo_e_sem_nenhum_nao_envia()
    {
        var (exame, paciente) = Montar();
        exame.Solicitacao!.ProcedimentoCodigoSisreg = null;
        exame.TipoExame!.CodigoSisreg = "1304043";

        var comDoTipo = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, DestinoCodigoProcedimento.ProcedimentoSolicitado);
        Assert.Equal("1304043", Codigo(comDoTipo[SeqProcedimentoSolicitado]).Valor);

        exame.TipoExame.CodigoSisreg = null;
        var semNenhum = ConstrutorMwlItem.Item(exame, paciente, "RX-CDT", 64, DestinoCodigoProcedimento.ProcedimentoSolicitado);
        Assert.False(semNenhum.ContainsKey(SeqProcedimentoSolicitado));
    }

    [Fact]
    public void Descricao_longa_abrevia_antes_de_cortar_e_nao_perde_o_lado()
    {
        // Os dois nomes do SISREG que passam de 64: cortados, direito e esquerdo ficariam iguais.
        var direito = ConstrutorMwlItem.DescricaoDoCodigo(
            "RADIOGRAFIA DE JOELHO OU PATELA (AP + LATERAL + OBLIQUA + 3 AXIAIS) DIREITO", 64);
        var esquerdo = ConstrutorMwlItem.DescricaoDoCodigo(
            "RADIOGRAFIA DE JOELHO OU PATELA (AP + LATERAL + OBLIQUA + 3 AXIAIS) ESQUERDO", 64);

        Assert.Equal("RX DE JOELHO OU PATELA (AP+LATERAL+OBLIQUA+3 AXIAIS) DIREITO", direito);
        Assert.Equal("RX DE JOELHO OU PATELA (AP+LATERAL+OBLIQUA+3 AXIAIS) ESQUERDO", esquerdo);
        Assert.True(esquerdo.Length <= 64);
    }

    [Fact]
    public void Descricao_sai_sem_acento_e_curta_nao_muda()
    {
        Assert.Equal("RADIOGRAFIA DE TORAX- PROGRAMA TUBERCULOSE",
            ConstrutorMwlItem.DescricaoDoCodigo("RADIOGRAFIA DE TÓRAX- PROGRAMA TUBERCULOSE", 64));
        Assert.Equal("RADIOGRAFIA DE BACIA", ConstrutorMwlItem.DescricaoDoCodigo("RADIOGRAFIA DE BACIA", 64));
    }

    [Theory]
    [InlineData("261016019", 0, "261016019")]
    [InlineData("261016019", 1, "261016019-1")]
    [InlineData("261016019", 2, "261016019-2")]
    public void Accession_do_principal_nao_tem_sufixo_e_os_extras_comecam_no_menos_um(
        string principal, int ordem, string esperado)
        => Assert.Equal(esperado, ExamesDoPedido.Accession(principal, ordem));

    private static (ExameImagem, PacienteResumo) Montar()
    {
        var pacienteId = Guid.CreateVersion7();
        const string nome = "RADIOGRAFIA DE JOELHO (AP + LATERAL) DIREITO";
        var exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            AccessionNumber = "261016019",
            StudyInstanceUID = "2.25.1",
            TipoExame = new TipoExame
            {
                Id = Guid.CreateVersion7(),
                Nome = nome,
                CodigoSisreg = "1304085",
                ModalidadeDicom = ModalidadeDicom.DX,
                RequestedProcedureDescription = nome,
                ScheduledProcedureStepDescription = nome,
            },
            Solicitacao = new Solicitacao
            {
                Id = Guid.CreateVersion7(),
                PacienteId = pacienteId,
                Prioridade = PrioridadeSolicitacao.Eletiva,
                ProcedimentoCodigoSisreg = "1304085",
                ProcedimentoTexto = nome,
            },
        };
        var paciente = new PacienteResumo(
            pacienteId, "PACIENTE TESTE", "18405018719", null, new DateOnly(1998, 1, 5), Sexo.Masculino);
        return (exame, paciente);
    }

    private static JsonObject Sps(JsonObject item) => (JsonObject)item["00400100"]!["Value"]![0]!;

    private static (string Valor, string Esquema, string Descricao) Codigo(JsonNode? sequencia)
    {
        var c = sequencia!["Value"]![0]!;
        return (V(c["00080100"]), V(c["00080102"]), V(c["00080104"]));
        static string V(JsonNode? n) => n!["Value"]![0]!.GetValue<string>();
    }
}
