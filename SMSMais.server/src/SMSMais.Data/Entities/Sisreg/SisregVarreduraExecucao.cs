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

    /// <summary>
    /// Última prova de vida do processo que roda esta execução — carimbada por um relógio próprio,
    /// a cada meio minuto, <b>independente da fase</b>.
    ///
    /// <para><b>Por que não bastava <see cref="IniciadoEm"/>.</b> Decidir "abandonada" pela IDADE
    /// não distingue corrida <b>longa e saudável</b> de corrida <b>morta</b>: como uma varredura
    /// legítima leva 20–45 minutos, o corte tinha de ser generoso (45 min), e o preço era uma
    /// execução morta anunciando "Rodando" por até 45 minutos — ou <b>para sempre</b>, já que a
    /// faxina só acontecia quando alguém iniciava a próxima varredura daquela unidade. Em
    /// 08/09/2026 uma execução do CDT ficou 65 minutos assim depois de dois deploys reiniciarem o
    /// serviço embaixo dela.</para>
    ///
    /// <para><b>Por que ela é escrita mesmo sem progresso.</b> A fase de pré-carga do SER não grava
    /// progresso de propósito (seriam milhares de escritas), então uma corrida viva parecia
    /// congelada. O batimento é uma linha por meio minuto e não depende de haver avanço: separa
    /// "não terminou" de "não responde", que são coisas diferentes para quem olha a tela.</para>
    ///
    /// <para>Null nas execuções anteriores à coluna — quem lê deve cair no critério antigo, a
    /// idade, em vez de tratar ausência de batimento como morte.</para>
    /// </summary>
    public DateTime? UltimoSinalEm { get; set; }

    /// <summary>Quem disparou. NULL no disparo agendado — não há usuário-robô, a autoria é o
    /// <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }

    public string? CriadoPorNome { get; set; }
}
