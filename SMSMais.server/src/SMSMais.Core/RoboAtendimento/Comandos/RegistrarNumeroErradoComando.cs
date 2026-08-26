using System.Text.Json;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Registra que o número não pertence ao paciente ("não sou essa pessoa"), com o vínculo declarado
/// (parente/responsável/sem vínculo). NÃO corrige cadastro — gera pendência para a recepção resolver.
/// </summary>
public sealed class RegistrarNumeroErradoComando(IPendenciaCadastroService pendencias) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.RegistrarNumeroErrado;
    public bool Idempotente => true;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:numero_errado";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var vinculo = LerVinculo(ctx.Args);
        var observacao = LerString(ctx.Args, "observacao");

        await pendencias.RegistrarNumeroErradoAsync(
            ctx.ConversaId, ctx.TelefoneCanonical, ctx.PacienteId, vinculo, observacao, criadoPor: null, ct);

        return new RoboComandoResultado(true,
            "Registrado. A equipe vai revisar o cadastro. Agradeça e não peça dados pessoais.");
    }

    private static VinculoContato LerVinculo(JsonElement args)
    {
        var texto = LerString(args, "vinculo");
        if (texto is not null && Enum.TryParse<VinculoContato>(texto, ignoreCase: true, out var v))
            return v;
        // Aceita também termos livres comuns.
        var t = (texto ?? string.Empty).ToLowerInvariant();
        if (t.Contains("respons")) return VinculoContato.Responsavel;
        if (t.Contains("parente") || t.Contains("famil") || t.Contains("mãe") || t.Contains("mae")
            || t.Contains("pai") || t.Contains("filh") || t.Contains("irm")) return VinculoContato.Parente;
        if (t.Contains("engano") || t.Contains("sem vínculo") || t.Contains("sem vinculo")
            || t.Contains("não conhe") || t.Contains("nao conhe")) return VinculoContato.SemVinculo;
        return VinculoContato.NaoInformado;
    }

    private static string? LerString(JsonElement args, string nome) =>
        args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(nome, out var el)
            && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
