namespace Automais.Zap.Data.Entities;

/// <summary>
/// Qual arte vai no cabeçalho de um modelo aprovado.
///
/// <para>O modelo em si vive na Meta, não aqui — por isso a ligação é pelo <b>nome</b> dentro do
/// WABA, que é como a Meta o identifica no envio. Apagar e recriar o modelo com o mesmo nome
/// mantém a arte, que é o que se espera de quem só corrigiu um texto.</para>
///
/// <para>Guardar isto no relay é o que faz o catálogo entregar a URL pronta: a instância do
/// município não precisa saber onde a arte está, nem manter mapa nenhum.</para>
/// </summary>
public sealed class TemplateArte
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WabaId { get; set; }
    public Waba? Waba { get; set; }

    /// <summary>Nome do modelo na Meta (minúsculas com underscore).</summary>
    public required string Template { get; set; }

    public Guid MidiaId { get; set; }
    public Midia? Midia { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    /// <summary>Quem escolheu, pelo painel. Nulo quando veio pela API, com token de tenant.</summary>
    public Guid? AtualizadoPorUsuarioId { get; set; }
}
