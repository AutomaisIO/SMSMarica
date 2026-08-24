using System.Text.Json;

namespace SMSMais.Core.EstudoAnotacoes.Dtos;

/// <summary>
/// Snapshot completo das anotações do estudo. O <see cref="Payload"/> é o JSON
/// devolvido por <c>annotationManager.state.getAllAnnotations()</c> do Cornerstone.
/// </summary>
public sealed record SalvarEstudoAnotacaoRequest(
    JsonElement Payload,
    string? Comentario
);
