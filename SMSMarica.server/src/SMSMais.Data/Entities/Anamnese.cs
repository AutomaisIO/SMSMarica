namespace SMSMais.Data.Entities;

/// <summary>
/// Anamnese (questionário pré-exame) vinculada 1:1 à solicitação de exame.
/// Preenchida pela enfermeira/atendente ao encaminhar o exame e consultada
/// pelo médico ao laudar; pode ser reaberta e editada enquanto o exame não
/// for laudado. O conteúdo é um documento JSON versionado por tipo de
/// questionário (hoje: <c>mamografia</c> v1 — CDT Maricá), o que permite novos
/// formulários por modalidade sem migração de schema. Campos consultáveis
/// (ex.: classificação de risco) são promovidos a colunas. Auditoria conforme
/// ADR-0006.
/// </summary>
public class Anamnese
{
    public Guid Id { get; set; }

    /// <summary>Vínculo 1:1 com o pedido de exame (único por solicitação).</summary>
    public Guid ExameImagemId { get; set; }
    public ExameImagem? ExameImagem { get; set; }

    /// <summary>Tipo do questionário (ex.: "mamografia"). Define o shape do ConteudoJson.</summary>
    public string Tipo { get; set; } = "mamografia";

    /// <summary>Versão do shape do questionário (evolução sem quebrar registros antigos).</summary>
    public int Versao { get; set; } = 1;

    /// <summary>Respostas completas do questionário (jsonb), incluindo marcações no diagrama.</summary>
    public string ConteudoJson { get; set; } = "{}";

    /// <summary>Classificação de risco promovida do conteúdo (Baixo/Moderado/Alto) p/ filtros.</summary>
    public string? ClassificacaoRisco { get; set; }

    // ---- Quem preencheu (snapshot p/ exibição mesmo se o usuário mudar) ----
    public Guid? PreenchidoPorUsuarioId { get; set; }
    public string? PreenchidoPorNome { get; set; }

    // ---- Auditoria (ADR-0006) ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public uint RowVersion { get; set; }
}
