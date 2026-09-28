using Automais.Pabx.Api.Asterisk.Ami;
using Automais.Pabx.Api.Data;
using Automais.Pabx.Api.Infra.Excecoes;
using Automais.Pabx.Api.Ramais;
using Microsoft.EntityFrameworkCore;

namespace Automais.Pabx.Api.Comandos;

/// <summary>
/// Comandos de telefonia executados no Asterisk via AMI. É aqui que a API cresce
/// (originar chamada, desligar, transferir, eventos): cada comando novo é um método,
/// e só age sobre ramais do inventário — o servidor é compartilhado.
/// </summary>
public interface IComandosAsterisk
{
    /// <summary>Detalhe do peer no chan_sip (SIPshowpeer): IP, user-agent, status, codecs negociáveis.</summary>
    Task<PeerDetalheDto> DetalharPeerAsync(string numero, CancellationToken ct = default);
}

public sealed class ComandosAsterisk(PabxDbContext db, IAmiClientFactory amiFactory) : IComandosAsterisk
{
    public async Task<PeerDetalheDto> DetalharPeerAsync(string numero, CancellationToken ct = default)
    {
        await GarantirNoInventarioAsync(numero, ct);

        if (!amiFactory.Habilitado)
            throw new ServicoIndisponivelException("AMI desabilitado na configuração: não há como consultar o Asterisk.");

        await using var ami = await amiFactory.ConectarAsync(ct);
        var resposta = await ami.ExecutarAsync("SIPshowpeer", new Dictionary<string, string> { ["Peer"] = numero }, ct);

        if (!string.Equals(resposta["Response"], "Success", StringComparison.OrdinalIgnoreCase))
            return new PeerDetalheDto(numero, Registrado: false, new Dictionary<string, string>());

        // Nunca devolver o que for segredo, ainda que o AMI venha a expor.
        var campos = resposta.Campos
            .Where(c => !c.Key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                        && c.Key is not "Response" and not "ActionID")
            .ToDictionary(c => c.Key, c => c.Value);

        var status = resposta["Status"];
        return new PeerDetalheDto(numero, status?.StartsWith("OK", StringComparison.OrdinalIgnoreCase) == true, campos);
    }

    private async Task GarantirNoInventarioAsync(string numero, CancellationToken ct)
    {
        if (!await db.Ramais.AnyAsync(r => r.Numero == numero, ct))
            throw new NaoEncontradoException("Ramal", numero);
    }
}
