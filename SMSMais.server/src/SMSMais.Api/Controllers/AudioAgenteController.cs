using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Core.Armazenamento;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Serve, publicamente e por pouco tempo, o áudio (TTS) que o Agente IA devolve pelo WhatsApp. A
/// Meta busca este link para montar a nota de voz. Sem autenticação de propósito (quem baixa é a
/// Meta); a proteção é o token imprevisível (GUID) e a rotação por idade/tamanho no servidor.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class AudioAgenteController(
    IArmazenamentoAudioTemporario audio, IArmazenamentoDocumentoPublico documentos) : ControllerBase
{
    /// <summary>Nota de voz (TTS) que o Agente IA devolve — a Meta busca este link.</summary>
    [HttpGet("publico/audio-agente/{token}")]
    public async Task<IActionResult> Audio(string token, CancellationToken cancellationToken)
    {
        var bytes = await audio.LerAsync(token, cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "audio/ogg");
    }

    /// <summary>Documento (PDF, planilha, etc.) que o Agente IA gera e entrega — a Meta busca este link.</summary>
    [HttpGet("publico/arquivo-agente/{token}")]
    public async Task<IActionResult> Arquivo(string token, CancellationToken cancellationToken)
    {
        var doc = await documentos.LerAsync(token, cancellationToken);
        if (doc is null) return NotFound();
        return File(doc.Conteudo, doc.ContentType);
    }
}
