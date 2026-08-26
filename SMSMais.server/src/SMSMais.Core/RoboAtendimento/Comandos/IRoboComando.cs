using System.Text.Json;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>Contexto de execução de um comando do robô (fixado pela conversa; args vêm do modelo).</summary>
public sealed record RoboComandoContexto(
    Guid ConversaId,
    Guid? PacienteId,
    Guid? AssuntoId,
    string TelefoneCanonical,
    JsonElement Args);

/// <summary>Resultado de um comando. <see cref="Mensagem"/> é o texto que volta AO MODELO (não ao cidadão).</summary>
public sealed record RoboComandoResultado(bool Sucesso, string Mensagem, object? Dados = null);

/// <summary>
/// Um comando executável pelo robô — o "guichê" tipado. É código, não dado: só existe aqui e só
/// roda se o assunto o habilitou. Cada comando faz UMA coisa estreita, com gate/minimização no
/// servidor; o robô nunca toca o banco livremente.
/// </summary>
public interface IRoboComando
{
    ComandoRobo Comando { get; }

    /// <summary>Se true, o dispatcher trava por <see cref="ChaveIdempotencia"/> (não reexecuta).</summary>
    bool Idempotente { get; }

    /// <summary>Chave estável para idempotência (usada quando <see cref="Idempotente"/>).</summary>
    string ChaveIdempotencia(RoboComandoContexto ctx);

    Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct);
}
