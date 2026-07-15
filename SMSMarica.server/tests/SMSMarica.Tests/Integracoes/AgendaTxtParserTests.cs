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

    [Fact]
    public void Csv_sem_data_no_nome_do_arquivo_ainda_deriva_o_executante()
    {
        var csv = CabecalhoColunasCsv + "\n" + Linha("543307499");

        var r = AgendaTxtParser.Parse(csv, "USF JOSEFA XAVIER LEAL.csv");

        r.Marcacoes.Should().ContainSingle();
        r.Marcacoes[0].NomeUnidadeExecutante.Should().Be("USF JOSEFA XAVIER LEAL");
    }
}
