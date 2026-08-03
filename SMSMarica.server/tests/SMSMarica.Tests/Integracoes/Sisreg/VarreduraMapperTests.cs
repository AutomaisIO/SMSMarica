using SMSMarica.Core.Integracoes.SisregWeb.Varredura;

namespace SMSMarica.Tests.Integracoes.Sisreg;

/// <summary>
/// O envelope da varredura e sua conversão em marcação. O ponto central: o envelope guarda o
/// <c>pa</c> do SISREG e NUNCA o SIGTAP — é o que permite ao "Validar" de uma pendência
/// aproveitar um de-para confirmado depois que a pendência já existia.
/// </summary>
public class VarreduraMapperTests
{
    private static RegistroVarreduraRaw Registro(
        string? data = "28/07/2026",
        string? hora = "08:00",
        string? procedimentos = "01 - MAMOGRAFIA BILATERAL",
        string? telefones = "21999998888",
        string? nascimento = "15/03/1970",
        string? origem = "MARICA - RJ",
        string? situacao = "Agendamento/Pendente Confirmação/Executante") =>
        new(
            V: RegistroVarreduraRaw.VersaoAtual,
            CoSolicitacao: "673133160",
            Cns: "700000000000000",
            Paciente: "FULANA DE TAL",
            Nascimento: nascimento,
            Idade: "56",
            Origem: origem,
            Telefones: telefones,
            UnidadeSolicitante: "USF JOSEFA XAVIER LEAL",
            CnesSolicitante: "1234567",
            VagaSolicitada: "1ª VEZ",
            VagaConsumida: "RESERVA",
            Cid10: "Z014",
            Data: data,
            DiaSemana: "TER",
            Hora: hora,
            Situacao: situacao,
            Procedimentos: procedimentos,
            ProfCpf: "12345678901",
            ProfNome: "MARCO ANTONIO",
            PaCodigo: "1305007",
            PaNome: "MAMOGRAFIA BILATERAL",
            CnesExecutante: "3132358",
            NomeExecutante: "CDT DR ALBERTO LUIS MACHADO BORGES",
            CapturadoEm: new DateTime(2026, 8, 3, 4, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void Envelope_sobrevive_ao_round_trip()
    {
        var original = Registro();

        var lido = RegistroVarreduraRaw.Desserializar(original.Serializar());

        Assert.Equal(original, lido);
    }

    [Fact]
    public void Raw_de_txt_nao_e_confundido_com_envelope()
    {
        // Uma linha de TXT do SISREG chegando aqui devolve null em vez de estourar — o reprocesso
        // degrada para "ilegível" em vez de quebrar na mão do operador.
        Assert.Null(RegistroVarreduraRaw.Desserializar("673133160;0;0204030188;MAMOGRAFIA;;;28.07.2026"));
        Assert.Null(RegistroVarreduraRaw.Desserializar(""));
        Assert.Null(RegistroVarreduraRaw.Desserializar(null));
        Assert.Null(RegistroVarreduraRaw.Desserializar("{}"));
    }

    [Fact]
    public void Mapeia_os_campos_que_a_agenda_entrega()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(), "0204030188");

        Assert.Equal("673133160", m.CodigoSolicitacao);
        Assert.Equal("700000000000000", m.CnsPaciente);
        Assert.Equal("FULANA DE TAL", m.NomePaciente);
        Assert.Equal("0204030188", m.CodigoSigtap);
        Assert.Equal("MAMOGRAFIA BILATERAL", m.ProcedimentoTexto);
        Assert.Equal("1234567", m.CnesUnidadeSolicitante);
        Assert.Equal("3132358", m.CnesUnidadeExecutante);
        Assert.Equal("Z014", m.Cid);
        Assert.Equal(new DateTime(2026, 7, 28, 8, 0, 0), m.DataHoraAtendimento);
        Assert.Equal(new DateOnly(1970, 3, 15), m.NascimentoPaciente);
        Assert.Equal("1305007", m.CodigoProcedimentoSisreg);
        Assert.Equal("12345678901", m.CpfProfissionalExecutante);
        Assert.Equal("MARICA", m.MunicipioResidencia);
    }

