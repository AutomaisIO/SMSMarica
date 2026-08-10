using FluentAssertions;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Testes puros do <see cref="AgendaTxtParser"/> — cobre os DOIS formatos do export de
/// agendamentos do SISREG (TXT com cabeçalho de unidade; CSV com cabeçalho de colunas e o
/// executante vindo do nome do arquivo). Dados SINTÉTICOS, layout fiel (38 campos).
/// </summary>
public class AgendaTxtParserTests
{
    // Uma linha de dados de 38 campos, parametrizada só no que os testes checam.
    private static string Linha(string codigo, string cns = "700000000000001") =>
        string.Join(';', new[]
        {
            codigo, "1305007", "0204030030", "MAMOGRAFIA BILATERAL",   // 0-3
            "72754842772", "MARCO EXECUTANTE",                          // 4-5 exec prof
            "01.07.2026", "08:00", "0",                                 // 6-8 data/hora/tipo
            cns, "FULANA DE TAL",                                       // 9-10 cns/nome
            "08.05.1983", "43", "517", "MAE DE TAL",                    // 11-14
            "RUA", "TRINTA", "LT05", "S/N", "CENTRO", "24921544",       // 15-20 endereço
            "(21)97002-9774",                                           // 21 telefone
            "MARICA", "330270", "MARICA", "330270",                     // 22-25
            "2930242", "CENTRO MATERNO INFANTIL",                       // 26-27 solicitante
            "F", "27.05.2026", "OPER-SOL",                              // 28-30
            "02.06.2026", "OPER-AUT",                                   // 31-32 regulação
            "22.50", "PENDENTE", "Z13",                                 // 33-35
            "00344588750", "MEDICO SOLICITANTE",                        // 36-37
        });

    private const string CabecalhoColunasCsv =
        "solicitacao;codigo_interno;codigo_unificado;descricao_procedimento;cpf_proficional_executante;" +
        "nome_profissional_executante;data_agendamento;hr_agendamento;tipo;cns;nome;dt_nascimento;idade;" +
        "idade_meses;nome_mae;tipo_logradouro;logradouro;complemento;numero_logradouro;bairro;cep;telefone;" +
        "municipio;ibge;mun_solicitante;ibge_solicitante;cnes_solicitante;unidade_fantasia;sexo;" +
        "data_solicitacao;operador_solicitante;data_autorizacao;operador_autorizador;valor_procedimento;" +
        "situacao;cid;cpf_profissional_solicitante;nome_profissional_solicitante";

    [Fact]
    public void Txt_com_cabecalho_de_unidade_traz_executante_por_cnes()
    {
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;2\n\n" + Linha("670119011") + "\n" + Linha("670121358");

        var r = AgendaTxtParser.Parse(txt, "irrelevante.txt");

        r.Cabecalho.CnesUnidade.Should().Be("3132358");
        r.Marcacoes.Should().HaveCount(2);
        var m = r.Marcacoes[0];
        m.CodigoSolicitacao.Should().Be("670119011");
        m.CnesUnidadeExecutante.Should().Be("3132358");      // veio do cabeçalho
        m.NomeUnidadeExecutante.Should().Be("CDT DR ALBERTO");
        m.CnesUnidadeSolicitante.Should().Be("2930242");     // coluna 26
        m.CodigoSigtap.Should().Be("0204030030");
        m.DataSolicitacao!.Value.ToString("yyyy-MM-dd").Should().Be("2026-05-27");
        m.DataRegulacao!.Value.ToString("yyyy-MM-dd").Should().Be("2026-06-02");
    }

    [Fact]
    public void Csv_pula_cabecalho_de_colunas_e_deriva_executante_do_nome_do_arquivo()
    {
        var csv = CabecalhoColunasCsv + "\r\n" + Linha("543307499") + "\r\n" + Linha("549296135");

        var r = AgendaTxtParser.Parse(csv, "CDT DR ALBERTO LUIS MACHADO BORGES-20260703.csv");

        r.Marcacoes.Should().HaveCount(2, "o cabeçalho de colunas não vira marcação");
        r.Marcacoes.Select(m => m.CodigoSolicitacao).Should().Equal("543307499", "549296135");

        var m = r.Marcacoes[0];
        m.CnesUnidadeExecutante.Should().BeNull("o CSV não traz CNES do executante nas linhas");
        m.NomeUnidadeExecutante.Should().Be("CDT DR ALBERTO LUIS MACHADO BORGES", "veio do nome do arquivo, sem a data");
        m.CnesUnidadeSolicitante.Should().Be("2930242");
        m.NomePaciente.Should().Be("FULANA DE TAL");
    }

    // ===== Código do procedimento no SISREG (o `pa`, coluna 1) =====

