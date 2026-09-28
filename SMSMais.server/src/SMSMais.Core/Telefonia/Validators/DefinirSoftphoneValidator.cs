using FluentValidation;
using SMSMais.Core.Telefonia.Dtos;

namespace SMSMais.Core.Telefonia.Validators;

public sealed class DefinirSoftphoneValidator : AbstractValidator<DefinirSoftphoneRequest>
{
    public DefinirSoftphoneValidator()
    {
        RuleFor(r => r.Ramal)
            .NotEmpty().WithMessage("Informe o número do ramal.")
            .Matches("^[0-9]{3,6}$").WithMessage("O ramal tem de 3 a 6 dígitos.");

        // O nome vai cru para o arquivo de configuração do Asterisk: quebra de linha ou colchete
        // abririam uma seção nova. Mesma regra do Pabx, repetida aqui para o erro sair no campo.
        RuleFor(r => r.NomeExibicao)
            .NotEmpty().WithMessage("Informe o nome que aparece para quem recebe a ligação.")
            .MaximumLength(60)
            .Matches(@"^[^\r\n\[\];""<>=]*$")
            .WithMessage("O nome não pode ter quebra de linha nem os caracteres [ ] ; \" < > =.");
    }
}
