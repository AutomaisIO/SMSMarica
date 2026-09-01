using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>Estado de uma varredura da agenda do SISREG.</summary>
public enum StatusVarredura
{
    Pendente = 1,
    EmExecucao = 2,

    /// <summary>Varreu todas as combinações habilitadas.</summary>
    Concluida = 3,

    /// <summary>
    /// <b>Cobertura incompleta, declarada.</b> Parou antes do fim (CAPTCHA anti-robô ou teto de
    /// requisições) preservando tudo que já entrou. Existe como status próprio porque marcar
    /// "Concluída" uma varredura interrompida é operacionalmente perigoso: o operador conclui
    /// que o dia está importado e não está.
    /// </summary>
    Parcial = 4,

    Erro = 5,
    Cancelada = 6,
}

/// <summary>
/// Uma execução do motor de varredura, por unidade. Rastreio próprio — deliberadamente separado de
/// <see cref="SisregImportacaoExecucao"/>, que modela um ARQUIVO importado: aquela entidade é
/// varrida por <c>ImportacaoLoteService.LimparOrfasAsync</c>, que marca como erro toda linha
/// pendente/em execução sem filtrar por lote — um upload manual de TXT mataria a linha de uma
/// varredura em curso.
/// </summary>
public class SisregVarreduraExecucao
{
    public Guid Id { get; set; }

    public Guid UnidadeId { get; set; }

    /// <summary>Nome da unidade no momento da execução — desnormalizado para o rastreio
    /// sobreviver à edição ou exclusão do cadastro.</summary>
    public string UnidadeNome { get; set; } = string.Empty;

    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;

    public StatusVarredura Status { get; set; } = StatusVarredura.Pendente;

    /// <summary>Janela de datas consultada no SISREG (de hoje até hoje + dias à frente).</summary>
    public DateOnly JanelaInicio { get; set; }

    public DateOnly JanelaFim { get; set; }

    /// <summary>Combinações profissional × procedimento habilitadas — o denominador da cobertura.</summary>
    public int CombinacoesTotal { get; set; }

    public int CombinacoesFeitas { get; set; }

    /// <summary>Requisições HTTP gastas no SISREG. Alimenta o teto por unidade e o diagnóstico
    /// de quão perto do limite anti-robô a unidade está operando.</summary>
    public int Requisicoes { get; set; }

    /// <summary>Agendamentos únicos lidos (já deduplicados por nº de solicitação).</summary>
    public int RegistrosEncontrados { get; set; }

    public int Validos { get; set; }
    public int Invalidos { get; set; }
    public int JaExistiam { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    /// <summary>Quem disparou. NULL no disparo agendado — não há usuário-robô, a autoria é o
    /// <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }

    public string? CriadoPorNome { get; set; }
}
