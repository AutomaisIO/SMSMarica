using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Data.Entities;

namespace Automais.Pabx.Tests;

public sealed class BlocoSipChanSipTests
{
    private static readonly WebRtcOptions WebRtc = new()
    {
        DtlsCertFile = "/etc/asterisk/keys/asterisk.pem",
        DtlsPrivateKey = "/etc/asterisk/keys/asterisk.key",
    };

    [Fact]
    public void Fisico_com_defaults_reproduz_o_bloco_que_os_aparelhos_ja_usam()
    {
        var ramal = new Ramal { Numero = "2101", SecretCifrado = "x" };

        var bloco = BlocoSipChanSip.Gerar(ramal, "segredo", WebRtc);

        // Texto idêntico ao que o gerador escrevia fixo antes da configuração por ramal.
        string[] esperado =
        [
            "[2101]",
            "secret=segredo",
            "type=friend",
            "qualify=yes",
            "nat=force_rport,comedia",
            "call-limit=1",
            "host=dynamic",
            "disallow=all",
            "allow=alaw",
            "allow=ulaw",
            "allow=gsm",
            "context=PLANO_HOSPITAIS",
            "callerid=2101",
            "canreinvite=no",
            "rtptimeout=60",
            "rtpholdtimeout=180",
            "",
            "",
        ];
        Assert.Equal(esperado, bloco.ReplaceLineEndings("\n").Split('\n'));
    }

    [Fact]
    public void Softphone_liga_wss_e_dtls_e_mostra_o_nome_no_callerid()
    {
        var ramal = new Ramal
        {
            Numero = "3001",
            SecretCifrado = "x",
            Tipo = TipoRamal.Softphone,
            Codecs = "ulaw,alaw",
            CallerId = "Maria Regulação",
            CallLimit = 2,
        };

        var linhas = BlocoSipChanSip.Gerar(ramal, "segredo", WebRtc)
            .ReplaceLineEndings("\n").Split('\n');

        Assert.Contains("transport=wss", linhas);
        Assert.Contains("encryption=yes", linhas);
        Assert.Contains("dtlsenable=yes", linhas);
        Assert.Contains("dtlsverify=fingerprint", linhas);
        Assert.Contains("dtlscertfile=/etc/asterisk/keys/asterisk.pem", linhas);
        Assert.Contains("rtcp_mux=yes", linhas);
        Assert.Contains("directmedia=no", linhas);
        Assert.Contains("call-limit=2", linhas);
        Assert.Contains("callerid=\"Maria Regulação\" <3001>", linhas);
        Assert.DoesNotContain("allow=gsm", linhas);
        Assert.DoesNotContain("canreinvite=no", linhas);
        Assert.Equal(["allow=ulaw", "allow=alaw"], linhas.Where(l => l.StartsWith("allow=", StringComparison.Ordinal)));
    }
}
