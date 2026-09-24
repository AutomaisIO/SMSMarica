namespace Automais.Assinador.Core.Pades;

/// <summary>
/// Assinatura PAdES diferida (two-step) com iText. A chave privada vive fora deste
/// serviço (agente local → VIDaaS Connect → HSM da VALID); aqui só montamos e colamos.
///   1. <see cref="Preparar"/> reserva o placeholder, aplica o carimbo visual e devolve
///      o hash a ser assinado + um estado opaco.
///   2. o cliente assina o hash.
///   3. <see cref="Concluir"/> embute o CMS no PDF preparado.
/// </summary>
public interface IPadesSigner
{
    PreparacaoResultado Preparar(PreparacaoRequisicao requisicao);

    ConclusaoResultado Concluir(ConclusaoRequisicao requisicao);

    /// <summary>
    /// Estampa o carimbo como conteúdo da página, sem campo de assinatura e sem CMS — para o
    /// médico que não tem certificado digital. Mesma página/retângulo do carimbo da
    /// assinatura; a imagem mantém a proporção dentro do retângulo.
    /// </summary>
    byte[] Carimbar(byte[] pdf, byte[] carimboPng, CarimboPosicao? posicao);
}
