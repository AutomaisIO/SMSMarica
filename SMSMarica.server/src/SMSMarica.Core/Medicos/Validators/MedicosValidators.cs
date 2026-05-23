using FluentValidation;
using SMSMarica.Core.Medicos.Dtos;

namespace SMSMarica.Core.Medicos.Validators;

public sealed class CadastrarMedicoValidator : AbstractValidator<CadastrarMedicoRequest>
{
    public CadastrarMedicoValidator()
    {
        RuleFor(m => m.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(m => m.Cpf)
            .NotEmpty()
            .Must(c => c.All(char.IsDigit) && c.Length == 11)
            .WithMessage("CPF deve ter 11 dígitos.");
        RuleFor(m => m.Crm).NotEmpty().MaximumLength(15);
        RuleFor(m => m.UfCrm).NotEmpty().Length(2).WithMessage("UF do CRM deve ter 2 letras.");
        RuleFor(m => m.Especialidade).MaximumLength(120);
        RuleFor(m => m.Rqe).MaximumLength(20);
        RuleFor(m => m.Email).EmailAddress().MaximumLength(200).When(m => !string.IsNullOrWhiteSpace(m.Email));
        RuleFor(m => m.Telefone).MaximumLength(30);
    }
}

public sealed class AtualizarMedicoValidator : AbstractValidator<AtualizarMedicoRequest>
{
    public AtualizarMedicoValidator()
    {
        RuleFor(m => m.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(m => m.Crm).NotEmpty().MaximumLength(15);
        RuleFor(m => m.UfCrm).NotEmpty().Length(2);
        RuleFor(m => m.Especialidade).MaximumLength(120);
        RuleFor(m => m.Rqe).MaximumLength(20);
        RuleFor(m => m.Telefone).MaximumLength(30);
    }
}

public sealed class PromoverMedicoValidator : AbstractValidator<PromoverMedicoRequest>
{
    public PromoverMedicoValidator()
    {
        RuleFor(m => m.UsuarioId).NotEmpty();
        RuleFor(m => m.Crm).NotEmpty().MaximumLength(15);
        RuleFor(m => m.UfCrm).NotEmpty().Length(2);
        RuleFor(m => m.Especialidade).MaximumLength(120);
        RuleFor(m => m.Rqe).MaximumLength(20);
    }
}
