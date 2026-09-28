namespace Automais.Pabx.Api.Asterisk;

/// <summary>Caminhos e credenciais do Asterisk na caixa. Em dev apontam para ./sandbox.</summary>
public sealed class AsteriskOptions
{
    public const string Secao = "Asterisk";

    /// <summary>Arquivo de ramais gerado pelo serviço (nosso, exclusivo).</summary>
    public string SipConfPath { get; set; } = "/etc/asterisk/sip_smsmarica.conf";

    /// <summary>Arquivo legado da FalarMais com os ramais pré-existentes — SOMENTE LEITURA (adoção).</summary>
    public string SipCustomConfPath { get; set; } = "/etc/asterisk/sip_custom.conf";

    /// <summary>Raiz do TFTP onde os XMLs de provisionamento são publicados.</summary>
    public string TftpDir { get; set; } = "/var/lib/tftpboot";

    /// <summary>Backups com timestamp antes de qualquer sobrescrita.</summary>
    public string BackupDir { get; set; } = "/opt/automais-pabx/backups";

    /// <summary>IP do Asterisk visto pelos telefones das unidades (hub WireGuard).</summary>
    public string SipServerParaTelefones { get; set; } = "10.201.0.1";

    public AmiOptions Ami { get; set; } = new();

    // Listas começam vazias de propósito: o binder de configuração ACRESCENTA ao default em vez
    // de substituir, e o appsettings.json é quem define os valores.

    /// <summary>Contextos do dialplan que a API aceita atribuir a um ramal.</summary>
    public List<string> ContextosPermitidos { get; set; } = [];

    /// <summary>Codecs que a API aceita configurar (nomes do Asterisk).</summary>
    public List<string> CodecsPermitidos { get; set; } = [];

    /// <summary>Faixa de numeração por tipo. Faixa zerada = sem restrição.</summary>
    // Físicos seguem a numeração que já existe no servidor (<grupo 2 díg><seq>, 10xx–31xx), sem
    // faixa fixa: a colisão é barrada pelo inventário + sip_custom.conf. Softphones ficam num bloco
    // próprio ainda vazio (auditoria de 23/09/2026: nenhum 6xxx nem padrão de dialplan 6).
    public FaixaOptions FaixaFisico { get; set; } = new();

    public FaixaOptions FaixaSoftphone { get; set; } = new() { Inicio = 6000, Fim = 6999 };

    public WebRtcOptions WebRtc { get; set; } = new();

    public FaixaOptions Faixa(Data.Entities.TipoRamal tipo) =>
        tipo == Data.Entities.TipoRamal.Softphone ? FaixaSoftphone : FaixaFisico;
}

public sealed class FaixaOptions
{
    public int Inicio { get; set; }
    public int Fim { get; set; }

    public bool Restrita => Inicio > 0 && Fim >= Inicio;
    public bool Contem(int numero) => !Restrita || (numero >= Inicio && numero <= Fim);
}

/// <summary>Parâmetros do softphone no navegador (SIP sobre WebSocket seguro).</summary>
public sealed class WebRtcOptions
{
    /// <summary>URL do WebSocket SIP que o navegador abre (nginx → http.conf do Asterisk).</summary>
    public string WssUrl { get; set; } = "wss://telefonia.smsmarica.online/ws";

    /// <summary>Domínio SIP usado na URI (sip:ramal@dominio).</summary>
    public string Dominio { get; set; } = "telefonia.smsmarica.online";

    /// <summary>Certificado e chave para o DTLS da mídia (chan_sip dtlscertfile/dtlsprivatekey).</summary>
    public string DtlsCertFile { get; set; } = "/etc/asterisk/keys/asterisk.pem";

    public string DtlsPrivateKey { get; set; } = "/etc/asterisk/keys/asterisk.key";

    /// <summary>Servidores STUN/TURN entregues ao navegador junto da credencial.</summary>
    public List<IceServerOptions> IceServers { get; set; } = [];
}

public sealed class IceServerOptions
{
    public string Urls { get; set; } = "";
    public string? Username { get; set; }
    public string? Credential { get; set; }
}

public sealed class AmiOptions
{
    /// <summary>Desligado em dev (sem Asterisk local): gera config mas não recarrega nem monitora.</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5038;
    public string Username { get; set; } = "automais_pabx";
    public string Secret { get; set; } = "";
}
