using System.Text.Json;
using FluentValidation;
using SMSMais.Core.Anamneses.Dtos;

namespace SMSMais.Core.Anamneses.Validators;

public sealed class SalvarAnamneseValidator : AbstractValidator<SalvarAnamneseDto>
{
    private static readonly string[] ClassificacoesValidas = ["Baixo", "Moderado", "Alto"];

    public SalvarAnamneseValidator()
    {
        RuleFor(x => x.Tipo)
            .NotEmpty().WithMessage("Tipo do questionário é obrigatório.")
            .MaximumLength(40);

        RuleFor(x => x.Versao)
            .GreaterThanOrEqualTo(1).WithMessage("Versão deve ser >= 1.");

        RuleFor(x => x.ConteudoJson)
            .NotEmpty().WithMessage("Conteúdo da anamnese é obrigatório.")
            .Must(SerJsonValido).WithMessage("Conteúdo da anamnese não é um JSON válido.");

        RuleFor(x => x.ClassificacaoRisco)
            .Must(c => c is null || ClassificacoesValidas.Contains(c))
            .WithMessage("Classificação de risco deve ser Baixo, Moderado ou Alto.");
    }

    private static bool SerJsonValido(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
