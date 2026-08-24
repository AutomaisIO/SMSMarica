using FluentValidation;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Medicos.Assinatura;

public sealed class SalvarAssinaturaMedicoValidator : AbstractValidator<SalvarAssinaturaMedicoRequest>
{
    // ~1.4 MB de base64 ≈ 1 MB de imagem — folga de sobra para um PNG 800×800 de rubrica.
    private const int MaxBase64 = 1_400_000;

    // Tolerância de ±5% na proporção (o recorte do client nunca é pixel-perfeito).
    private const double Tolerancia = 0.05;

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
            .Must(v => DecodificarOuNull(v) is not null).WithMessage("Imagem da assinatura inválida.");

        // A proporção real dos pixels precisa casar com o formato declarado:
        // Quadrada ⇒ ~1:1, Horizontal ⇒ ~2:1. Sem isso, o carimbo do PDF sai
        // distorcido/cortado. Quando as dimensões não são legíveis, não bloqueia
        // (as regras de MIME/base64 acima já barram imagem inválida).
        RuleFor(x => x)
            .Must(ProporcaoCompativel)
            .WithMessage("A proporção da imagem não corresponde ao formato escolhido " +
                         "(use 2:1 para faixa horizontal ou 1:1 para quadrada).");
    }

    private static bool ProporcaoCompativel(SalvarAssinaturaMedicoRequest req)
    {
        var bytes = DecodificarOuNull(req.ImagemBase64);
        if (bytes is null) return true; // outras regras tratam imagem inválida

        if (DimensaoImagem.Ler(bytes) is not { } d || d.Altura <= 0)
            return true; // formato não reconhecido → não bloqueia por proporção

        var razao = (double)d.Largura / d.Altura;
        var alvo = req.Formato == FormatoAssinaturaMedico.Horizontal ? 2.0 : 1.0;
        return Math.Abs(razao - alvo) <= alvo * Tolerancia;
    }

    /// <summary>Decodifica data URL (data:image/...;base64,XXXX) ou base64 puro; null se inválido.</summary>
    private static byte[]? DecodificarOuNull(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var dados = valor;
        var virgula = valor.IndexOf(',');
        if (valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && virgula > 0)
        {
            dados = valor[(virgula + 1)..];
        }

        try
        {
            return Convert.FromBase64String(dados);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