    [Fact]
    public void Traz_o_codigo_do_procedimento_no_sisreg_da_coluna_1()
    {
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;1\n" + Linha("670119011");

        var r = AgendaTxtParser.Parse(txt);

        r.Marcacoes.Should().ContainSingle()
            .Which.CodigoProcedimentoSisreg.Should().Be("1305007");
    }

    [Fact]
    public void Codigo_do_procedimento_vazio_vira_null_e_nao_impede_a_marcacao()
    {
        // O SISREG deixa a coluna em branco em ~1/3 das linhas de produção — não é erro.
        var semPa = Linha("670119011").Replace(";1305007;", ";;");
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;1\n" + semPa;

        var r = AgendaTxtParser.Parse(txt);

        var m = r.Marcacoes.Should().ContainSingle().Subject;
        m.CodigoProcedimentoSisreg.Should().BeNull();
        m.ProcedimentoTexto.Should().Be("MAMOGRAFIA BILATERAL", "o nome é o que identifica o procedimento");
    }

    [Fact]
    public void Csv_sem_data_no_nome_do_arquivo_ainda_deriva_o_executante()
    {
        var csv = CabecalhoColunasCsv + "\n" + Linha("543307499");

        var r = AgendaTxtParser.Parse(csv, "USF JOSEFA XAVIER LEAL.csv");

        r.Marcacoes.Should().ContainSingle();
        r.Marcacoes[0].NomeUnidadeExecutante.Should().Be("USF JOSEFA XAVIER LEAL");
    }

    // ===== Linhas recusadas: nada de dados pode sumir em silêncio (vira falha para o operador) =====

    [Fact]
    public void Linha_truncada_e_rejeitada_com_o_raw_e_o_numero_da_linha()
    {
        var truncada = "670119011;1305007;0204030030"; // 3 campos; o layout pede 38
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;1\n" + truncada;

        var r = AgendaTxtParser.Parse(txt);

        r.Marcacoes.Should().BeEmpty();
        var rej = r.Rejeitadas.Should().ContainSingle().Subject;
        rej.Numero.Should().Be(2, "1-based, e o cabeçalho ocupa a linha 1");
        rej.LinhaRaw.Should().Be(truncada, "o RAW é o que o botão Validar reprocessa");
        rej.Motivo.Should().Contain("38");
    }

    [Fact]
    public void Linha_com_numero_de_solicitacao_nao_numerico_e_rejeitada()
    {
        var r = AgendaTxtParser.Parse(Linha("ABC-99"));

        r.Marcacoes.Should().BeEmpty();
        r.Rejeitadas.Should().ContainSingle().Which.Motivo.Should().Contain("ABC-99");
    }

    [Fact]
    public void Linha_sem_numero_de_solicitacao_e_rejeitada()
    {
        var r = AgendaTxtParser.Parse(Linha(string.Empty));

        r.Marcacoes.Should().BeEmpty();
        r.Rejeitadas.Should().ContainSingle().Which.Motivo.Should().Contain("sem o nº");
    }

    [Fact]
    public void Cabecalhos_e_linhas_vazias_nao_viram_rejeitadas()
    {
        // Cabeçalho de unidade, cabeçalho de colunas do CSV e linhas em branco são estrutura
        // legítima do arquivo — não podem poluir a lista de erros do operador.
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;1\n" + CabecalhoColunasCsv + "\n\n   \n" + Linha("670119011");

        var r = AgendaTxtParser.Parse(txt);

        r.Marcacoes.Should().ContainSingle();
        r.Rejeitadas.Should().BeEmpty();
    }

    [Fact]
    public void Linhas_boas_continuam_passando_no_meio_das_ruins()
    {
        var txt = Linha("111") + "\ncampos;de;menos\n" + Linha("222") + "\n" + Linha("XPTO");

        var r = AgendaTxtParser.Parse(txt);

        r.Marcacoes.Select(m => m.CodigoSolicitacao).Should().Equal("111", "222");
        r.Rejeitadas.Should().HaveCount(2);
    }

    /// <summary>
    /// "Validar" (reprocessar) não tem o arquivo: o serviço reconstrói um cabeçalho sintético
    /// (<c>CNES;nome;;;0</c>) na frente do RAW guardado e reusa ESTE parser. Se esse contrato
    /// quebrar, o executante some da linha revalidada — daí o round-trip viver aqui.
    /// </summary>
    [Fact]
    public void Cabecalho_sintetico_do_reprocesso_recompoe_o_executante()
    {
        var raw = Linha("670119011");

        var r = AgendaTxtParser.Parse("3132358;CDT DR ALBERTO;;;0\n" + raw);

        var m = r.Marcacoes.Should().ContainSingle().Subject;
        m.CnesUnidadeExecutante.Should().Be("3132358");
        m.NomeUnidadeExecutante.Should().Be("CDT DR ALBERTO");
        m.LinhaRaw.Should().Be(raw);
        r.Rejeitadas.Should().BeEmpty();
    }

