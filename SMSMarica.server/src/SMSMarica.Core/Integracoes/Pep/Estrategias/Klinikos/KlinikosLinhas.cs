namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Klinikos;

/// <summary>
/// Linhas cruas lidas do Klinikos (SQL Server), uma por consulta do conector. São o contrato
/// entre o SQL e o mapper: se a origem mudar de coluna, quebra aqui e não no meio do FHIR.
///
/// <para><c>Rv</c> é o <c>rv_atualizacao</c> (rowversion) convertido para bigint — o ponteiro de
/// CDC da fase. Ver <c>docs/klinikos/mapeamento-fhir.md §5</c>.</para>
/// </summary>
internal sealed record UnidadeLinha(string Codigo, string? Nome, string? Fantasia, string? Sigla,
    string? Cnes, string? Telefone, string? Email);

internal sealed record ProfissionalLinha(string Codigo, string? Nome, string? Cpf, string? Cns,
    string? Conselho, string? Cbo, string? Ativo, long Rv);

internal sealed record PacienteLinha(
    string Codigo, string? Nome, string? Cpf, string? Cns, string? Nascimento, string? Sexo,
    string? Mae, string? Pai, string? Telefone, string? Celular, string? Email,
    string? Obito, string? Responsavel, string? TelefoneResponsavel, string? Raca, long Rv)
{
    /// <summary>CPF só de dígitos; vazio quando ausente — é a âncora do merge canônico.</summary>
    public string CpfDigitos => new([.. (Cpf ?? string.Empty).Where(char.IsDigit)]);
}

/// <summary>Boletim de atendimento (<c>Pronto_Atendimento</c>) — vira o Encounter.</summary>
internal sealed record BoletimLinha(
    string Codigo, string? PacCodigo, string? UnidCodigo, string? Chegada, string? DataBoletim,
    string? NomeSocial, string? CartaoSus, string? FormaChegada, string? RiscoCodigo, long Rv);

/// <summary>
/// Linha de <c>UPA_Evolucao</c>. É o registro clínico desta implantação — o CID, a nota e a
/// prescrição vêm daqui, porque as tabelas de atendimento do módulo de emergência estão vazias
/// (ver <c>docs/klinikos/mapeamento-fhir.md §3</c>).
/// </summary>
internal sealed record EvolucaoLinha(
    long Codigo, string? SpaCodigo, string? Tipo, string? DataHora, string? Descricao,
    string? ProfCodigo, string? CidPrimario, string? CidSecundario, long Rv)
{
    /// <summary>Tipo normalizado (maiúsculas sem acento) — o discriminador do papel FHIR.</summary>
    public string TipoNorm => Normalizar(Tipo);

    internal static string Normalizar(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return string.Empty;
        var s = v.Trim().ToUpperInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}

/// <summary>Sinais vitais colunados (<c>UPA_SinaisVitais</c>) — cada coluna vira uma Observation.</summary>
internal sealed record SinaisVitaisLinha(
    long Codigo, string? SpaCodigo, string? Data, string? ProfCodigo,
    string? PressaoArterial, string? Pulso, string? Temperatura, string? FrequenciaRespiratoria,
    string? Hgt, string? SaturacaoO2, string? Peso, long Rv);
