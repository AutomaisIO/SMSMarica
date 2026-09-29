using FluentValidation;
using SMSMais.Core.UnidadesAtendimento.Dtos;

namespace SMSMais.Core.UnidadesAtendimento.Validators;

public sealed class SalvarUnidadeAtendimentoValidator : AbstractValidator<SalvarUnidadeAtendimentoRequest>
{
    public SalvarUnidadeAtendimentoValidator()
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Observacoes).MaximumLength(1000);

        // Destino de rota: sem endereço não há o que mostrar ao motorista nem o que geocodificar.
        RuleFor(u => u.Endereco).NotNull().WithMessage("Informe o endereço da unidade de atendimento.");
        When(u => u.Endereco is not null, () =>
        {
            RuleFor(u => u.Endereco!.Cep)
                .Must(cep => string.IsNullOrWhiteSpace(cep) || cep.Count(char.IsDigit) == 8)
                .WithMessage("CEP deve ter 8 dígitos.");
            RuleFor(u => u.Endereco!.Logradouro).NotEmpty().WithMessage("Informe o logradouro.").MaximumLength(200);
            RuleFor(u => u.Endereco!.Numero).MaximumLength(20);
            RuleFor(u => u.Endereco!.Complemento).MaximumLength(120);
            RuleFor(u => u.Endereco!.Bairro).NotEmpty().WithMessage("Informe o bairro.").MaximumLength(120);
            RuleFor(u => u.Endereco!.Cidade).NotEmpty().WithMessage("Informe a cidade.").MaximumLength(120);
            RuleFor(u => u.Endereco!.Uf).NotEmpty().WithMessage("Informe a UF.").Length(2);
            RuleFor(u => u.Endereco!.PontoReferencia).MaximumLength(200);
        });

        RuleFor(u => u.Latitude!).InclusiveBetween(-90, 90).When(u => u.Latitude is not null);
        RuleFor(u => u.Longitude!).InclusiveBetween(-180, 180).When(u => u.Longitude is not null);
        RuleFor(u => u)
            .Must(u => (u.Latitude is null) == (u.Longitude is null))
            .OverridePropertyName("localizacao")
            .WithMessage("Latitude e longitude vão juntas: informe as duas ou nenhuma.");
    }
}
