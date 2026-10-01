namespace SMSMais.Data.Entities;

/// <summary>
/// Chave da API de publicação da extensão (ADR-0064). É uma OPÇÃO da tela de versões: enquanto não
/// houver chave ativa, só se publica pelo painel. A chave serve para uma coisa só — subir versão e
/// promovê-la — e não abre mais nada do sistema. O valor em claro é mostrado uma única vez; aqui
/// fica o hash. Só uma ativa por vez: gerar outra revoga a anterior.
/// </summary>
public class ExtensaoChavePublicacao
{
    public Guid Id { get; set; }

    /// <summary>Hash SHA-256 (hex) da chave. UNIQUE.</summary>
    public string ChaveHash { get; set; } = string.Empty;

    /// <summary>Começo legível da chave, só para reconhecê-la na tela.</summary>
    public string Prefixo { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; }
    public Guid? CriadaPor { get; set; }

    public DateTime? UltimoUsoEm { get; set; }

    public DateTime? RevogadaEm { get; set; }
    public Guid? RevogadaPor { get; set; }
}
