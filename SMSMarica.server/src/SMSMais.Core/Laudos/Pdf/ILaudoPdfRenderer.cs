namespace SMSMais.Core.Laudos.Pdf;

public interface ILaudoPdfRenderer
{
    /// <summary>
    /// Gera o PDF do laudo identificado por <paramref name="laudoId"/>.
    /// Lança <see cref="Common.Excecoes.NaoEncontradoException"/> se não existir.
    /// </summary>
    /// <param name="modo">
    /// Estado visual do documento (tarja/marca d'água/ausência do bloco do
    /// médico). O default <see cref="ModoRodapeLaudo.FinalizadoNaoAssinado"/>
    /// cobre o download on-demand. Passe
    /// <see cref="ModoRodapeLaudo.PreparandoAssinatura"/> ao preparar o PDF-base
    /// para a assinatura digital. Laudos não finalizados são SEMPRE rebaixados
    /// para <see cref="ModoRodapeLaudo.Rascunho"/> (marca d'água), qualquer que
    /// seja o modo pedido.
    /// </param>
    Task<byte[]> GerarAsync(
        Guid laudoId,
        ModoRodapeLaudo modo = ModoRodapeLaudo.FinalizadoNaoAssinado,
        CancellationToken cancellationToken = default);
}
