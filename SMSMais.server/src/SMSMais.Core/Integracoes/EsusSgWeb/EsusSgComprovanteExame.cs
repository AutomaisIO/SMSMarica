using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.EsusSgWeb;

/// <summary>
/// Os dados do comprovante de UM agendamento de exame, como o ESUS devolve
/// (<c>obter-comprovante-exame-agendamento-em-lote-com-preparo-subprocedimentos</c>). O ESUS não
/// gera PDF: o front dele monta um cupom de 7 cm no navegador com estes campos (template <c>X2a</c>
/// do bundle, portado em <c>Automais.esus_saocongalo/probe_comprovante.py</c>). A guia que guardamos
/// é montada daqui (ADR-0076).
/// </summary>
public sealed partial record EsusSgComprovanteExame(
    string? Municipio,
    string? Unidade,
    string? Endereco,
    string? Referencia,
    string? DataExame,
    string? HoraExame,
    string? Paciente,
    string? NomeSocial,
    string? Prontuario,
    string? ProntuarioUnico,
    string? CartaoSus,
    string? Setor,
    string? Local,
    string? Procedimento,
    string? Subprocedimentos,
    string? Profissional,
    string? Ocupacao,
    string? Observacoes,
    string? MarcadoEm,
    string? MarcadoPor,
    string? AgendadoPelaFila,
    string? RotuloRegulador,
    string? Regulador,
    string? SenhaEfetivacao,
    string? RecomendacaoPreparo,
    IReadOnlyList<(string Subprocedimento, string Preparo)> PreparoSubprocedimentos)
{
    public static EsusSgComprovanteExame De(JsonElement r)
    {
        string? T(string chave) => EsusSgJson.Texto(r, chave);

        var endereco = string.Join(" ", new[]
        {
            Juntar(" ", T("tlg_nome"), T("end_logradouro")) is { } rua
                ? Juntar(", ", rua, T("end_numero"), T("end_complemento"))
                : null,
            Juntar(" ", T("bai_nome"), T("mun_nome")) is { } lugar ? "- " + lugar : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        var preparoSub = new List<(string, string)>();
        if (r.TryGetProperty("recomendacaoPreparoSub", out var subs) && subs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in subs.EnumerateArray())
            {
                var nome = EsusSgJson.Texto(s, "nome_suprocedimento");
                var preparo = SemHtml(EsusSgJson.Texto(s, "preparo"));
                if (nome is not null || preparo is not null) preparoSub.Add((nome ?? string.Empty, preparo ?? string.Empty));
            }
        }

        return new(
            Municipio: T("cli_nome"),
            Unidade: T("uns_nome"),
            Endereco: string.IsNullOrWhiteSpace(endereco) ? null : endereco,
            Referencia: T("end_referencia"),
            DataExame: T("eha_data_exame"),
            HoraExame: T("eap_hora"),
            Paciente: T("pes_nome"),
            NomeSocial: T("pes_nome_social"),
            Prontuario: T("ppr_numero"),
            ProntuarioUnico: T("pac_prontuario_unico"),
            CartaoSus: T("crs_numero"),
            Setor: T("set_nome"),
            Local: T("lca_nome"),
            Procedimento: T("stp_novo_nome_procedimento"),
            Subprocedimentos: T("subprocedimentos"),
            Profissional: T("atendente"),
            Ocupacao: T("ocp_nome"),
            Observacoes: T("observacoes"),
            MarcadoEm: Juntar(" às ", T("data_marcacao"), T("hora_marcacao")),
            MarcadoPor: T("usu_nome"),
            AgendadoPelaFila: T("agendadoPelaFila"),
            RotuloRegulador: T("lblNomeRegulador"),
            Regulador: T("nomeRegulador"),
            SenhaEfetivacao: T("eap_senha_efetivacao"),
            // O front injeta este campo como HTML cru; aqui vira texto.
            RecomendacaoPreparo: SemHtml(T("recomendacaoPreparo")),
            PreparoSubprocedimentos: preparoSub);
    }

    private static string? Juntar(string separador, params string?[] partes)
    {
        var validas = partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
        return validas.Count == 0 ? null : string.Join(separador, validas);
    }

    /// <summary>Texto de um trecho HTML: quebras viram linha, tags somem, entidades decodificadas.</summary>
    internal static string? SemHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var texto = RegexQuebra().Replace(html, "\n");
        texto = RegexTag().Replace(texto, string.Empty);
        texto = WebUtility.HtmlDecode(texto);
        var linhas = texto.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        var limpo = string.Join("\n", linhas);
        return limpo.Length == 0 ? null : limpo;
    }

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|hr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex RegexQuebra();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RegexTag();
}
