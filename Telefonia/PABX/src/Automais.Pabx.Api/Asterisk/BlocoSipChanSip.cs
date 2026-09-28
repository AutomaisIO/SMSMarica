using System.Text;
using Automais.Pabx.Api.Data.Entities;

namespace Automais.Pabx.Api.Asterisk;

/// <summary>
/// Monta a seção chan_sip de um ramal. Separado do gerador para ser testável sem banco nem
/// disco: mesma entrada ⇒ mesmo texto. O físico reproduz exatamente o bloco que os aparelhos
/// das unidades já usam; o softphone acrescenta o necessário para WebRTC (WSS + DTLS-SRTP).
/// </summary>
public static class BlocoSipChanSip
{
    public static string Gerar(Ramal ramal, string secret, WebRtcOptions webRtc)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(ramal.Numero).AppendLine("]");
        sb.Append("secret=").AppendLine(secret);
        sb.AppendLine("type=friend");
        sb.AppendLine("qualify=yes");
        sb.AppendLine("nat=force_rport,comedia");
        sb.Append("call-limit=").AppendLine(ramal.CallLimit.ToString());
        sb.AppendLine("host=dynamic");
        sb.AppendLine("disallow=all");
        foreach (var codec in ListaCodecs(ramal.Codecs))
            sb.Append("allow=").AppendLine(codec);
        sb.Append("context=").AppendLine(ramal.Contexto);
        sb.Append("callerid=").AppendLine(CallerIdDe(ramal));

        if (ramal.Tipo == TipoRamal.Softphone)
        {
            // Navegador: sinalização só por WSS, mídia sempre criptografada e passando pelo Asterisk
            // (o áudio precisa do Asterisk no meio para gravar e para falar com os aparelhos da VPN).
            sb.AppendLine("transport=wss");
            sb.AppendLine("avpf=yes");
            sb.AppendLine("force_avp=yes");
            sb.AppendLine("encryption=yes");
            sb.AppendLine("icesupport=yes");
            sb.AppendLine("rtcp_mux=yes");
            sb.AppendLine("dtlsenable=yes");
            sb.AppendLine("dtlsverify=fingerprint");
            sb.Append("dtlscertfile=").AppendLine(webRtc.DtlsCertFile);
            sb.Append("dtlsprivatekey=").AppendLine(webRtc.DtlsPrivateKey);
            sb.AppendLine("dtlssetup=actpass");
            sb.AppendLine("directmedia=no");
        }
        else
        {
            sb.AppendLine("canreinvite=no");
        }

        sb.AppendLine("rtptimeout=60");
        sb.AppendLine("rtpholdtimeout=180");
        sb.AppendLine();
        return sb.ToString();
    }

    public static IReadOnlyList<string> ListaCodecs(string codecs) =>
        [.. codecs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToLowerInvariant())
            .Distinct()];

    /// <summary>
    /// Físico mantém o formato de hoje (texto livre ou só o número). Softphone usa
    /// <c>"Nome" &lt;ramal&gt;</c> para o nome do atendente aparecer no visor de quem recebe.
    /// </summary>
    private static string CallerIdDe(Ramal ramal)
    {
        if (string.IsNullOrWhiteSpace(ramal.CallerId))
            return ramal.Numero;
        return ramal.Tipo == TipoRamal.Softphone
            ? $"\"{ramal.CallerId.Trim()}\" <{ramal.Numero}>"
            : ramal.CallerId.Trim();
    }
}
