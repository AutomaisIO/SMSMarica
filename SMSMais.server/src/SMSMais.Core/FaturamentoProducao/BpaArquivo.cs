using System.Globalization;
using System.Text;

namespace SMSMais.Core.FaturamentoProducao;

/// <summary>
/// Uma linha-fonte já resolvida (todos os campos do BPA-I disponíveis), pronta para formatar.
/// É o contrato entre o serviço (que resolve da base + hub FHIR + de-para) e o formatador puro.
/// </summary>
public sealed record BpaLinhaFonte(
    Guid SolicitacaoId,
    string Cnes,
    int Competencia,
    string CnsProfissional,
    string Cbo,
    DateOnly DataAtendimento,
    string ProcedimentoSigtap,
    string? CnsPaciente,
    string? CpfPaciente,
    char Sexo,
    string? Ibge6,
    string? Cid,
    DateOnly? DataNascimento,
    int Quantidade,
    string NomePaciente,
    string Raca,
    string Nacionalidade,
    string? Cep,
    string? Logradouro,
    char SituacaoRua = 'N');

public sealed record LinhaPaginada(BpaLinhaFonte Fonte, int Folha, int Sequencia);

public sealed record BpaArquivoResultado(string Texto, int Linhas, int Folhas, int Controle);

/// <summary>
/// Formatador do BPA Magnético (DATASUS, versão D05.00) — puro, sem dependências, testável.
///
/// Header tipo 01 + linhas tipo 03 (BPA-I, 350 colunas de largura fixa, terminador CR+LF). Layout e
/// campo de controle conforme <c>Layout_Exportacao_BPA.pdf</c>. Validado byte a byte contra o arquivo
/// de referência do Prime (<c>BPA/PACDT09-.SET</c>): registro = 350 chars + CRLF; header = 126 chars.
/// </summary>
public static class BpaArquivo
{
    private const string Versao = "D05.00";
    private const int LinhasPorFolha = 99; // prd_seq ∈ [01..99]

    /// <summary>
    /// Agrupa as linhas por profissional+CBO (a "folha" do BPA-I) e numera folha (001..999) e
    /// sequência (01..99), transbordando para a próxima folha ao passar de 99 linhas.
    /// </summary>
    public static IReadOnlyList<LinhaPaginada> Paginar(IEnumerable<BpaLinhaFonte> linhas)
    {
        var ordenadas = linhas
            .OrderBy(l => l.CnsProfissional, StringComparer.Ordinal)
            .ThenBy(l => l.Cbo, StringComparer.Ordinal)
            .ThenBy(l => l.DataAtendimento)
            .ThenBy(l => l.ProcedimentoSigtap, StringComparer.Ordinal)
            .ThenBy(l => l.SolicitacaoId)
            .ToList();

        var res = new List<LinhaPaginada>(ordenadas.Count);
        int folha = 0, seq = 0;
        string? chaveAtual = null;
        foreach (var l in ordenadas)
        {
            var chave = l.CnsProfissional + "|" + l.Cbo;
            if (chave != chaveAtual || seq >= LinhasPorFolha)
            {
                folha++;
                seq = 0;
                chaveAtual = chave;
            }
            seq++;
            res.Add(new LinhaPaginada(l, folha, seq));
        }
        return res;
    }

    /// <summary>Gera o conteúdo texto do arquivo (header + linhas), com os totais e o controle.</summary>
    public static BpaArquivoResultado Gerar(IReadOnlyList<BpaLinhaFonte> linhas)
    {
        var paginadas = Paginar(linhas);
        int competencia = linhas.Count > 0 ? linhas[0].Competencia : 0;

        long somaPa = 0, somaQt = 0;
        var sb = new StringBuilder();
        var corpo = new StringBuilder();
        foreach (var p in paginadas)
        {
            var f = p.Fonte;
            somaPa += long.TryParse(SoDigitos(f.ProcedimentoSigtap), out var pa) ? pa : 0;
            somaQt += f.Quantidade;
            corpo.Append(FormatarLinha(p)).Append("\r\n");
        }

        int folhas = paginadas.Count == 0 ? 0 : paginadas[^1].Folha;
        int controle = Controle(somaPa, somaQt);

        sb.Append(Header(competencia, paginadas.Count, folhas, controle)).Append("\r\n");
        sb.Append(corpo);
        return new BpaArquivoResultado(sb.ToString(), paginadas.Count, folhas, controle);
    }

