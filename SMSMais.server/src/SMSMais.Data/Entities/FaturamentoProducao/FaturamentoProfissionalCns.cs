namespace SMSMais.Data.Entities.FaturamentoProducao;

/// <summary>
/// De-para CPF → CNS/CBO do profissional executante, para preencher o <c>prd_cnsmed</c> e o
/// <c>prd_cbo</c> do BPA-I. A <c>solicitacao</c> do SISREG só guarda o CPF do executante
/// (<see cref="Solicitacao.ProfissionalExecutanteCpf"/>); o BPA exige o CNS.
///
/// <para>Semeado a partir do e-SUS PEC (consulta de profissional por CPF) ou do CNES; editável por
/// admin. Em runtime o faturamento NÃO consulta o PEC — resolve por esta tabela. CPF sem linha aqui
/// vira pendência (a linha fica fora do arquivo e aparece nas estatísticas).</para>
/// </summary>
public class FaturamentoProfissionalCns
{
    /// <summary>CPF do profissional (só dígitos) — chave.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>CNS do profissional (15 dígitos).</summary>
    public string Cns { get; set; } = string.Empty;

    /// <summary>CBO 2002 (6 dígitos) com que o profissional executa — usado no <c>prd_cbo</c>.</summary>
    public string? Cbo { get; set; }

    public string? Nome { get; set; }

    /// <summary>Proveniência do dado (ex.: "PEC", "CNES", "manual").</summary>
    public string? Origem { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
