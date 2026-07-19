namespace Automais.Pabx.Api.Data.Entities;

public enum TipoArquivoGerenciado
{
    ConfigSip,
    ProvisionamentoXml,
}

/// <summary>
/// Registro de todo arquivo que o serviço escreveu no servidor (sip_smsmarica.conf,
/// XMLs de provisionamento no TFTP). O serviço SÓ sobrescreve/apaga arquivos presentes
/// aqui — o servidor é compartilhado (FalarMais) e arquivos alheios são intocáveis.
/// </summary>
public sealed class ArquivoGerenciado
{
    public int Id { get; set; }
    public required string Caminho { get; set; }
    public required string HashSha256 { get; set; }
    public TipoArquivoGerenciado Tipo { get; set; }
    public int? RamalId { get; set; }
    public Ramal? Ramal { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
