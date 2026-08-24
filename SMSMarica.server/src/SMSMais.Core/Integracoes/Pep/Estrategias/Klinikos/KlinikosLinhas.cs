namespace SMSMais.Core.Integracoes.Pep.Estrategias.Klinikos;

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
/// Fechamento do boletim: quando a pessoa saiu e por quê. Não vem do <c>Pronto_Atendimento</c>
/// — essa tabela só tem a chegada; a saída mora em <c>atendimento_ambulatorial</c> e o desfecho
/// em <c>UPA_Atendimento_Medico</c> (ver <c>SqlDesfechos</c>).
///
/// <para><c>Fim</c> é o <c>atendamb_datafinal</c>. Medido em 08/08/2026 nos últimos 90 dias:
/// preenchido em 95,1% dos boletins da UPA e 96,9% dos da Santa Rita, espalhado pelas 24h do dia
/// — é evento real, não fechamento em lote. <b>Não usar <c>upaatemed_DataSaida</c></b>, que tem
/// nome de campo certo e preenchimento de 6,0% (UPA) e 0,2% (Santa Rita).</para>
/// </summary>
/// <param name="ProfCodigo">
/// Médico que encerrou o atendimento (<c>UPA_Atendimento_Medico.prof_codigo_encerramento</c>).
/// Vem de carona: <c>SqlDesfechos</c> já faz o JOIN com essa tabela para pegar o tipo de saída,
/// então o médico do atendimento custa uma coluna a mais, não uma consulta a mais.
/// </param>
internal sealed record DesfechoBoletim(string? Fim, int? TipoSaida, string? TipoSaidaDs, string? ProfCodigo);

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

/// <summary>
/// Boletim médico (<c>UPA_Atendimento_Medico</c>) — a narrativa do atendimento. É o equivalente
/// do "Boletim de Atendimento de Urgência" que o Salux entrega como eDoc, e a razão pela qual o
/// prontuário do Klinikos no hub parecia vazio: até 11/08/2026 esta tabela era lida só para
/// pegar o <c>tipsai_codigo</c> do desfecho, e os cinco campos de texto abaixo eram descartados.
///
/// <para>Preenchimento medido na UPA em 11/08/2026, sobre 173.654 linhas: exame físico 96,8%,
/// hipótese 96,7%, conduta 96,7%, anamnese 89,4%. <c>upaatemed_Reavaliacao</c> está zerada nas
/// 173.654 — a reavaliação nesta implantação é evento (sinais vitais + CID), não narrativa.</para>
/// </summary>
/// <param name="DataInicio">
/// <c>atendimento_ambulatorial.atendamb_datainicio</c> — quando o atendimento médico começou.
/// É a data do documento. Preenchida em <b>100%</b> das 173.734 linhas (medido 11/08/2026), ao
/// contrário de <c>upaatemed_DataSaida</c>, que tem nome promissor e 6% de preenchimento.
/// </param>
internal sealed record BoletimMedicoLinha(
    string AtendCodigo, string? SpaCodigo, string? DataInicio, string? Anamnese, string? ExameFisico,
    string? Hipotese, string? Conduta, string? Observacao, string? ProfCodigo, long Rv)
{
    /// <summary>Sem nenhum dos campos narrativos não há documento — só o esqueleto do desfecho.</summary>
    public bool TemNarrativa =>
        !string.IsNullOrWhiteSpace(Anamnese) || !string.IsNullOrWhiteSpace(ExameFisico)
        || !string.IsNullOrWhiteSpace(Hipotese) || !string.IsNullOrWhiteSpace(Conduta)
        || !string.IsNullOrWhiteSpace(Observacao);
}

/// <summary>
/// Item de medicamento prescrito (<c>Item_Prescricao_Medicamento</c> + <c>Prescricao</c>). É a
/// prescrição de verdade: nome do insumo, quantidade, unidade e via. Substitui o
/// MedicationRequest que saía de <c>UPA_Evolucao</c> tipo RECEITA/PRESCRIÇÃO, cujo texto era
/// literalmente a palavra "Receita" — a coluna <c>upaevo_descricao</c> guarda o rótulo da linha,
/// não o medicamento.
/// </summary>
internal sealed record ItemPrescricaoLinha(
    string ItemId, string PrescCodigo, string? SpaCodigo, string? Data, string? ProfCodigo,
    string? Insumo, decimal? Quantidade, string? Unidade, string? Via, int? Frequencia,
    int? Duracao, string? Sos, long Rv);

/// <summary>Sinais vitais colunados (<c>UPA_SinaisVitais</c>) — cada coluna vira uma Observation.</summary>
internal sealed record SinaisVitaisLinha(
    long Codigo, string? SpaCodigo, string? Data, string? ProfCodigo,
    string? PressaoArterial, string? Pulso, string? Temperatura, string? FrequenciaRespiratoria,
    string? Hgt, string? SaturacaoO2, string? Peso, long Rv);
