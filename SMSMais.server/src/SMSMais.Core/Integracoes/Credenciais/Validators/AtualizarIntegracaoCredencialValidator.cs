using System.Text.Json;
using FluentValidation;
using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Core.Integracoes.Credenciais.Validators;

/// <summary>
/// Valida a atualização de credenciais de provedor. Não valida client_id/secret
/// (formato livre por provedor); garante apenas que o RedirectUri seja uma URL
/// http(s) absoluta e que ParametrosJson, se informado, seja um JSON válido
/// (a coluna é jsonb — JSON inválido falharia no banco).
/// </summary>
public sealed class AtualizarIntegracaoCredencialValidator : AbstractValidator<AtualizarIntegracaoCredencialRequest>
{
    public AtualizarIntegracaoCredencialValidator()
    {
        RuleFor(x => x.RedirectUri)
            .Must(SerUriHttpAbsoluta)
            .When(x => !string.IsNullOrWhiteSpace(x.RedirectUri))
            .WithMessage("Redirect URI deve ser uma URL http(s) absoluta válida.");

        RuleFor(x => x.ParametrosJson)
            .Must(SerJsonValido)
            .When(x => !string.IsNullOrWhiteSpace(x.ParametrosJson))
            .WithMessage("Parâmetros adicionais devem ser um JSON válido.");
    }

    private static bool SerUriHttpAbsoluta(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    private static bool SerJsonValido(string? json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
