using SMSMais.Core.Integracoes.SisregWeb.Escalas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Leitura do CSV de escalas do SISREG — a grade de OFERTA que sustenta a Agenda.
///
/// <para>As linhas aqui são sintéticas, montadas campo a campo a partir do layout de 34 colunas
/// medido em 04/09/2026. O arquivo real não pode virar fixture: carrega CPF de 199 profissionais.</para>
///
/// <para>Cada teste prende uma armadilha achada no arquivo de verdade, não um caso imaginado:
/// <c>---</c> como "vazio", <c>ATIVA</c> que não significa vigente, minutos zerados com vagas
/// preenchidas, e código de GRUPO terminado em <c>000</c>.</para>
/// </summary>
public class EscalasCsvParserTests
{
    private const string Cabecalho =
        "COD. ESCALA AMBULATORIAL;COD. CENTRAL EXEC.;DESC. CENTRAL EXEC.;CPF PROFISSIONAL EXEC.;"
        + "NOME PROFISSIONAL EXEC.;COD. CBO;DESC. CBO;COD CNES EXEC.;DESC. CNES EXEC.;"
        + "COD. PROCEDIMENTO INTERNO;DESC. PROCEDIMENTO INTERNO;COD. PROCEDIMENTO UNIFICADO;"
        + "SIGLA DIA SEMANA;QTD VAGAS PRIM. VEZ;QTD. MINUTOS PRIM. VEZ;QTD. VAGAS RETORNO;"
        + "QTD. MINUTOS RETORNO;QTD. VAGAS RESERVA;QTD. MINUTOS RESERVA;QUEBRA AUTOMATICA;"
        + "AGENDA LOCAL;DATA DE VIGENCIA INICIAL;DATA DE VIGENCIA FINAL;HORA INICIAL;HORA FINAL;"
        + "NOME OPERADOR CRIADOR;NOME OPERADOR MODIFICADOR;DATA ULTIMA ALTERACAO;"
        + "HORA ULTIMA ALTERACAO;STATUS;DATA DA INSERCAO;HORA DA INSERCAO;"
        + "DATA DA ULTIMA ATIVACAO;HORA DA ULTIMA ATIVACAO";

    /// <summary>Uma linha do layout real, com os campos que importam parametrizáveis.</summary>
    private static string Linha(
        string codigo = "13588839",
        string cpf = "12345678901",
        string cbo = "225320",
        string cnes = "3132358",
        string procedimento = "0229000",
        string sigtap = "---",
        string dia = "SEX",
        string vagas1 = "10", string min1 = "0",
        string vagasRet = "0", string minRet = "0",
        string vagasRes = "0", string minRes = "0",
        string vigIni = "06/01/2025", string vigFim = "11/09/2026",
        string horaIni = "11:10", string horaFim = "12:00",
        string status = "ATIVA")
    {
        var c = new string[34];
        Array.Fill(c, string.Empty);
        c[0] = codigo;
        c[1] = "330270"; c[2] = "MARICA";
        c[3] = cpf; c[4] = "PROFISSIONAL DE TESTE";
        c[5] = cbo; c[6] = "MEDICO EM RADIOLOGIA";
        c[7] = cnes; c[8] = "CDT DR ALBERTO LUIS MACHADO BORGES";
        c[9] = procedimento; c[10] = "GRUPO - ULTRASSONOGRAFIA GESTANTES"; c[11] = sigtap;
        c[12] = dia;
        c[13] = vagas1; c[14] = min1;
        c[15] = vagasRet; c[16] = minRet;
        c[17] = vagasRes; c[18] = minRes;
        c[19] = "SIM"; c[20] = "NAO";
        c[21] = vigIni; c[22] = vigFim;
        c[23] = horaIni; c[24] = horaFim;
        c[25] = "VIVIAN-REGULACAO"; c[26] = "KELLY-REGULACAO";
        c[27] = "24/08/2026"; c[28] = "12:08";
        c[29] = status;
        c[30] = "21/08/2026"; c[31] = "09:08";
        c[32] = "24/08/2026"; c[33] = "12:08";
        return string.Join(';', c);
    }

    private static string Arquivo(params string[] linhas) =>
        Cabecalho + "\n" + string.Join("\n", linhas) + "\n";