    [Fact]
    public void O_que_a_agenda_nao_entrega_fica_nulo_em_vez_de_inventado()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(), "0204030188");

        // Buscar a ficha para preencher estes custaria +1 requisição POR agendamento — sozinho
        // estouraria o limite anti-robô do SISREG. Degradação declarada, não silenciosa.
        Assert.Null(m.CpfMedicoSolicitante);
        Assert.Null(m.NomeMedicoSolicitante);
        Assert.Null(m.DataSolicitacao);
        Assert.Null(m.DataRegulacao);
        Assert.Null(m.Logradouro);
        Assert.Null(m.Cep);
        Assert.Null(m.CodigoIbgeResidencia);
    }

    [Fact]
    public void Sem_de_para_o_sigtap_fica_nulo_e_o_pa_sobrevive_no_raw()
    {
        // É este o estado que vira pendência SigtapNaoMapeado.
        var m = VarreduraMapper.ParaMarcacao(Registro(), codigoSigtapResolvido: null);

        Assert.Null(m.CodigoSigtap);
        Assert.Equal("1305007", m.CodigoProcedimentoSisreg);

        // E o `pa` continua no RAW, que é o que permite reresolver depois.
        var relido = RegistroVarreduraRaw.Desserializar(m.LinhaRaw);
        Assert.Equal("1305007", relido!.PaCodigo);
    }

    [Fact]
    public void Confirmar_o_de_para_depois_faz_a_pendencia_entrar()
    {
        // O ciclo real: pendência criada sem SIGTAP → operador confirma → "Validar" relê o mesmo
        // RAW com o de-para novo. O RAW não muda; o resultado, sim.
        var semSigtap = VarreduraMapper.ParaMarcacao(Registro(), null);
        var raw = RegistroVarreduraRaw.Desserializar(semSigtap.LinhaRaw)!;

        var comSigtap = VarreduraMapper.ParaMarcacao(raw, "0204030188");

        Assert.Equal("0204030188", comSigtap.CodigoSigtap);
        Assert.Equal(semSigtap.CodigoSolicitacao, comSigtap.CodigoSolicitacao);
        Assert.Equal(semSigtap.CnsPaciente, comSigtap.CnsPaciente);
    }

    [Theory]
    [InlineData("01 - MAMOGRAFIA BILATERAL", "MAMOGRAFIA BILATERAL")]
    [InlineData("1 - MAMOGRAFIA BILATERAL", "MAMOGRAFIA BILATERAL")]
    [InlineData("MAMOGRAFIA BILATERAL", "MAMOGRAFIA BILATERAL")]
    public void Prefixo_de_ordem_do_procedimento_e_removido(string entrada, string esperado)
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(procedimentos: entrada), "0204030188");
        Assert.Equal(esperado, m.ProcedimentoTexto);
    }

    [Fact]
    public void Sem_procedimento_na_listagem_cai_no_nome_do_pa()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(procedimentos: null), "0204030188");
        Assert.Equal("MAMOGRAFIA BILATERAL", m.ProcedimentoTexto);
    }

    [Fact]
    public void Varios_telefones_guardam_o_primeiro()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(telefones: "21999998888, 2126334455"), "0204030188");
        Assert.Equal("21999998888", m.TelefonePaciente);
    }

    [Fact]
    public void Data_sem_hora_vira_meia_noite_em_vez_de_perder_o_dia()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(hora: null), "0204030188");
        Assert.Equal(new DateTime(2026, 7, 28, 0, 0, 0), m.DataHoraAtendimento);
    }

    [Fact]
    public void Data_ilegivel_nao_estoura()
    {
        var m = VarreduraMapper.ParaMarcacao(Registro(data: "sem data", nascimento: "??"), "0204030188");

        Assert.Null(m.DataHoraAtendimento);
        Assert.Null(m.NascimentoPaciente);
    }

    [Fact]
    public void Nbsp_do_html_do_sisreg_e_normalizado()
    {
        // "Pendente Confirmação" — string.Trim() não remove NBSP, e sem normalizar a situação
        // chegaria com um espaço que não é espaço.
        var m = VarreduraMapper.ParaMarcacao(
            Registro(situacao: "Agendamento/Pendente Confirmação/Executante"), "0204030188");

        Assert.Equal("Agendamento/Pendente Confirmação/Executante", m.SituacaoAgendamento);
    }
}
