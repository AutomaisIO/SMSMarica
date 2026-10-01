namespace SMSMais.Data.Entities.Enums;

/// <summary>O que a plataforma distribui aos computadores (ADR-0064).</summary>
public enum ExtensaoArtefato
{
    /// <summary>O conteúdo da extensão do Chrome (um .zip com os arquivos).</summary>
    Extensao = 1,

    /// <summary>O programa atualizador (um executável do Windows).</summary>
    Atualizador = 2,
}
