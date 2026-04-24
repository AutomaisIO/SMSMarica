using FluentValidation;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes.Validators;

public sealed class CadastrarPacienteValidator : AbstractValidator<CadastrarPacienteRequest>
{
    public CadastrarPacienteValidator()
    {
        RuleFor(p => p.NomeCompleto)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MaximumLength(200);

        RuleFor(p => p.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(c => SoDigitos(c) && c.Length == 11)
            .WithMessage("CPF deve ter 11 dígitos.")
            .When(p => p.Cpf is not null);

        RuleFor(p => p.DataNascimento)
            .Must(d => d > DateOnly.FromDateTime(new DateTime(1900, 1, 1)) &&
                       d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Data de nascimento inválida.");

        RuleFor(p => p.Cns)
            .Must(c => c is null || (SoDigitos(c) && c.Length == 15))
            .WithMessage("CNS deve ter 15 dígitos quando informado.");

        RuleFor(p => p.Email)
            .EmailAddress().When(p => !string.IsNullOrWhiteSpace(p.Email))
            .WithMessage("E-mail inválido.");

        RuleFor(p => p.AlturaCm)
            .InclusiveBetween(30, 250).When(p => p.AlturaCm.HasValue)
            .WithMessage("Altura deve estar entre 30 e 250 cm.");

        RuleFor(p => p.PesoKg)
            .InclusiveBetween(1m, 500m).When(p => p.PesoKg.HasValue)
            .WithMessage("Peso deve estar entre 1 e 500 kg.");

        When(p => p.Endereco is not null, () =>
        {
            RuleFor(p => p.Endereco!.Cep).Must(c => SoDigitos(c) && c.Length == 8)
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

    private static bool SoDigitos(string valor) => !string.IsNullOrEmpty(valor) && valor.All(char.IsDigit);
}
