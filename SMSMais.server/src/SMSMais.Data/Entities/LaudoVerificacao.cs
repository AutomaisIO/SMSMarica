namespace SMSMais.Data.Entities;

/// <summary>
/// Selo de verificação do laudo (ADR-0061). O <see cref="Id"/> é o código público impresso
/// no QR Code do rodapé: quem lê o QR cai numa página pública que diz se o laudo é válido e
/// oferece o download do PDF oficial. Um selo por laudo, criado quando o PDF-base de
/// assinatura é gerado (o código precisa existir ANTES de o PDF ser assinado, porque a
/// assinatura trava os bytes).
///
/// <para>
/// O código é um UUID v4 (122 bits aleatórios) e não o v7 da declaração de comparecimento:
/// o laudo carrega dado clínico e o v7 expõe o instante de criação nos primeiros bits.
/// </para>
/// </summary>
public class LaudoVerificacao
{
    /// <summary>Código público (selo) referenciado no QR Code.</summary>
    public Guid Id { get; set; }

    public Guid LaudoId { get; set; }
    public Laudo? Laudo { get; set; }

    public DateTime CriadoEm { get; set; }
}
