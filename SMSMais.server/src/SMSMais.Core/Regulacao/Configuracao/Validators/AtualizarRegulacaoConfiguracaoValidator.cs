using System.Text.Json;
using System.Text.RegularExpressions;

using FluentValidation;

using SMSMais.Core.Regulacao.Configuracao.Dtos;

namespace SMSMais.Core.Regulacao.Configuracao.Validators;

public sealed class AtualizarRegulacaoConfiguracaoValidator
    : AbstractValidator<AtualizarRegulacaoConfiguracaoRequest>
{
    /// <summary>
    /// Tipos que o armazenamento aceita. Imagens entram porque o caminho real é foto de celular
    /// tirada no balcão — o `webp` inclusive, que é o que os Android novos geram por padrão.
    /// </summary>
    private static readonly string[] TiposAceitos =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    /// <summary>
    /// Categorias do classificador de follow-up, medidas no spike d sobre 18.904 eventos reais.
    /// São nove, não as quatro que o plano 09 supunha: as duas maiores (`SemVaga` e
    /// `ReclassificacaoRisco`) não estavam previstas, e `OrientacaoAoPaciente` precisou ser
    /// separada de `SolicitacaoAoSolicitante` — juntas, a fila de pendências nasceria com 5,5×
    /// itens falsos.
    /// </summary>
    private static readonly string[] CategoriasFollowUp =
    [
        "ReclassificacaoRisco", "FalhaContato", "ContatoRealizado", "CancelamentoOuReagendamento",
        "SolicitacaoAoSolicitante", "OrientacaoAoPaciente", "SemVaga", "Agendamento", "Outro",
    ];

    public AtualizarRegulacaoConfiguracaoValidator()
    {
        RuleFor(x => x.RotuloFila)
            .NotEmpty().WithMessage("Informe o rótulo da fila.")
            .MaximumLength(60);

        RuleFor(x => x.SisregPrazoEdicaoDias)
            .InclusiveBetween(0, 60)
            .WithMessage("O prazo de edição no SISREG deve ficar entre 0 e 60 dias.");

        RuleFor(x => x.BuscaCorteDistancia)
            .InclusiveBetween(0m, 1m)
            .WithMessage("O corte de distância é uma distância de cosseno: fica entre 0 e 1.");

        RuleFor(x => x.BuscaScoreSugestaoPareamento)
            .InclusiveBetween(0m, 1m)
            .WithMessage("O score de sugestão é uma semelhança de cosseno: fica entre 0 e 1.");

        RuleFor(x => x.AnexoLimiteMb)
            .InclusiveBetween(1, 50)
            .WithMessage("O limite de anexo deve ficar entre 1 e 50 MB.");

        RuleFor(x => x.AnexoTiposPermitidos)
            .NotEmpty().WithMessage("Escolha ao menos um tipo de arquivo aceito.")
            .Must(t => t.All(x => TiposAceitos.Contains(x)))
            .WithMessage($"Tipos aceitos: {string.Join(", ", TiposAceitos)}.");

        RuleFor(x => x.RegrasFollowup)
            .Must(SerListaDeRegrasValida)
            .When(x => x.RegrasFollowup.HasValue)
            .WithMessage(
                "As regras de follow-up devem ser uma lista de {categoria, regex, ordem}, com "
                + $"regex compilável e categoria entre: {string.Join(", ", CategoriasFollowUp)}.");
    }

    private static bool SerListaDeRegrasValida(JsonElement? elemento)
    {
        if (elemento is not { } e) return true;
        if (e.ValueKind != JsonValueKind.Array) return false;

        foreach (var item in e.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) return false;

            if (!item.TryGetProperty("categoria", out var cat)
                || cat.ValueKind != JsonValueKind.String
                || !CategoriasFollowUp.Contains(cat.GetString()))
            {
                return false;
            }

            if (!item.TryGetProperty("padrao", out var padrao) || padrao.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            // Regex que não compila salva sem erro e explode depois, no consumidor — em produção,
            // classificando follow-up de paciente. Falhar aqui é o barato.
            try
            {
                _ = Regex.Match(string.Empty, padrao.GetString()!, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        return true;
    }
}
