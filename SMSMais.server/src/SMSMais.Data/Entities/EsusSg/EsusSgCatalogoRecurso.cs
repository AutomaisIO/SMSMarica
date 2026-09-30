namespace SMSMais.Data.Entities.EsusSg;

/// <summary>
/// Um procedimento que Maricá pode pedir ao ESUS de São Gonçalo — lido do combo
/// "procedimentos reguláveis por solicitante" do próprio ESUS (medido 30/09/2026: 14 de exame,
/// todos PPI). Vira origem do catálogo canônico (ADR-0055) com
/// <c>SistemaRegulacao.EsusSg</c>, e é o que permite às regras valerem para os pedidos do SG.
///
/// <para>Chave natural (<see cref="Tipo"/>, <see cref="Valor"/>) — o <c>data</c> do combo, id do
/// procedimento no ESUS. O pedido da fila só traz o NOME; a ligação pedido→recurso é por
/// <see cref="Rotulo"/> normalizado.</para>
/// </summary>
public class EsusSgCatalogoRecurso
{
    public Guid Id { get; set; }

    public TipoRecursoEsusSg Tipo { get; set; } = TipoRecursoEsusSg.Exame;

    /// <summary>Id do procedimento no ESUS (o <c>data</c> do combo).</summary>
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Some do combo → inativa, nunca apaga (pedido antigo continua apontando).</summary>
    public bool Ativo { get; set; } = true;

    public DateTime SincronizadoEm { get; set; }
}
