using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// Agrupa o motivo de cancelamento (texto livre de quem cancelou) em CATEGORIA. O texto livre nunca vai
/// para tela nem PDF — pode carregar nome e telefone. Regras calibradas em 30/09/2026 sobre ~2.200
/// justificativas do SISREG e ~2.300 cancelamentos do SER (docs/regulacao/relatorio-2025-2026): a ordem
/// importa (a primeira que casar vence).
/// </summary>
public static partial class MotivoCancelamento
{
    public const string SemMotivo = "Sem motivo informado";
    public const string Outros = "Outros";

    private static readonly (string Categoria, string[] Chaves)[] Regras =
    [
        ("Óbito", ["OBITO", "FALECI", "FALECEU", "FALECIMENTO"]),
        ("Sem cota PPI para a especialidade", ["SEM PPI", "NAO POSSUI PPI", "SEM COTA", "NAO POSSUI COTA"]),
        ("Plano Estadual de Redução de Filas (CIB)", ["PLANO ESTADUAL DE REDUCAO", "PALNO ESTADUAL", "CIB N"]),
        ("Já em fila em outro sistema", ["FILA SISREG", "AGENDAMENTO SISREG", "NO SISTEMA SERNIT", "NO SISREG", "FILA DO SISREG", "FILA NO SER"]),
        ("Recurso não ofertado pela unidade executora", ["NAO TEMOS ESSE RECURSO", "NAO TEMOS MAIS", "NAO REALIZAMOS", "NAO DISPOMOS", "COMPETENCIA DA UNIDADE", "NAO OFERT"]),
        ("Solicitação em recurso/especialidade errado", ["FAVOR INSERIR", "INSERIR EM", "RECURSO ERRADO", "RECURSO INCORRETO", "OUTRO TIPO DE AGENDAMENTO", "INDICADA A FAZER"]),
        ("Fora do perfil / protocolo", ["ATENCAO PRIMARIA", "FORA DO PERFIL", "PROTOCOLO", "CRITERIO", "FAIXA ETARIA", "INADEQUAD", "EM CRIANCAS"]),
        ("Já atendido / em tratamento", ["JA EM TRATAMENTO", "EM TRATAMENTO"]),
        ("Sem contato com o paciente", ["SEM CONTATO", "NAO COMPLETA", "TENTATIVAS SEM SUCESSO", "NAO LOCALIZ", "NAO ATENDE", "CAIXA POSTAL", "NUMERO INEXISTENTE", "TELEFONE ERRADO"]),
        ("Mudança de endereço / município", ["MUDOU-SE", "MUDOU SE", "MUDOU DE", "MUDANCA DE ENDERECO", "MUDANCA DE MUNICIPIO", "RESIDE EM OUTRO", "OUTRO MUNICIPIO", "NAO MORA MAIS"]),
        ("Duplicidade", ["DUPLIC", "NOVO CODIGO", "OUTRO CODIGO", "DUAS VEZES", "EM DOBRO", "CONSULTA NO MESMO DIA"]),
        ("Cancelado pela unidade solicitante", ["PROPRIO SOLICITANTE", "PELA UNIDADE SOLICITANTE", "ACS SOLICITOU", "SOLICITADO PELA UNIDADE"]),
        ("Desistência / impedimento do paciente", ["NAO AGUARDA", "DESIST", "RECUSA", "RECUSOU", "NAO DESEJA", "NAO TEM INTERESSE", "NAO QUER", "SEM INTERESSE", "NAO PODERA", "NAO VAI PODER", "NAO PODE IR", "NAO VAI FAZER", "NAO VAI COMPARECER", "INFORMOU QUE NAO", "MOTIVO PESSOAL", "VIAJANDO", "VIAGEM", "PEDIU PARA CANCELAR", "NAO GOSTOU", "TRABALHO", "NAO CONSEGUIRA", "SEM TEMPO HABIL"]),
        ("Sem resposta no prazo", ["NAO RESPONDID", "RESPONDIDA NO PRAZO", "PRAZO ESTABELECIDO", "SEM RESPOSTA"]),
        ("Já atendido / realizado", ["ATENDID", "JA REALIZ", "REALIZOU", "JA FEZ", "JA FOI", "REALIZADO", "JA TEVE"]),
        ("Desistência / impedimento do paciente", ["A PEDIDO", "SOLICITACAO DO PACIENTE", "PACIENTE SOLICITOU", "PACIENTE PEDIU", "MEIOS PROPRIOS", "PARTICULAR"]),
        ("Erro de marcação / agenda", ["ERRO", "EQUIVOC", "ERRAD", "INCORRET", "INDEVID"]),
        ("Ausência do profissional", ["PROFISSIONAL", "MEDICO", "FERIAS", "LICENCA", "AFASTAMENTO", "AUSENCIA", "FALTA DO", "ATESTADO"]),
        ("Equipamento / estrutura indisponível", ["EQUIPAMENTO", "APARELHO", "MANUTENC", "QUEBRAD", "DEFEITO", "SEM ENERGIA", "LUZ", "AGUA", "INTERDI"]),
        ("Remarcação / reagendamento", ["REMARC", "REAGEND", "REMANEJ", "AGENDADO", "AGENDADA", "NOVA DATA", "TROCA DE DATA"]),
        ("Transferência / outra unidade", ["TRANSFER", "OUTRA UNIDADE", "UNIDADE:", "ENCAMINHAD"]),
        ("Reclassificação / avaliação do regulador", ["RECLASSIFIC", "REGULADOR", "AVALIAD", "ANEXO", "LAUDO", "EXAME COMPLEMENTAR", "INFORMAC"]),
        ("Sem vaga / oferta suspensa", ["SEM VAGA", "PRESTADOR", "SUSPENS", "BLOQUEI", "CANCELAMENTO DA AGENDA"]),
    ];