    [Fact]
    public void Le_uma_escala_completa()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha()));

        Assert.Empty(r.Rejeitadas);
        var e = Assert.Single(r.Escalas);
        Assert.Equal("13588839", e.CodigoEscala);
        Assert.Equal("3132358", e.Cnes);
        Assert.Equal("12345678901", e.ProfissionalCpf);
        Assert.Equal(DayOfWeek.Friday, e.DiaSemana);
        Assert.Equal(new TimeOnly(11, 10), e.HoraInicio);
        Assert.Equal(new TimeOnly(12, 0), e.HoraFim);
        Assert.Equal(new DateOnly(2025, 1, 6), e.VigenciaInicio);
        Assert.Equal(new DateOnly(2026, 9, 11), e.VigenciaFim);
        Assert.Equal(StatusEscalaSisreg.Ativa, e.Status);
        Assert.Equal(10, e.VagasTotal);
    }

    /// <summary>
    /// O cabeçalho de colunas não pode virar escala nem entrar como linha rejeitada — poluiria a
    /// tela de falhas em toda sincronização.
    /// </summary>
    [Fact]
    public void Cabecalho_de_colunas_e_ignorado()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha()));

        Assert.Single(r.Escalas);
        Assert.Empty(r.Rejeitadas);
    }

    /// <summary>
    /// O SISREG escreve <c>---</c> onde não tem valor. Tratar isso como texto faria o SIGTAP virar
    /// a string "---" e o de-para procurar por um código que não existe.
    /// </summary>
    [Fact]
    public void Tres_tracos_significa_vazio()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(sigtap: "---", cbo: "---")));

        var e = Assert.Single(r.Escalas);
        Assert.Null(e.ProcedimentoSigtap);
        Assert.Null(e.CboCodigo);
    }

    /// <summary>
    /// Código terminado em <c>000</c> é GRUPO. É o que permite casar a oferta do grupo com o
    /// agendamento que chega com o item — sem isso, metade dos agendamentos fica sem oferta que os
    /// explique (medido: 7.172 órfãos casando só por código exato).
    /// </summary>
    [Theory]
    [InlineData("0229000", true)]
    [InlineData("1402000", true)]
    [InlineData("0301010", false)]
    [InlineData("1305007", false)]
    public void Codigo_terminado_em_000_e_grupo(string codigo, bool esperado)
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(procedimento: codigo)));

        Assert.Equal(esperado, Assert.Single(r.Escalas).EhGrupo);
    }

    /// <summary>
    /// Minutos zerados com vagas preenchidas é o caso real (131 dos 934 blocos vigentes): o bloco de
    /// 11:10–12:00 tem 10 vagas e 0 minutos. Tem de ser lido como está — quem transforma isso em
    /// grade de horário é o cálculo, com a dedução declarada, não o parser inventando um valor.
    /// </summary>
    [Fact]
    public void Minutos_zerados_com_vagas_preenchidas_e_lido_como_esta()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(vagas1: "10", min1: "0")));

        var e = Assert.Single(r.Escalas);
        Assert.Equal(10, e.VagasPrimeiraVez);
        Assert.Equal(0, e.MinutosPrimeiraVez);
    }

    [Fact]
    public void Soma_as_tres_categorias_de_vaga()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(vagas1: "3", vagasRet: "5", vagasRes: "7")));

        Assert.Equal(15, Assert.Single(r.Escalas).VagasTotal);
    }

    [Theory]
    [InlineData("ATIVA", StatusEscalaSisreg.Ativa)]
    [InlineData("INATIVA", StatusEscalaSisreg.Inativa)]
    [InlineData("EXPIRADA", StatusEscalaSisreg.Expirada)]
    [InlineData("EXCLUIDA", StatusEscalaSisreg.Excluida)]
    public void Traduz_os_quatro_status(string texto, StatusEscalaSisreg esperado)
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(status: texto)));

        Assert.Equal(esperado, Assert.Single(r.Escalas).Status);
    }

    /// <summary>
    /// Escala de um dia só (vigência inicial = final) é 6.372 das 17.469 linhas — não é anomalia.
    /// Precisa passar pelo mesmo caminho, sem ramo especial.
    /// </summary>
    [Fact]
    public void Escala_de_um_dia_so_e_valida()
    {
        var r = EscalasCsvParser.Parse(
            Arquivo(Linha(vigIni: "10/09/2026", vigFim: "10/09/2026", dia: "QUI")));

        var e = Assert.Single(r.Escalas);
        Assert.Equal(e.VigenciaInicio, e.VigenciaFim);
    }

    /// <summary>
    /// Vigência invertida tornaria a expansão de ocorrências silenciosamente vazia — a unidade
    /// apareceria sem oferta e ninguém saberia por quê. Melhor rejeitar com motivo.
    /// </summary>
    [Fact]
    public void Vigencia_invertida_e_rejeitada_com_motivo()
    {
        var r = EscalasCsvParser.Parse(
            Arquivo(Linha(vigIni: "10/09/2026", vigFim: "01/09/2026")));

        Assert.Empty(r.Escalas);
        Assert.Contains("termina antes de começar", Assert.Single(r.Rejeitadas).Motivo);
    }

    /// <summary>Sem CPF não há agenda de profissional — a linha não pode entrar pela metade.</summary>
    [Fact]
    public void Linha_sem_cpf_de_profissional_e_rejeitada()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(cpf: "")));

        Assert.Empty(r.Escalas);
        Assert.Single(r.Rejeitadas);
    }

    [Fact]
    public void Dia_da_semana_desconhecido_e_rejeitado_sem_derrubar_o_resto()
    {
        var r = EscalasCsvParser.Parse(Arquivo(Linha(dia: "XXX"), Linha(codigo: "999", dia: "SEG")));

        Assert.Single(r.Escalas);
        Assert.Single(r.Rejeitadas);
    }

    /// <summary>
    /// Contagem de colunas sozinha aceitaria qualquer CSV alheio com 34 campos e encheria a tela de
    /// falhas de lixo. A assinatura tem de olhar a FORMA dos campos estruturais.
    /// </summary>
    [Fact]
    public void Reconhece_o_export_de_escalas()
    {
        Assert.True(EscalasCsvParser.Reconhecer(Arquivo(Linha())).Reconhecido);
    }

    [Fact]
    public void Nao_reconhece_arquivo_de_outro_formato()
    {
        var r = EscalasCsvParser.Reconhecer("a;b;c\n1;2;3\n");

        Assert.False(r.Reconhecido);
        Assert.NotEmpty(r.Motivo);
    }

    [Fact]
    public void Nao_reconhece_arquivo_vazio()
    {
        Assert.False(EscalasCsvParser.Reconhecer("").Reconhecido);
    }
}
