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
[Route("publico/audio-agente")]
public sealed class AudioAgenteController(IArmazenamentoAudioTemporario armazenamento) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Obter(string token, CancellationToken cancellationToken)
    {
        var bytes = await armazenamento.LerAsync(token, cancellationToken);
        if (bytes is null) return NotFound();
        return File(bytes, "audio/ogg");
    }
}
