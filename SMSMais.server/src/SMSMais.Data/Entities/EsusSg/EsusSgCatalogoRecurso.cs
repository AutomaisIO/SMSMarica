namespace SMSMais.Data.Entities.EsusSg;

/// <summary>
/// Um procedimento que Maricá pode pedir ao ESUS de São Gonçalo — lido do combo
/// "procedimentos reguláveis por solicitante" do próprio ESUS (medido 30/09/2026: 14 de exame,
/// todos PPI). Vira origem do catálogo canônico (ADR-0055) com
/// <c>SistemaRegulacao.EsusSg</c>, e é o que permite às regras valerem para os pedidos do SG.
///
/// <para>Identidade (<see cref="Tipo"/>, <see cref="RotuloChave"/>) — o NOME, como no SER e no
/// SERNIT (07/10/2026). O <see cref="Valor"/> (o <c>data</c> do combo) fica como atributo: o pedido
/// da fila só traz o nome, e número de combo de sistema de terceiro não é identidade nossa.</para>
/// </summary>
public class EsusSgCatalogoRecurso
{
    public Guid Id { get; set; }

    public TipoRecursoEsusSg Tipo { get; set; } = TipoRecursoEsusSg.Exame;

    /// <summary>Id do procedimento no ESUS (o <c>data</c> do combo).</summary>
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary>
    /// <b>A identidade do recurso</b>: o rótulo normalizado (sem acento, pontuação nem espaço
    /// duplo, em maiúsculas — <c>ChaveRotulo.Normalizar</c>). Único por tipo. Nulo só em
    /// linha antiga que a cópia ainda não consolidou.
    /// </summary>
    public string? RotuloChave { get; set; }

    /// <summary>Some do combo → inativa, nunca apaga (pedido antigo continua apontando).</summary>
    public bool Ativo { get; set; } = true;

    public DateTime SincronizadoEm { get; set; }
}