    /// <summary>Falha sem CNES guardado: a linha sozinha ainda precisa ser reprocessável — aí a
    /// executante vem do contexto de unidade do operador.</summary>
    [Fact]
    public void Linha_sozinha_sem_cabecalho_ainda_parseia()
    {
        var r = AgendaTxtParser.Parse(Linha("670119011"));

        var m = r.Marcacoes.Should().ContainSingle().Subject;
        m.CodigoSolicitacao.Should().Be("670119011");
        m.CnesUnidadeExecutante.Should().BeNull();
    }

    // ===== Reconhecer(): é mesmo um export do SISREG? Decide DESCARTAR o arquivo inteiro. =====

    [Fact]
    public void Reconhece_txt_com_cabecalho_de_unidade()
    {
        var txt = "3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;1\n" + Linha("670119011");

        AgendaTxtParser.Reconhecer(txt).Reconhecido.Should().BeTrue();
    }

    [Fact]
    public void Reconhece_csv_com_cabecalho_de_colunas()
    {
        var csv = CabecalhoColunasCsv + "\r\n" + Linha("543307499");

        AgendaTxtParser.Reconhecer(csv).Reconhecido.Should().BeTrue();
    }

    [Fact]
    public void Reconhece_linha_de_dados_sem_cabecalho_nenhum()
    {
        AgendaTxtParser.Reconhecer(Linha("670119011")).Reconhecido.Should().BeTrue();
    }

    /// <summary>O caso que motivou a checagem de FORMA: contagem de coluna sozinha é fraca demais.
    /// Um CSV alheio com 38 campos não pode ser aceito e virar 300 falhas de lixo na aba Erros.</summary>
    [Fact]
    public void Nao_reconhece_csv_alheio_com_as_mesmas_38_colunas()
    {
        var impostor = string.Join(';', Enumerable.Range(0, 38).Select(i => $"valor{i}"));

        var a = AgendaTxtParser.Reconhecer(impostor);

        a.Reconhecido.Should().BeFalse();
        a.Motivo.Should().Contain("nº da solicitação");
    }

    [Fact]
    public void Nao_reconhece_arquivo_com_38_colunas_e_data_em_outro_formato()
    {
        // Passa no nº, passa no SIGTAP, tem 38 campos — e ainda assim NÃO é do SISREG:
        // a data de atendimento é o discriminador forte.
        var c = Linha("670119011").Split(';');
        c[6] = "2026-07-01";  // ISO, não dd.MM.yyyy
        var a = AgendaTxtParser.Reconhecer(string.Join(';', c));

        a.Reconhecido.Should().BeFalse();
        a.Motivo.Should().Contain("data de atendimento");
    }

    [Fact]
    public void Nao_reconhece_data_de_atendimento_invalida_no_calendario()
    {
        var c = Linha("670119011").Split(';');
        c[6] = "31.02.2026"; // formato certo, dia inexistente
        AgendaTxtParser.Reconhecer(string.Join(';', c)).Reconhecido.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("qualquer coisa\nque nao e csv")]
    [InlineData("a;b;c")]
    public void Nao_reconhece_conteudo_que_nao_e_do_sisreg(string conteudo)
    {
        AgendaTxtParser.Reconhecer(conteudo).Reconhecido.Should().BeFalse();
    }

    [Fact]
    public void Nao_reconhece_arquivo_so_com_cabecalho_e_nenhuma_linha_de_dados()
    {
        var a = AgendaTxtParser.Reconhecer("3132358;CDT DR ALBERTO;01/07/2026;08/07/2026;0\n\n");

        a.Reconhecido.Should().BeFalse();
        a.Motivo.Should().Contain("linha de dados");
    }

    /// <summary>Campos que vêm vazios em arquivo BOM não podem reprovar o arquivo — CNS ausente é
    /// falha de LINHA (o operador resolve na aba Erros), não motivo pra jogar o arquivo fora.</summary>
    [Fact]
    public void Reconhece_arquivo_bom_mesmo_com_campos_opcionais_vazios()
    {
        var c = Linha("670119011").Split(';');
        c[9] = "";   // sem CNS
        c[21] = "";  // sem telefone
        c[37] = "";  // sem médico solicitante
        c[35] = "";  // sem CID

        AgendaTxtParser.Reconhecer(string.Join(';', c)).Reconhecido.Should().BeTrue();
    }
}
