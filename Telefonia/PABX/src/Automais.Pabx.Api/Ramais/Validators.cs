using FluentValidation;

namespace Automais.Pabx.Api.Ramais;

public sealed class CriarRamalValidator : AbstractValidator<CriarRamalRequest>
{
    public CriarRamalValidator()
    {
        RuleFor(r => r.Numero)
            .NotEmpty().WithMessage("Número do ramal é obrigatório.")
            .Matches("^[0-9]{3,6}$").WithMessage("Número do ramal deve ter de 3 a 6 dígitos.");

        RuleFor(r => r.UnidadeId).GreaterThanOrEqualTo(0);

        RuleFor(r => r.Mac)
            .Matches("^[0-9a-fA-F]{12}$")
            .When(r => !string.IsNullOrWhiteSpace(r.Mac))
            .WithMessage("MAC deve ter 12 dígitos hexadecimais (sem separadores).");

        RuleFor(r => r.Marca)
            .NotNull()
            .When(r => !string.IsNullOrWhiteSpace(r.Mac))
            .WithMessage("Informe a marca do telefone para gerar o provisionamento do MAC.");
    }
}

public sealed class AtualizarRamalValidator : AbstractValidator<AtualizarRamalRequest>
{
    public AtualizarRamalValidator()
    {
        RuleFor(r => r.UnidadeId).GreaterThanOrEqualTo(0);

        RuleFor(r => r.Mac)
            .Matches("^[0-9a-fA-F]{12}$")
            .When(r => !string.IsNullOrWhiteSpace(r.Mac))
            .WithMessage("MAC deve ter 12 dígitos hexadecimais (sem separadores).");

        RuleFor(r => r.Marca)
            .NotNull()
            .When(r => !string.IsNullOrWhiteSpace(r.Mac))
            .WithMessage("Informe a marca do telefone para gerar o provisionamento do MAC.");
    }
}

public sealed class AdotarRamaisValidator : AbstractValidator<AdotarRamaisRequest>
{
    public AdotarRamaisValidator()
    {
        RuleFor(r => r.Numeros).NotEmpty().WithMessage("Informe ao menos um número para adotar.");
        RuleFor(r => r.UnidadeId).GreaterThanOrEqualTo(0);
    }
}
