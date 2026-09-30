namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma falta OFICIAL do SISREG — linha da "Consulta de Absenteísmo por Unidade de Saúde"
/// (<c>rel_amb_faltas_sol.pl</c>): agendamento cuja chegada a unidade executante não confirmou.
///
/// <para>É a fonte do indicador de absenteísmo. O "PENDENTE" do Arquivo de Agendamentos (coluna 34)
/// é parecido mas não é igual: congela no dia da importação e inclui unidades que o SISREG não conta
/// como falta (medido em 30/09/2026: +8% em jan/2026). Guardamos SÓ código, data, procedimento e
/// unidade — nome, endereço e telefone que a lista traz ficam fora.</para>
///
/// <para>A lista ENCOLHE quando a unidade confirma a chegada depois: uma janela relida por completo
/// substitui o que havia nela (com trava de encolhimento no coletor).</para>
/// </summary>
public class SisregFaltaOficial
{
    public Guid Id { get; set; }
    public string CodigoSolicitacao { get; set; } = string.Empty;
    public DateOnly DataExecucao { get; set; }
    public string? Hora { get; set; }
    public string? Procedimento { get; set; }
    public string? UnidadeSolicitante { get; set; }
    public DateTime LidoEm { get; set; }
}

/// <summary>
/// Uma marcação CANCELADA no SISREG, com a justificativa — linha de <c>cons_marcacao_cancelada</c>.
///
/// <para>O Arquivo de Agendamentos não traz marcação cancelada: cancelamento antigo só existe nesta
/// tela. Upsert-only (cancelamento é fato, nunca se apaga). Operador e profissional ficam fora (dado
/// de pessoa); o motivo vai para o relatório agrupado em categoria, nunca em texto livre.</para>
/// </summary>
public class SisregMarcacaoCancelada
{
    public Guid Id { get; set; }
    public string CodigoSolicitacao { get; set; } = string.Empty;

    /// <summary>Instante do cancelamento (UTC). Obrigatório: é metade da chave única.</summary>
    public DateTime CanceladoEm { get; set; }

    public DateOnly? DataMarcacao { get; set; }
    public string? Procedimento { get; set; }
    public string? Justificativa { get; set; }
    public DateTime LidoEm { get; set; }
}

/// <summary>Como uma solicitação saiu da fila do SISREG SEM agendamento. Os números são os da
/// situação no <c>gerenciador_solicitacao</c>.</summary>
public enum SituacaoDesfechoSisreg
{
    CanceladaAntesDeAgendar = 3,
    Devolvida = 4,
    Negada = 6,
}

/// <summary>De onde veio o desfecho.</summary>
public enum OrigemDesfechoSisreg
{
    /// <summary>Listagem do SISREG lida pelo coletor (sem motivo).</summary>
    Tela = 1,

    /// <summary>Decisão do regulador capturada pela extensão do navegador (APLICAR com justificativa).</summary>
    Extensao = 2,
}

/// <summary>
/// Solicitação que saiu da fila sem virar agendamento: devolvida, negada ou cancelada antes de
/// agendar. Fecha o buraco da fila reconstruída (quem saiu sem agendar antes de 10/09/2026) e é o
/// "excluídas" do indicador. As listagens só respondem por UNIDADE SOLICITANTE (rede inteira estoura
/// ~65 s); o motivo não vem na listagem — só pela extensão, quando o regulador usa.
/// </summary>
public class SisregSolicitacaoDesfecho
{
    public Guid Id { get; set; }
    public string CodigoSolicitacao { get; set; } = string.Empty;
    public SituacaoDesfechoSisreg Situacao { get; set; }
    public DateOnly? DataSolicitacao { get; set; }

    /// <summary>Quando saiu da fila — só conhecido na devolução (<c>cons_negados_reg</c>) e na extensão.</summary>
    public DateOnly? DataDesfecho { get; set; }

    public string? Procedimento { get; set; }
    public string? UnidadeSolicitanteCnes { get; set; }
    public string? Justificativa { get; set; }
    public OrigemDesfechoSisreg Origem { get; set; }
    public DateTime LidoEm { get; set; }
}

/// <summary>
/// Cota da PPI (Programação Pactuada Integrada) por procedimento e competência —
/// <c>cons_ppi_cotas</c>. ATENÇÃO: em Maricá o total é o mesmo em todas as competências (teto
/// configurado); confirmar com a regulação antes de chamar de "vagas contratualizadas".
/// </summary>
public class SisregPpiCota
{
    public Guid Id { get; set; }

    /// <summary>Primeiro dia do mês da competência.</summary>
    public DateOnly Competencia { get; set; }

    public string CodigoInterno { get; set; } = string.Empty;
    public string? CodigoUnificado { get; set; }
    public string? Procedimento { get; set; }
    public int Total { get; set; }
    public int Usada { get; set; }
    public int? Saldo { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime LidoEm { get; set; }
}

public enum ColetorIndicadorSisreg
{
    Faltas = 1,
    Canceladas = 2,
    Desfechos = 3,
    Ppi = 4,
}

public enum StatusColetaIndicador
{
    Pendente = 1,
    EmAndamento = 2,
    Concluida = 3,
    Falha = 4,
}

/// <summary>
/// Cursor PERSISTIDO do coletor de indicadores: uma linha por (coletor, janela, escopo). Sobrevive a
/// restart (a fila em memória da releitura da fila não sobrevive) e diz, mês a mês, o que já foi lido
/// — é daí que sai o selo "Oficial"/"Parcial" de cada série.
/// </summary>
public class SisregIndicadorColeta
{
    public Guid Id { get; set; }
    public ColetorIndicadorSisreg Coletor { get; set; }
    public DateOnly JanelaInicio { get; set; }
    public DateOnly JanelaFim { get; set; }

    /// <summary>CNES da unidade solicitante (desfechos) ou vazio. Nunca nulo: nulo quebra o índice único.</summary>
    public string Escopo { get; set; } = string.Empty;

    public StatusColetaIndicador Status { get; set; }

    /// <summary>Contadas ANTES da chamada — um processo que morre no meio não zera a conta.</summary>
    public int Tentativas { get; set; }

    public int? Linhas { get; set; }
    public DateTime? IniciadoEm { get; set; }
    public DateTime? LidoEm { get; set; }
    public string? Erro { get; set; }
    public DateTime CriadoEm { get; set; }
}
