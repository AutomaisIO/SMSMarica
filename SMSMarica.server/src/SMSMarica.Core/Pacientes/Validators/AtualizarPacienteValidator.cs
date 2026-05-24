using FluentValidation;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes.Validators;

public sealed class AtualizarPacienteValidator : AbstractValidator<AtualizarPacienteRequest>
{
    public AtualizarPacienteValidator()
    {
        RuleFor(p => p.Cns)
            .Must(c => c is null || (c.All(char.IsDigit) && c.Length == 15))
            .WithMessage("CNS deve ter 15 dígitos quando informado.");

        RuleFor(p => p.Email)
            .EmailAddress().When(p => !string.IsNullOrWhiteSpace(p.Email))
            .WithMessage("E-mail inválido.");

        RuleFor(p => p.AlturaCm)
            .InclusiveBetween(30, 250).When(p => p.AlturaCm.HasValue);

        RuleFor(p => p.PesoKg)
            .InclusiveBetween(1m, 500m).When(p => p.PesoKg.HasValue);

        When(p => p.Endereco is not null, () =>
        {
            RuleFor(p => p.Endereco!.Cep).Must(c => c.All(char.IsDigit) && c.Length == 8)
                .WithMessage("CEP deve ter 8 dígitos.");
            RuleFor(p => p.Endereco!.Logradouro).NotEmpty().MaximumLength(200);
            RuleFor(p => p.Endereco!.Bairro).NotEmpty().MaximumLength(120);
            RuleFor(p => p.Endereco!.Cidade).NotEmpty().MaximumLength(120);
            RuleFor(p => p.Endereco!.Uf).NotEmpty().Length(2);
        });

        When(p => p.ContatoEmergencia is not null, () =>
        {
            RuleFor(p => p.ContatoEmergencia!.Nome).NotEmpty().MaximumLength(200);
            RuleFor(p => p.ContatoEmergencia!.Telefone).NotEmpty().MaximumLength(20);
        });
    }
}
