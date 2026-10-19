using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado;

/// <summary>
/// Por onde o transporte fala com o paciente fora do app aberto — o push do app do cidadão (ADR-0070),
/// quando ele chegar ao main. Sem canal ligado, as duas coisas aparecem no app e no PWA quando o paciente
/// abre a tela do Transporte, com os mesmos botões (decisões de 19/10):
/// <list type="bullet">
/// <item>a pergunta "você já está disponível para retorno?", ao estourar a previsão do procedimento;</item>
/// <item>a avaliação do motorista (até 5 estrelas), ao desembarcar na unidade e ao chegar em casa.</item>
/// </list>
/// Sem dado de saúde no texto (aparece na tela bloqueada): só o primeiro nome.
/// </summary>
public interface IAvisoAoPacienteTransporte
{
    /// <summary>Verdadeiro quando entregou (ex.: o push saiu para algum aparelho).</summary>
    Task<bool> PerguntarSeDisponivelAsync(Guid pacienteId, string primeiroNome, Guid sessaoId, CancellationToken ct = default);

    /// <summary>"Como foi o motorista?" logo depois do desembarque do <paramref name="trecho"/>.</summary>
    Task<bool> PedirAvaliacaoAsync(
        Guid pacienteId, string primeiroNome, Guid sessaoId, TrechoViagem trecho, string? motorista,
        CancellationToken ct = default);
}

/// <summary>"ANA SIMULADA TESTE" → "Ana": o que vai num aviso que aparece na tela bloqueada.</summary>
internal static class NomeNoAviso
{
    public static string PrimeiroNome(string? nome)
    {
        var p = (nome ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return p.Length == 0 ? string.Empty : char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant();
    }
}

/// <summary>Enquanto o push do app do cidadão não está ligado: fica só no app/PWA.</summary>
public sealed class AvisoAoPacienteSoNoApp : IAvisoAoPacienteTransporte
{
    public Task<bool> PerguntarSeDisponivelAsync(Guid pacienteId, string primeiroNome, Guid sessaoId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> PedirAvaliacaoAsync(
        Guid pacienteId, string primeiroNome, Guid sessaoId, TrechoViagem trecho, string? motorista,
        CancellationToken ct = default) =>
        Task.FromResult(false);
}
