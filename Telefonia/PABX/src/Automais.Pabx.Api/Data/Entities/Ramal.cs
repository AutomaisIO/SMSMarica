namespace Automais.Pabx.Api.Data.Entities;

public enum MarcaTelefone
{
    Intelbras,
    Cisco,
    Grandstream,
}

public enum OrigemRamal
{
    /// <summary>Criado e gerenciado pelo Automais.Pabx (vive em sip_smsmarica.conf).</summary>
    Gerenciado,

    /// <summary>Pré-existente no servidor (sip_custom.conf); inventariado mas o bloco original não é tocado.</summary>
    Adotado,
}

public sealed class Ramal
{
    public int Id { get; set; }

    /// <summary>Número do ramal (nome da seção no chan_sip). Único.</summary>
    public required string Numero { get; set; }

    /// <summary>Secret SIP cifrado com Data Protection — nunca sai em GET.</summary>
    public required string SecretCifrado { get; set; }

    public int UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>Setor/sala/pessoa — texto livre exibido nas listagens.</summary>
    public string? Descricao { get; set; }

    /// <summary>MAC do aparelho, normalizado: 12 hex minúsculos sem separador.</summary>
    public string? Mac { get; set; }

    public MarcaTelefone? Marca { get; set; }

    /// <summary>Modelo dentro da marca (ex.: TIP125, 3905, GXP1610). Usado no XML de provisionamento.</summary>
    public string? Modelo { get; set; }

    public string? CallerId { get; set; }
    public bool Ativo { get; set; } = true;
    public OrigemRamal Origem { get; set; } = OrigemRamal.Gerenciado;

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
