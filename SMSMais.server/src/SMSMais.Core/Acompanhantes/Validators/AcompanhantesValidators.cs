using FluentValidation;
using SMSMais.Core.Acompanhantes.Dtos;
using SMSMais.Core.Common.Documentos;

namespace SMSMais.Core.Acompanhantes.Validators;

public sealed class ConsultarAcompanhanteValidator : AbstractValidator<ConsultarAcompanhanteRequest>
{
    public ConsultarAcompanhanteValidator()
    {
        RuleFor(r => r.Cpf).Must(CpfBr.EhValido).WithMessage("CPF inválido. Confira os números.");
        RuleFor(r => r.DataNascimento).NascimentoPlausivel();
    }
}

public sealed class AdicionarAcompanhanteValidator : AbstractValidator<AdicionarAcompanhanteRequest>
{
    public AdicionarAcompanhanteValidator()
    {
        RuleFor(r => r.Cpf).Must(CpfBr.EhValido).WithMessage("CPF inválido. Confira os números.");
        RuleFor(r => r.DataNascimento).NascimentoPlausivel();
        RuleFor(r => r.Parentesco).IsInEnum().When(r => r.Parentesco is not null);
        RuleFor(r => r.Telefone).MaximumLength(20);
    }
}

internal static class NascimentoRegra
{
    public static IRuleBuilderOptions<T, DateOnly> NascimentoPlausivel<T>(this IRuleBuilder<T, DateOnly> regra) =>
        regra
            .Must(d => d.Year >= 1900 && d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Data de nascimento inválida.");
}
