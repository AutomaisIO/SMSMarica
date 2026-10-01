using FluentValidation;
using SMSMais.Core.Motoristas.Dtos;

namespace SMSMais.Core.Motoristas.Validators;

public sealed class CadastrarMotoristaValidator : AbstractValidator<CadastrarMotoristaRequest>
{
    public CadastrarMotoristaValidator()
    {
        RuleFor(m => m.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(m => m.Cpf)
            .NotEmpty()
            .Must(c => c.All(char.IsDigit) && c.Length == 11)
            .WithMessage("CPF deve ter 11 dígitos.");
        RuleFor(m => m.Cnh)
            .NotEmpty()
            .MaximumLength(11);
        RuleFor(m => m.Email).EmailAddress().MaximumLength(200).When(m => !string.IsNullOrWhiteSpace(m.Email));
        RuleFor(m => m.Telefone).MaximumLength(30);
        RuleFor(m => m.CategoriaCnh).Must(CategoriasCnh.Valida).WithMessage(CategoriasCnh.Mensagem);
        RuleFor(m => m.RegimeContratacao).IsInEnum();
    }
}

public sealed class AtualizarMotoristaValidator : AbstractValidator<AtualizarMotoristaRequest>
{
    public AtualizarMotoristaValidator()
    {
        RuleFor(m => m.Cnh).NotEmpty().MaximumLength(11);
        RuleFor(m => m.Telefone).MaximumLength(30);
        RuleFor(m => m.CategoriaCnh).Must(CategoriasCnh.Valida).WithMessage(CategoriasCnh.Mensagem);
        RuleFor(m => m.RegimeContratacao).IsInEnum();
    }
}

public sealed class PromoverMotoristaValidator : AbstractValidator<PromoverMotoristaRequest>
{
    public PromoverMotoristaValidator()
    {
        RuleFor(m => m.UsuarioId).NotEmpty();
        RuleFor(m => m.Cnh).NotEmpty().MaximumLength(11);
        RuleFor(m => m.CategoriaCnh).Must(CategoriasCnh.Valida).WithMessage(CategoriasCnh.Mensagem);
        RuleFor(m => m.RegimeContratacao).IsInEnum();
    }
}

/// <summary>Categorias de CNH aceitas (CTB art. 143). Vazio = não informada.</summary>
internal static class CategoriasCnh
{
    private static readonly HashSet<string> Aceitas = ["A", "B", "C", "D", "E", "AB", "AC", "AD", "AE"];

    public const string Mensagem = "Categoria da CNH deve ser A, B, C, D, E, AB, AC, AD ou AE.";

    public static bool Valida(string? categoria) =>
        string.IsNullOrWhiteSpace(categoria) || Aceitas.Contains(categoria.Trim().ToUpperInvariant());
}