    private static readonly HashSet<string> SoPalavraCancelar = ["CANCELAR", "CANCELADO", "CANCELAMENTO", "CANCELADA"];

    /// <summary>Categoria do texto (vazio/"CANCELAR"/"." = <see cref="SemMotivo"/>; nada casou = <see cref="Outros"/>).</summary>
    public static string Categoria(string? texto)
    {
        var t = SemAcento(texto ?? string.Empty).Trim();
        var letras = RegexNaoLetra().Replace(t, string.Empty);
        if (letras.Length < 3 || SoPalavraCancelar.Contains(RegexNaoLetraOuEspaco().Replace(t, string.Empty).Trim()))
            return SemMotivo;

        foreach (var (categoria, chaves) in Regras)
        {
            foreach (var chave in chaves)
            {
                if (t.Contains(chave, StringComparison.Ordinal)) return categoria;
            }
        }
        return Outros;
    }

    private static readonly Dictionary<string, string> PorFollowUp = new(StringComparer.Ordinal)
    {
        ["FalhaContato"] = "Sem contato com o paciente",
        ["SemVaga"] = "Sem vaga / oferta suspensa",
        ["ReclassificacaoRisco"] = "Reclassificação / avaliação do regulador",
        ["SolicitacaoAoSolicitante"] = "Reclassificação / avaliação do regulador",
        ["CancelamentoOuReagendamento"] = "Remarcação / reagendamento",
    };

    /// <summary>SER/SERNIT: quando o texto do cancelamento é genérico ("não respondida no prazo"), o último
    /// FollowUP antes dele diz o porquê (ex.: "sem contato: diversas tentativas").</summary>
    public static string CategoriaComFollowUp(string? textoCancelamento, string? categoriaFollowUp, string? textoFollowUp)
    {
        var cat = Categoria(textoCancelamento);
        if (cat is not ("Sem resposta no prazo" or Outros or SemMotivo) || string.IsNullOrEmpty(categoriaFollowUp)) return cat;

        var pelaObservacao = Categoria(textoFollowUp);
        if (categoriaFollowUp == "ContatoRealizado")
            return pelaObservacao is Outros or SemMotivo or "Sem resposta no prazo" ? cat : pelaObservacao;
        if (PorFollowUp.TryGetValue(categoriaFollowUp, out var porCategoria)) return porCategoria;
        return pelaObservacao is Outros or SemMotivo or "Sem resposta no prazo" ? cat : pelaObservacao;
    }

    internal static string SemAcento(string s)
    {
        var decomposto = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    [GeneratedRegex("[^A-Z]")]
    private static partial Regex RegexNaoLetra();

    [GeneratedRegex("[^A-Z ]")]
    private static partial Regex RegexNaoLetraOuEspaco();
}
