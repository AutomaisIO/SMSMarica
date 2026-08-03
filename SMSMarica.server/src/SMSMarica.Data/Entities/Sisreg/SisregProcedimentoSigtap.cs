namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>
/// De-para entre o código de procedimento do SISREG (o <c>pa</c>, 7 dígitos, ex.: <c>1305007</c>)
/// e o código SIGTAP oficial (10 dígitos). <b>Catálogo global</b>, chaveado pelo <c>pa</c>.
///
/// <para><b>Por que isso existe:</b> a varredura da agenda (<c>cons_agendas</c>) devolve o
/// <c>pa</c> e o nome do procedimento — nunca o SIGTAP. E sem SIGTAP a solicitação nasce com
/// categoria <c>Outro</c>, sem satélite de imagem, sem worklist e sem PACS — <b>marcada como
/// sucesso</b>. Seria lixo silencioso em escala de centenas por dia. Por isso o de-para é um gate:
/// procedimento habilitado sem SIGTAP confirmado não entra na varredura.</para>
///
/// <para><b>Por que global e não por par profissional×procedimento:</b> o <c>pa</c> é
/// identificador nacional do SISREG III. Uma coluna por par obrigaria o operador a decidir 256
/// vezes no CDT (para ~40 <c>pa</c> distintos) e a refazer tudo em cada unidade nova, além de
/// permitir que o mesmo <c>pa</c> ficasse mapeado para dois SIGTAPs diferentes. Se a premissa
/// se mostrar errada, o sinal é o log <c>SISREG_PA_DIVERGENTE</c>.</para>
/// </summary>
public class SisregProcedimentoSigtap
{
    public Guid Id { get; set; }

    /// <summary>O <c>pa</c> do SISREG. Chave natural — índice único.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Descrição como o SISREG a exibe, na última vez que foi vista.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Código terminado em <c>000</c>: é um "GRUPO - X" que agrega itens individuais.</summary>
    public bool Grupo { get; set; }

    /// <summary>Procedimento SIGTAP confirmado pelo operador. NULL = ainda não mapeado.</summary>
    public Guid? ProcedimentoSigtapId { get; set; }

    /// <summary>
    /// Código SIGTAP <b>só dígitos</b>, desnormalizado — é o valor que vai para a marcação e que
    /// <c>CategoriaSigtap.Resolver</c> consome. O catálogo guarda o código formatado
    /// (<c>"02.04.03.018-8"</c>), então a conversão acontece aqui, uma vez, e não a cada leitura.
    /// </summary>
    public string? CodigoSigtap { get; set; }

    /// <summary>Sugestão automática por semelhança de nome, ainda NÃO confirmada. A varredura
    /// ignora sugestão — só o confirmado vale.</summary>
    public Guid? SugeridoSigtapId { get; set; }

    /// <summary>Confiança da sugestão, 0..1. Só há auto-confirmação em igualdade exata.</summary>
    public float? SugeridoScore { get; set; }

    // O flag de "avisar o paciente por WhatsApp" NÃO mora aqui: é decisão de cada unidade sobre
    // cada procedimento DELA, e vive em SisregProcedimentoProfissional.EnviarConfirmacao. Este
    // catálogo é só o de-para de código, que é mesmo nacional.

    public DateTime? ConfirmadoEm { get; set; }
    public Guid? ConfirmadoPor { get; set; }

    public DateTime PrimeiroVistoEm { get; set; }
    public DateTime VistoEm { get; set; }

    public ProcedimentoSigtap? ProcedimentoSigtap { get; set; }
}
