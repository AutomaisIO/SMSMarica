using FluentValidation;

namespace SMSMarica.Core.Medicos.Assinatura;

public sealed class SalvarAssinaturaMedicoValidator : AbstractValidator<SalvarAssinaturaMedicoRequest>
{
    // ~1.4 MB de base64 ≈ 1 MB de imagem — folga de sobra para um PNG 800×800 de rubrica.
    private const int MaxBase64 = 1_400_000;

    public SalvarAssinaturaMedicoValidator()
    {
        RuleFor(x => x.Formato).IsInEnum();

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(c => c is "image/png" or "image/jpeg")
            .WithMessage("A assinatura deve ser PNG ou JPEG.");

        RuleFor(x => x.ImagemBase64)
            .NotEmpty().WithMessage("Imagem da assinatura é obrigatória.")
            .MaximumLength(MaxBase64).WithMessage("Imagem muito grande.")
            .Must(SerImagemDataUrlValida).WithMessage("Imagem da assinatura inválida.");
    }

    /// <summary>Aceita data URL (data:image/...;base64,XXXX) ou base64 puro decodificável.</summary>
    private static bool SerImagemDataUrlValida(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        var dados = valor;
        var virgula = valor.IndexOf(',');
        if (valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && virgula > 0)
        {
            dados = valor[(virgula + 1)..];
        }

        try
        {
            _ = Convert.FromBase64String(dados);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
