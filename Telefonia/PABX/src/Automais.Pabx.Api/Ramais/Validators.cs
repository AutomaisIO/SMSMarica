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

        RuleFor(r => r.CallerId).CallerIdSeguro();

        RuleFor(r => r.Mac)
            .Empty()
            .When(r => r.Tipo == Data.Entities.TipoRamal.Softphone)
            .WithMessage("Softphone não tem aparelho: não informe MAC.");

        RuleFor(r => r.DonoId)
            .NotEmpty()
            .When(r => !string.IsNullOrWhiteSpace(r.DonoSistema))
            .WithMessage("Informe o id do dono junto com o sistema.");
        RuleFor(r => r.DonoSistema).MaximumLength(40);
        RuleFor(r => r.DonoId).MaximumLength(80);
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

        RuleFor(r => r.CallerId).CallerIdSeguro();
    }
}

public sealed class AtualizarConfigRamalValidator : AbstractValidator<AtualizarConfigRamalRequest>
{
    public AtualizarConfigRamalValidator(Microsoft.Extensions.Options.IOptions<Asterisk.AsteriskOptions> options)
    {
        var opcoes = options.Value;

        RuleFor(r => r.Contexto)
            .NotEmpty()
            .Must(c => opcoes.ContextosPermitidos.Contains(c))
            .WithMessage(r => $"Contexto '{r.Contexto}' não permitido. Use: {string.Join(", ", opcoes.ContextosPermitidos)}.");

        RuleFor(r => r.Codecs)
            .NotEmpty().WithMessage("Informe ao menos um codec.");
        RuleForEach(r => r.Codecs)
            .Must(c => opcoes.CodecsPermitidos.Contains(c.Trim().ToLowerInvariant()))
            .WithMessage((_, c) => $"Codec '{c}' não permitido. Use: {string.Join(", ", opcoes.CodecsPermitidos)}.");

        RuleFor(r => r.CallLimit).InclusiveBetween(1, 10);
    }
}

public sealed class DefinirDonoValidator : AbstractValidator<DefinirDonoRequest>
{
    public DefinirDonoValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty()
            .When(r => !string.IsNullOrWhiteSpace(r.Sistema))
            .WithMessage("Informe o id do dono junto com o sistema.");
        RuleFor(r => r.Sistema)
            .NotEmpty()
            .When(r => !string.IsNullOrWhiteSpace(r.Id))
            .WithMessage("Informe o sistema junto com o id do dono.");
        RuleFor(r => r.Sistema).MaximumLength(40);
        RuleFor(r => r.Id).MaximumLength(80);
    }
}

internal static class RegrasRamal
{
    /// <summary>
    /// O caller-id é escrito cru no sip_smsmarica.conf: quebra de linha, colchete, ponto-e-vírgula
    /// ou aspas abririam uma seção nova ou cortariam o valor. Só texto de visor.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> CallerIdSeguro<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .MaximumLength(60)
            .Matches(@"^[^\r\n\[\];""<>=]*$")
            .WithMessage("Nome de exibição não pode conter quebra de linha nem os caracteres [ ] ; \" < > =.");
}

public sealed class AdotarRamaisValidator : AbstractValidator<AdotarRamaisRequest>
{
    public AdotarRamaisValidator()
    {
        RuleFor(r => r.Numeros).NotEmpty().WithMessage("Informe ao menos um número para adotar.");
        RuleFor(r => r.UnidadeId).GreaterThanOrEqualTo(0);
    }
}
