namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma falta OFICIAL do SISREG — linha da "Consulta de Absenteísmo por Unidade de Saúde"
/// (<c>rel_amb_faltas_sol.pl</c>): agendamento em que a unidade executante <b>registrou falta</b>
/// (situação <c>Agendamento/Falta/Executante</c>).
///
/// <para><b>Não é "quem não foi confirmado".</b> O SISREG tem três estados para o agendamento que já
/// passou — Confirmado, Falta e Pendente de confirmação (a unidade não apontou nada) — e esta lista
/// traz só o segundo. Conferido em 01/10/2026 contra a tela de agenda do CDT capturada em 25/07: das
/// 156 "Falta", 155 estão aqui; das 108 "Pendente", só 4 (apontadas como falta depois), e 104
/// seguiam pendentes seis semanas mais tarde. Por isso o "PENDENTE" do Arquivo de Agendamentos
/// (coluna 34), que junta falta e pendente, é sempre maior que a lista — e a sobra é agendamento
/// EM ABERTO, não falta.</para>
///
/// <para>É a fonte do indicador de absenteísmo. Guardamos SÓ código, data, procedimento e unidade —
/// nome, endereço e telefone que a lista traz ficam fora.</para>
///
/// <para>A lista muda depois do dia: a unidade aponta falta com atraso e, mais raramente, troca uma
/// falta por chegada confirmada. Uma janela relida por completo substitui o que havia nela. As
/// semanas com menos de 30 dias são relidas pelo coletor
/// <see cref="ColetorIndicadorSisreg.FaltasRecentes"/>.</para>
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

    /// <summary>
    /// A MESMA lista de faltas, lida cedo e relida de hora em hora, das semanas que ainda não têm
    /// idade para o número oficial. Cursor separado de <see cref="Faltas"/> de propósito: o indicador
    /// de absenteísmo só considera coberto o que <see cref="Faltas"/> leu, porque nas primeiras
    /// semanas a unidade ainda está apontando — lida cedo, a lista está incompleta. Serve à ficha do
    /// paciente, onde a falta que a unidade já registrou aparece sem esperar os 30 dias.
    /// </summary>
    FaltasRecentes = 5,
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
