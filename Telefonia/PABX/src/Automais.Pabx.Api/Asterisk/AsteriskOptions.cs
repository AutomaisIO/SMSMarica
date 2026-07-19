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