    /// <summary>Campo de controle: (Σ códigos de procedimento + Σ quantidades) % 1111 + 1111.</summary>
    public static int Controle(long somaProcedimentos, long somaQuantidades) =>
        (int)((somaProcedimentos + somaQuantidades) % 1111 + 1111);

    private static string Header(int competencia, int linhas, int folhas, int controle)
    {
        var sb = new StringBuilder(126);
        sb.Append("01");                         // 1-2  indicador header
        sb.Append("#BPA#");                       // 3-7  marca
        sb.Append(Num(competencia.ToString(CultureInfo.InvariantCulture), 6)); // 8-13
        sb.Append(Num(linhas.ToString(CultureInfo.InvariantCulture), 6));      // 14-19
        sb.Append(Num(folhas.ToString(CultureInfo.InvariantCulture), 6));      // 20-25
        sb.Append(controle.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')); // 26-29
        sb.Append(Branco(30));                    // 30-59  órgão origem (em branco, como o Prime)
        sb.Append(Branco(6));                     // 60-65  sigla
        sb.Append(new string('0', 14));           // 66-79  CGC/CPF (zeros, como o Prime)
        sb.Append(Branco(40));                    // 80-119 órgão destino
        sb.Append(' ');                           // 120    indicador destino (em branco, como o Prime)
        sb.Append(Versao);                        // 121-126 versão
        return sb.ToString();
    }

    private static string FormatarLinha(LinhaPaginada p)
    {
        var f = p.Fonte;
        bool usaCns = !string.IsNullOrWhiteSpace(f.CnsPaciente);

        var sb = new StringBuilder(350);
        sb.Append("03");                                              // 1-2   prd_ident
        sb.Append(Num(f.Cnes, 7));                                    // 3-9   prd_cnes
        sb.Append(Num(f.Competencia.ToString(CultureInfo.InvariantCulture), 6)); // 10-15 prd_cmp
        sb.Append(Num(f.CnsProfissional, 15));                        // 16-30 prd_cnsmed
        sb.Append(Num(f.Cbo, 6));                                     // 31-36 prd_cbo
        sb.Append(f.DataAtendimento.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // 37-44 prd_dtaten
        sb.Append(Num(p.Folha.ToString(CultureInfo.InvariantCulture), 3)); // 45-47 prd_flh
        sb.Append(Num(p.Sequencia.ToString(CultureInfo.InvariantCulture), 2)); // 48-49 prd_seq
        sb.Append(Num(f.ProcedimentoSigtap, 10));                     // 50-59 prd_pa
        sb.Append(usaCns ? Num(f.CnsPaciente, 15) : Branco(15));      // 60-74 prd_cnspac
        sb.Append(f.Sexo);                                            // 75    prd_sexo
        sb.Append(OpcNum(f.Ibge6, 6));                                // 76-81 prd_ibge
        sb.Append(Txt(RemoverPonto(f.Cid), 4));                       // 82-85 prd_cid
        sb.Append(OpcNum(IdadeAnos(f.DataNascimento, f.DataAtendimento), 3)); // 86-88 prd_idade
        sb.Append(Num((f.Quantidade <= 0 ? 1 : f.Quantidade).ToString(CultureInfo.InvariantCulture), 6)); // 89-94 prd_qt
        sb.Append("01");                                              // 95-96 prd_caten (eletivo)
        sb.Append(Branco(13));                                        // 97-109 prd_naut
        sb.Append("BPA");                                             // 110-112 prd_org
        sb.Append(Txt(f.NomePaciente, 30));                           // 113-142 prd_nmpac
        sb.Append(DataOuBranco(f.DataNascimento, 8));                 // 143-150 prd_dtnasc
        sb.Append(Num(f.Raca, 2));                                    // 151-152 prd_raca
        sb.Append(Branco(4));                                         // 153-156 prd_etnia
        sb.Append(Num(f.Nacionalidade, 3));                           // 157-159 prd_nac
        sb.Append(Branco(3));                                         // 160-162 prd_srv
        sb.Append(Branco(3));                                         // 163-165 prd_clf
        sb.Append(Branco(8));                                         // 166-173 prd_equipe_seq (INE)
        sb.Append(Branco(4));                                         // 174-177 prd_equipe_area
        sb.Append(Branco(14));                                        // 178-191 prd_cnpj
        sb.Append(OpcNum(f.Cep, 8));                                  // 192-199 prd_cep
        sb.Append(Branco(3));                                         // 200-202 prd_lograd_cod
        sb.Append(Txt(f.Logradouro, 30));                             // 203-232 prd_end
        sb.Append(Branco(10));                                        // 233-242 prd_compl
        sb.Append(Branco(5));                                         // 243-247 prd_num
        sb.Append(Branco(30));                                        // 248-277 prd_bairro
        sb.Append(Branco(11));                                        // 278-288 prd_fone
        sb.Append(Branco(40));                                        // 289-328 prd_email
        sb.Append(Branco(10));                                        // 329-338 prd_ine
        sb.Append(usaCns ? Branco(11) : Num(f.CpfPaciente, 11));      // 339-349 prd_cpf
        sb.Append(f.SituacaoRua);                                     // 350    prd_situacao_rua

        var linha = sb.ToString();
        if (linha.Length != 350)
            throw new InvalidOperationException($"Linha BPA-I com {linha.Length} colunas (esperado 350).");
        return linha;
    }

    // ---- helpers de formatação ----

    private static string SoDigitos(string? v) => new((v ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Numérico: só dígitos, justificado à direita com zeros à esquerda; trunca mantendo a direita.</summary>
    private static string Num(string? v, int len)
    {
        var d = SoDigitos(v);
        if (d.Length > len) d = d[^len..];
        return d.PadLeft(len, '0');
    }

    /// <summary>Numérico OPCIONAL: branco quando ausente (não zeros).</summary>
    private static string OpcNum(string? v, int len) =>
        string.IsNullOrWhiteSpace(SoDigitos(v)) ? Branco(len) : Num(v, len);

    /// <summary>Texto: sem acento, maiúsculo, justificado à esquerda com espaços; trunca à direita.</summary>
    private static string Txt(string? v, int len)
    {
        var s = SemAcento(v ?? string.Empty).ToUpperInvariant();
        if (s.Length > len) s = s[..len];
        return s.PadRight(len);
    }

    private static string Branco(int len) => new(' ', len);

    private static string? RemoverPonto(string? cid) => cid?.Replace(".", string.Empty);

    private static string DataOuBranco(DateOnly? d, int len) =>
        d is { } v ? v.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : Branco(len);

    private static string? IdadeAnos(DateOnly? nascimento, DateOnly referencia)
    {
        if (nascimento is not { } n) return null;
        var anos = referencia.Year - n.Year;
        if (referencia < n.AddYears(anos)) anos--;
        return anos < 0 ? null : anos.ToString(CultureInfo.InvariantCulture);
    }

    private static string SemAcento(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(texto.Length);
        foreach (var c in normalizado)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            // mantém ASCII imprimível; o resto (ç já vira c sem a marca; símbolos raros) vira espaço
            sb.Append(c <= 0x7F ? c : ' ');
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
