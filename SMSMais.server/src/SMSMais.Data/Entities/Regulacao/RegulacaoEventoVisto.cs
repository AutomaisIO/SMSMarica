namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Marca que um usuário já viu aquele evento (plano 05).
///
/// <para><b>Por que "visto" é por usuário e não uma coluna no evento:</b> a mesma movimentação
/// interessa a várias pessoas — quem abriu o pedido na unidade e o agente que o acompanha. Uma
/// flag no evento faria o primeiro que abrisse a tela apagar o aviso de todos os outros.</para>
///
/// <para>Ausência de linha = não vista. Não existe "desmarcar como vista": a leitura é um fato,
/// e desfazer só serviria para esconder que alguém já sabia.</para>
/// </summary>
public sealed class RegulacaoEventoVisto
{
    public Guid EventoId { get; set; }
    public RegulacaoEvento? Evento { get; set; }

    public Guid UsuarioId { get; set; }

    public DateTime VistoEm { get; set; }
}
